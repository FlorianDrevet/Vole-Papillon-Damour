export const CURATED_CATALOG_GENRES = [
  'Roman',
  'Jeunesse',
  'Bande dessinée',
  'Manga',
  'Policier',
  'Thriller',
  'Science-fiction',
  'Fantasy',
  'Romance',
  'Histoire',
  'Biographie',
  'Documentaire',
  'Essai',
  'Théâtre',
  'Poésie',
  'Cuisine',
] as const;

export function mergeCatalogGenres(
  genres: readonly string[] | null | undefined,
  selectedGenre = '',
): string[] {
  const values = [selectedGenre, ...(genres ?? [])];
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

export function catalogSearchSidebarGenres(
  selectedGenre = '',
  pinnedGenres: readonly string[] = [],
): string[] {
  const values = [...pinnedGenres, ...CURATED_CATALOG_GENRES, selectedGenre];
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
