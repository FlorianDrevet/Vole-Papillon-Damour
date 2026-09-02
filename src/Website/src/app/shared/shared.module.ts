import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

import { DesignSystemModule } from '@vpd/ui';

import { PapillonIconComponent } from './components/papillon-icon/papillon-icon.component';
import { ActualityCardComponent } from './components/actuality-card/actuality-card.component';
import { DiaporamaComponent } from './components/diaporama/diaporama.component';
import { GoUpComponent } from './components/go-up/go-up.component';
import { CookieBannerComponent } from './components/cookie-banner/cookie-banner.component';
import { PricesComponent } from './components/prices/prices.component';
import { ProductComponent } from './components/prices/components/product/product.component';
import { SectionEyebrowComponent } from './components/section-eyebrow/section-eyebrow.component';
import { PillButtonComponent } from './components/pill-button/pill-button.component';
import { StatCardComponent } from './components/stat-card/stat-card.component';
import { QuoteBlockComponent } from './components/quote-block/quote-block.component';
import { PhotoPlaceholderComponent } from './components/photo-placeholder/photo-placeholder.component';
import { ActionCardComponent } from './components/action-card/action-card.component';
import { TitledSectionComponent } from './components/titled-section/titled-section.component';
import { EventLocationsComponent } from './components/event-locations/event-locations.component';

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
    TitledSectionComponent,
    EventLocationsComponent,
    CookieBannerComponent,
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
    TitledSectionComponent,
    EventLocationsComponent,
    CookieBannerComponent,
    DesignSystemModule,
  ],
  imports: [CommonModule, RouterLink, DesignSystemModule],
})
export class SharedModule {}
