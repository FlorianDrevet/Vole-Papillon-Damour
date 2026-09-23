import {registerLocaleData} from '@angular/common';
import {provideHttpClient, withFetch, withInterceptors} from '@angular/common/http';
import {LOCALE_ID, NgModule, provideZonelessChangeDetection} from '@angular/core';
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
import {AdminRareBookFormComponent} from './features/administration/rare-books/admin-rare-book-form.component';
import {AdminRareBookPhotosComponent} from './features/administration/rare-books/admin-rare-book-photos.component';
import {AdminRareBooksComponent} from './features/administration/rare-books/admin-rare-books.component';
import {CatalogAccountPageComponent} from './features/account/catalog-account-page.component';
import {CatalogUnsubscribePageComponent} from './features/account/catalog-unsubscribe-page.component';
import {CatalogFooterComponent} from './core/layouts/footer/catalog-footer.component';
import {CatalogNavigationComponent} from './core/layouts/navigation/catalog-navigation.component';
import {CatalogCookieBannerComponent} from './shared/components/cookie-banner/catalog-cookie-banner.component';
import {CatalogAuthPromptComponent} from './shared/components/auth-prompt/catalog-auth-prompt.component';
import {SelectionButtonComponent} from './shared/components/selection-button/selection-button.component';
import {CatalogNotFoundPageComponent} from './features/not-found/catalog-not-found-page.component';
import {CatalogRareBookDetailPageComponent} from './features/rare-books/catalog-rare-book-detail-page.component';
import {CatalogRareBooksPageComponent} from './features/rare-books/catalog-rare-books-page.component';
import {CatalogRareBookCardComponent} from './shared/rare-book-card/rare-book-card.component';
import {DesignSystemModule} from '@vpd/ui';
import {catalogAuthInterceptor} from './core/catalog-auth.interceptor';

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
    AdminRareBooksComponent,
    AdminRareBookFormComponent,
    AdminRareBookPhotosComponent,
    CatalogAccountPageComponent,
    CatalogUnsubscribePageComponent,
    CatalogNavigationComponent,
    CatalogFooterComponent,
    CatalogCookieBannerComponent,
    CatalogNotFoundPageComponent,
    CatalogAuthPromptComponent,
    SelectionButtonComponent,
    CatalogRareBooksPageComponent,
    CatalogRareBookDetailPageComponent,
    CatalogRareBookCardComponent,
  ],
  imports: [
    BrowserModule,
    FormsModule,
    AppRoutingModule,
    DesignSystemModule,
  ],
  providers: [
    {provide: LOCALE_ID, useValue: 'fr-FR'},
    provideHttpClient(withFetch(), withInterceptors([catalogAuthInterceptor])),
    provideZonelessChangeDetection(),
    provideClientHydration(withEventReplay()),
  ],
  bootstrap: [AppComponent],
})
export class AppModule {}
