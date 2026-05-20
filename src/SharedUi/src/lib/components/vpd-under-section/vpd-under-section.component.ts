import { Component, input } from '@angular/core';

@Component({
  selector: 'vpd-under-section, app-under-section',
  templateUrl: './vpd-under-section.component.html',
  styleUrl: './vpd-under-section.component.scss',
  standalone: false,
})
export class VpdUnderSectionComponent {
  Title = input.required<string>();
}
