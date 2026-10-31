import {ErrorHandler, inject, NgModule, provideAppInitializer, provideZonelessChangeDetection} from '@angular/core';
import {BrowserModule} from '@angular/platform-browser';

import {AppRoutingModule} from './app-routing.module';
import {AppComponent} from './app.component';
import {provideAnimationsAsync} from '@angular/platform-browser/animations/async';
import {CoreModule} from "./core/core.module";
import {FeatureModule} from "./feature/feature.module";
import {MsalModule, MsalRedirectComponent, MsalService} from '@azure/msal-angular';
import {environment} from '../environments/environment';

import {
  msalGuardConfig,
  msalInstanceFactory,
  msalInterceptorConfig,
} from './shared/auth/msal-config';
import {KeycloakAuthService} from './shared/auth/keycloak-auth.service';
import {ApplicationInsightsErrorHandler} from './shared/services/application-insights-error-handler';

@NgModule({
  declarations: [
    AppComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    CoreModule,
    FeatureModule,
    MsalModule.forRoot(
      msalInstanceFactory(),
      msalGuardConfig,
      msalInterceptorConfig,
    )
  ],
  providers: [
    // Initialize the selected provider before session state and route guards start.
    provideAppInitializer(() => environment.auth_provider === 'keycloak'
      ? inject(KeycloakAuthService).initialize()
      : inject(MsalService).initialize()),
    provideAnimationsAsync(),
    provideZonelessChangeDetection(),
    {provide: ErrorHandler, useFactory: () => new ApplicationInsightsErrorHandler()}
  ],
  bootstrap: [
    AppComponent,
    ...(environment.auth_provider === 'entra' ? [MsalRedirectComponent] : []),
  ]
})
export class AppModule { }
