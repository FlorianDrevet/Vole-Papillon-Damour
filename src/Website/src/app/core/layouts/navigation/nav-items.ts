export interface SiteNavItem {
  url: string;
  label: string;
}

/** Les 7 entrées du header, dans l'ordre de la maquette. */
export const SITE_NAV_ITEMS: SiteNavItem[] = [
  { url: '/accueil', label: 'Accueil' },
  { url: '/association', label: "L'association" },
  { url: '/maxence', label: 'Maxence' },
  { url: '/nos-actions', label: 'Nos actions' },
  { url: '/evenement', label: 'Évènements' },
  { url: '/toute-l-actualite', label: 'Actualités' },
  { url: '/contact', label: 'Contact' },
];

/** Fil d'ariane mobile : préfixe d'URL -> libellé "Rubrique · sous-rubrique". */
const BREADCRUMB_ENTRIES: [string, string][] = [
  ['/association/presentation', "L'association · qui sommes-nous"],
  ['/association/comment-aider', "L'association · nous aider"],
  ['/association/revue-de-presses', "L'association · la presse en parle"],
  ['/association/photos', "L'association · galerie photos"],
  ['/maxence/histoire', 'Maxence · son histoire'],
  ['/maxence/maladies', 'Maxence · ses maladies'],
  ['/maxence/vie-quotidienne/soins-quotidiens', 'Maxence · soins quotidiens'],
  ['/maxence/vie-quotidienne/soins-hospitaliers', 'Maxence · soins hospitaliers'],
  ['/maxence/vie-quotidienne/ecole', 'Maxence · école'],
  ['/maxence/vie-quotidienne/greffe', 'Maxence · la greffe'],
  ['/nos-actions', 'Nos actions'],
  ['/evenement/all', 'Évènements · tout l’agenda'],
  ['/evenement', 'Évènements'],
  ['/toute-l-actualite', 'Actualités'],
  ['/actualite', 'Actualités · article'],
  ['/contact', 'Contact'],
  ['/accueil', 'Accueil'],
];

export function getActiveNavUrl(currentUrl: string): string | null {
  const match = SITE_NAV_ITEMS.find(item => item.url !== '/accueil' && currentUrl.startsWith(item.url));
  if (match) return match.url;
  return currentUrl.startsWith('/accueil') ? '/accueil' : null;
}

export function getBreadcrumb(currentUrl: string): string {
  const match = BREADCRUMB_ENTRIES.find(([prefix]) => currentUrl.startsWith(prefix));
  return match ? match[1] : 'Accueil';
}
