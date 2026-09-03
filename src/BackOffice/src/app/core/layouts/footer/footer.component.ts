import {Component} from '@angular/core';

import {environment} from "../../../../environments/environment";

@Component({
    selector: 'app-footer',
    templateUrl: './footer.component.html',
    standalone: false
})
export class FooterComponent {
  protected readonly environment = environment;
}
