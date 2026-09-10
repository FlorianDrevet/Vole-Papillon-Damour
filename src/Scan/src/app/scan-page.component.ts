import {Component} from '@angular/core';
import {ActivatedRoute} from '@angular/router';

import {ScanScreen} from './scanner/scanner.component';

@Component({
  selector: 'app-scan-page',
  template: '<app-scanner [routeScreen]="routeScreen"></app-scanner>',
  standalone: false,
})
export class ScanPageComponent {
  readonly routeScreen: ScanScreen;

  constructor(route: ActivatedRoute) {
    this.routeScreen = route.snapshot.data['screen'] as ScanScreen;
  }
}
