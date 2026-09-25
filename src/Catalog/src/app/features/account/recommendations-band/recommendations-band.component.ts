import {ChangeDetectionStrategy, Component, OnInit, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../../core/catalog-member-api.service';
import {CatalogPersonalRecommendation} from '../../../core/catalog.models';
import {catalogCoverUrl} from '../../../shared/catalog-cover-url';
import {publicBookPath} from '../../../shared/catalog-url';

type RecommendationsBandState = 'hidden' | 'enabled' | 'no-purchases';

@Component({
  selector: 'app-recommendations-band',
  standalone: false,
  templateUrl: './recommendations-band.component.html',
  styleUrls: ['./recommendations-band.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RecommendationsBandComponent implements OnInit {
  readonly state = signal<RecommendationsBandState>('hidden');
  readonly items = signal<CatalogPersonalRecommendation[]>([]);

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly memberApi: CatalogMemberApiService,
  ) {}

  ngOnInit(): void {
    void this.loadRecommendations();
  }

  bookPath(item: CatalogPersonalRecommendation): string {
    return publicBookPath(item);
  }

  coverUrl(item: CatalogPersonalRecommendation): string | null {
    return catalogCoverUrl(item.coverUrl);
  }

  availabilityLabel(item: CatalogPersonalRecommendation): string {
    if (item.quantityAvailable > 0) {
      return `${item.quantityAvailable} dispo.`;
    }

    if (item.quantityAnnounced > 0) {
      return item.nextFairAt
        ? `${item.quantityAnnounced} dès le ${this.formatShortDate(item.nextFairAt)}`
        : `${item.quantityAnnounced} à venir`;
    }

    return 'Bientôt disponible';
  }

  private async loadRecommendations(): Promise<void> {
    try {
      if (!this.auth.isAuthenticated()) {
        return;
      }

      const accessToken = await this.auth.getApiAccessToken();
      const response = await firstValueFrom(this.memberApi.getRecommendations(accessToken, 4));
      if (response.status === 'NoPurchases') {
        this.state.set('no-purchases');
        return;
      }

      if (response.status !== 'Enabled' || response.items.length === 0) {
        return;
      }

      this.items.set(response.items.slice(0, 4));
      this.state.set('enabled');
    } catch {
      this.state.set('hidden');
    }
  }

  private formatShortDate(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'short',
      timeZone: 'Europe/Paris',
    }).format(new Date(value)).replace('.', '');
  }
}
