import {TestBed} from '@angular/core/testing';
import {Router} from '@angular/router';

import {CatalogAdministrationPageComponent} from './features/administration/catalog-administration-page.component';
import {LegalPageComponent} from './features/legal/legal-page.component';
import {AppRoutingModule} from './app-routing.module';
import {CatalogRareBookDetailPageComponent} from './features/rare-books/catalog-rare-book-detail-page.component';
import {CatalogRareBooksPageComponent} from './features/rare-books/catalog-rare-books-page.component';

describe('AppRoutingModule', () => {
  it('publishes a dedicated personal-data rights page', () => {
    TestBed.configureTestingModule({imports: [AppRoutingModule]});

    const route = TestBed.inject(Router).config.find(item => item.path === 'donnees-personnelles');

    expect(route?.component).toBe(LegalPageComponent);
    expect(route?.data?.['page']).toBe('rights');
    expect(route?.title).toBe('Vos données et le RGPD | Vole Papillon d’Amour');
  });

  it('routes each administration workspace through a dedicated URL segment', () => {
    TestBed.configureTestingModule({imports: [AppRoutingModule]});

    const route = TestBed.inject(Router).config.find(item => item.path === 'administration/:section');

    expect(route?.component).toBe(CatalogAdministrationPageComponent);
  });

  it('publishes dedicated public rare-book list and detail routes', () => {
    TestBed.configureTestingModule({imports: [AppRoutingModule]});
    const routes = TestBed.inject(Router).config;

    expect(routes.find(item => item.path === 'livres-rares')?.component)
      .toBe(CatalogRareBooksPageComponent);
    expect(routes.find(item => item.path === 'livres-rares/:slug')?.component)
      .toBe(CatalogRareBookDetailPageComponent);
  });
});
