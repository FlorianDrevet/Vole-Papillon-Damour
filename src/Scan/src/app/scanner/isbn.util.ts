import {isInternalCode} from '../label-printer/ean13';

export function normalizeIsbn(input: string | null | undefined): string | null {
  if (!input?.trim()) {
    return null;
  }

  const normalized = input.replace(/[\s-]/g, '');

  if (normalized.length === 10 && isValidIsbn10(normalized)) {
    const firstTwelveDigits = `978${normalized.slice(0, 9)}`;
    return `${firstTwelveDigits}${isbn13CheckDigit(firstTwelveDigits)}`;
  }

  if (normalized.length === 13 && isValidIsbn13(normalized)) {
    return normalized;
  }

  return null;
}

function isValidIsbn10(value: string): boolean {
  if (!/^\d{9}[\dXx]$/.test(value)) {
    return false;
  }

  let sum = value[9].toUpperCase() === 'X' ? 10 : Number(value[9]);
  for (let index = 0; index < 9; index += 1) {
    sum += (10 - index) * Number(value[index]);
  }

  return sum % 11 === 0;
}

function isValidIsbn13(value: string): boolean {
  return /^(978|979)\d{10}$/.test(value)
    && Number(value[12]) === isbn13CheckDigit(value.slice(0, 12));
}

function isbn13CheckDigit(firstTwelveDigits: string): number {
  let sum = 0;
  for (let index = 0; index < firstTwelveDigits.length; index += 1) {
    sum += Number(firstTwelveDigits[index]) * (index % 2 === 0 ? 1 : 3);
  }

  return (10 - (sum % 10)) % 10;
}

// ISBN-13 normalisé, ou code interne valide (RG-170, RG-172). Null sinon.
export function normalizeBookCode(input: string | null | undefined): string | null {
  const isbn13 = normalizeIsbn(input);
  if (isbn13 !== null) {
    return isbn13;
  }

  const digits = (input ?? '').replace(/[\s-]/g, '');
  return isInternalCode(digits) ? digits : null;
}