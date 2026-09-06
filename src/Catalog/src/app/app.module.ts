import {registerLocaleData} from '@angular/common';
import {HttpClientModule} from '@angular/common/http';
import {inject, LOCALE_ID, NgModule, provideAppInitializer, provideZonelessChangeDetection} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {BrowserModule, provideClientHydration, withEventReplay} from '@angular/platform-browser';

import localeFr from '@angular/common/locales/fr';

import {AppRoutingModule} from './app-routing.module';
import {AppComponent} from './app.component';
import {CatalogBookDetailPageComponent} from './features/book-detail/catalog-book-detail-page.component';
import {CatalogHomePageComponent} from './features/home/catalog-home-page.component';
import {LegalPageComponent} from './features/legal/legal-page.component';
import {CatalogSearchPageComponent} from './features/search/catalog-search-page.component';
import {CatalogWorkPageComponent} from './features/work/catalog-work-page.component';
import {BookCardComponent} from './shared/book-card/book-card.component';
import {CatalogAdministrationPageComponent} from './features/administration/catalog-administration-page.component';
import {CatalogAuthService} from './core/catalog-auth.service';
import {CatalogAccountPageComponent} from './features/account/catalog-account-page.component';
import {CatalogFooterComponent} from './core/layouts/footer/catalog-footer.component';
import {CatalogNavigationComponent} from './core/layouts/navigation/catalog-navigation.component';

registerLocaleData(localeFr);

@NgModule({
  declarations: [
    AppComponent,
    CatalogHomePageComponent,
    CatalogSearchPageComponent,
    CatalogBookDetailPageComponent,
    CatalogWorkPageComponent,
    LegalPageComponent,
    BookCardComponent,
    CatalogAdministrationPageComponent,
    CatalogAccountPageComponent,
    CatalogNavigationComponent,
    CatalogFooterComponent,
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    FormsModule,
    AppRoutingModule,
  ],
  providers: [
    {provide: LOCALE_ID, useValue: 'fr-FR'},
    provideAppInitializer(() => inject(CatalogAuthService).initialize()),
    provideZonelessChangeDetection(),
    provideClientHydration(withEventReplay()),
  ],
  bootstrap: [AppComponent],
})
export class AppModule {}
