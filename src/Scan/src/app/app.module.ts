import {inject, NgModule, provideAppInitializer, provideZonelessChangeDetection} from '@angular/core';
import {CommonModule} from '@angular/common';
import {BrowserModule} from '@angular/platform-browser';
import {FormsModule} from '@angular/forms';
import {HTTP_INTERCEPTORS, HttpClientModule} from '@angular/common/http';
import {ServiceWorkerModule} from '@angular/service-worker';
import {MsalInterceptor, MsalModule, MsalRedirectComponent, MsalService} from '@azure/msal-angular';

import {DesignSystemModule} from '@vpd/ui';
import {AppComponent} from './app.component';
import {ScanLoginComponent} from './auth/scan-login.component';
import {ScannerComponent} from './scanner/scanner.component';
import {
  msalGuardConfig,
  msalInstanceFactory,
  msalInterceptorConfig,
} from './auth/msal-config';
import {environment} from '../environments/environment';

@NgModule({
  declarations: [AppComponent, ScanLoginComponent, ScannerComponent],
  imports: [
    BrowserModule,
    CommonModule,
    FormsModule,
    HttpClientModule,
    DesignSystemModule,
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
