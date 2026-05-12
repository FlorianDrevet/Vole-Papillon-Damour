import {NgModule, provideExperimentalZonelessChangeDetection} from '@angular/core';
import {BrowserModule} from '@angular/platform-browser';

import {AppRoutingModule} from './app-routing.module';
import {AppComponent} from './app.component';
import {provideAnimationsAsync} from '@angular/platform-browser/animations/async';
import {CoreModule} from "./core/core.module";
import {FeatureModule} from "./feature/feature.module";

@NgModule({
  declarations: [
    AppComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    CoreModule,
    FeatureModule
  ],
  providers: [
    provideAnimationsAsync(),
    provideExperimentalZonelessChangeDetection()
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
