import {LabelJob, LabelPrinter, LabelPrinterStatus, validateLabelJob} from '../label-printer';
import {ean13CheckDigit} from '../ean13';
import {
  buildDataFrames, CMD, DATA_FRAME_LEN, DATA_PAYLOAD_SIZE, describeStatus, frameLen, makeCmd, MaterialInfo,
  parseDeviceName, parseFirmwareVersion, parseMaterial, parseProtocolVersion, parseStatus, PrinterStatus,
  statusErrors, toSpacedHex, validateResponse,
} from './supvan-protocol';
import {barcodeModuleDots, renderLabel} from './supvan-label-render';
import {compressGrouped, DEFAULT_DENSITY, splitIntoBuffers} from './supvan-raster';

// --- Web Serial (types minimaux : absents de lib.dom) ---------------------------------------------------

interface SerialPortLike {
  readable: ReadableStream<Uint8Array> | null;
  writable: WritableStream<Uint8Array> | null;
  open(options: {baudRate: number; bufferSize?: number}): Promise<void>;
  close(): Promise<void>;
}

interface SerialLike {
  requestPort(options?: object): Promise<SerialPortLike>;
  getPorts(): Promise<SerialPortLike[]>;
}

const SPP_UUID = '00001101-0000-1000-8000-00805f9b34fb';
const CHUNK = 128;
const CHUNK_DELAY_MS = 10;
const REPLY_TIMEOUT_MS = 2000;
const REPLY_POLL_MS = 20;

export function webSerial(): SerialLike | null {
  const serial = (navigator as unknown as {serial?: SerialLike}).serial;
  return serial ?? null;
}

const sleep = (ms: number) => new Promise<void>(resolve => setTimeout(resolve, ms));

export type SupvanLog = (line: string) => void;

// Tuyau série Bluetooth : écritures découpées, lecture continue, réponses « 7E 5A » reconstituées.
export class SerialPipe {
  private rx: number[] = [];
  private reader: ReadableStreamDefaultReader<Uint8Array> | null = null;
  private writer: WritableStreamDefaultWriter<Uint8Array> | null = null;
  private closed = false;

  constructor(private readonly port: SerialPortLike) {}

  async open(): Promise<void> {
    await this.port.open({baudRate: 115200, bufferSize: 8192});
    if (!this.port.readable || !this.port.writable) {
      throw new Error('Le port série ne fournit pas de flux de lecture/écriture.');
    }
    this.reader = this.port.readable.getReader();
    this.writer = this.port.writable.getWriter();
    void this.readLoop();
  }

  private async readLoop(): Promise<void> {
    try {
      while (this.reader && !this.closed) {
        const {value, done} = await this.reader.read();
        if (done) {
          break;
        }
        if (value) {
          for (const byte of value) {
            this.rx.push(byte);
          }
        }
      }
    } catch {
      // Liaison coupée : les lectures suivantes expireront.
    }
  }

  drain(): void {
    this.rx = [];
  }

  private async write(bytes: Uint8Array): Promise<void> {
    if (!this.writer) {
      throw new Error('Imprimante non connectée.');
    }
    await this.writer.write(bytes);
  }

  // Réponse complète = trame « 7E 5A » dont la longueur annoncée est arrivée.
  async readReply(): Promise<Uint8Array | null> {
    const deadline = Date.now() + REPLY_TIMEOUT_MS;
    while (Date.now() < deadline) {
      const bytes = Uint8Array.from(this.rx);
      const length = frameLen(bytes);
      if (length !== null && bytes.length >= length) {
        this.rx = [];
        return bytes.subarray(0, length);
      }
      await sleep(REPLY_POLL_MS);
    }
    const rest = Uint8Array.from(this.rx);
    this.rx = [];
    return rest.length > 0 ? rest : null;
  }

  async sendCmd(frame: Uint8Array): Promise<Uint8Array | null> {
    this.drain();
    await this.write(frame);
    await sleep(CHUNK_DELAY_MS);
    return this.readReply();
  }

  async sendDataFrame(frame: Uint8Array, readReply: boolean): Promise<Uint8Array | null> {
    for (let offset = 0; offset < frame.length; offset += CHUNK) {
      await sleep(CHUNK_DELAY_MS);
      this.drain();
      await this.write(frame.subarray(offset, offset + CHUNK));
    }
    return readReply ? this.readReply() : null;
  }

  async close(): Promise<void> {
    this.closed = true;
    try {
      await this.reader?.cancel();
    } catch { /* déjà fermé */ }
    this.reader?.releaseLock();
    try {
      await this.writer?.close();
    } catch { /* déjà fermé */ }
    this.writer?.releaseLock();
    this.reader = null;
    this.writer = null;
    try {
      await this.port.close();
    } catch { /* déjà fermé */ }
  }
}

// --- Commandes et flux d'impression ---------------------------------------------------------------------

const STATUS_POLL_MS = 100;
const BUFFER_POLL_MS = 20;

export class SupvanClient {
  constructor(private readonly pipe: SerialPipe, private readonly log: SupvanLog = () => undefined) {}

  private async command(name: string, cmd: number, a = 0, b = 0): Promise<Uint8Array | null> {
    const reply = await this.pipe.sendCmd(makeCmd(cmd, a, b));
    this.log(`${name} → ${reply ? toSpacedHex(reply, 40) : '(aucune réponse)'}`);
    return reply;
  }

  async checkDevice(): Promise<boolean> {
    return validateResponse(await this.command('CHECK_DEVICE', CMD.CHECK_DEVICE), CMD.CHECK_DEVICE);
  }

  async queryStatus(): Promise<PrinterStatus | null> {
    return parseStatus(await this.pipe.sendCmd(makeCmd(CMD.INQUIRY_STA)));
  }

  async queryMaterial(): Promise<MaterialInfo | null> {
    return parseMaterial(await this.command('RETURN_MAT', CMD.RETURN_MAT));
  }

  async readDeviceName(): Promise<string | null> {
    return parseDeviceName(await this.command('RD_DEV_NAME', CMD.RD_DEV_NAME));
  }

  async readFirmwareVersion(): Promise<number | null> {
    return parseFirmwareVersion(await this.command('READ_FWVER', CMD.READ_FWVER));
  }

  async readProtocolVersion(): Promise<string | null> {
    return parseProtocolVersion(await this.command('READ_REV', CMD.READ_REV));
  }

  private async pollStatus<T>(what: string, budgetMs: number, intervalMs: number,
                              done: (status: PrinterStatus) => T | null): Promise<T> {
    const deadline = Date.now() + budgetMs;
    for (;;) {
      const status = await this.queryStatus();
      if (status) {
        const errors = statusErrors(status);
        if (errors.length > 0) {
          throw new Error(`Imprimante : ${errors.join(', ')} (pendant : ${what}).`);
        }
        const value = done(status);
        if (value !== null) {
          return value;
        }
      }
      if (Date.now() >= deadline) {
        throw new Error(`Délai dépassé : ${what}.`);
      }
      await sleep(intervalMs);
    }
  }

  // Une étiquette (une page) : image déjà rendue.
  async printBuffers(buffers: Uint8Array[]): Promise<void> {
    const page = compressGrouped(buffers);
    this.log(`${buffers.length} buffer(s) → ${page.blocks.length} bloc(s) [${page.blocks.map(block => block.data.length).join(', ')} octets], vitesse ${page.speed}`);

    if (!(await this.checkDevice())) {
      throw new Error('L\'imprimante ne répond pas à CHECK_DEVICE.');
    }
    await this.pollStatus('imprimante prête', 6000, STATUS_POLL_MS, s => (!s.deviceBusy && !s.printing ? true : null));

    await this.command('START_PRINT', CMD.START_PRINT, 0);
    await this.pollStatus('démarrage de l\'impression', 6000, STATUS_POLL_MS, s => (s.printing ? true : null));

    for (const [index, block] of page.blocks.entries()) {
      try {
        await this.pollStatus('mémoire tampon libre', 15000, BUFFER_POLL_MS, s => (!s.bufFull ? true : null));
      } catch (error) {
        await this.command('STOP_PRINT', CMD.STOP_PRINT);
        throw error;
      }

      const packets = Math.ceil(block.data.length / DATA_PAYLOAD_SIZE);
      const header = await this.command('NEXT_ZIPPEDBULK', CMD.NEXT_ZIPPEDBULK, DATA_FRAME_LEN, packets);
      if (header === null) {
        throw new Error('Pas de réponse à NEXT_ZIPPEDBULK.');
      }

      const frames = buildDataFrames(block.data);
      for (const [frameIndex, frame] of frames.entries()) {
        // Comme l'app d'origine : un accusé par trame, sauf pour la dernière (le firmware ne répond qu'après digestion).
        await this.pipe.sendDataFrame(frame, frameIndex < frames.length - 1);
      }

      await sleep(20);
      const full = await this.command(`BUF_FULL (bloc ${index + 1}/${page.blocks.length})`, CMD.BUF_FULL, block.data.length, page.speed);
      if (full === null) {
        throw new Error('Pas de réponse à BUF_FULL.');
      }
    }

    const stillBuffered = await this.pollStatus('fin d\'impression', 30000, STATUS_POLL_MS,
      s => (!s.printing && !s.deviceBusy ? s.bufFull : null));
    if (stillBuffered) {
      throw new Error('L\'imprimante s\'est arrêtée avec des données en attente : l\'étiquette n\'est pas complète.');
    }
  }
}

// --- LabelPrinter ---------------------------------------------------------------------------------------

export const TEST_EAN13 = `${'200000000000'}${ean13CheckDigit('200000000000')}`;

export class SupvanLabelPrinter implements LabelPrinter {
  readonly simulated = false;
  deviceName = 'T50M Pro';
  density = DEFAULT_DENSITY;

  private pipe: SerialPipe | null = null;
  private client: SupvanClient | null = null;
  private material: MaterialInfo | null = null;
  private currentStatus: LabelPrinterStatus = 'disconnected';

  constructor(private readonly log: SupvanLog = () => undefined) {}

  status(): LabelPrinterStatus {
    return this.currentStatus;
  }

  get currentMaterial(): MaterialInfo | null {
    return this.material;
  }

  get supvanClient(): SupvanClient | null {
    return this.client;
  }

  async connect(): Promise<void> {
    const serial = webSerial();
    if (!serial) {
      throw new Error('Ce navigateur ne gère pas le Bluetooth série (Web Serial). Utilisez Chrome sur Android ou ordinateur.');
    }

    // L'imprimante doit déjà être appairée dans les réglages Bluetooth de l'appareil.
    const port = await serial.requestPort({allowedBluetoothServiceClassIds: [SPP_UUID]});
    const pipe = new SerialPipe(port);
    await pipe.open();
    const client = new SupvanClient(pipe, this.log);

    try {
      if (!(await client.checkDevice())) {
        throw new Error('L\'appareil choisi ne répond pas comme une étiqueteuse Supvan.');
      }
      this.material = await client.queryMaterial();
      this.deviceName = (await client.readDeviceName()) ?? 'T50M Pro';
    } catch (error) {
      await pipe.close();
      throw error;
    }

    this.pipe = pipe;
    this.client = client;
    this.currentStatus = 'connected';
  }

  disconnect(): void {
    const pipe = this.pipe;
    this.pipe = null;
    this.client = null;
    this.currentStatus = 'disconnected';
    void pipe?.close();
  }

  async test(): Promise<void> {
    await this.printLabel({code: TEST_EAN13, price: null, copies: 1}, true);
  }

  async print(job: LabelJob): Promise<void> {
    const error = validateLabelJob(job);
    if (error) {
      throw new Error(error);
    }
    for (let copy = 0; copy < job.copies; copy += 1) {
      await this.printLabel(job, false);
    }
  }

  private async printLabel(job: LabelJob, testLabel: boolean): Promise<void> {
    const client = this.client;
    if (this.currentStatus !== 'connected' || !client) {
      throw new Error('Imprimante non connectée.');
    }

    // Le rouleau a pu changer depuis la connexion : relire sa taille.
    this.material = (await client.queryMaterial()) ?? this.material;
    const material = this.material;
    if (!material || material.widthMm === 0 || material.heightMm === 0) {
      throw new Error('Taille du rouleau inconnue : vérifiez que le rouleau est bien en place, capot fermé.');
    }

    if (barcodeModuleDots(material.widthMm) < 2) {
      this.log('Attention : rouleau étroit, code-barres très serré (peut être illisible).');
    }

    const image = renderLabel({
      code: job.code,
      price: job.price,
      widthMm: material.widthMm,
      heightMm: material.heightMm,
      border: testLabel,
      caption: testLabel ? `TEST ${material.widthMm}x${material.heightMm} mm` : null,
    });
    await client.printBuffers(splitIntoBuffers(image, this.density));
  }

  describeStatus(status: PrinterStatus): string {
    return describeStatus(status);
  }
}
