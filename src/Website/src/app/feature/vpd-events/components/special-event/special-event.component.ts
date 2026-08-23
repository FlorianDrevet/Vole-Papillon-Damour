import { Component, computed, input } from '@angular/core';

@Component({
    selector: 'app-special-event',
    templateUrl: './special-event.component.html',
    standalone: false
})
export class SpecialEventComponent {
  Title = input.required<string>();
  Date = input<Date | null>();
  DateEnd = input<Date | null>();
  UrlImage = input.required<string>();
  IdEvent = input<string | null>();
  UrlMoreDetail = computed(() => `/evenement/${this.IdEvent()}`);
}
