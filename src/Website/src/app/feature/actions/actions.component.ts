import { Component, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-actions',
  templateUrl: './actions.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
})
export class ActionsComponent {
  readonly filters = ['Tout', 'Matériel médical', 'Informatique', 'Mobilier adapté', 'Mobilité', 'Séjours de répit', 'Autonomie'];
}
