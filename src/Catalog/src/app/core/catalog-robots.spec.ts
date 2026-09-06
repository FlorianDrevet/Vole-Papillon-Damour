import {catalogRobotsForUrl} from './catalog-robots';

describe('catalogRobotsForUrl', () => {
  it('keeps member and administration routes out of search indexes', () => {
    expect(catalogRobotsForUrl('/compte')).toBe('noindex, nofollow');
    expect(catalogRobotsForUrl('/compte?returnUrl=%2F')).toBe('noindex, nofollow');
    expect(catalogRobotsForUrl('/administration/')).toBe('noindex, nofollow');
    expect(catalogRobotsForUrl('/desinscription')).toBe('noindex, nofollow');
  });

  it('keeps public routes indexable', () => {
    expect(catalogRobotsForUrl('/')).toBe('index, follow');
    expect(catalogRobotsForUrl('/recherche?q=livre')).toBe('index, follow');
    expect(catalogRobotsForUrl('/livres/un-livre-9782070612758#details')).toBe('index, follow');
  });
});
