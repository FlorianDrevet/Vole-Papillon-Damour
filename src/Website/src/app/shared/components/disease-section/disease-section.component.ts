import { Component, input } from '@angular/core';

/** Sous-section d'une fiche maladie (titre + corps projeté). */
@Component({
  selector: 'app-disease-section',
  templateUrl: './disease-section.component.html',
  standalone: false,
})
export class DiseaseSectionComponent {
  title = input.required<string>();
}
