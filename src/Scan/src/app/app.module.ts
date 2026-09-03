import {NgModule, provideZonelessChangeDetection} from '@angular/core';
import {BrowserModule} from '@angular/platform-browser';
import {FormsModule} from '@angular/forms';
import {HttpClientModule} from '@angular/common/http';

import {DesignSystemModule} from '@vpd/ui';
import {AppComponent} from './app.component';
import {ScannerComponent} from './scanner/scanner.component';

@NgModule({
  declarations: [AppComponent, ScannerComponent],
  imports: [BrowserModule, FormsModule, HttpClientModule, DesignSystemModule],
  providers: [provideZonelessChangeDetection()],
  bootstrap: [AppComponent],
})
export class AppModule {}
