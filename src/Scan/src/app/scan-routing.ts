import {CanActivateFn, Routes, Router} from '@angular/router';
import {inject} from '@angular/core';

import {ScanAuthService} from './auth/scan-auth.service';
import {ScanWorkflowService} from './offline/scan-workflow.service';
import {ScanPageComponent} from './scan-page.component';
import {ScanShellComponent} from './scan-shell.component';
import {ScanStatisticsComponent} from './statistics/scan-statistics.component';

export type ScanRequiredRole = 'Tri' | 'Caisse';

export function scanRoleGuard(role: ScanRequiredRole): CanActivateFn {
  return () => {
    const auth = inject(ScanAuthService);
    const router = inject(Router);
    const authenticated = auth.authState.status === 'authorized' || auth.authState.status === 'degraded';
    const allowed = role === 'Tri' ? auth.canSort : auth.canSell;
    return authenticated && allowed ? true : router.parseUrl('/');
  };
}

export const scanVolunteerGuard: CanActivateFn = () => {
  const auth = inject(ScanAuthService);
  const router = inject(Router);
  const authenticated = auth.authState.status === 'authorized' || auth.authState.status === 'degraded';
  return authenticated && (auth.canSort || auth.canSell) ? true : router.parseUrl('/');
};

export const scanSessionEndGuard: CanActivateFn = () => {
  const auth = inject(ScanAuthService);
  const router = inject(Router);
  const workflow = inject(ScanWorkflowService);
  const authenticated = auth.authState.status === 'authorized' || auth.authState.status === 'degraded';
  if (!authenticated || !auth.canSort) {
    return router.parseUrl('/');
  }

  return workflow.getSession().then(session => session?.closeRequested === true
    ? true
    : router.parseUrl('/accueil'));
};

const triGuard = scanRoleGuard('Tri');
const cashGuard = scanRoleGuard('Caisse');

export const scanRoutes: Routes = [
  {
    path: '',
    component: ScanShellComponent,
    children: [
      {path: '', pathMatch: 'full', redirectTo: 'accueil'},
      {path: 'accueil', component: ScanPageComponent, data: {screen: 'home'}},
      {path: 'reprise', component: ScanPageComponent, data: {screen: 'reprise'}},
      {path: 'tri/mode', component: ScanPageComponent, canActivate: [triGuard], data: {screen: 'session-mode'}},
      {path: 'tri', component: ScanPageComponent, canActivate: [triGuard], data: {screen: 'tri'}},
      {path: 'tri/fin', component: ScanPageComponent, canActivate: [triGuard, scanSessionEndGuard], data: {screen: 'session-end'}},
      {path: 'caisse', component: ScanPageComponent, canActivate: [cashGuard], data: {screen: 'cash'}},
      {path: 'consulter', component: ScanPageComponent, data: {screen: 'consultation'}},
      {path: 'statistiques', component: ScanStatisticsComponent, canActivate: [scanVolunteerGuard]},
      {path: '**', redirectTo: 'accueil'},
    ],
  },
];
