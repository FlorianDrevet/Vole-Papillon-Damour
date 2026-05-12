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
import { LineNumberTitlePipe } from './pipes/line-number-title.pipe';
import {PricesComponent} from "./components/prices/prices.component";
import {PricePipe} from "./pipes/price.pipe";
import {ProductComponent} from "./components/prices/components/product/product.component";

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
    ProductComponent
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
    ProductComponent
  ],
  imports: [
    CommonModule,
    RouterLink
  ],
})
export class SharedModule { }
