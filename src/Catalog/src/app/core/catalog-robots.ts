export type CatalogRobotsDirective = 'index, follow' | 'noindex, nofollow';

const PRIVATE_ROUTE_PATHS = new Set(['/compte', '/administration', '/desinscription']);

export function catalogRobotsForUrl(url: string): CatalogRobotsDirective {
  const path = url.split(/[?#]/, 1)[0].replace(/\/+$/, '') || '/';

  return PRIVATE_ROUTE_PATHS.has(path) ? 'noindex, nofollow' : 'index, follow';
}
