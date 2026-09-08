import {mergeCatalogGenres} from './catalog-genres';

describe('catalog genres', () => {
  it('uses only genres returned by the API and removes duplicates', () => {
    const genres = mergeCatalogGenres(['Romans', '  Théâtre  ', 'JEUNESSE']);

    expect(genres).toEqual(['Romans', 'Théâtre', 'JEUNESSE']);
  });

  it('keeps a selected genre available while its response is loading', () => {
    expect(mergeCatalogGenres([], 'Science-fiction')).toContain('Science-fiction');
  });
});
