import {NgModule} from '@angular/core';
import {RouterModule, Routes} from '@angular/router';

import {CatalogBookDetailPageComponent} from './features/book-detail/catalog-book-detail-page.component';
import {CatalogHomePageComponent} from './features/home/catalog-home-page.component';
import {CatalogSearchPageComponent} from './features/search/catalog-search-page.component';
import {CatalogWorkPageComponent} from './features/work/catalog-work-page.component';
import {LegalPageComponent} from './features/legal/legal-page.component';

const routes: Routes = [
  {path: '', component: CatalogHomePageComponent},
  {path: 'recherche', component: CatalogSearchPageComponent},
  {path: 'catalogue', component: CatalogSearchPageComponent, data: {browse: true}},
  {path: 'livres/:slug', component: CatalogBookDetailPageComponent},
  {path: 'oeuvre/:workId', component: CatalogWorkPageComponent},
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
  {path: '**', redirectTo: ''},
];

@NgModule({
  imports: [RouterModule.forRoot(routes, {scrollPositionRestoration: 'top'})],
  exports: [RouterModule],
})
export class AppRoutingModule {}
