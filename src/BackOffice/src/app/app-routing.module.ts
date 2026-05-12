import {NgModule} from '@angular/core';
import {RouterModule, Routes} from '@angular/router';
import {LoginComponent} from "./core/login/login.component";
import {AuthenticationGuard} from "./shared/guards/authentication.guard";
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
    canActivate: [AuthenticationGuard]
  },
  {
    path: 'actualite/:id',
    component: ActualityDetailComponent,
    canActivate: [AuthenticationGuard]
  },
  {
    path: 'evenements',
    component: VpdEventsComponent,
    canActivate: [AuthenticationGuard]
  },
  {
    path: 'evenement/:id',
    component: EventDetailComponent,
    canActivate: [AuthenticationGuard]
  },
  {
    path: 'evenement/:id/loto-tableau',
    component: TableauComponent,
    canActivate: [AuthenticationGuard]
  },
  {
    path: 'caisse',
    component: CaisseComponent,
    canActivate: [AuthenticationGuard]
  },
  {path: '**', redirectTo: '/actualites', pathMatch: 'full'}
];


@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
