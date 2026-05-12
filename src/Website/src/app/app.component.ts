import {Component, inject} from '@angular/core';
import {Router} from "@angular/router";
import {rule} from "postcss";

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrl: './app.component.scss',
    standalone: false
})
export class AppComponent {
  title = 'Vole Papillon D\'amour';

  router = inject(Router);
  protected readonly rule = rule;
}
