import {ChangeDetectorRef, Component, OnInit, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {ScanAuthService} from '../auth/scan-auth.service';
import {ScanApiService} from '../offline/scan-api.service';
import {ScanLocalStoreService} from '../offline/scan-local-store.service';
import {
  ScanVolunteerGenreStatistics,
  ScanVolunteerMonthlyStatistics,
  ScanVolunteerStatisticsResponse,
  ScanVolunteerTimeSlotStatistics,
} from '../offline/scan-offline.model';

type ScanStatisticsRole = 'tri' | 'cash';

@Component({
  selector: 'app-scan-statistics',
  templateUrl: './scan-statistics.component.html',
  styleUrls: ['./scan-statistics.component.scss'],
  standalone: false,
})
export class ScanStatisticsComponent implements OnInit {
  readonly Math = Math;
  readonly contributionSlots = ['morning', 'afternoon', 'evening'] as const;
  readonly contributionDays = [
    {label: 'Lun', value: 1},
    {label: 'Mar', value: 2},
    {label: 'Mer', value: 3},
    {label: 'Jeu', value: 4},
    {label: 'Ven', value: 5},
    {label: 'Sam', value: 6},
    {label: 'Dim', value: 0},
  ] as const;
  readonly statistics = signal<ScanVolunteerStatisticsResponse | null>(null);
  readonly loading = signal(true);
  readonly fromCache = signal(false);
  readonly pendingSynchronizationCount = signal(0);
  readonly errorMessage = signal<string | null>(null);
  readonly activeRole = signal<ScanStatisticsRole>('tri');

  readonly canSort: boolean;
  readonly canSell: boolean;
  readonly displayName: string;

  constructor(
    private readonly auth: ScanAuthService,
    private readonly api: ScanApiService,
    private readonly store: ScanLocalStoreService,
    private readonly changeDetector: ChangeDetectorRef,
  ) {
    this.canSort = auth.canSort;
    this.canSell = auth.canSell;
    this.displayName = auth.displayName ?? 'bénévole';
    this.activeRole.set(this.canSort ? 'tri' : 'cash');
  }

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.pendingSynchronizationCount.set(await this.store.countBlockingOutboxEntries());

    const accountId = this.auth.authState.account?.homeAccountId ?? 'current-volunteer';
    const canRefresh = this.auth.authState.status === 'authorized' &&
      (typeof navigator === 'undefined' || navigator.onLine !== false);

    if (canRefresh) {
      try {
        const response = await firstValueFrom(this.api.getVolunteerStatistics());
        await this.store.saveVolunteerStatistics(accountId, response);
        this.statistics.set(response);
        this.fromCache.set(false);
        this.finishLoading();
        return;
      } catch {
        // A cached snapshot is still useful on a temporary API outage.
      }
    }

    const cached = await this.store.getVolunteerStatistics(accountId);
    if (cached) {
      this.statistics.set(cached.statistics);
      this.fromCache.set(true);
    } else {
      this.statistics.set(null);
      this.errorMessage.set('Les statistiques apparaîtront après une première synchronisation.');
    }
    this.finishLoading();
  }

  selectRole(role: ScanStatisticsRole): void {
    if (role === 'tri' && !this.canSort) {
      return;
    }
    if (role === 'cash' && !this.canSell) {
      return;
    }
    this.activeRole.set(role);
  }

  formatSnapshot(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      hour: '2-digit',
      minute: '2-digit',
      timeZone: 'Europe/Paris',
    }).format(new Date(value));
  }

  formatDuration(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const remainder = minutes % 60;
    return hours === 0 ? `${remainder} min` : `${hours} h ${remainder.toString().padStart(2, '0')}`;
  }

  formatShortDate(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'short',
      timeZone: 'Europe/Paris',
    }).format(new Date(value)).replace('.', '');
  }

  monthLabel(month: ScanVolunteerMonthlyStatistics): string {
    return new Intl.DateTimeFormat('fr-FR', {
      month: 'short',
      timeZone: 'Europe/Paris',
    }).format(new Date(month.periodStart)).replace('.', '');
  }

  monthBarHeight(
    month: ScanVolunteerMonthlyStatistics,
    kind: 'kept' | 'rejected',
    months: readonly ScanVolunteerMonthlyStatistics[],
  ): number {
    const maximum = Math.max(1, ...months.map(candidate => candidate.kept + candidate.rejected));
    const value = kind === 'kept' ? month.kept : month.rejected;
    return Math.max(4, Math.round(value / maximum * 100));
  }

  fairBarHeight(
    fair: {netSoldQuantity: number},
    fairs: readonly {netSoldQuantity: number}[],
  ): number {
    const maximum = Math.max(1, ...fairs.map(candidate => candidate.netSoldQuantity));
    return Math.max(8, Math.round(fair.netSoldQuantity / maximum * 100));
  }

  genreWidth(
    genre: ScanVolunteerGenreStatistics,
    genres: readonly ScanVolunteerGenreStatistics[],
  ): number {
    const maximum = Math.max(1, ...genres.map(candidate => candidate.quantity));
    return Math.max(4, Math.round(genre.quantity / maximum * 100));
  }

  timeSlotCount(
    slots: readonly ScanVolunteerTimeSlotStatistics[],
    dayOfWeek: number,
    slot: string,
  ): number {
    return slots.find(candidate => candidate.dayOfWeek === dayOfWeek && candidate.slot === slot)?.count ?? 0;
  }

  timeSlotLabel(slot: string): string {
    return slot === 'morning' ? 'Matin' : slot === 'afternoon' ? 'Après-midi' : 'Fin de journée';
  }

  formatCurrency(value: number | null): string {
    if (value === null) {
      return '—';
    }
    return new Intl.NumberFormat('fr-FR', {
      style: 'currency',
      currency: 'EUR',
      maximumFractionDigits: 0,
    }).format(value);
  }

  private finishLoading(): void {
    this.loading.set(false);
    this.changeDetector.markForCheck();
  }
}
