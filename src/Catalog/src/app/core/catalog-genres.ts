export interface CatalogGenreOption {
  readonly value: string;
  readonly label: string;
  readonly hint: string;
}

export const CATALOG_FEATURED_GENRES: readonly CatalogGenreOption[] = [
  {value: 'Romans', label: 'Romans', hint: 'Récits, littérature et poche'},
  {value: 'Jeunesse', label: 'Jeunesse', hint: 'Albums et premières lectures'},
  {value: 'BD', label: 'BD, mangas & comics', hint: 'Séries et one-shots'},
  {value: 'Policier', label: 'Policier & thriller', hint: 'Enquêtes, suspense et noir'},
  {value: 'Documentaires', label: 'Documentaires', hint: 'Histoire, nature, cuisine et art'},
];

export function mergeCatalogGenres(
  genres: readonly string[] | null | undefined,
  selectedGenre = '',
): string[] {
  const values = [selectedGenre, ...CATALOG_FEATURED_GENRES.map(genre => genre.value), ...(genres ?? [])];
  const seen = new Set<string>();

  return values.reduce<string[]>((result, genre) => {
    const trimmedGenre = genre.trim();
    const key = trimmedGenre.toLocaleLowerCase('fr-FR');
    if (!trimmedGenre || seen.has(key)) {
      return result;
    }

    seen.add(key);
    result.push(trimmedGenre);
    return result;
  }, []);
}
