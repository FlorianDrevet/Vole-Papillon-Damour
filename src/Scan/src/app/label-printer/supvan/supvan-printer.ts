import {
  LabelJob, LabelPrinter, LabelPrinterStatus, LabelPrinterTransport, LabelRoll, MAX_LABEL_COPIES, validateLabelJob,
} from '../label-printer';
import {ean13CheckDigit} from '../ean13';
import {
  buildDataFrames, CMD, DATA_FRAME_LEN, DATA_PAYLOAD_SIZE, describeStatus, frameLen, makeCmd, MaterialInfo,
  parseDeviceName, parseFirmwareVersion, parseMaterial, parseProtocolVersion, parseStatus, PrinterStatus,
  statusErrors, toSpacedHex, validateResponse,
} from './supvan-protocol';
import {barcodeModuleDots, renderLabel} from './supvan-label-render';
import {compressGrouped, DEFAULT_DENSITY, MAX_DENSITY, splitIntoBuffers} from './supvan-raster';

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
export class SerialPipe implements SupvanPipe {
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

export interface SupvanPipe {
  sendCmd(frame: Uint8Array): Promise<Uint8Array | null>;
  sendDataFrame(frame: Uint8Array, readReply: boolean): Promise<Uint8Array | null>;
  close(): Promise<void>;
}

// --- Web Bluetooth (BLE GATT) : Chrome Android, ou Bluefy sur iPhone -------------------------------------

interface BleCharacteristic {
  startNotifications(): Promise<BleCharacteristic>;
  addEventListener(type: string, listener: (event: {target: {value?: DataView}}) => void): void;
  writeValueWithResponse?(value: BufferSource): Promise<void>;
  writeValue(value: BufferSource): Promise<void>;
}

interface BleService {
  uuid: string;
  getCharacteristic(uuid: string): Promise<BleCharacteristic>;
}

interface BleServer {
  getPrimaryServices(): Promise<BleService[]>;
  disconnect(): void;
}

interface BleDevice {
  name?: string;
  gatt?: {connect(): Promise<BleServer>};
  addEventListener(type: string, listener: () => void): void;
}

interface BluetoothLike {
  requestDevice(options: object): Promise<BleDevice>;
}

const sig = (short: string) => `0000${short}-0000-1000-8000-00805f9b34fb`;
const BLE_SERVICES: ReadonlyArray<{service: string; notify: string; write: string}> = [
  {service: sig('fee7'), notify: sig('fec1'), write: sig('fec1')},
  {service: '0000e0ff-3c17-d293-8e48-14fe2e4da212', notify: sig('ffe1'), write: sig('ffe9')},
  {service: sig('ff00'), notify: sig('ff01'), write: sig('ff02')},
];
const BLE_REPLY_TIMEOUT_MS = 4000;

export function webBluetooth(): BluetoothLike | null {
  const bluetooth = (navigator as unknown as {bluetooth?: BluetoothLike}).bluetooth;
  return bluetooth ?? null;
}

export class BlePipe implements SupvanPipe {
  private rx: number[] = [];
  private device: BleDevice | null = null;
  private server: BleServer | null = null;
  private writeChar: BleCharacteristic | null = null;
  deviceName = '';

  constructor(private readonly log: SupvanLog, private readonly onLost: () => void = () => undefined) {}

  // Première connexion : l'utilisateur choisit l'étiqueteuse dans la fenêtre du navigateur.
  async open(): Promise<void> {
    const bluetooth = webBluetooth();
    if (!bluetooth) {
      throw new Error('Ce navigateur ne gère pas le Bluetooth Web.');
    }

    const device = await bluetooth.requestDevice({
      acceptAllDevices: true,
      optionalServices: BLE_SERVICES.map(entry => entry.service),
    });
    this.device = device;
    this.deviceName = device.name ?? '';
    this.log(`Appareil BLE : ${this.deviceName || '(sans nom)'}`);
    device.addEventListener('gattserverdisconnected', () => {
      this.log('Liaison BLE coupée par l\'imprimante ou le téléphone.');
      this.writeChar = null;
      this.server = null;
      this.onLost();
    });
    await this.connectGatt();
  }

  // Reconnexion à l'appareil déjà choisi, sans nouvelle fenêtre de sélection.
  async reconnect(): Promise<void> {
    if (!this.device) {
      throw new Error('Aucune imprimante mémorisée.');
    }
    this.log('Reconnexion BLE à l\'imprimante déjà choisie…');
    await this.connectGatt();
  }

  private async connectGatt(): Promise<void> {
    const device = this.device;
    if (!device?.gatt) {
      throw new Error('Cet appareil n\'expose pas de GATT.');
    }

    this.rx = [];
    this.server = await device.gatt.connect();
    const services = await this.server.getPrimaryServices();
    this.log(`Services GATT : ${services.map(service => service.uuid).join(', ') || '(aucun autorisé)'}`);

    for (const entry of BLE_SERVICES) {
      // Bluefy (iPhone) renvoie les UUID en majuscules, Chrome Android en minuscules : on compare sans la casse.
      const service = services.find(candidate => candidate.uuid.toLowerCase() === entry.service);
      if (!service) {
        continue;
      }
      const notifyChar = await service.getCharacteristic(entry.notify);
      const writeChar = entry.write === entry.notify ? notifyChar : await service.getCharacteristic(entry.write);
      await notifyChar.startNotifications();
      notifyChar.addEventListener('characteristicvaluechanged', event => {
        const value = event.target.value;
        if (value) {
          for (let i = 0; i < value.byteLength; i += 1) {
            this.rx.push(value.getUint8(i));
          }
        }
      });
      this.writeChar = writeChar;
      this.log(`Service Supvan utilisé : ${entry.service}`);
      return;
    }

    this.server.disconnect();
    this.server = null;
    throw new Error('Aucun service Supvan connu (FEE7, E0FF, FF00) sur cet appareil.');
  }

  private async write(bytes: Uint8Array): Promise<void> {
    const characteristic = this.writeChar;
    if (!characteristic) {
      throw new Error('Imprimante non connectée.');
    }
    for (let offset = 0; offset < bytes.length; offset += CHUNK) {
      if (offset > 0) {
        await sleep(CHUNK_DELAY_MS);
      }
      const chunk = bytes.slice(offset, offset + CHUNK);
      if (characteristic.writeValueWithResponse) {
        await characteristic.writeValueWithResponse(chunk);
      } else {
        await characteristic.writeValue(chunk);
      }
    }
  }

  // Réponse = première trame « 7E 5A » complète qui répète l'octet de commande à l'offset 7.
  private async awaitReply(cmd: number): Promise<Uint8Array | null> {
    const deadline = Date.now() + BLE_REPLY_TIMEOUT_MS;
    while (Date.now() < deadline) {
      while (this.rx.length > 0 && !(this.rx[0] === 0x7e && (this.rx.length < 2 || this.rx[1] === 0x5a))) {
        this.rx.shift();
      }
      const bytes = Uint8Array.from(this.rx);
      const length = frameLen(bytes);
      if (length !== null && bytes.length >= length) {
        this.rx.splice(0, length);
        if (bytes[7] === cmd) {
          return bytes.subarray(0, length);
        }
        continue;
      }
      await sleep(REPLY_POLL_MS);
    }
    return null;
  }

  async sendCmd(frame: Uint8Array): Promise<Uint8Array | null> {
    this.rx = [];
    await this.write(frame);
    return this.awaitReply(frame[7]);
  }

  // Pas d'accusé sur les trames de données en BLE.
  async sendDataFrame(frame: Uint8Array, _readReply: boolean): Promise<Uint8Array | null> {
    await this.write(frame);
    return null;
  }

  async close(): Promise<void> {
    this.server?.disconnect();
    this.server = null;
    this.writeChar = null;
  }
}

// --- Commandes et flux d'impression ---------------------------------------------------------------------

const STATUS_POLL_MS = 100;
const BUFFER_POLL_MS = 20;

export class SupvanClient {
  constructor(private readonly pipe: SupvanPipe, private readonly log: SupvanLog = () => undefined) {}

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
          this.log(`Statut en erreur pendant « ${what} » : ${describeStatus(status)}`);
          throw new Error(`Imprimante : ${errors.join(', ')} (pendant : ${what}).`);
        }
        const value = done(status);
        if (value !== null) {
          return value;
        }
      }
      if (Date.now() >= deadline) {
        this.log(`Délai dépassé pendant « ${what} » (dernier statut : ${status ? describeStatus(status) : 'illisible'}).`);
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

const STORAGE_TRANSPORT = 'vpd.labelPrinter.transport';
const STORAGE_DENSITY = 'vpd.labelPrinter.density';
const JOURNAL_MAX_LINES = 400;
const TELEMETRY_JOURNAL_CHARS = 6000;

export interface PrinterTelemetry {
  trackEvent(name: string, properties: {[key: string]: string}): void;
}

function readSetting(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeSetting(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch { /* stockage indisponible : le réglage ne sera pas retenu */ }
}

// Message lisible pour l'erreur d'une fenêtre de choix ou d'une liaison Bluetooth.
export function describeConnectionError(error: unknown): string {
  const name = error instanceof Error ? error.name : '';
  if (name === 'NotFoundError') {
    return 'Aucune imprimante choisie. Vérifiez qu\'elle est allumée, puis réessayez.';
  }
  if (name === 'NetworkError') {
    return 'Connexion impossible : imprimante éteinte, trop loin ou déjà utilisée par un autre appareil.';
  }
  if (name === 'SecurityError' || name === 'NotAllowedError') {
    return 'Le navigateur a refusé l\'accès au Bluetooth. Autorisez-le dans les réglages du site, puis réessayez.';
  }
  return error instanceof Error ? error.message : 'Connexion impossible.';
}

function rawError(error: unknown): string {
  return error instanceof Error ? `${error.name}: ${error.message}` : String(error);
}

export class SupvanLabelPrinter implements LabelPrinter {
  deviceName = 'T50M Pro';

  private pipe: SupvanPipe | null = null;
  private blePipe: BlePipe | null = null;
  private client: SupvanClient | null = null;
  private material: MaterialInfo | null = null;
  private currentStatus: LabelPrinterStatus = 'disconnected';
  private transport: LabelPrinterTransport | null = null;
  private readonly lines: string[] = [];
  private readonly listeners = new Set<() => void>();
  private readonly started = Date.now();
  private densityLevel = DEFAULT_DENSITY;

  constructor(private readonly telemetry: PrinterTelemetry | null = null) {
    const stored = Number(readSetting(STORAGE_DENSITY));
    if (Number.isInteger(stored) && stored >= 1 && stored <= MAX_DENSITY) {
      this.densityLevel = stored;
    }
    this.log(`Navigateur : ${navigator.userAgent}`);
    this.log(`Web Bluetooth : ${webBluetooth() ? 'oui' : 'non'} · Web Serial : ${webSerial() ? 'oui' : 'non'} · contexte sécurisé : ${window.isSecureContext ? 'oui' : 'non'}`);
  }

  // Écrit une ligne du journal (diagnostic et télémétrie).
  readonly log: SupvanLog = line => {
    const seconds = ((Date.now() - this.started) / 1000).toFixed(1).padStart(7, ' ');
    this.lines.push(`${seconds}s  ${line}`);
    if (this.lines.length > JOURNAL_MAX_LINES) {
      this.lines.splice(0, this.lines.length - JOURNAL_MAX_LINES);
    }
    this.notify();
  };

  get journal(): string {
    return this.lines.join('\n');
  }

  get density(): number {
    return this.densityLevel;
  }

  set density(value: number) {
    const level = Math.min(MAX_DENSITY, Math.max(1, Math.round(value)));
    this.densityLevel = level;
    writeSetting(STORAGE_DENSITY, String(level));
    this.notify();
  }

  get roll(): LabelRoll | null {
    return this.material
      ? {widthMm: this.material.widthMm, heightMm: this.material.heightMm, remaining: this.material.remaining}
      : null;
  }

  get supvanClient(): SupvanClient | null {
    return this.client;
  }

  status(): LabelPrinterStatus {
    return this.currentStatus;
  }

  subscribe(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  availableTransports(): LabelPrinterTransport[] {
    const transports: LabelPrinterTransport[] = [];
    if (webBluetooth()) {
      transports.push('ble');
    }
    if (webSerial()) {
      transports.push('serial');
    }
    return transports;
  }

  // Dernier mode qui a fonctionné s'il est disponible, sinon le BLE (iPhone et Android), sinon le série.
  preferredTransport(): LabelPrinterTransport | null {
    const available = this.availableTransports();
    const remembered = readSetting(STORAGE_TRANSPORT);
    if ((remembered === 'ble' || remembered === 'serial') && available.includes(remembered)) {
      return remembered;
    }
    return available[0] ?? null;
  }

  async connect(transport: LabelPrinterTransport | null = this.preferredTransport()): Promise<void> {
    if (transport === null) {
      this.log('Connexion impossible : aucun Bluetooth disponible dans ce navigateur.');
      throw new Error('Ce navigateur ne peut pas parler à l\'imprimante. Android : Chrome. iPhone : Bluefy.');
    }

    const started = Date.now();
    this.log(`Connexion (${transport === 'ble' ? 'Bluetooth basse consommation' : 'Bluetooth série'})…`);
    try {
      await this.open(transport);
      writeSetting(STORAGE_TRANSPORT, transport);
      const roll = this.roll;
      this.log(`Connectée : ${this.deviceName}${roll ? `, rouleau ${roll.widthMm}×${roll.heightMm} mm, restantes ${roll.remaining ?? '?'}` : ', rouleau non lu'}.`);
      this.track('label_printer_connect', {
        ok: 'true', transport, deviceName: this.deviceName, durationMs: String(Date.now() - started),
        roll: roll ? `${roll.widthMm}x${roll.heightMm}` : '',
      });
    } catch (error) {
      const raw = rawError(error);
      this.log(`ERREUR de connexion : ${raw}`);
      this.track('label_printer_connect', {
        ok: 'false', transport, error: raw, durationMs: String(Date.now() - started),
      });
      throw new Error(describeConnectionError(error));
    }
  }

  private async open(transport: LabelPrinterTransport): Promise<void> {
    this.discard();

    let pipe: SupvanPipe;
    if (transport === 'ble') {
      const blePipe: BlePipe = new BlePipe(this.log, () => this.onLinkLost(blePipe));
      await blePipe.open();
      this.deviceName = blePipe.deviceName || this.deviceName;
      this.blePipe = blePipe;
      pipe = blePipe;
    } else {
      const serial = webSerial();
      if (!serial) {
        throw new Error('Ce navigateur ne gère pas le Bluetooth série (Web Serial). Utilisez Chrome sur Android ou ordinateur.');
      }

      // L'imprimante doit déjà être appairée dans les réglages Bluetooth de l'appareil.
      const port = await serial.requestPort({allowedBluetoothServiceClassIds: [SPP_UUID]});
      const serialPipe = new SerialPipe(port);
      await serialPipe.open();
      pipe = serialPipe;
    }

    this.transport = transport;
    try {
      await this.handshake(pipe);
    } catch (error) {
      this.discard();
      await pipe.close();
      throw error;
    }
  }

  // Vérifie que l'appareil est bien une Supvan et lit le rouleau.
  private async handshake(pipe: SupvanPipe): Promise<void> {
    const client = new SupvanClient(pipe, this.log);
    if (!(await client.checkDevice())) {
      throw new Error('L\'appareil choisi ne répond pas comme une étiqueteuse Supvan.');
    }
    this.material = await client.queryMaterial();
    this.deviceName = (await client.readDeviceName()) ?? (this.deviceName || 'T50M Pro');

    this.pipe = pipe;
    this.client = client;
    this.currentStatus = 'connected';
    this.notify();
  }

  // Coupure BLE : l'appareil reste mémorisé pour se reconnecter sans nouvelle fenêtre de choix.
  private onLinkLost(lost: BlePipe): void {
    if (this.blePipe !== lost) {
      return;
    }
    this.client = null;
    this.pipe = null;
    this.currentStatus = 'disconnected';
    this.track('label_printer_link_lost', {deviceName: this.deviceName});
    this.notify();
  }

  private discard(): void {
    const pipe = this.pipe ?? this.blePipe;
    this.pipe = null;
    this.blePipe = null;
    this.client = null;
    this.currentStatus = 'disconnected';
    void pipe?.close();
  }

  disconnect(): void {
    this.discard();
    this.material = null;
    this.transport = null;
    this.log('Déconnectée à la demande de l\'utilisateur.');
    this.notify();
  }

  // Client prêt à imprimer ; tente d'abord une reconnexion silencieuse si le BLE s'est coupé.
  private async requireClient(): Promise<SupvanClient> {
    if (this.currentStatus === 'connected' && this.client) {
      return this.client;
    }

    const blePipe = this.blePipe;
    if (blePipe) {
      try {
        await blePipe.reconnect();
        await this.handshake(blePipe);
        this.track('label_printer_reconnect', {ok: 'true', deviceName: this.deviceName});
        if (this.client) {
          return this.client;
        }
      } catch (error) {
        const raw = rawError(error);
        this.log(`Reconnexion impossible : ${raw}`);
        this.track('label_printer_reconnect', {ok: 'false', error: raw});
      }
    }

    throw new Error('Imprimante non connectée. Ouvrez « Imprimante » pour la reconnecter.');
  }

  async print(job: LabelJob, onProgress?: (done: number, total: number) => void): Promise<void> {
    const invalid = validateLabelJob(job);
    if (invalid) {
      throw new Error(invalid);
    }

    const started = Date.now();
    this.log(`Impression : code ${job.code}${job.price ? ', avec prix' : ''}, ${job.copies} exemplaire(s), densité ${this.densityLevel}.`);
    try {
      const client = await this.requireClient();
      onProgress?.(0, job.copies);
      for (let copy = 0; copy < job.copies; copy += 1) {
        await this.printLabel(client, job, false);
        onProgress?.(copy + 1, job.copies);
      }
      this.log(`Impression terminée en ${((Date.now() - started) / 1000).toFixed(1)} s.`);
      this.track('label_printer_print', {
        ok: 'true', code: job.code, copies: String(job.copies), hasPrice: String(job.price !== null),
        density: String(this.densityLevel), durationMs: String(Date.now() - started),
        roll: this.material ? `${this.material.widthMm}x${this.material.heightMm}` : '',
      });
    } catch (error) {
      const raw = rawError(error);
      this.log(`ERREUR d'impression : ${raw}`);
      this.track('label_printer_print', {
        ok: 'false', code: job.code, copies: String(job.copies), error: raw, density: String(this.densityLevel),
        durationMs: String(Date.now() - started),
      });
      throw error;
    }
  }

  // Étiquette de contrôle du diagnostic (cadre + légende) : hors parcours bénévole.
  async printDiagnosticLabel(): Promise<void> {
    const client = await this.requireClient();
    await this.printLabel(client, {code: TEST_EAN13, price: null, copies: 1}, true);
  }

  private async printLabel(client: SupvanClient, job: LabelJob, diagnostic: boolean): Promise<void> {
    // Le rouleau a pu changer depuis la connexion : relire sa taille.
    this.material = (await client.queryMaterial()) ?? this.material;
    const material = this.material;
    if (!material || material.widthMm === 0 || material.heightMm === 0) {
      throw new Error('Taille du rouleau inconnue : vérifiez que le rouleau est bien en place, capot fermé.');
    }

    this.log(`Rouleau : ${material.widthMm}×${material.heightMm} mm, écart ${material.gapMm} mm, restantes ${material.remaining ?? '?'}.`);
    if (barcodeModuleDots(material.widthMm) < 2) {
      this.log('Attention : rouleau étroit, code-barres très serré (peut être illisible).');
    }

    const image = renderLabel({
      code: job.code,
      price: job.price,
      widthMm: material.widthMm,
      heightMm: material.heightMm,
      border: diagnostic,
      caption: diagnostic ? `DIAGNOSTIC ${material.widthMm}x${material.heightMm} mm` : null,
    });
    await client.printBuffers(splitIntoBuffers(image, this.densityLevel));
  }

  describeStatus(status: PrinterStatus): string {
    return describeStatus(status);
  }

  private track(name: string, properties: {[key: string]: string}): void {
    this.telemetry?.trackEvent(name, {
      transport: this.transport ?? '',
      ...properties,
      journal: this.journal.slice(-TELEMETRY_JOURNAL_CHARS),
    });
  }

  private notify(): void {
    for (const listener of this.listeners) {
      listener();
    }
  }
}
