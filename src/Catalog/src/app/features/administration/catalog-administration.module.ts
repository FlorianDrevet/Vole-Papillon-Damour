import {CommonModule} from '@angular/common';
import {NgModule} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {RouterModule, Routes} from '@angular/router';
import {DesignSystemModule} from '@vpd/ui';

import {CatalogCoverModule} from '../../shared/catalog-cover.module';
import {CatalogAdministrationPageComponent} from './catalog-administration-page.component';
import {AdminRareBookFormComponent} from './rare-books/admin-rare-book-form.component';
import {AdminRareBookPhotosComponent} from './rare-books/admin-rare-book-photos.component';
import {AdminRareBooksComponent} from './rare-books/admin-rare-books.component';
import {NotFoundReportCloseDialogComponent} from './not-found-reports/not-found-report-close-dialog.component';
import {NotFoundReportsViewComponent} from './not-found-reports/not-found-reports-view.component';
import {ManualRequestsViewComponent} from './manual-requests/manual-requests-view.component';
import {ManualRequesterDetailComponent} from './manual-requests/manual-requester-detail.component';
import {ManualRequestAddDialogComponent} from './manual-requests/manual-request-add-dialog.component';
import {ManualRequestDecisionDialogComponent} from './manual-requests/manual-request-decision-dialog.component';
import {IsbndbAbsentViewComponent} from './isbndb/isbndb-absent-view.component';
import {IsbndbStatusCardComponent} from './isbndb/isbndb-status-card.component';
import {FairDigestAdminComponent} from './fair-digest/fair-digest-admin.component';

const routes: Routes = [
  {path: '', component: CatalogAdministrationPageComponent},
];

@NgModule({
  declarations: [
    CatalogAdministrationPageComponent,
    AdminRareBooksComponent,
    AdminRareBookFormComponent,
    AdminRareBookPhotosComponent,
    NotFoundReportsViewComponent,
    ManualRequestsViewComponent,
    ManualRequesterDetailComponent,
    ManualRequestAddDialogComponent,
    ManualRequestDecisionDialogComponent,
    IsbndbAbsentViewComponent,
    IsbndbStatusCardComponent,
    FairDigestAdminComponent,
    NotFoundReportCloseDialogComponent,
  ],
  imports: [
    CommonModule,
    FormsModule,
    CatalogCoverModule,
    DesignSystemModule,
    RouterModule.forChild(routes),
  ],
})
export class CatalogAdministrationModule {}
