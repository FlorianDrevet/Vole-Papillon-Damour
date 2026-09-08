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
