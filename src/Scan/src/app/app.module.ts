import {NgModule, provideZonelessChangeDetection} from '@angular/core';
import {BrowserModule} from '@angular/platform-browser';
import {FormsModule} from '@angular/forms';
import {HTTP_INTERCEPTORS, HttpClientModule} from '@angular/common/http';
import {ServiceWorkerModule} from '@angular/service-worker';
import {MsalInterceptor, MsalModule, MsalRedirectComponent} from '@azure/msal-angular';

import {DesignSystemModule} from '@vpd/ui';
import {AppComponent} from './app.component';
import {ScannerComponent} from './scanner/scanner.component';
import {
  msalGuardConfig,
  msalInstanceFactory,
  msalInterceptorConfig,
} from './auth/msal-config';
import {environment} from '../environments/environment';

@NgModule({
  declarations: [AppComponent, ScannerComponent],
  imports: [
    BrowserModule,
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
    provideZonelessChangeDetection(),
    {provide: HTTP_INTERCEPTORS, useClass: MsalInterceptor, multi: true},
  ],
  bootstrap: [AppComponent, MsalRedirectComponent],
})
export class AppModule {}
