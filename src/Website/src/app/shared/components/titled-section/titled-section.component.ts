import { Component, input } from '@angular/core';

/** Sous-section de page à contenu long (titre + corps projeté) : fiches maladies, récits du quotidien. */
@Component({
  selector: 'app-titled-section',
  templateUrl: './titled-section.component.html',
  standalone: false,
})
export class TitledSectionComponent {
  title = input.required<string>();
}
