import {catalogSearchSidebarGenres, mergeCatalogGenres} from './catalog-genres';

describe('catalog genres', () => {
  it('keeps a curated genre shortlist independent from the API catalogue', () => {
    const genres = catalogSearchSidebarGenres();

    expect(genres).toEqual([
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
    ]);
  });

  it('keeps pinned genres before the curated shortlist and de-duplicates them', () => {
    expect(catalogSearchSidebarGenres('Sciences', ['Manga', 'Romans', 'manga'])).toEqual([
      'Manga',
      'Romans',
      'Roman',
      'Jeunesse',
      'Bande dessinée',
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
      'Sciences',
    ]);
  });

  it('keeps the shared genre merge dynamic for other catalog surfaces', () => {
    expect(mergeCatalogGenres(['Romans', '  Théâtre  ', 'JEUNESSE']))
      .toEqual(['Romans', 'Théâtre', 'JEUNESSE']);
  });

  it('keeps a selected genre available while its response is loading', () => {
    expect(mergeCatalogGenres([], 'Science-fiction')).toContain('Science-fiction');
  });
});
