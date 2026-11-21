// Protocole Supvan T50M Pro sur Bluetooth SPP (trames « 7E 5A »).
// Port TypeScript du chemin SPP de https://github.com/heeen/supvan-cups (MIT, crate supvan-proto),
// lui-même issu de la rétro-ingénierie de l'application Android Katasymbol.

export const MAGIC1 = 0x7e;
export const MAGIC2 = 0x5a;
const PROTO_ID = 0x10;
const PROTO_VER = 0x01;
const MARKER_AA = 0xaa;
const DATA_TYPE = 0x02;
const CMD_FLAGS = 0x01;

export const FRAME_HEADER_LEN = 4;
export const CMD_FRAME_LEN = 16;
export const CMD_OFFSET = 7;
const RESPONSE_HEADER_LEN = 22;

export const CMD = {
  BUF_FULL: 0x10,
  INQUIRY_STA: 0x11,
  CHECK_DEVICE: 0x12,
  START_PRINT: 0x13,
  STOP_PRINT: 0x14,
  RD_DEV_NAME: 0x16,
  READ_REV: 0x17,
  PAPER_SKIP: 0x2e,
  RETURN_MAT: 0x30,
  NEXT_ZIPPEDBULK: 0x5c,
  READ_FWVER: 0xc5,
} as const;

export const DATA_PAYLOAD_SIZE = 500;
export const DATA_FRAME_LEN = 512;
const DATA_PACKET_LEN = 6 + DATA_PAYLOAD_SIZE;

// Somme des octets, tronquée à 16 bits.
export function checksum16(bytes: Uint8Array): number {
  let sum = 0;
  for (const byte of bytes) {
    sum = (sum + byte) & 0xffff;
  }
  return sum;
}

// Trame de commande de 16 octets. `a` et `b` sont deux paramètres 16 bits little-endian
// (octets 12-13 et 14-15) : taille/quantité pour NEXT_ZIPPEDBULK et BUF_FULL, sinon `b` = 0.
export function makeCmd(cmd: number, a = 0, b = 0): Uint8Array {
  const frame = new Uint8Array(CMD_FRAME_LEN);
  frame[0] = MAGIC1;
  frame[1] = MAGIC2;
  frame[2] = CMD_FRAME_LEN - FRAME_HEADER_LEN;
  frame[3] = 0;
  frame[4] = PROTO_ID;
  frame[5] = PROTO_VER;
  frame[6] = MARKER_AA;
  frame[CMD_OFFSET] = cmd;
  frame[11] = CMD_FLAGS;
  frame[12] = a & 0xff;
  frame[13] = (a >> 8) & 0xff;
  frame[14] = b & 0xff;
  frame[15] = (b >> 8) & 0xff;
  const checksum = checksum16(frame.subarray(10, 16));
  frame[8] = checksum & 0xff;
  frame[9] = (checksum >> 8) & 0xff;
  return frame;
}

// Longueur totale d'une trame « 7E 5A » d'après son en-tête ; null tant qu'elle n'est pas lisible.
export function frameLen(bytes: Uint8Array): number | null {
  if (bytes.length < FRAME_HEADER_LEN || bytes[0] !== MAGIC1 || bytes[1] !== MAGIC2) {
    return null;
  }
  return (bytes[2] | (bytes[3] << 8)) + FRAME_HEADER_LEN;
}

// Découpe des données compressées en trames de 512 octets (paquets « AA BB » de 500 octets utiles).
export function buildDataFrames(data: Uint8Array): Uint8Array[] {
  const total = Math.ceil(data.length / DATA_PAYLOAD_SIZE);
  if (total > 255) {
    throw new Error(`${data.length} octets demandent plus de 255 paquets.`);
  }

  const frames: Uint8Array[] = [];
  for (let index = 0; index < total; index += 1) {
    const chunk = data.subarray(index * DATA_PAYLOAD_SIZE, (index + 1) * DATA_PAYLOAD_SIZE);
    const packet = new Uint8Array(DATA_PACKET_LEN);
    packet[0] = MARKER_AA;
    packet[1] = 0xbb;
    packet[4] = index;
    packet[5] = total;
    packet.set(chunk, 6);
    const checksum = checksum16(packet.subarray(4));
    packet[2] = checksum & 0xff;
    packet[3] = (checksum >> 8) & 0xff;

    const frame = new Uint8Array(DATA_FRAME_LEN);
    frame[0] = MAGIC1;
    frame[1] = MAGIC2;
    frame[2] = (DATA_FRAME_LEN - FRAME_HEADER_LEN) & 0xff;
    frame[3] = ((DATA_FRAME_LEN - FRAME_HEADER_LEN) >> 8) & 0xff;
    frame[4] = PROTO_ID;
    frame[5] = DATA_TYPE;
    frame.set(packet, 6);
    frames.push(frame);
  }
  return frames;
}

export interface PrinterStatus {
  bufFull: boolean;
  labelRwError: boolean;
  labelEnd: boolean;
  labelModeError: boolean;
  ribbonRwError: boolean;
  ribbonEnd: boolean;
  lowBattery: boolean;
  deviceBusy: boolean;
  headTempHigh: boolean;
  coverOpen: boolean;
  insertUsb: boolean;
  printing: boolean;
  labelNotInstalled: boolean;
  printCount: number;
  regs: [number, number, number, number];
}

function hasHeader(data: Uint8Array, minLength: number, cmd: number): boolean {
  return data.length >= Math.max(minLength, CMD_OFFSET + 1)
    && data[0] === MAGIC1
    && data[1] === MAGIC2
    && data[CMD_OFFSET] === cmd;
}

export function validateResponse(data: Uint8Array | null, cmd: number): boolean {
  return data !== null && hasHeader(data, CMD_OFFSET + 1, cmd);
}

export function parseStatus(data: Uint8Array | null): PrinterStatus | null {
  if (data === null || !hasHeader(data, 20, CMD.INQUIRY_STA)) {
    return null;
  }

  const [b0, b1, b2, b3] = [data[14], data[15], data[16], data[17]];
  return {
    bufFull: (b0 & 0x01) !== 0,
    labelRwError: (b0 & 0x02) !== 0,
    labelEnd: (b0 & 0x04) !== 0,
    labelModeError: (b0 & 0x08) !== 0,
    ribbonRwError: (b0 & 0x10) !== 0,
    ribbonEnd: (b0 & 0x20) !== 0,
    lowBattery: (b0 & 0x40) !== 0,
    deviceBusy: (b1 & 0x04) !== 0,
    headTempHigh: (b1 & 0x08) !== 0,
    coverOpen: (b2 & 0x08) !== 0,
    insertUsb: (b2 & 0x10) !== 0,
    printing: (b2 & 0x40) !== 0,
    labelNotInstalled: (b3 & 0x01) !== 0,
    printCount: data[18] | (data[19] << 8),
    regs: [b0, b1, b2, b3],
  };
}

// Erreurs bloquantes, avec un libellé destiné à la bénévole.
export function statusErrors(status: PrinterStatus): string[] {
  const errors: string[] = [];
  if (status.coverOpen) errors.push('capot ouvert');
  if (status.labelNotInstalled) errors.push('aucun rouleau d\'étiquettes détecté');
  if (status.labelEnd) errors.push('fin de rouleau');
  if (status.labelRwError) errors.push('rouleau non reconnu (erreur de lecture du rouleau)');
  if (status.labelModeError) errors.push('type de rouleau incompatible');
  if (status.ribbonRwError) errors.push('erreur de ruban');
  if (status.ribbonEnd) errors.push('fin de ruban');
  if (status.headTempHigh) errors.push('tête d\'impression trop chaude');
  return errors;
}

export function describeStatus(status: PrinterStatus): string {
  const flags: Array<[string, boolean]> = [
    ['printing', status.printing],
    ['busy', status.deviceBusy],
    ['buf_full', status.bufFull],
    ['cover_open', status.coverOpen],
    ['label_end', status.labelEnd],
    ['no_label', status.labelNotInstalled],
    ['label_rw_err', status.labelRwError],
    ['label_mode_err', status.labelModeError],
    ['ribbon_end', status.ribbonEnd],
    ['ribbon_rw_err', status.ribbonRwError],
    ['head_hot', status.headTempHigh],
    ['low_batt', status.lowBattery],
    ['usb', status.insertUsb],
  ];
  const set = flags.filter(([, on]) => on).map(([name]) => name);
  const regs = status.regs.map(reg => reg.toString(16).padStart(2, '0')).join(' ');
  return `[${set.length > 0 ? set.join(',') : '-'}] count=${status.printCount} regs=${regs}`;
}

export interface MaterialInfo {
  uuid: string;
  code: string;
  sn: number;
  labelType: number;
  widthMm: number;
  heightMm: number;
  gapMm: number;
  remaining: number | null;
}

export function parseMaterial(data: Uint8Array | null): MaterialInfo | null {
  if (data === null || !hasHeader(data, RESPONSE_HEADER_LEN, CMD.RETURN_MAT)) {
    return null;
  }

  const p = data.subarray(RESPONSE_HEADER_LEN);
  if (p.length < 21) {
    return null;
  }

  return {
    uuid: toHex(p.subarray(0, 7)),
    code: toHex(p.subarray(7, 15)),
    sn: p[15] | (p[16] << 8),
    labelType: p[17],
    widthMm: p[18],
    heightMm: p[19],
    gapMm: p[20],
    remaining: p.length >= 25 ? (p[21] | (p[22] << 8) | (p[23] << 16) | (p[24] << 24)) >>> 0 : null,
  };
}

export function parseDeviceName(data: Uint8Array | null): string | null {
  if (data === null || !hasHeader(data, RESPONSE_HEADER_LEN + 1, CMD.RD_DEV_NAME)) {
    return null;
  }

  const payloadLen = data[2] | (data[3] << 8);
  const nameLen = payloadLen - 18;
  if (nameLen <= 0 || nameLen > data.length - RESPONSE_HEADER_LEN) {
    return null;
  }
  return nulTerminated(data.subarray(RESPONSE_HEADER_LEN, RESPONSE_HEADER_LEN + nameLen));
}

export function parseFirmwareVersion(data: Uint8Array | null): number | null {
  if (data === null || !hasHeader(data, RESPONSE_HEADER_LEN + 1, CMD.READ_FWVER)) {
    return null;
  }
  return data[RESPONSE_HEADER_LEN];
}

export function parseProtocolVersion(data: Uint8Array | null): string | null {
  if (data === null || !hasHeader(data, RESPONSE_HEADER_LEN + 3, CMD.READ_REV)) {
    return null;
  }
  return nulTerminated(data.subarray(RESPONSE_HEADER_LEN, RESPONSE_HEADER_LEN + 3));
}

function nulTerminated(bytes: Uint8Array): string | null {
  const end = bytes.indexOf(0);
  const slice = end === -1 ? bytes : bytes.subarray(0, end);
  return slice.length > 0 ? new TextDecoder().decode(slice) : null;
}

export function toHex(bytes: Uint8Array): string {
  return Array.from(bytes, byte => byte.toString(16).padStart(2, '0')).join('').toUpperCase();
}

export function toSpacedHex(bytes: Uint8Array, max = 64): string {
  const shown = Array.from(bytes.subarray(0, max), byte => byte.toString(16).padStart(2, '0')).join(' ');
  return bytes.length > max ? `${shown} … (${bytes.length} octets)` : shown;
}
