import {CatalogCoverDirective} from './catalog-cover.directive';

describe('CatalogCoverDirective', () => {
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
