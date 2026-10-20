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
export function createCatalogNavItems(): CatalogNavItem[] {
  return [
    {url: '/', label: 'Accueil'},
    {url: '/recherche', label: 'Rechercher'},
    {url: '/livres-rares', label: 'Livres rares', hint: 'La sélection des exemplaires singuliers'},
    {url: '/prochaines-dates', label: 'Les prochaines dates'},
  ];
}
