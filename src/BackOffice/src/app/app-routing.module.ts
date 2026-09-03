import {Injectable, NgModule} from '@angular/core';
import {Title} from '@angular/platform-browser';
import {RouterModule, RouterStateSnapshot, Routes, TitleStrategy} from '@angular/router';
import {MsalGuard} from '@azure/msal-angular';

import {AuthLandingComponent} from "./core/auth-landing/auth-landing.component";
import {LoginComponent} from "./core/login/login.component";
import {ActualitiesComponent} from "./feature/actualities/actualities.component";
import {ActualityDetailComponent} from "./feature/actuality-detail/actuality-detail.component";
import {CaisseComponent} from "./feature/caisse/caisse.component";
import {VpdEventsComponent} from "./feature/vpd-events/vpd-events.component";
import {EventDetailComponent} from "./feature/event-detail/event-detail.component";
import {TableauComponent} from "./feature/event-detail/components/tableau/tableau.component";

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
    // Page d'atterrissage d'Entra : elle ne porte volontairement pas de MsalGuard,
    // sans quoi le guard traite la redirection en même temps que MsalRedirectComponent
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
    canActivate: [MsalGuard],
    title: 'Actualités',
  },
  {
    path: 'actualite/:id',
    component: ActualityDetailComponent,
    canActivate: [MsalGuard],
    title: 'Actualité',
  },
  {
    path: 'evenements',
    component: VpdEventsComponent,
    canActivate: [MsalGuard],
    title: 'Évènements',
  },
  {
    path: 'evenement/:id',
    component: EventDetailComponent,
    canActivate: [MsalGuard],
    title: 'Évènement',
  },
  {
    path: 'evenement/:id/loto-tableau',
    component: TableauComponent,
    canActivate: [MsalGuard],
    data: {chrome: false},
    title: 'Tableau du loto',
  },
  {
    path: 'caisse',
    component: CaisseComponent,
    canActivate: [MsalGuard],
    title: 'Caisse',
  },
  {path: '**', redirectTo: '/actualites'}
];


@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule],
  providers: [{provide: TitleStrategy, useClass: BackOfficeTitleStrategy}],
})
export class AppRoutingModule { }
