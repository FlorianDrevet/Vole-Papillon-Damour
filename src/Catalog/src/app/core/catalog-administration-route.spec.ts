import {
  CATALOG_ADMIN_SECTIONS,
  catalogAdminSectionPath,
  isCatalogAdminSection,
} from './catalog-administration-route';

describe('catalog administration routes', () => {
  it('recognizes the rare-books workspace as an administration section', () => {
    expect(CATALOG_ADMIN_SECTIONS).toContain('rare-books');
    expect(isCatalogAdminSection('rare-books')).toBeTrue();
  });

  it('route: not-found is a valid admin section', () => {
    expect(CATALOG_ADMIN_SECTIONS).toContain('not-found');
    expect(isCatalogAdminSection('not-found')).toBeTrue();
    expect(catalogAdminSectionPath('not-found')).toBe('/administration/not-found');
  });
});
