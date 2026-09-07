import {CATALOG_FEATURED_GENRES, mergeCatalogGenres} from './catalog-genres';

describe('catalog genres', () => {
  it('keeps featured genres first and merges API genres without duplicates', () => {
    const genres = mergeCatalogGenres(['Romans', '  Théâtre  ', 'JEUNESSE']);

    expect(genres.slice(0, CATALOG_FEATURED_GENRES.length)).toEqual(
      CATALOG_FEATURED_GENRES.map(genre => genre.value),
    );
    expect(genres).toContain('Théâtre');
    expect(genres.filter(genre => genre.toLowerCase() === 'jeunesse').length).toBe(1);
  });

  it('keeps a selected genre available while its response is loading', () => {
    expect(mergeCatalogGenres([], 'Science-fiction')).toContain('Science-fiction');
  });
});
