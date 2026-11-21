// Encodeur LZMA1 « alone » sans marqueur de fin, tel que l'attend le firmware Supvan
// (en-tête 13 octets : props 0x5D, dictionnaire 8192, taille exacte ; aucun marqueur de fin).
// Encodeur volontairement minimal : littéraux uniquement avec le modèle adaptatif LZMA.
// Les trames d'étiquettes sont très régulières, donc le gain reste largement suffisant (< 4096 octets).
// La sortie est un flux LZMA valide : n'importe quel décodeur standard la relit.

const LC = 3;
const LP = 0;
const PB = 2;
const DICT_SIZE = 8192;
const PROB_INIT = 1024;
const TOP = 0x1000000;

class RangeEncoder {
  private low = 0;
  private range = 0xffffffff;
  private cache = 0;
  private cacheSize = 1;
  private readonly out: number[] = [];

  encodeBit(probs: Uint16Array, index: number, bit: number): void {
    const prob = probs[index];
    const bound = (this.range >>> 11) * prob;
    if (bit === 0) {
      this.range = bound;
      probs[index] = prob + ((2048 - prob) >> 5);
    } else {
      this.low += bound;
      this.range = (this.range - bound) >>> 0;
      probs[index] = prob - (prob >> 5);
    }
    while (this.range < TOP) {
      this.range = (this.range << 8) >>> 0;
      this.shiftLow();
    }
  }

  finish(): Uint8Array {
    for (let i = 0; i < 5; i += 1) {
      this.shiftLow();
    }
    return Uint8Array.from(this.out);
  }

  private shiftLow(): void {
    if (this.low < 0xff000000 || this.low >= 0x100000000) {
      const carry = this.low >= 0x100000000 ? 1 : 0;
      let temp = this.cache;
      do {
        this.out.push((temp + carry) & 0xff);
        temp = 0xff;
        this.cacheSize -= 1;
      } while (this.cacheSize !== 0);
      this.cache = (this.low >>> 24) & 0xff;
    }
    this.cacheSize += 1;
    this.low = (this.low & 0x00ffffff) * 256;
  }
}

export function lzmaCompress(data: Uint8Array): Uint8Array {
  const isMatch = new Uint16Array(12 << 4).fill(PROB_INIT);
  const literal = new Uint16Array(0x300 << (LC + LP)).fill(PROB_INIT);
  const encoder = new RangeEncoder();
  const posMask = (1 << PB) - 1;

  let previous = 0;
  for (let pos = 0; pos < data.length; pos += 1) {
    // État toujours 0 : que des littéraux.
    encoder.encodeBit(isMatch, pos & posMask, 0);
    const base = 0x300 * (previous >> (8 - LC));
    let symbol = 1;
    for (let shift = 7; shift >= 0; shift -= 1) {
      const bit = (data[pos] >> shift) & 1;
      encoder.encodeBit(literal, base + symbol, bit);
      symbol = (symbol << 1) | bit;
    }
    previous = data[pos];
  }

  const payload = encoder.finish();
  const result = new Uint8Array(13 + payload.length);
  result[0] = (PB * 5 + LP) * 9 + LC;
  new DataView(result.buffer).setUint32(1, DICT_SIZE, true);
  new DataView(result.buffer).setUint32(5, data.length >>> 0, true);
  result.set(payload, 13);
  return result;
}
