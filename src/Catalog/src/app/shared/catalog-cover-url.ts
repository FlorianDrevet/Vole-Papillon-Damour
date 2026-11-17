import {environment} from '../../environments/environment';

export interface CatalogCoverSize {
  readonly width: number;
  readonly height: number;
}

/** Taille de la fiche livre : comportement historique, inchangé. */
export const CATALOG_DETAIL_COVER_SIZE: CatalogCoverSize = {width: 660, height: 990};

const CLOUDFLARE_IMAGE_QUALITY = 'fit=scale-down,quality=80,format=auto';
const OPTIMIZABLE_COVER_HOSTS = new Set([
  'openapi.bnf.fr',
  'covers.openlibrary.org',
  'books.google.com',
]);

export function catalogCoverUrl(
  coverUrl: string | null,
  size: CatalogCoverSize | null = null,
): string | null {
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

    const target = size ?? CATALOG_DETAIL_COVER_SIZE;
    const imageOrigin = environment.publicUrl.replace(/\/+$/, '');
    return `${imageOrigin}/cdn-cgi/image/width=${target.width},height=${target.height},${CLOUDFLARE_IMAGE_QUALITY}/${url.toString()}`;
  } catch {
    return originalCoverUrl;
  }
}

/** Liste `srcset` (descripteurs `Nw`) pour une même couverture à plusieurs tailles. */
export function catalogCoverSrcset(
  coverUrl: string | null,
  sizes: readonly CatalogCoverSize[],
): string | null {
  if (!coverUrl || sizes.length === 0) {
    return null;
  }

  return sizes
    .map(size => `${catalogCoverUrl(coverUrl, size)} ${size.width}w`)
    .join(', ');
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
