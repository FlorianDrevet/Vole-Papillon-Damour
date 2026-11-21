import type {CatalogAdminBookWithoutIsbnRequest} from '../../../core/catalog.models';

export const MAX_BOOK_PHOTOS = 10;
export const MAX_BOOK_PHOTO_BYTES = 8 * 1024 * 1024;
export const BOOK_PHOTO_TYPES: readonly string[] = ['image/jpeg', 'image/png', 'image/webp'];
export const MAX_BOOK_QUANTITY = 100_000;

export interface OrdinaryBookForm {
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

export function emptyOrdinaryBookForm(): OrdinaryBookForm {
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

// Premier message d'erreur du formulaire, ou null (RG-173 : quantité ≥ 1).
export function validateOrdinaryBookForm(form: OrdinaryBookForm): string | null {
  if (form.title.trim() === '') {
    return 'Le titre est obligatoire.';
  }

  const quantity = Number(form.quantity);
  if (!Number.isInteger(quantity) || quantity < 1 || quantity > MAX_BOOK_QUANTITY) {
    return 'La quantité doit être comprise entre 1 et 100 000.';
  }

  const year = nullableInteger(form.publicationYear);
  if (year !== null && (Number.isNaN(year) || year < 1000 || year > 2100)) {
    return 'L’année de publication doit être comprise entre 1000 et 2100.';
  }

  const pages = nullableInteger(form.pages);
  if (pages !== null && (Number.isNaN(pages) || pages < 1)) {
    return 'Le nombre de pages doit être positif.';
  }

  return null;
}

export function toOrdinaryBookRequest(form: OrdinaryBookForm): CatalogAdminBookWithoutIsbnRequest {
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

// Type et taille d'une photo avant envoi (Q11). Null si elle est acceptable.
export function validateBookPhoto(file: {type: string; size: number}): string | null {
  if (!BOOK_PHOTO_TYPES.includes(file.type)) {
    return 'Les photos doivent être au format JPEG, PNG ou WebP.';
  }

  return file.size > MAX_BOOK_PHOTO_BYTES ? 'Chaque photo doit peser au plus 8 Mo.' : null;
}

function nullableText(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

// null si vide ; NaN si ce n'est pas un entier (signalé par la validation).
function nullableInteger(value: string): number | null {
  const trimmed = value.trim();
  if (trimmed === '') {
    return null;
  }

  const parsed = Number(trimmed);
  return Number.isInteger(parsed) ? parsed : Number.NaN;
}