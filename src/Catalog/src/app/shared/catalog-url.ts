import {CatalogBook} from '../core/catalog.models';

export function slugify(value: string | null | undefined): string {
  const normalized = (value || 'livre')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');

  return normalized || 'livre';
}

export function publicBookPath(book: CatalogBook): string {
  const words = [book.title, book.authors].filter((value): value is string => Boolean(value?.trim()));
  const slug = slugify(words.join(' '));
  return `/livres/${slug}-${book.isbn13}`;
}
