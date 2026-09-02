import {NgModule, provideZonelessChangeDetection} from '@angular/core';
import {BrowserModule} from '@angular/platform-browser';

import {AppRoutingModule} from './app-routing.module';
import {AppComponent} from './app.component';
import {provideAnimationsAsync} from '@angular/platform-browser/animations/async';
import {CoreModule} from "./core/core.module";
import {FeatureModule} from "./feature/feature.module";
import {MsalModule, MsalRedirectComponent} from '@azure/msal-angular';

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
    provideAnimationsAsync(),
    provideZonelessChangeDetection()
  ],
  bootstrap: [AppComponent, MsalRedirectComponent]
})
export class AppModule { }
