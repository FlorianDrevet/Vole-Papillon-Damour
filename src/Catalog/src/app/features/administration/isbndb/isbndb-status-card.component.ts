import {ChangeDetectionStrategy, Component, Input} from '@angular/core';

import {CatalogAdminIsbndbStatus} from '../../../core/catalog.models';

@Component({
  selector: 'app-isbndb-status-card',
  standalone: false,
  templateUrl: './isbndb-status-card.component.html',
  styleUrls: ['./isbndb-status-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IsbndbStatusCardComponent {
  @Input() status: CatalogAdminIsbndbStatus | null = null;

  quotaStatusLabel(): string {
    if (this.status?.status === 'Exhausted') {
      const until = this.status.exhaustedUntil ?? this.status.resetsAt;
      return `Quota épuisé jusqu’à ${this.formatParisTime(until)}`;
    }
    return this.status?.status === 'Unavailable' ? 'Service indisponible' : 'Opérationnel';
  }

  quotaStatusClass(): string {
    return this.status?.status === 'Exhausted'
      ? 'status-exhausted'
      : this.status?.status === 'Unavailable' ? 'status-unavailable' : 'status-operational';
  }

  remainingCreditsLabel(): string {
    if (!this.status) return '—';
    return `${this.formatNumber(this.status.dailyRemaining)} / ${this.formatNumber(this.status.dailyLimit)}`;
  }

  creditProgressPercent(): number {
    if (!this.status || this.status.dailyLimit <= 0) return 0;
    return Math.max(0, Math.min(100, (this.status.dailyRemaining / this.status.dailyLimit) * 100));
  }

  consumedToday(): number {
    return this.status?.usageToday.reduce((total, entry) => total + entry.credits, 0) ?? 0;
  }

  usageLabel(usage: string): string {
    const labels: Record<string, string> = {
      CatchUp: 'Rattrapage nocturne',
      Scan: 'Scans',
      PublicSearch: 'Recherche publique',
      Administration: 'Administration',
    };
    return labels[usage] ?? usage;
  }

  usageCredits(usage: string): number {
    return this.status?.usageToday.find(item => item.usage === usage)?.credits ?? 0;
  }

  parisTime(value: string | null): string {
    return this.formatParisTime(value);
  }

  lastRunDate(value: string): string {
    return this.formatParisDate(value);
  }

  lastRunStopReason(value: string | null): string {
    switch (value) {
      case 'QuotaExhausted': return 'Quota épuisé — reprise la nuit prochaine';
      case 'Unavailable': return 'Service indisponible';
      case 'Completed': return 'Terminé';
      case 'Cancelled': return 'Arrêté';
      default: return value || 'En cours';
    }
  }

  private formatParisTime(value: string | null): string {
    const date = value ? new Date(value) : null;
    return date && Number.isFinite(date.getTime())
      ? new Intl.DateTimeFormat('fr-FR', {
        timeZone: 'Europe/Paris',
        hour: '2-digit',
        minute: '2-digit',
      }).format(date)
      : '—';
  }

  private formatParisDate(value: string): string {
    const date = new Date(value);
    return Number.isFinite(date.getTime())
      ? new Intl.DateTimeFormat('fr-FR', {
        timeZone: 'Europe/Paris',
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      }).format(date)
      : '—';
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat('fr-FR').format(value);
  }
}
