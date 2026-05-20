import {Component, input} from '@angular/core';
import {FormGroup} from "@angular/forms";

@Component({
    selector: 'app-input',
    templateUrl: './input.component.html',
    styleUrl: './input.component.scss',
    standalone: false
})
export class InputComponent {
  icon = input<string[]>([]);
  placeholder = input<string>('');
  isPassword = input<boolean>(false);
  controlName = input<string>('');
  required = input<boolean>(false);
  form = input.required<FormGroup>();
  disabled = input<boolean>(false);
  valueInput = input<string | null | undefined>(null);
}
