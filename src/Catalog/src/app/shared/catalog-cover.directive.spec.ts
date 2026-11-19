import {CatalogCoverDirective} from './catalog-cover.directive';

describe('CatalogCoverDirective', () => {
  it('drops the srcset when falling back to the source URL', () => {
    const directive = new CatalogCoverDirective();
    const originalUrl = 'https://covers.openlibrary.org/b/id/123-L.jpg';
    directive.catalogCover = originalUrl;
    directive.catalogCoverSize = {width: 240, height: 360};
    directive.catalogCoverSrcset = [{width: 240, height: 360}, {width: 480, height: 720}];

    directive.ngOnChanges();
    expect(directive.src).toContain('width=240,height=360');
    expect(directive.srcset).toContain(' 480w');

    directive.onImageError();
    expect(directive.src).toBe(originalUrl);
    expect(directive.srcset).toBeNull();
  });

  it('keeps the detail size and no srcset when no card size is requested', () => {
    const directive = new CatalogCoverDirective();
    directive.catalogCover = 'https://covers.openlibrary.org/b/id/123-L.jpg';

    directive.ngOnChanges();
    expect(directive.src).toContain('width=660,height=990');
    expect(directive.srcset).toBeNull();
  });

  it('falls back to the source URL once and hides the image after a second failure', () => {
    const directive = new CatalogCoverDirective();
    const originalUrl = 'https://covers.openlibrary.org/b/id/123-L.jpg';
    const failed = jasmine.createSpy('failed');
    directive.catalogCoverFailed.subscribe(failed);
    directive.catalogCover = originalUrl;

    directive.ngOnChanges();
    expect(directive.src).toBe(
      'https://livres.volepapillondamour.fr/cdn-cgi/image/width=660,height=990,fit=scale-down,quality=80,format=auto/https://covers.openlibrary.org/b/id/123-L.jpg',
    );

    directive.onImageError();
    expect(directive.src).toBe(originalUrl);
    expect(directive.hidden).toBeNull();

    directive.onImageError();
    expect(directive.hidden).toBe('');
    expect(failed).toHaveBeenCalledTimes(1);
  });

  it('hides a broken image immediately when its source does not use transformations', () => {
    const directive = new CatalogCoverDirective();
    directive.catalogCover = 'https://example.test/book.jpg';

    directive.ngOnChanges();
    directive.onImageError();

    expect(directive.hidden).toBe('');
    expect(directive.src).toBe('https://example.test/book.jpg');
  });
});
