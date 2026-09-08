import {ChangeDetectionStrategy, Component, inject} from '@angular/core';

import {CookieConsentService} from '../../../shared/services/cookie-consent.service';

@Component({
  selector: 'app-catalog-footer',
  standalone: false,
  templateUrl: './catalog-footer.component.html',
  styleUrls: ['./catalog-footer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogFooterComponent {
  readonly consent = inject(CookieConsentService);
  readonly currentYear = new Date().getFullYear();
}
