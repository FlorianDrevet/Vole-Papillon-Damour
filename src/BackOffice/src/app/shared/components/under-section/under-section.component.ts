import {Component, input} from '@angular/core';

@Component({
  selector: 'app-under-section',
  templateUrl: './under-section.component.html',
  styleUrl: './under-section.component.scss'
})
export class UnderSectionComponent {
  Title = input.required<string>()
}
