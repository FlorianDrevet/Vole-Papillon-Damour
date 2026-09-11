import {inject, NgModule, provideAppInitializer, provideZonelessChangeDetection} from '@angular/core';
import {CommonModule} from '@angular/common';
import {BrowserModule} from '@angular/platform-browser';
import {FormsModule} from '@angular/forms';
import {HTTP_INTERCEPTORS, HttpClientModule} from '@angular/common/http';
import {ServiceWorkerModule} from '@angular/service-worker';
import {RouterModule} from '@angular/router';
import {MsalInterceptor, MsalModule, MsalRedirectComponent, MsalService} from '@azure/msal-angular';

import {DesignSystemModule} from '@vpd/ui';
import {AppComponent} from './app.component';
import {ScanLoginComponent} from './auth/scan-login.component';
import {ScannerComponent} from './scanner/scanner.component';
import {ScanDiagnosticComponent} from './offline/scan-diagnostic.component';
import {ScanRecoveryComponent} from './offline/scan-recovery.component';
import {ScanStatusBarComponent} from './offline/scan-status-bar.component';
import {ScanConfirmationComponent} from './scan-confirmation.component';
import {ScanPageComponent} from './scan-page.component';
import {ScanShellComponent} from './scan-shell.component';
import {ScanStatisticsComponent} from './statistics/scan-statistics.component';
import {scanRoutes} from './scan-routing';
import {
  msalGuardConfig,
  msalInstanceFactory,
  msalInterceptorConfig,
} from './auth/msal-config';
import {environment} from '../environments/environment';

@NgModule({
  declarations: [
    AppComponent,
    ScanLoginComponent,
    ScannerComponent,
    ScanDiagnosticComponent,
    ScanRecoveryComponent,
    ScanStatusBarComponent,
    ScanConfirmationComponent,
    ScanPageComponent,
    ScanShellComponent,
    ScanStatisticsComponent,
  ],
  imports: [
    BrowserModule,
    CommonModule,
    FormsModule,
    HttpClientModule,
    DesignSystemModule,
    RouterModule.forRoot(scanRoutes),
    MsalModule.forRoot(
      msalInstanceFactory(),
      msalGuardConfig,
      msalInterceptorConfig,
    ),
    ServiceWorkerModule.register('ngsw-worker.js', {
      enabled: environment.production,
      registrationStrategy: 'registerWhenStable:30000',
    }),
  ],
  providers: [
    // ScanAuthService reads the MSAL cache during construction. Complete MSAL
    // initialization before either root component is created, including after
    // a browser refresh or an authentication redirect.
    provideAppInitializer(() => inject(MsalService).initialize()),
    provideZonelessChangeDetection(),
    {provide: HTTP_INTERCEPTORS, useClass: MsalInterceptor, multi: true},
  ],
  bootstrap: [AppComponent, MsalRedirectComponent],
})
export class AppModule {}
