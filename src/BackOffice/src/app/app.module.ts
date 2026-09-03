import {inject, NgModule, provideAppInitializer, provideZonelessChangeDetection} from '@angular/core';
import {BrowserModule} from '@angular/platform-browser';

import {AppRoutingModule} from './app-routing.module';
import {AppComponent} from './app.component';
import {provideAnimationsAsync} from '@angular/platform-browser/animations/async';
import {CoreModule} from "./core/core.module";
import {FeatureModule} from "./feature/feature.module";
import {MsalModule, MsalRedirectComponent, MsalService} from '@azure/msal-angular';

import {
  msalGuardConfig,
  msalInstanceFactory,
  msalInterceptorConfig,
} from './shared/auth/msal-config';

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
    // AuthSessionService reads the MSAL cache from its constructor. Angular must
    // therefore finish the PublicClientApplication initialization before either
    // root component (AppComponent or MsalRedirectComponent) is created.
    provideAppInitializer(() => inject(MsalService).initialize()),
    provideAnimationsAsync(),
    provideZonelessChangeDetection()
  ],
  bootstrap: [AppComponent, MsalRedirectComponent]
})
export class AppModule { }
