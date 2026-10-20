import {environment} from '../../environments/environment';

const CLOUDFLARE_IMAGE_OPTIONS = 'width=660,height=990,fit=scale-down,quality=80,format=auto';
const OPTIMIZABLE_COVER_HOSTS = new Set([
  'openapi.bnf.fr',
  'covers.openlibrary.org',
  'books.google.com',
]);

export function catalogCoverUrl(coverUrl: string | null): string | null {
  if (!coverUrl) {
    return null;
  }

  const originalCoverUrl = catalogOriginalCoverUrl(coverUrl);
  if (!originalCoverUrl || !environment.production) {
    return originalCoverUrl;
  }

  try {
    const url = new URL(originalCoverUrl);
    if (!OPTIMIZABLE_COVER_HOSTS.has(url.hostname)) {
      return originalCoverUrl;
    }

    const imageOrigin = environment.publicUrl.replace(/\/+$/, '');
    return `${imageOrigin}/cdn-cgi/image/${CLOUDFLARE_IMAGE_OPTIONS}/${url.toString()}`;
  } catch {
    return originalCoverUrl;
  }
}

export function catalogOriginalCoverUrl(coverUrl: string | null): string | null {
  if (!coverUrl) {
    return null;
  }

  try {
    const url = new URL(coverUrl);
    if (
      url.hostname === 'openapi.bnf.fr' &&
      url.pathname.endsWith('/recupererImage') &&
      url.searchParams.get('taille') !== 'originale'
    ) {
      url.searchParams.set('taille', 'originale');
      url.searchParams.set('largeur', '660');
      url.searchParams.set('hauteur', '990');
    }

    if (url.hostname === 'books.google.com' && url.protocol === 'http:') {
      url.protocol = 'https:';
    }

    return url.toString();
  } catch {
    return coverUrl;
  }
}
