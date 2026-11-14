import {isPlatformBrowser} from '@angular/common';
import {Component, inject, PLATFORM_ID, ChangeDetectionStrategy} from '@angular/core';

@Component({
  selector: 'app-go-up',
  templateUrl: './go-up.component.html',
  styleUrl: './go-up.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false
})
export class GoUpComponent {
  private readonly platformId = inject(PLATFORM_ID);

  scrollToTop(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    globalThis.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
