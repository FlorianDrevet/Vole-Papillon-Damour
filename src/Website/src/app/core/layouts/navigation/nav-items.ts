export interface SiteNavItem {
  url: string;
  label: string;
  /** Accroche affichée sous le libellé dans le sous-menu desktop. */
  hint?: string;
  /** Préfixe d'URL qui surligne l'entrée, quand `url` pointe une page parmi plusieurs. */
  matchPrefix?: string;
  /** Sous-rubriques : dépliées au survol sur desktop, listées sous le parent sur mobile. */
  children?: SiteNavItem[];
}

/** Les entrées du header, dans l'ordre de la maquette. */
export const SITE_NAV_ITEMS: SiteNavItem[] = [
  { url: '/accueil', label: 'Accueil' },
  {
    url: '/association',
    label: "L'association",
    // Les quatre pages de la rubrique n'étaient atteignables que par le footer :
    // le sous-menu leur redonne une entrée depuis le header.
    children: [
      { url: '/association/presentation', label: 'Qui sommes-nous ?', hint: 'Nos missions et nos valeurs' },
      { url: '/association/comment-aider', label: 'Comment nous aider ?', hint: 'Donner des livres, devenir bénévole' },
      { url: '/association/revue-de-presses', label: 'La presse en parle', hint: 'Les articles consacrés à Maxence' },
      { url: '/association/photos', label: 'Galerie photos', hint: 'Les dons de livres, le loto et nos actions' },
    ],
  },
  {
    url: '/maxence',
    label: 'Maxence',
    // Reprend les onglets de la rubrique Maxence,
    // supprimée au profit de ce sous-menu.
    children: [
      { url: '/maxence/histoire', label: 'Son histoire', hint: 'La chronologie, année par année' },
      { url: '/maxence/maladies', label: 'Ses maladies', hint: 'Les pathologies expliquées simplement' },
      {
        url: '/maxence/vie-quotidienne',
        label: 'Son quotidien, ses combats',
        hint: "Les soins, l'école, l'hôpital, la greffe",
        matchPrefix: '/maxence/vie-quotidienne',
      },
      { url: '/maxence/souvenirs', label: 'Des souvenirs plein les yeux', hint: 'Les rencontres, sorties et rêves réalisés' },
    ],
  },
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
  ['/maxence/souvenirs', 'Maxence · ses souvenirs'],
  ['/maxence/vie-quotidienne/soins-quotidiens', 'Maxence · soins quotidiens'],
  ['/maxence/vie-quotidienne/soins-hospitaliers', 'Maxence · soins hospitaliers'],
  ['/maxence/vie-quotidienne/ecole', 'Maxence · école'],
  ['/maxence/vie-quotidienne/greffe', 'Maxence · la greffe'],
  ['/maxence/vie-quotidienne', 'Maxence · son quotidien'],
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
