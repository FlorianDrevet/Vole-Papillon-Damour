import {catalogCoverUrl} from './catalog-cover-url';

describe('catalogCoverUrl', () => {
  it('normalizes and routes BnF covers through one bounded transformation', () => {
    const original = 'https://openapi.bnf.fr/couverture/image/image/recupererImage?ISBN=9782266339568&couverture=1';

    expect(catalogCoverUrl(original)).toBe(
      'https://livres.volepapillondamour.fr/cdn-cgi/image/width=660,height=990,fit=scale-down,quality=80,format=auto/https://openapi.bnf.fr/couverture/image/image/recupererImage?ISBN=9782266339568&couverture=1&taille=originale&largeur=660&hauteur=990',
    );
  });

  it('routes Open Library covers through the same transformation parameters', () => {
    expect(catalogCoverUrl('https://covers.openlibrary.org/b/id/123-L.jpg?default=false')).toBe(
      'https://livres.volepapillondamour.fr/cdn-cgi/image/width=660,height=990,fit=scale-down,quality=80,format=auto/https://covers.openlibrary.org/b/id/123-L.jpg?default=false',
    );
  });

  it('upgrades Google Books cover URLs to HTTPS before building the transformation URL', () => {
    expect(catalogCoverUrl('http://books.google.com/books/content?id=volume-42')).toBe(
      'https://livres.volepapillondamour.fr/cdn-cgi/image/width=660,height=990,fit=scale-down,quality=80,format=auto/https://books.google.com/books/content?id=volume-42',
    );
  });

  it('keeps unapproved and local image origins unchanged', () => {
    expect(catalogCoverUrl('https://example.test/book.jpg')).toBe('https://example.test/book.jpg');
    expect(catalogCoverUrl('/assets/book.jpg')).toBe('/assets/book.jpg');
    expect(catalogCoverUrl(null)).toBeNull();
  });
});
