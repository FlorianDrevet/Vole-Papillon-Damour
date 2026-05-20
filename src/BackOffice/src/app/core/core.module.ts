import {NgModule} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FooterComponent} from './layouts/footer/footer.component';
import {NavigationComponent} from './layouts/navigation/navigation.component';
import {LoginComponent} from "./login/login.component";
import {ReactiveFormsModule} from "@angular/forms";
import {SharedModule} from "../shared/shared.module";
import {NavigationMobileComponent} from "./layouts/navigation/navigation-mobile/navigation-mobile.component";
import {RouterLink} from "@angular/router";


@NgModule({
  declarations: [
    FooterComponent,
    NavigationComponent,
    LoginComponent,
    NavigationMobileComponent
  ],
  exports: [
    FooterComponent,
    NavigationComponent,
    LoginComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    SharedModule,
    RouterLink
  ]
})
export class CoreModule { }
