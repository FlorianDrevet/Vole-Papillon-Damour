export function catalogCoverUrl(coverUrl: string | null): string | null {
  if (!coverUrl) {
    return null;
  }

  try {
    const url = new URL(coverUrl);
    if (
      url.hostname !== 'openapi.bnf.fr' ||
      !url.pathname.endsWith('/recupererImage') ||
      url.searchParams.get('taille') === 'originale'
    ) {
      return coverUrl;
    }

    url.searchParams.set('taille', 'originale');
    url.searchParams.set('largeur', '660');
    url.searchParams.set('hauteur', '990');
    return url.toString();
  } catch {
    return coverUrl;
  }
}
