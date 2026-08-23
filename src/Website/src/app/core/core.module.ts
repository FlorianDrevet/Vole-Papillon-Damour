import { NgModule } from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
import { FooterComponent } from './layouts/footer/footer.component';
import { NavigationComponent } from './layouts/navigation/navigation.component';
import {RouterLink, RouterLinkActive} from "@angular/router";
import { NavigationMobileComponent } from './layouts/navigation/navigation-mobile/navigation-mobile.component';
import { CarouselComponent } from './layouts/footer/components/carousel/carousel.component';
import {SharedModule} from "../shared/shared.module";



@NgModule({
  declarations: [
    FooterComponent,
    NavigationComponent,
    NavigationMobileComponent,
    CarouselComponent,
  ],
  exports: [
    FooterComponent,
    NavigationComponent,
    CarouselComponent,
  ],
  imports: [
    CommonModule,
    RouterLink,
    RouterLinkActive,
    NgOptimizedImage,
    SharedModule
  ]
})
export class CoreModule { }
