import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

import { VpdTitleComponent } from './components/vpd-title/vpd-title.component';
import { VpdUnderSectionComponent } from './components/vpd-under-section/vpd-under-section.component';
import { VpdButtonComponent } from './components/vpd-button/vpd-button.component';
import { VpdImageComponent } from './components/vpd-image/vpd-image.component';
import { VpdActualityCardComponent } from './components/vpd-actuality-card/vpd-actuality-card.component';
import { VpdEventCardComponent } from './components/vpd-event-card/vpd-event-card.component';
import { VpdProductCardComponent } from './components/vpd-product-card/vpd-product-card.component';
import { VpdProductListComponent } from './components/vpd-product-list/vpd-product-list.component';

import { VpdPricePipe } from './pipes/vpd-price.pipe';
import { VpdCapitalizePipe } from './pipes/vpd-capitalize.pipe';
import { VpdLineNumberTitlePipe } from './pipes/vpd-line-number-title.pipe';

const COMPONENTS = [
  VpdTitleComponent,
  VpdUnderSectionComponent,
  VpdButtonComponent,
  VpdImageComponent,
  VpdActualityCardComponent,
  VpdEventCardComponent,
  VpdProductCardComponent,
  VpdProductListComponent,
];

const PIPES = [VpdPricePipe, VpdCapitalizePipe, VpdLineNumberTitlePipe];

@NgModule({
  declarations: [...COMPONENTS, ...PIPES],
  imports: [CommonModule, RouterLink],
  exports: [...COMPONENTS, ...PIPES],
})
export class DesignSystemModule {}
