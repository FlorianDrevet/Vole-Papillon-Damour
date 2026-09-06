import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

import { VpdTitleComponent } from './components/vpd-title/vpd-title.component';
import { VpdButtonComponent } from './components/vpd-button/vpd-button.component';
import { VpdImageComponent } from './components/vpd-image/vpd-image.component';
import { VpdActualityCardComponent } from './components/vpd-actuality-card/vpd-actuality-card.component';
import { VpdEventCardComponent } from './components/vpd-event-card/vpd-event-card.component';
import { VpdProductCardComponent } from './components/vpd-product-card/vpd-product-card.component';
import { VpdProductListComponent } from './components/vpd-product-list/vpd-product-list.component';
import { VpdBookCoverPlaceholderComponent } from './components/vpd-book-cover-placeholder/vpd-book-cover-placeholder.component';

import { VpdPricePipe } from './pipes/vpd-price.pipe';
import { VpdCapitalizePipe } from './pipes/vpd-capitalize.pipe';
import { VpdLineNumberTitlePipe } from './pipes/vpd-line-number-title.pipe';

const COMPONENTS = [
  VpdTitleComponent,
  VpdButtonComponent,
  VpdImageComponent,
  VpdActualityCardComponent,
  VpdEventCardComponent,
  VpdProductCardComponent,
  VpdProductListComponent,
  VpdBookCoverPlaceholderComponent,
];

const PIPES = [VpdPricePipe, VpdCapitalizePipe, VpdLineNumberTitlePipe];

@NgModule({
  declarations: [...COMPONENTS, ...PIPES],
  imports: [CommonModule, RouterLink],
  exports: [...COMPONENTS, ...PIPES],
})
export class DesignSystemModule {}
