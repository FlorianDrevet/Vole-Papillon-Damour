// De l'image de l'étiquette aux blocs LZMA envoyés à la T50M Pro.
// Port de supvan-proto (bitmap.rs, buffer.rs, compress.rs, speed.rs), chemin Android / SPP, mono, plein cadre.

import {lzmaCompress} from './supvan-lzma';

export const DOTS_PER_MM = 8;
export const PRINTHEAD_WIDTH_MM = 48;
export const PRINTHEAD_DOTS = PRINTHEAD_WIDTH_MM * DOTS_PER_MM;
export const PRINT_BUF_SIZE = 4096;
const PRINT_BUF_HEADER = 14;
const MAX_BUF_DATA = 4074;
const MAX_COMPRESSED_BLOCK = PRINT_BUF_SIZE;
const MAX_GROUP_BUFFERS = 4;
const CHECKSUM_STRIDE = 256;
export const MAX_DENSITY = 15;
const MAX_SPEED = 60;
const MAT_LABEL = 1;

export const DEFAULT_DENSITY = 4;

// Image monochrome : `ink[y * width + x] === 1` = point noir.
export interface InkImage {
  width: number;
  height: number;
  ink: Uint8Array;
}

// Disposition imprimante : une colonne d'avance = `bytesPerLine` octets, point n = bit (n % 8) de l'octet n / 8.
// La ligne y de l'image est la colonne d'avance y ; le x de l'image est le point de la tête, centré sur 384 points.
export function toColumnMajor(image: InkImage): {data: Uint8Array; cols: number; bytesPerLine: number} {
  const bytesPerLine = PRINTHEAD_DOTS / 8;
  const offset = Math.floor((PRINTHEAD_DOTS - image.width) / 2);
  const data = new Uint8Array(image.height * bytesPerLine);

  for (let y = 0; y < image.height; y += 1) {
    for (let x = 0; x < image.width; x += 1) {
      if (image.ink[y * image.width + x] === 0) {
        continue;
      }
      const dot = x + offset;
      if (dot < 0 || dot >= PRINTHEAD_DOTS) {
        continue;
      }
      data[y * bytesPerLine + (dot >> 3)] |= 1 << (dot & 7);
    }
  }

  return {data, cols: image.height, bytesPerLine};
}

interface BufferParams {
  data: Uint8Array;
  bytesPerLine: number;
  cols: number;
  pageStart: boolean;
  pageEnd: boolean;
  density: number;
}

function buildPrintBuffer(params: BufferParams): Uint8Array {
  const buf = new Uint8Array(PRINT_BUF_SIZE);
  const density = Math.min(Math.max(Math.round(params.density), 0), MAX_DENSITY);

  buf[2] = (params.pageStart ? 0x02 : 0) | (params.pageEnd ? 0x04 : 0) | (params.pageEnd ? 0x08 : 0);
  buf[3] = ((density & 0x0f) << 2) | (MAT_LABEL << 6);
  buf[4] = params.cols & 0xff;
  buf[5] = (params.cols >> 8) & 0xff;
  buf[6] = params.bytesPerLine;
  // Marges haut et bas : 1 point (plein cadre).
  buf[8] = 1;
  buf[10] = 1;
  buf[12] = density;

  buf.set(params.data.subarray(0, PRINT_BUF_SIZE - PRINT_BUF_HEADER), PRINT_BUF_HEADER);

  const dataEnd = params.cols * params.bytesPerLine + PRINT_BUF_HEADER;
  let checksum = 0;
  for (let i = 2; i < PRINT_BUF_HEADER; i += 1) {
    checksum += buf[i];
  }
  for (let i = CHECKSUM_STRIDE - 1; i < Math.min(dataEnd, PRINT_BUF_SIZE); i += CHECKSUM_STRIDE) {
    checksum += buf[i];
  }
  buf[0] = checksum & 0xff;
  buf[1] = (checksum >> 8) & 0xff;
  return buf;
}

export function splitIntoBuffers(image: InkImage, density: number = DEFAULT_DENSITY): Uint8Array[] {
  const {data, cols, bytesPerLine} = toColumnMajor(image);
  const maxCols = Math.floor(MAX_BUF_DATA / bytesPerLine);
  const buffers: Uint8Array[] = [];

  for (let start = 0; start < cols; start += maxCols) {
    const count = Math.min(maxCols, cols - start);
    buffers.push(buildPrintBuffer({
      data: data.subarray(start * bytesPerLine, (start + count) * bytesPerLine),
      bytesPerLine,
      cols: count,
      pageStart: start === 0,
      pageEnd: start + count >= cols,
      density,
    }));
  }

  return buffers;
}

// Vitesse (2e paramètre de BUF_FULL) selon l'octet moyen compressé par buffer : plus c'est dense, plus c'est lent.
const SPEED_STEPS: ReadonlyArray<readonly [number, number]> = [
  [3000, 10], [2800, 15], [2500, 20], [2000, 25], [1500, 40], [1000, 45], [500, 55],
];

export function calcSpeed(bytesPerBuffer: number): number {
  const step = SPEED_STEPS.find(([min]) => bytesPerBuffer > min);
  return step ? step[1] : MAX_SPEED;
}

export interface CompressedBlock {
  data: Uint8Array;
  sourceBuffers: number;
}

export interface CompressedPage {
  blocks: CompressedBlock[];
  speed: number;
}

function concat(parts: Uint8Array[]): Uint8Array {
  const result = new Uint8Array(parts.reduce((sum, part) => sum + part.length, 0));
  let offset = 0;
  for (const part of parts) {
    result.set(part, offset);
    offset += part.length;
  }
  return result;
}

// Jusqu'à 4 buffers par bloc LZMA, en retirant un buffer à la fois tant que le bloc dépasse 4096 octets.
export function compressGrouped(buffers: Uint8Array[]): CompressedPage {
  if (buffers.length === 0) {
    throw new Error('Aucun buffer à compresser.');
  }

  const blocks: CompressedBlock[] = [];
  let speed = MAX_SPEED;
  let index = 0;

  while (index < buffers.length) {
    let take = Math.min(MAX_GROUP_BUFFERS, buffers.length - index);
    let data = lzmaCompress(concat(buffers.slice(index, index + take)));
    while (data.length > MAX_COMPRESSED_BLOCK) {
      if (take === 1) {
        throw new Error(`Bloc trop gros pour l'imprimante (${data.length} octets) : étiquette trop détaillée.`);
      }
      take -= 1;
      data = lzmaCompress(concat(buffers.slice(index, index + take)));
    }

    speed = Math.min(speed, calcSpeed(Math.floor(data.length / take)));
    blocks.push({data, sourceBuffers: take});
    index += take;
  }

  return {blocks, speed};
}
