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
import { SectionEyebrowComponent } from './components/section-eyebrow/section-eyebrow.component';
import { PillButtonComponent } from './components/pill-button/pill-button.component';
import { StatCardComponent } from './components/stat-card/stat-card.component';
import { QuoteBlockComponent } from './components/quote-block/quote-block.component';
import { PhotoPlaceholderComponent } from './components/photo-placeholder/photo-placeholder.component';
import { ActionCardComponent } from './components/action-card/action-card.component';
import { DiseaseSectionComponent } from './components/disease-section/disease-section.component';

@NgModule({
  declarations: [
    PapillonIconComponent,
    ActualityCardComponent,
    DiaporamaComponent,
    GoUpComponent,
    PricesComponent,
    ProductComponent,
    SectionEyebrowComponent,
    PillButtonComponent,
    StatCardComponent,
    QuoteBlockComponent,
    PhotoPlaceholderComponent,
    ActionCardComponent,
    DiseaseSectionComponent,
  ],
  exports: [
    PapillonIconComponent,
    ActualityCardComponent,
    DiaporamaComponent,
    GoUpComponent,
    PricesComponent,
    ProductComponent,
    SectionEyebrowComponent,
    PillButtonComponent,
    StatCardComponent,
    QuoteBlockComponent,
    PhotoPlaceholderComponent,
    ActionCardComponent,
    DiseaseSectionComponent,
    DesignSystemModule,
  ],
  imports: [CommonModule, RouterLink, DesignSystemModule],
})
export class SharedModule {}
