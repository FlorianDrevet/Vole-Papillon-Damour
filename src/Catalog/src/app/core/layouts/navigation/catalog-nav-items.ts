export interface CatalogNavItem {
  url: string;
  label: string;
  hint?: string;
  queryParams?: Record<string, string>;
  children?: CatalogNavItem[];
}

/**
 * The catalog keeps the Website's navigation rhythm while exposing only the
 * routes that belong to this application.
 */
export const CATALOG_NAV_ITEMS: CatalogNavItem[] = [
  {url: '/recherche', label: 'Rechercher'},
  {
    url: '/catalogue',
    label: 'Catalogue par genre',
    children: [
      {url: '/catalogue', label: 'Romans', hint: 'Récits, littérature et poche', queryParams: {genre: 'Romans'}},
      {url: '/catalogue', label: 'Jeunesse', hint: 'Albums et premières lectures', queryParams: {genre: 'Jeunesse'}},
      {url: '/catalogue', label: 'BD, mangas & comics', hint: 'Séries et one-shots', queryParams: {genre: 'BD'}},
      {url: '/catalogue', label: 'Policier & thriller', hint: 'Enquêtes, suspense et noir', queryParams: {genre: 'Policier'}},
      {url: '/catalogue', label: 'Documentaires', hint: 'Histoire, nature, cuisine et art', queryParams: {genre: 'Documentaires'}},
      {url: '/catalogue', label: 'Voir tous les genres', hint: 'Parcourir le catalogue complet'},
    ],
  },
  {url: '/', label: 'La prochaine bourse'},
];
