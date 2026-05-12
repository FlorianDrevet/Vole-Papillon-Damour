import {Component, Input} from '@angular/core';
import {Router} from "@angular/router";

@Component({
  selector: 'app-button-login',
  templateUrl: './button-login.component.html',
  styleUrl: './button-login.component.scss'
})
export class ButtonLoginComponent {
  @Input() routerUrl: string | null = null;
  @Input() disabled: boolean = false;

  constructor(private router: Router) {
  }

  redirectToLink() {
    if (this.routerUrl) {
      console.log(this.routerUrl)
      this.router.navigate([this.routerUrl]);
    }
  }
}
