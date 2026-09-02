import {NgModule} from '@angular/core';
import {RouterModule, Routes} from '@angular/router';
import {MsalGuard} from '@azure/msal-angular';
import {LoginComponent} from "./core/login/login.component";
import {ActualitiesComponent} from "./feature/actualities/actualities.component";
import {ActualityDetailComponent} from "./feature/actuality-detail/actuality-detail.component";
import {CaisseComponent} from "./feature/caisse/caisse.component";
import {VpdEventsComponent} from "./feature/vpd-events/vpd-events.component";
import {EventDetailComponent} from "./feature/event-detail/event-detail.component";
import {TableauComponent} from "./feature/event-detail/components/tableau/tableau.component";

const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent
  },
  {
    path: 'actualites',
    component: ActualitiesComponent,
    canActivate: [MsalGuard]
  },
  {
    path: 'actualite/:id',
    component: ActualityDetailComponent,
    canActivate: [MsalGuard]
  },
  {
    path: 'evenements',
    component: VpdEventsComponent,
    canActivate: [MsalGuard]
  },
  {
    path: 'evenement/:id',
    component: EventDetailComponent,
    canActivate: [MsalGuard]
  },
  {
    path: 'evenement/:id/loto-tableau',
    component: TableauComponent,
    canActivate: [MsalGuard]
  },
  {
    path: 'caisse',
    component: CaisseComponent,
    canActivate: [MsalGuard]
  },
  {path: '**', redirectTo: '/actualites', pathMatch: 'full'}
];


@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
