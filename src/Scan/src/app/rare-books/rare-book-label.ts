// Code imprimé sur l'étiquette d'un exemplaire rare : son code interne s'il existe (RG-173), sinon son ISBN.
// Le code interne reste sur l'étiquette même après l'ajout d'un ISBN (RG-174).
export function rareBookLabelCode(book: {isbn13: string | null; internalCode?: string | null}): string | null {
  return book.internalCode ?? book.isbn13 ?? null;
}
