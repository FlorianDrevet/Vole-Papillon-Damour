// EAN-13 : calcul de la clé, validation et encodage des barres.
// Un ISBN-13 (préfixe 978 ou 979) est un EAN-13 ; un code interne (RG-170) en est un
// avec un préfixe réservé à un usage interne (plage 2xx).

const L_CODES = ['0001101', '0011001', '0010011', '0111101', '0100011', '0110001', '0101111', '0111011', '0110111', '0001011'];
const G_CODES = ['0100111', '0110011', '0011011', '0100001', '0011101', '0111001', '0000101', '0010001', '0001001', '0010111'];
const R_CODES = ['1110010', '1100110', '1101100', '1000010', '1011100', '1001110', '1010000', '1000100', '1001000', '1110100'];

// Motif de parité des six chiffres de gauche, selon le premier chiffre.
const PARITY_PATTERNS = [
  'LLLLLL', 'LLGLGG', 'LLGGLG', 'LLGGGL', 'LGLLGG',
  'LGGLLG', 'LGGGLL', 'LGLGLG', 'LGLGGL', 'LGGLGL',
];

const GUARD_START = '101';
const GUARD_CENTER = '01010';
const GUARD_END = '101';

export const EAN13_MODULE_COUNT = 95;

export function ean13CheckDigit(first12: string): number {
  if (!/^\d{12}$/.test(first12)) {
    throw new Error('Un EAN-13 demande exactement 12 chiffres avant la clé.');
  }

  let sum = 0;
  for (let index = 0; index < 12; index += 1) {
    sum += Number(first12[index]) * (index % 2 === 0 ? 1 : 3);
  }

  return (10 - (sum % 10)) % 10;
}

export function isEan13(value: string | null | undefined): boolean {
  if (!value || !/^\d{13}$/.test(value)) {
    return false;
  }

  return Number(value[12]) === ean13CheckDigit(value.slice(0, 12));
}

// Code interne attribué par la plateforme (RG-170) : EAN-13 dont le préfixe est 2xx.
export function isInternalCode(value: string | null | undefined): boolean {
  return isEan13(value) && value![0] === '2';
}

// Retourne les 95 modules (1 = barre, 0 = espace) de l'EAN-13.
export function ean13Modules(code: string): string {
  if (!isEan13(code)) {
    throw new Error(`Code EAN-13 invalide : ${code}`);
  }

  const parity = PARITY_PATTERNS[Number(code[0])];
  let left = '';
  for (let index = 1; index <= 6; index += 1) {
    const digit = Number(code[index]);
    left += parity[index - 1] === 'L' ? L_CODES[digit] : G_CODES[digit];
  }

  let right = '';
  for (let index = 7; index <= 12; index += 1) {
    right += R_CODES[Number(code[index])];
  }

  return GUARD_START + left + GUARD_CENTER + right + GUARD_END;
}

export interface Ean13BarRect {
  x: number;
  width: number;
}

// Regroupe les modules consécutifs à 1 en rectangles, pour un SVG.
export function ean13BarRects(code: string): Ean13BarRect[] {
  const modules = ean13Modules(code);
  const rects: Ean13BarRect[] = [];

  let index = 0;
  while (index < modules.length) {
    if (modules[index] === '1') {
      const start = index;
      while (index < modules.length && modules[index] === '1') {
        index += 1;
      }
      rects.push({x: start, width: index - start});
    } else {
      index += 1;
    }
  }

  return rects;
}
