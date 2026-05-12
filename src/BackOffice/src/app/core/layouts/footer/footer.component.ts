import {Component, inject} from '@angular/core';
import {environment} from "../../../../environments/environment";
import {Router} from "@angular/router";

@Component({
    selector: 'app-footer',
    templateUrl: './footer.component.html',
    styleUrl: './footer.component.scss',
    standalone: false
})
export class FooterComponent {
  router = inject(Router)
  protected readonly environment = environment;
}
