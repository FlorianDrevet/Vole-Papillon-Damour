import {Component, input} from '@angular/core';

@Component({
  selector: 'app-section-infos-event',
  templateUrl: './section-infos-event.component.html',
  styleUrl: './section-infos-event.component.scss'
})
export class SectionInfosEventComponent {
  UrlIcon = input.required<string>()
  Title = input.required<string>()
  Value = input<string | null>()
}
