import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { Router } from '@angular/router';

import { CookieConsentService } from '../../../shared/services/cookie-consent.service';

@Component({
  selector: 'app-footer',
  templateUrl: './footer.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
})
export class FooterComponent {
  route = inject(Router);
  consent = inject(CookieConsentService);
}
