import {CATALOG_FEATURED_GENRES} from '../../catalog-genres';

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
  {url: '/', label: 'Accueil'},
  {url: '/recherche', label: 'Rechercher'},
  {
    url: '/catalogue',
    label: 'Catalogue par genre',
    children: [
      ...CATALOG_FEATURED_GENRES.map(genre => ({
        url: '/catalogue',
        label: genre.label,
        hint: genre.hint,
        queryParams: {genre: genre.value},
      })),
      {url: '/catalogue', label: 'Voir tous les genres', hint: 'Parcourir le catalogue complet'},
    ],
  },
  {url: '/prochaines-dates', label: 'Les prochaines dates'},
];
