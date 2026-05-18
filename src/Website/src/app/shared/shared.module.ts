import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

import { DesignSystemModule } from '@vpd/ui';

import { PapillonIconComponent } from './components/papillon-icon/papillon-icon.component';
import { ActualityCardComponent } from './components/actuality-card/actuality-card.component';
import { DiaporamaComponent } from './components/diaporama/diaporama.component';
import { GoUpComponent } from './components/go-up/go-up.component';
import { PricesComponent } from './components/prices/prices.component';
import { ProductComponent } from './components/prices/components/product/product.component';

@NgModule({
  declarations: [
    PapillonIconComponent,
    ActualityCardComponent,
    DiaporamaComponent,
    GoUpComponent,
    PricesComponent,
    ProductComponent,
  ],
  exports: [
    PapillonIconComponent,
    ActualityCardComponent,
    DiaporamaComponent,
    GoUpComponent,
    PricesComponent,
    ProductComponent,
    DesignSystemModule,
  ],
  imports: [CommonModule, RouterLink, DesignSystemModule],
})
export class SharedModule {}
