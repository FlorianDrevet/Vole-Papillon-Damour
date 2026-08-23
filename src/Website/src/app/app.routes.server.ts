import { RenderMode, ServerRoute } from '@angular/ssr';

import { LEGAL_PAGE_PATHS } from './feature/legal/legal-page-paths';

export const serverRoutes: ServerRoute[] = [
  {
    path: 'association',
    renderMode: RenderMode.Server,
  },
  {
    path: 'association/presentation',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'association/comment-aider',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'association/revue-de-presses',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'association/photos',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence',
    renderMode: RenderMode.Server,
  },
  {
    path: 'maxence/histoire',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/maladies',
    renderMode: RenderMode.Server,
  },
  {
    path: 'maxence/maladies/gastrostomie',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/maladies/hirschsprung',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/maladies/wolff-parkinson-white',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/maladies/dysplasie-ectodermique',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/maladies/neuropathie',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/maladies/ostéoporose',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/maladies/poic',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/maladies/hyperthyroidie',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/vie-quotidienne',
    renderMode: RenderMode.Server,
  },
  {
    path: 'maxence/vie-quotidienne/soins-quotidiens',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/vie-quotidienne/soins-hospitaliers',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/vie-quotidienne/ecole',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'maxence/vie-quotidienne/greffe',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'accueil',
    renderMode: RenderMode.Server,
  },
  {
    path: 'nos-actions',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'contact',
    renderMode: RenderMode.Prerender,
  },
  {
    path: 'toute-l-actualite',
    renderMode: RenderMode.Server,
  },
  {
    path: 'actualite/:id',
    renderMode: RenderMode.Server,
  },
  {
    path: 'evenement',
    renderMode: RenderMode.Server,
  },
  {
    path: 'evenement/all',
    renderMode: RenderMode.Server,
  },
  {
    path: 'evenement/:id/tableau',
    renderMode: RenderMode.Client,
  },
  {
    path: 'evenement/:id',
    renderMode: RenderMode.Server,
  },
  {
    path: LEGAL_PAGE_PATHS.mentionsLegales,
    renderMode: RenderMode.Prerender,
  },
  {
    path: LEGAL_PAGE_PATHS.politiqueConfidentialite,
    renderMode: RenderMode.Prerender,
  },
  {
    path: LEGAL_PAGE_PATHS.politiqueCookies,
    renderMode: RenderMode.Prerender,
  },
  {
    path: LEGAL_PAGE_PATHS.accessibilite,
    renderMode: RenderMode.Prerender,
  },
  {
    path: '**',
    renderMode: RenderMode.Server,
  },
];
