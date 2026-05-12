import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { VpdImageComponent } from './components/vpd-image/vpd-image.component';
import { PapillonIconComponent } from './components/papillon-icon/papillon-icon.component';
import { TitleComponent } from './components/title/title.component';
import { ActualityCardComponent } from './components/actuality-card/actuality-card.component';
import {RouterLink} from "@angular/router";
import { ButtonComponent } from './components/button/button.component';
import { CapitalizePipe } from './pipes/capitalize.pipe';
import { DiaporamaComponent } from './components/diaporama/diaporama.component';
import { UnderSectionComponent } from './components/under-section/under-section.component';
import { GoUpComponent } from './components/go-up/go-up.component';
import {MAT_DATE_FORMATS, MAT_DATE_LOCALE, MatDateFormats, MatOption} from "@angular/material/core";
import { LineNumberTitlePipe } from './pipes/line-number-title.pipe';
import {PricesComponent} from "./components/prices/prices.component";
import {PricePipe} from "./pipes/price.pipe";
import {ProductComponent} from "./components/prices/components/product/product.component";
import {AddEmailDialogComponent} from "./components/dialogs/add-email-dialog/add-email-dialog.component";
import {MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle} from "@angular/material/dialog";
import {FormsModule, ReactiveFormsModule} from "@angular/forms";
import {MatFormField, MatFormFieldModule, MatLabel, MatSuffix} from "@angular/material/form-field";
import {MatInput} from "@angular/material/input";
import {MatProgressSpinner} from "@angular/material/progress-spinner";
import {MatButton} from "@angular/material/button";
import {CdkTextareaAutosize} from "@angular/cdk/text-field";
import {MatDatepicker, MatDatepickerInput, MatDatepickerToggle} from "@angular/material/datepicker";
import {MatSelect} from "@angular/material/select";

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
    VpdImageComponent,
    PapillonIconComponent,
    TitleComponent,
    ActualityCardComponent,
    ButtonComponent,
    CapitalizePipe,
    DiaporamaComponent,
    UnderSectionComponent,
    GoUpComponent,
    LineNumberTitlePipe,
    PricesComponent,
    PricePipe,
    ProductComponent,
    AddEmailDialogComponent
  ],
  exports: [
    VpdImageComponent,
    PapillonIconComponent,
    TitleComponent,
    ActualityCardComponent,
    ButtonComponent,
    CapitalizePipe,
    DiaporamaComponent,
    UnderSectionComponent,
    GoUpComponent,
    LineNumberTitlePipe,
    PricesComponent,
    PricePipe,
    ProductComponent,
    AddEmailDialogComponent
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
    MatFormFieldModule,
    MatSelect,
    MatOption
  ],
  providers: [
    {provide: MAT_DATE_LOCALE, useValue: 'fr-FR'},
    {provide: MAT_DATE_FORMATS, useValue: MY_DATE_FORMATS},
  ],
})
export class SharedModule { }
