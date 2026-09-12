import {NgModule} from '@angular/core';
import {RouterModule, Routes} from '@angular/router';

import {CatalogBookDetailPageComponent} from './features/book-detail/catalog-book-detail-page.component';
import {CatalogHomePageComponent} from './features/home/catalog-home-page.component';
import {CatalogSearchPageComponent} from './features/search/catalog-search-page.component';
import {CatalogWorkPageComponent} from './features/work/catalog-work-page.component';
import {LegalPageComponent} from './features/legal/legal-page.component';
import {CatalogAdministrationPageComponent} from './features/administration/catalog-administration-page.component';
import {CatalogAccountPageComponent} from './features/account/catalog-account-page.component';
import {CatalogUnsubscribePageComponent} from './features/account/catalog-unsubscribe-page.component';
import {CatalogNotFoundPageComponent} from './features/not-found/catalog-not-found-page.component';

const routes: Routes = [
  {path: '', component: CatalogHomePageComponent},
  {path: 'prochaines-dates', component: CatalogHomePageComponent, data: {upcomingOnly: true}},
  {path: 'recherche', component: CatalogSearchPageComponent},
  {path: 'catalogue', component: CatalogSearchPageComponent, data: {browse: true}},
  {path: 'livres/:slug', component: CatalogBookDetailPageComponent},
  {path: 'oeuvre/:workId', component: CatalogWorkPageComponent},
  {path: 'administration', component: CatalogAdministrationPageComponent},
  {path: 'compte', component: CatalogAccountPageComponent},
  {path: 'desinscription', component: CatalogUnsubscribePageComponent},
  {
    path: 'mentions-legales',
    component: LegalPageComponent,
    data: {page: 'legal'},
  },
  {
    path: 'confidentialite',
    component: LegalPageComponent,
    data: {page: 'privacy'},
  },
  {
    path: 'donnees-personnelles',
    component: LegalPageComponent,
    title: 'Vos données et le RGPD | Vole Papillon d’Amour',
    data: {page: 'rights'},
  },
  {
    path: 'politique-de-confidentialite',
    redirectTo: 'confidentialite',
    pathMatch: 'full',
  },
  {
    path: 'politique-de-cookies',
    component: LegalPageComponent,
    data: {page: 'cookies'},
  },
  {
    path: 'accessibilite',
    component: LegalPageComponent,
    data: {page: 'accessibility'},
  },
  {path: '**', component: CatalogNotFoundPageComponent},
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {scrollPositionRestoration: 'top'})],
  exports: [RouterModule],
})
export class AppRoutingModule {}
