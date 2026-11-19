import type {OrdinaryBookPayload, RareBookPayload} from './book-without-isbn.service';

export const MAX_PHOTOS = 10;
export const MAX_PHOTO_BYTES = 8 * 1024 * 1024;
export const PHOTO_TYPES: readonly string[] = ['image/jpeg', 'image/png', 'image/webp'];
export const MAX_QUANTITY = 100_000;

export interface OrdinaryForm {
  title: string;
  quantity: string;
  authors: string;
  publisher: string;
  publicationYear: string;
  physicalFormat: string;
  language: string;
  genre: string;
  summary: string;
  edition: string;
  pages: string;
}

export interface RareForm {
  title: string;
  authorMention: string;
  publisher: string;
  publicationYear: string;
  price: string;
  condition: string;
  publicDescription: string;
}

export function emptyOrdinaryForm(): OrdinaryForm {
  return {
    title: '',
    quantity: '1',
    authors: '',
    publisher: '',
    publicationYear: '',
    physicalFormat: '',
    language: '',
    genre: '',
    summary: '',
    edition: '',
    pages: '',
  };
}

export function emptyRareForm(): RareForm {
  return {
    title: '',
    authorMention: '',
    publisher: '',
    publicationYear: '',
    price: '',
    condition: 'GoodWithFlaws',
    publicDescription: '',
  };
}

// Premier message d'erreur du formulaire ordinaire, ou null.
export function validateOrdinaryForm(form: OrdinaryForm): string | null {
  if (form.title.trim() === '') {
    return 'Le titre est obligatoire.';
  }

  const quantity = Number(form.quantity);
  if (!Number.isInteger(quantity) || quantity < 1 || quantity > MAX_QUANTITY) {
    return 'La quantité doit être comprise entre 1 et 100 000.';
  }

  return validateOptionalYear(form.publicationYear) ?? validateOptionalPages(form.pages);
}

// Premier message d'erreur du formulaire rare, ou null.
export function validateRareForm(form: RareForm): string | null {
  if (form.title.trim() === '') {
    return 'Le titre est obligatoire.';
  }

  const price = parsePrice(form.price);
  if (price === null || price <= 0) {
    return 'Le prix ferme doit être un montant positif.';
  }

  if (form.condition.trim() === '') {
    return 'L’état du livre est obligatoire.';
  }

  return validateOptionalYear(form.publicationYear);
}

export function toOrdinaryPayload(form: OrdinaryForm): OrdinaryBookPayload {
  return {
    title: form.title.trim(),
    quantityAvailable: Number(form.quantity),
    authors: nullableText(form.authors),
    publisher: nullableText(form.publisher),
    publicationYear: nullableInteger(form.publicationYear),
    physicalFormat: nullableText(form.physicalFormat),
    language: nullableText(form.language),
    genre: nullableText(form.genre),
    summary: nullableText(form.summary),
    edition: nullableText(form.edition),
    pages: nullableInteger(form.pages),
  };
}

export function toRarePayload(form: RareForm, clientGestureId: string): RareBookPayload {
  return {
    title: form.title.trim(),
    authorMention: nullableText(form.authorMention),
    publisher: nullableText(form.publisher),
    publicationYear: nullableInteger(form.publicationYear),
    price: parsePrice(form.price) ?? 0,
    condition: form.condition,
    publicDescription: nullableText(form.publicDescription),
    isbn13: null,
    clientGestureId,
  };
}

// Vérifie une photo avant envoi : type et taille (RG-183, Q11). Null si elle est acceptable.
export function validatePhoto(file: {type: string; size: number}): string | null {
  if (!PHOTO_TYPES.includes(file.type)) {
    return 'Les photos doivent être au format JPEG, PNG ou WebP.';
  }

  if (file.size > MAX_PHOTO_BYTES) {
    return 'Chaque photo doit peser au plus 8 Mo.';
  }

  return null;
}

function validateOptionalYear(value: string): string | null {
  const year = nullableInteger(value);
  return year === null || (year >= 1000 && year <= 2100)
    ? null
    : 'L’année de publication doit être comprise entre 1000 et 2100.';
}

function validateOptionalPages(value: string): string | null {
  const pages = nullableInteger(value);
  return pages === null || pages >= 1 ? null : 'Le nombre de pages doit être positif.';
}

function parsePrice(value: string): number | null {
  const normalized = value.trim().replace(',', '.');
  if (normalized === '') {
    return null;
  }

  const price = Number(normalized);
  return Number.isFinite(price) ? Math.round(price * 100) / 100 : null;
}

function nullableText(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

function nullableInteger(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return null;
  }

  const parsed = Number(trimmed);
  return Number.isInteger(parsed) ? parsed : Number.NaN;
}