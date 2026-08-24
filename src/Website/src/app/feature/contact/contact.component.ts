import { isPlatformBrowser } from '@angular/common';
import { Component, inject, PLATFORM_ID, signal } from '@angular/core';

@Component({
    selector: 'app-contact',
    templateUrl: './contact.component.html',
    standalone: false
})
export class ContactComponent {
  private readonly platformId = inject(PLATFORM_ID);

  readonly reasons = ['Un dépôt de livres', 'Une demande d’aide', 'Le bénévolat', 'La presse', 'Autre'];

  reason = signal(this.reasons[0]);
  consent = signal(false);

  send(name: string, email: string, message: string): void {
    if (!isPlatformBrowser(this.platformId) || !this.consent()) return;

    const subject = encodeURIComponent(`[Site] ${this.reason()}`);
    const bodyLines = [
      name ? `De : ${name}` : '',
      email ? `Réponse à : ${email}` : '',
      '',
      message,
    ].filter(Boolean);
    const body = encodeURIComponent(bodyLines.join('\n'));
    globalThis.location.href = `mailto:volepapillondamour@sfr.fr?subject=${subject}&body=${body}`;
  }
}
