import {CatalogDeadStockBook, CatalogDeadStockResponse} from '../../core/catalog.models';

export function toDeadStockCsv(response: CatalogDeadStockResponse): string {
  const header = [
    'ISBN',
    'Titre',
    'Auteur',
    'Éditeur',
    'Année',
    'Genre',
    'Exemplaires',
    'Disponible depuis',
  ];
  const rows = response.books.map(book => [
    book.isbn13,
    book.title,
    book.authors,
    book.publisher,
    book.publicationYear,
    book.genre,
    book.quantityAvailable,
    book.firstAvailableAt,
  ]);

  return [header, ...rows]
    .map(row => row.map(value => csvCell(value)).join(';'))
    .join('\r\n') + '\r\n';
}

function csvCell(value: CatalogDeadStockBook[keyof CatalogDeadStockBook]): string {
  const text = value === null ? '' : String(value);
  const safeText = typeof value === 'string' && /^[=+\-@]/.test(text) ? `'${text}` : text;
  return /[;"\r\n]/.test(safeText)
    ? `"${safeText.replaceAll('"', '""')}"`
    : safeText;
}
