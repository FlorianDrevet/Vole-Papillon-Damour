import {NgModule} from '@angular/core';
import {CommonModule} from '@angular/common';
import {DashboardVpdComponent} from './dashboard-vpd/dashboard-vpd.component';
import {VpdEventsComponent} from './vpd-events/vpd-events.component';
import {SharedModule} from "../shared/shared.module";
import {ActualitiesComponent} from './actualities/actualities.component';
import {ActualityByMonthComponent} from './actualities/components/actuality-by-month/actuality-by-month.component';
import {ActualityDetailComponent} from "./actuality-detail/actuality-detail.component";
import {RouterLink} from "@angular/router";
import {
  CreateUpdateActualityDialogComponent
} from '../shared/components/dialogs/create-update-actuality-dialog/create-update-actuality-dialog.component';
import {MatButton} from "@angular/material/button";
import {MatDialogActions, MatDialogContent, MatDialogTitle} from "@angular/material/dialog";
import {FormsModule, ReactiveFormsModule} from "@angular/forms";
import {MatFormField, MatFormFieldModule} from "@angular/material/form-field";
import {
  MatDatepicker,
  MatDatepickerInput,
  MatDatepickerModule,
  MatDatepickerToggle
} from "@angular/material/datepicker";
import {MatInput, MatInputModule} from "@angular/material/input";
import {
  MAT_DATE_FORMATS,
  MAT_DATE_LOCALE,
  MatDateFormats,
  MatNativeDateModule,
  MatOption
} from "@angular/material/core";
import {MatProgressSpinner} from "@angular/material/progress-spinner";
import {CaisseComponent} from './caisse/caisse.component';
import {EventDetailComponent} from "./event-detail/event-detail.component";
import {BingoEventComponent} from "./event-detail/components/bingo-event/bingo-event.component";
import {BooksEventComponent} from "./event-detail/components/books-event/books-event.component";
import {OtherEventComponent} from "./event-detail/components/other-event/other-event.component";
import {SectionInfosEventComponent} from "./event-detail/components/section-infos-event/section-infos-event.component";
import {GeneralInfosComponent} from "./event-detail/components/general-infos/general-infos.component";
import {LotCardComponent} from "./event-detail/components/bingo-event/components/lot-card/lot-card.component";
import {PartieCardComponent} from "./event-detail/components/bingo-event/components/partie-card/partie-card.component";
import {
  CreationUpdatePartieComponent
} from './event-detail/components/bingo-event/components/dialogs/creation-partie/creation-update-partie.component';
import {MatSelect} from "@angular/material/select";
import {MatRadioButton, MatRadioGroup} from "@angular/material/radio";
import {
  CreationUpdateLotComponent
} from './event-detail/components/bingo-event/components/dialogs/creation-update-line-partie/creation-update-lot.component';
import {TableauComponent} from "./event-detail/components/tableau/tableau.component";
import {ModalComponent} from "./event-detail/components/tableau/components/modal-tableau/modal.component";
import {TagComponent} from "./event-detail/components/tableau/components/tag/tag.component";
import {NumberComponent} from "./event-detail/components/tableau/components/number/number.component";
import {
  CreateUpdateProductDialogComponent
} from '../shared/components/dialogs/create-update-product-dialog/create-update-product-dialog.component';
import {MatSlideToggle} from "@angular/material/slide-toggle";
import {CatalogAdministrationComponent} from './catalog-administration/catalog-administration.component';

export const MY_DATE_FORMATS: MatDateFormats = {
  parse: {
    dateInput: 'DD/MM/YYYY',
  },
  display: {
    dateInput: 'DD/MM/YYYY',
    monthYearLabel: 'MMM YYYY',
    dateA11yLabel: 'LL',
    monthYearA11yLabel: 'MMMM YYYY',
  },
};

@NgModule({
  declarations: [
    DashboardVpdComponent,
    VpdEventsComponent,
    ActualitiesComponent,
    ActualityByMonthComponent,
    ActualityDetailComponent,
    CreateUpdateActualityDialogComponent,
    CaisseComponent,
    EventDetailComponent,
    BingoEventComponent,
    BooksEventComponent,
    OtherEventComponent,
    SectionInfosEventComponent,
    GeneralInfosComponent,
    LotCardComponent,
    PartieCardComponent,
    CreationUpdatePartieComponent,
    CreationUpdateLotComponent,
    TableauComponent,
    ModalComponent,
    TagComponent,
    NumberComponent,
    CreateUpdateProductDialogComponent,
    CatalogAdministrationComponent,
  ],
  imports: [
    CommonModule,
    SharedModule,
    RouterLink,
    MatButton,
    MatDialogActions,
    MatDialogTitle,
    ReactiveFormsModule,
    FormsModule,
    MatDialogContent,
    MatFormField,
    MatDatepickerInput,
    MatDatepickerToggle,
    MatDatepicker,
    MatInput,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatProgressSpinner,
    MatOption,
    MatSelect,
    MatRadioGroup,
    MatRadioButton,
    SharedModule,
    SharedModule,
    MatSlideToggle
  ],
  providers: [
    MatDatepickerModule,
    {provide: MAT_DATE_LOCALE, useValue: 'fr-FR'},
    {provide: MAT_DATE_FORMATS, useValue: MY_DATE_FORMATS},
  ],
})
export class FeatureModule { }
