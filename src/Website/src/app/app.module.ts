import {LOCALE_ID, NgModule, provideZonelessChangeDetection} from '@angular/core';
import { BrowserModule, provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { registerLocaleData } from '@angular/common';
import localeFr from '@angular/common/locales/fr';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import {CoreModule} from "./core/core.module";
import {SharedModule} from "./shared/shared.module";
import {FeatureModule} from "./feature/feature.module";

registerLocaleData(localeFr);

@NgModule({
  declarations: [
    AppComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    CoreModule,
    FeatureModule,
    SharedModule,
  ],
  providers: [
    { provide: LOCALE_ID, useValue: 'fr-FR' },
    provideAnimationsAsync(),
    provideZonelessChangeDetection(),
    provideClientHydration(withEventReplay())
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
