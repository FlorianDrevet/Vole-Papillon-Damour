import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { JWT_OPTIONS, JwtHelperService } from '@auth0/angular-jwt';
import { MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle } from '@angular/material/dialog';
import { MatFormField, MatFormFieldModule, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatButton } from '@angular/material/button';
import { CdkTextareaAutosize } from '@angular/cdk/text-field';
import { MatDatepicker, MatDatepickerInput, MatDatepickerToggle } from '@angular/material/datepicker';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatNativeDateTimeModule, MatTimepickerModule } from '@dhutaryan/ngx-mat-timepicker';
import { MatOption, MatSelect } from '@angular/material/select';

import { DesignSystemModule } from '@vpd/ui';

import { InputComponent } from './components/input/input.component';
import { ButtonLoginComponent } from './components/button-login/button-login.component';
import { ActualityCardComponent } from './components/actuality-card/actuality-card.component';
import { EventCardComponent } from './components/event-card/event-card.component';
import { PricesComponent } from './components/prices/prices.component';
import { ProductComponent } from './components/prices/components/product/product.component';
import { ConfirmationDialogComponent } from './components/dialogs/confirmation-dialog/confirmation-dialog.component';
import { CreateUpdateEventDialogComponent } from './components/dialogs/create-update-event-dialog/create-update-event-dialog.component';
import { IconComponent } from './components/icon/icon.component';
import { PillButtonComponent } from './components/pill-button/pill-button.component';
import { IconButtonComponent } from './components/icon-button/icon-button.component';
import { SectionEyebrowComponent } from './components/section-eyebrow/section-eyebrow.component';
import { PageHeaderComponent } from './components/page-header/page-header.component';
import { GroupHeadingComponent } from './components/group-heading/group-heading.component';

const DESIGN_SYSTEM_COMPONENTS = [
  IconComponent,
  PillButtonComponent,
  IconButtonComponent,
  SectionEyebrowComponent,
  PageHeaderComponent,
  GroupHeadingComponent,
];

@NgModule({
  declarations: [
    InputComponent,
    ButtonLoginComponent,
    ActualityCardComponent,
    EventCardComponent,
    PricesComponent,
    ProductComponent,
    ConfirmationDialogComponent,
    CreateUpdateEventDialogComponent,
    ...DESIGN_SYSTEM_COMPONENTS,
  ],
  exports: [
    InputComponent,
    ButtonLoginComponent,
    ActualityCardComponent,
    EventCardComponent,
    PricesComponent,
    ProductComponent,
    DesignSystemModule,
    ...DESIGN_SYSTEM_COMPONENTS,
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
    MatOption,
    DesignSystemModule,
  ],
  providers: [
    {
      provide: JWT_OPTIONS,
      useValue: JWT_OPTIONS,
    },
    JwtHelperService,
  ],
})
export class SharedModule {}
