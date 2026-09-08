import {catalogRoutePath, isKnownCatalogRoute} from './catalog-route';

export type CatalogRobotsDirective = 'index, follow' | 'noindex, nofollow';

const PRIVATE_ROUTE_PATHS = new Set(['/compte', '/administration', '/desinscription']);

export function catalogRobotsForUrl(url: string): CatalogRobotsDirective {
  const path = catalogRoutePath(url);

  return PRIVATE_ROUTE_PATHS.has(path) || !isKnownCatalogRoute(path)
    ? 'noindex, nofollow'
    : 'index, follow';
}
