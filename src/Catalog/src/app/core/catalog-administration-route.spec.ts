import {CATALOG_ADMIN_SECTIONS, isCatalogAdminSection} from './catalog-administration-route';

describe('catalog administration routes', () => {
  it('recognizes the rare-books workspace as an administration section', () => {
    expect(CATALOG_ADMIN_SECTIONS).toContain('rare-books');
    expect(isCatalogAdminSection('rare-books')).toBeTrue();
  });
});
