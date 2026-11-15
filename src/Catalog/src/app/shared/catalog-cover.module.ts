import {NgModule} from '@angular/core';

import {CatalogCoverDirective} from './catalog-cover.directive';

@NgModule({
  declarations: [CatalogCoverDirective],
  exports: [CatalogCoverDirective],
})
export class CatalogCoverModule {}
