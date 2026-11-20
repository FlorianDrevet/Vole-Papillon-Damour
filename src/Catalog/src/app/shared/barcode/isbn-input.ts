import {ean13CheckDigit, isEan13} from './ean13';

// Saisie de l'ISBN ajouté à une fiche à code interne (RG-174). Accepte un ISBN-13 ou un ISBN-10, converti en
// ISBN-13 ; un code interne (préfixe 2) n'est pas un ISBN et est refusé.
export type AddIsbnInput =
  | {valid: true; isbn13: string}
  | {valid: false; reason: 'empty' | 'invalid' | 'internal'};

export function parseIsbnToAdd(raw: string): AddIsbnInput {
  const normalized = raw.replace(/[\s-]/g, '').toUpperCase();
  if (normalized.length === 0) {
    return {valid: false, reason: 'empty'};
  }

  if (/^\d{9}[\dX]$/.test(normalized)) {
    if (!isIsbn10ChecksumValid(normalized)) {
      return {valid: false, reason: 'invalid'};
    }

    const body = `978${normalized.slice(0, 9)}`;
    return {valid: true, isbn13: `${body}${ean13CheckDigit(body)}`};
  }

  if (isEan13(normalized)) {
    if (normalized.startsWith('978') || normalized.startsWith('979')) {
      return {valid: true, isbn13: normalized};
    }

    if (normalized.startsWith('2')) {
      return {valid: false, reason: 'internal'};
    }
  }

  return {valid: false, reason: 'invalid'};
}

function isIsbn10ChecksumValid(value: string): boolean {
  let sum = 0;
  for (let index = 0; index < 9; index += 1) {
    sum += (10 - index) * Number(value[index]);
  }

  const checkValue = value[9] === 'X' ? 10 : Number(value[9]);
  return (sum + checkValue) % 11 === 0;
}
