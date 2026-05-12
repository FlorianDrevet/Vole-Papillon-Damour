import {NgModule} from '@angular/core';
import {CommonModule} from '@angular/common';
import {InputComponent} from "./components/input/input.component";
import {ButtonLoginComponent} from "./components/button-login/button-login.component";
import {FormsModule, ReactiveFormsModule} from "@angular/forms";
import {JWT_OPTIONS, JwtHelperService} from "@auth0/angular-jwt";
import {ActualityCardComponent} from "./components/actuality-card/actuality-card.component";
import {EventCardComponent} from "./components/event-card/event-card.component";
import {TitleComponent} from "./components/title/title.component";
import {UnderSectionComponent} from "./components/under-section/under-section.component";
import {RouterLink} from "@angular/router";
import {CapitalizePipe} from "./pipes/capitalize.pipe";
import {VpdImageComponent} from "./components/vpd-image/vpd-image.component";
import {VpdButtonComponent} from './components/vpd-button/vpd-button.component';
import {ConfirmationDialogComponent} from './components/dialogs/confirmation-dialog/confirmation-dialog.component';
import {MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle} from "@angular/material/dialog";
import {MatFormField, MatFormFieldModule, MatLabel, MatSuffix} from "@angular/material/form-field";
import {MatInput} from "@angular/material/input";
import {MatButton} from "@angular/material/button";
import {
  CreateUpdateEventDialogComponent
} from './components/dialogs/create-update-event-dialog/create-update-event-dialog.component';
import {CdkTextareaAutosize} from "@angular/cdk/text-field";
import {MatDatepicker, MatDatepickerInput, MatDatepickerToggle} from "@angular/material/datepicker";
import {MatProgressSpinner} from "@angular/material/progress-spinner";
import {MatNativeDateTimeModule, MatTimepickerModule} from "@dhutaryan/ngx-mat-timepicker";
import {MatOption, MatSelect} from "@angular/material/select";
import {LineNumberTitlePipe} from "./pipes/line-number-title.pipe";
import {PricesComponent} from "./components/prices/prices.component";
import {ProductComponent} from "./components/prices/components/product/product.component";
import {PricePipe} from './pipes/price.pipe';
import {
  ScanBingoCardDialogComponent
} from "./components/dialogs/scan-bingo-card-dialog/scan-bingo-card-dialog.component";
import {BingoCardComponent} from './components/bingo-card/bingo-card.component';

@NgModule({
  declarations: [
    InputComponent,
    ButtonLoginComponent,
    ActualityCardComponent,
    EventCardComponent,
    TitleComponent,
    UnderSectionComponent,
    CapitalizePipe,
    VpdImageComponent,
    VpdButtonComponent,
    ConfirmationDialogComponent,
    CreateUpdateEventDialogComponent,
    LineNumberTitlePipe,
    PricesComponent,
    ProductComponent,
    PricePipe,
    ScanBingoCardDialogComponent,
    BingoCardComponent
  ],
  exports: [
    InputComponent,
    ButtonLoginComponent,
    ActualityCardComponent,
    EventCardComponent,
    TitleComponent,
    UnderSectionComponent,
    CapitalizePipe,
    VpdImageComponent,
    VpdButtonComponent,
    LineNumberTitlePipe,
    PricesComponent,
    ScanBingoCardDialogComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatDialogContent,
    MatFormField,
    MatInput,
    MatButton,
    MatDialogActions,
    MatDialogClose,
    FormsModule,
    MatDialogTitle,
    CdkTextareaAutosize,
    MatDatepicker,
    MatDatepickerInput,
    MatDatepickerToggle,
    MatLabel,
    MatProgressSpinner,
    MatSuffix,
    MatTimepickerModule,
    MatNativeDateTimeModule,
    MatFormFieldModule,
    MatSelect,
    MatOption
  ],
  providers: [
    {
      provide: JWT_OPTIONS,
      useValue: JWT_OPTIONS
    },
    JwtHelperService
  ],
})
export class SharedModule { }
