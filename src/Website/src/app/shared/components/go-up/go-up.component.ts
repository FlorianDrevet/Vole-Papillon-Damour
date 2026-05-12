import { Component } from '@angular/core';

@Component({
  selector: 'app-go-up',
  templateUrl: './go-up.component.html',
  styleUrl: './go-up.component.scss'
})
export class GoUpComponent {
  scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
