import {isPlatformBrowser} from '@angular/common';
import {Component, inject, PLATFORM_ID} from '@angular/core';

@Component({
  selector: 'app-history',
  templateUrl: './history.component.html',
  styleUrl: './history.component.scss',
  standalone: false
})
export class HistoryComponent {
  private readonly platformId = inject(PLATFORM_ID);

  protected readonly scrollTo = isPlatformBrowser(this.platformId)
    ? globalThis.scrollTo.bind(globalThis)
    : (() => undefined);
}
