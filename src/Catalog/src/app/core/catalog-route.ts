import {isCatalogAdminSection} from './catalog-administration-route';

const KNOWN_STATIC_ROUTE_PATHS = new Set([
  '/',
  '/prochaines-dates',
  '/recherche',
  '/catalogue',
  '/livres-rares',
  '/administration',
  '/compte',
  '/desinscription',
  '/mentions-legales',
  '/confidentialite',
  '/donnees-personnelles',
  '/politique-de-confidentialite',
  '/politique-de-cookies',
  '/accessibilite',
]);

export function catalogRoutePath(url: string): string {
  return url.split(/[?#]/, 1)[0].replace(/\/+$/, '') || '/';
}

export function isKnownCatalogRoute(url: string): boolean {
  const path = catalogRoutePath(url);
  if (KNOWN_STATIC_ROUTE_PATHS.has(path)) {
    return true;
  }

  const segments = path.split('/').filter(Boolean);
  if (segments[0] === 'administration') {
    return segments.length === 2 && isCatalogAdminSection(segments[1]);
  }

  return segments.length === 2 && (
    segments[0] === 'livres' ||
    segments[0] === 'oeuvre' ||
    segments[0] === 'livres-rares'
  );
}

export function isCatalogAdministrationRoute(url: string): boolean {
  const path = catalogRoutePath(url);
  if (path === '/administration') {
    return true;
  }

  const segments = path.split('/').filter(Boolean);
  return segments.length === 2 && segments[0] === 'administration' && isCatalogAdminSection(segments[1]);
}
