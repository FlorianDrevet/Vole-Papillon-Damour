import {NgModule} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ReactiveFormsModule} from "@angular/forms";
import {RouterLink} from "@angular/router";

import {FooterComponent} from './layouts/footer/footer.component';
import {NavigationComponent} from './layouts/navigation/navigation.component';
import {NavigationMobileComponent} from "./layouts/navigation/navigation-mobile/navigation-mobile.component";
import {AccountMenuComponent} from "./layouts/navigation/account-menu/account-menu.component";
import {LoginComponent} from "./login/login.component";
import {AuthLandingComponent} from "./auth-landing/auth-landing.component";
import {SharedModule} from "../shared/shared.module";


@NgModule({
  declarations: [
    FooterComponent,
    NavigationComponent,
    NavigationMobileComponent,
    AccountMenuComponent,
    LoginComponent,
    AuthLandingComponent
  ],
  exports: [
    FooterComponent,
    NavigationComponent,
    LoginComponent,
    AuthLandingComponent
  ],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    SharedModule,
    RouterLink
  ]
})
export class CoreModule { }
