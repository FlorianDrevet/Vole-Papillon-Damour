import {TestBed} from '@angular/core/testing';
import {Router} from '@angular/router';
import {RenderMode} from '@angular/ssr';

import {LegalPageComponent} from './features/legal/legal-page.component';
import {AppRoutingModule} from './app-routing.module';
import {CatalogRareBookDetailPageComponent} from './features/rare-books/catalog-rare-book-detail-page.component';
import {CatalogRareBooksPageComponent} from './features/rare-books/catalog-rare-books-page.component';
import {CatalogFavoritesPageComponent} from './features/favorites/catalog-favorites-page.component';
import {serverRoutes} from './app.routes.server';

describe('AppRoutingModule', () => {
  it('publishes a dedicated personal-data rights page', () => {
    TestBed.configureTestingModule({imports: [AppRoutingModule]});

    const route = TestBed.inject(Router).config.find(item => item.path === 'donnees-personnelles');

    expect(route?.component).toBe(LegalPageComponent);
    expect(route?.data?.['page']).toBe('rights');
    expect(route?.title).toBe('Vos données et le RGPD | Vole Papillon d’Amour');
  });

  it('redirects the administration root to overview and lazy-loads a section route', () => {
    TestBed.configureTestingModule({imports: [AppRoutingModule]});
    const routes = TestBed.inject(Router).config;
    const adminRoutes = routes.filter(item => item.path === 'administration' || item.path === 'administration/:section');
    const rootRoute = routes.find(item => item.path === 'administration');
    const sectionRoute = routes.find(item => item.path === 'administration/:section');

    expect(adminRoutes.length).toBe(2);
    expect(rootRoute?.redirectTo).toBe('administration/overview');
    expect(rootRoute?.pathMatch).toBe('full');
    expect(sectionRoute?.component).toBeUndefined();
    expect(sectionRoute?.loadChildren).toEqual(jasmine.any(Function));
    expect(serverRoutes.find(item => item.path === 'administration')?.renderMode).toBe(RenderMode.Client);
    expect(serverRoutes.find(item => item.path === 'administration/:section')?.renderMode).toBe(RenderMode.Client);
  });

  it('publishes dedicated public rare-book list and detail routes', () => {
    TestBed.configureTestingModule({imports: [AppRoutingModule]});
    const routes = TestBed.inject(Router).config;

    expect(routes.find(item => item.path === 'livres-rares')?.component)
      .toBe(CatalogRareBooksPageComponent);
    expect(routes.find(item => item.path === 'livres-rares/:id')?.component)
      .toBe(CatalogRareBookDetailPageComponent);
  });

  it('keeps the personal favorites page client-rendered and gives it a descriptive title', () => {
    TestBed.configureTestingModule({imports: [AppRoutingModule]});
    const route = TestBed.inject(Router).config.find(item => item.path === 'trouver-mon-prochain-livre');

    expect(route?.component).toBe(CatalogFavoritesPageComponent);
    expect(route?.title).toBe('Trouver mon prochain livre | Vole Papillon d’Amour');
    expect(serverRoutes.find(item => item.path === 'trouver-mon-prochain-livre')?.renderMode)
      .toBe(RenderMode.Client);
  });
});
