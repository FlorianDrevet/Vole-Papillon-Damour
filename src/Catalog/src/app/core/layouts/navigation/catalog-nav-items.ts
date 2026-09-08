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
export function createCatalogNavItems(genres: readonly string[] = []): CatalogNavItem[] {
  return [
    {url: '/', label: 'Accueil'},
    {url: '/recherche', label: 'Rechercher'},
    {
      url: '/catalogue',
      label: 'Catalogue par genre',
      children: [
        ...genres.map(genre => ({
          url: '/catalogue',
          label: genre,
          hint: 'Livres classés dans ce genre',
          queryParams: {genre},
        })),
        {url: '/catalogue', label: 'Voir tous les genres', hint: 'Parcourir le catalogue complet'},
      ],
    },
    {url: '/prochaines-dates', label: 'Les prochaines dates'},
  ];
}
