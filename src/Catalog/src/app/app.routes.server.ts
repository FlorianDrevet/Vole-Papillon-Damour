import {RenderMode, ServerRoute} from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    path: 'administration',
    renderMode: RenderMode.Client,
  },
  {
    path: 'administration/:section',
    renderMode: RenderMode.Client,
  },
  {
    path: 'compte',
    renderMode: RenderMode.Client,
  },
  {
    path: 'desinscription',
    renderMode: RenderMode.Client,
  },
  {
    path: 'livres-rares',
    renderMode: RenderMode.Server,
  },
  {
    path: 'livres-rares/:slug',
    renderMode: RenderMode.Server,
  },
  {
    path: '**',
    renderMode: RenderMode.Server,
  },
];
