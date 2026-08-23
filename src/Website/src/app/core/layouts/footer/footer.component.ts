import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-footer',
  templateUrl: './footer.component.html',
  standalone: false,
})
export class FooterComponent {
  route = inject(Router);
}
