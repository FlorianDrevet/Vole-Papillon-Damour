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

  it('recognizes manual requests immediately after not-found', () => {
    expect(CATALOG_ADMIN_SECTIONS.indexOf('manual-requests'))
      .toBe(CATALOG_ADMIN_SECTIONS.indexOf('not-found') + 1);
    expect(isCatalogAdminSection('manual-requests')).toBeTrue();
    expect(catalogAdminSectionPath('manual-requests')).toBe('/administration/manual-requests');
  });

  it('recognizes the ISBN analysis queue as an administration section', () => {
    expect(isCatalogAdminSection('isbn-queue')).toBeTrue();
    expect(catalogAdminSectionPath('isbn-queue')).toBe('/administration/isbn-queue');
  });

  it('recognizes the ISBNdb absent-books workspace', () => {
    expect(CATALOG_ADMIN_SECTIONS).toContain('isbndb-absent');
    expect(isCatalogAdminSection('isbndb-absent')).toBeTrue();
    expect(catalogAdminSectionPath('isbndb-absent')).toBe('/administration/isbndb-absent');
  });
});
