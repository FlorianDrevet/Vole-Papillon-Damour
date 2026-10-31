import {Injectable, NgModule} from '@angular/core';
import {Title} from '@angular/platform-browser';
import {RouterModule, RouterStateSnapshot, Routes, TitleStrategy} from '@angular/router';

import {BackOfficeAuthGuard} from './shared/auth/auth-guard';
import {AuthLandingComponent} from "./core/auth-landing/auth-landing.component";
import {LoginComponent} from "./core/login/login.component";
import {ActualitiesComponent} from "./feature/actualities/actualities.component";
import {ActualityDetailComponent} from "./feature/actuality-detail/actuality-detail.component";
import {CaisseComponent} from "./feature/caisse/caisse.component";
import {VpdEventsComponent} from "./feature/vpd-events/vpd-events.component";
import {EventDetailComponent} from "./feature/event-detail/event-detail.component";
import {TableauComponent} from "./feature/event-detail/components/tableau/tableau.component";
import {CatalogAdministrationComponent} from "./feature/catalog-administration/catalog-administration.component";

const APPLICATION_NAME = "Espace bénévoles · Vole Papillon d'Amour";

/**
 * Les onglets du navigateur affichaient tous le même titre : impossible de s'y
 * retrouver quand on garde la caisse et un évènement ouverts côte à côte pendant
 * un loto.
 */
@Injectable()
export class BackOfficeTitleStrategy extends TitleStrategy {
  constructor(private readonly title: Title) {
    super();
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const pageTitle = this.buildTitle(snapshot);
    this.title.setTitle(pageTitle ? `${pageTitle} · ${APPLICATION_NAME}` : APPLICATION_NAME);
  }
}

const routes: Routes = [
  {
    // Page d'atterrissage de la redirection : elle ne porte volontairement pas de garde,
    // sans quoi le guard traite la réponse en même temps que le fournisseur d'identité
    // et la navigation reste suspendue sur un cadre vide.
    path: '',
    component: AuthLandingComponent,
    pathMatch: 'full',
    data: {chrome: false},
    title: 'Connexion',
  },
  {
    path: 'login',
    component: LoginComponent,
    data: {chrome: false},
    title: 'Connexion',
  },
  {
    path: 'actualites',
    component: ActualitiesComponent,
    canActivate: [BackOfficeAuthGuard],
    title: 'Actualités',
  },
  {
    path: 'actualite/:id',
    component: ActualityDetailComponent,
    canActivate: [BackOfficeAuthGuard],
    title: 'Actualité',
  },
  {
    path: 'evenements',
    component: VpdEventsComponent,
    canActivate: [BackOfficeAuthGuard],
    title: 'Évènements',
  },
  {
    path: 'evenement/:id',
    component: EventDetailComponent,
    canActivate: [BackOfficeAuthGuard],
    title: 'Évènement',
  },
  {
    path: 'evenement/:id/loto-tableau',
    component: TableauComponent,
    canActivate: [BackOfficeAuthGuard],
    data: {chrome: false},
    title: 'Tableau du loto',
  },
  {
    path: 'caisse',
    component: CaisseComponent,
    canActivate: [BackOfficeAuthGuard],
    title: 'Caisse',
  },
  {
    path: 'administration',
    component: CatalogAdministrationComponent,
    canActivate: [BackOfficeAuthGuard],
    title: 'Administration catalogue',
  },
  {path: '**', redirectTo: '/actualites'}
];


@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule],
  providers: [{provide: TitleStrategy, useClass: BackOfficeTitleStrategy}],
})
export class AppRoutingModule { }
