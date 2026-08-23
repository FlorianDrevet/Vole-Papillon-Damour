import { Component, input } from '@angular/core';

/** Citation encadrée : filet vertical dégradé + texte serif italique + attribution. */
@Component({
  selector: 'app-quote-block',
  templateUrl: './quote-block.component.html',
  standalone: false,
})
export class QuoteBlockComponent {
  quote = input.required<string>();
  attribution = input<string>();
  tone = input<'light' | 'dark'>('light');
}
