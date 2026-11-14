import {ChangeDetectionStrategy, Component, OnInit, signal} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {CatalogAuthenticationRedirectStartedError, CatalogAuthService} from '../../../core/catalog-auth.service';
import {
  CatalogAdminFairDigestStatus,
} from '../../../core/catalog.models';

type FairDigestAction = 'test' | 'suspend' | 'resume' | null;

@Component({
  selector: 'app-fair-digest-admin',
  standalone: false,
  templateUrl: './fair-digest-admin.component.html',
  styleUrls: ['./fair-digest-admin.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FairDigestAdminComponent implements OnInit {
  readonly status = signal<CatalogAdminFairDigestStatus | null>(null);
  readonly previewHtml = signal<string | null>(null);
  readonly loading = signal(false);
  readonly actionPending = signal<FairDigestAction>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly previewError = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  constructor(
    private readonly auth: CatalogAuthService,
    private readonly api: CatalogAdminApiService,
  ) {}

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    if (!this.auth.isAuthenticated() || !this.auth.isAdministrator()) {
      this.errorMessage.set('Cette rubrique est réservée à l’administration.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.previewError.set(null);
    try {
      const accessToken = await this.auth.getApiAccessToken();
      const [statusResult, previewResult] = await Promise.allSettled([
        firstValueFrom(this.api.getFairDigestStatus(accessToken)),
        firstValueFrom(this.api.getFairDigestPreview(accessToken)),
      ]);

      if (statusResult.status === 'fulfilled') {
        this.status.set(statusResult.value);
      } else {
        this.errorMessage.set(this.statusError(statusResult.reason));
      }

      if (previewResult.status === 'fulfilled') {
        this.previewHtml.set(previewResult.value);
      } else {
        this.previewError.set('L’aperçu de l’e-mail n’a pas pu être chargé.');
      }
    } catch (error: unknown) {
      this.errorMessage.set(this.statusError(error));
      this.previewError.set('L’aperçu de l’e-mail n’a pas pu être chargé.');
    } finally {
      this.loading.set(false);
    }
  }

  async sendTest(): Promise<void> {
    if (!this.canAct()) {
      return;
    }

    this.actionPending.set('test');
    this.errorMessage.set(null);
    this.successMessage.set(null);
    try {
      const accessToken = await this.auth.getApiAccessToken();
      const result = await firstValueFrom(this.api.sendFairDigestTest(accessToken));
      this.successMessage.set(
        `E-mail de test envoyé à ${result.email}.${result.isExample ? ' Le contenu utilise des données d’exemple.' : ''}`,
      );
    } catch {
      this.errorMessage.set('L’e-mail de test n’a pas pu être envoyé. Réessayez.');
    } finally {
      this.actionPending.set(null);
    }
  }

  async toggleSuspension(): Promise<void> {
    const current = this.status();
    if (!current?.campaignId || !this.canAct()) {
      return;
    }

    const resuming = current.suspended;
    this.actionPending.set(resuming ? 'resume' : 'suspend');
    this.errorMessage.set(null);
    this.successMessage.set(null);
    try {
      const accessToken = await this.auth.getApiAccessToken();
      const result = await firstValueFrom(resuming
        ? this.api.resumeFairDigest(accessToken)
        : this.api.suspendFairDigest(accessToken));
      this.status.update(status => status
        ? {...status, suspended: result.suspendedAtUtc !== null}
        : status);
      this.successMessage.set(result.changed
        ? resuming ? 'L’envoi est repris pour cette bourse.' : 'L’envoi est suspendu pour cette bourse.'
        : resuming ? 'L’envoi était déjà actif pour cette bourse.' : 'L’envoi était déjà suspendu pour cette bourse.');
    } catch {
      this.errorMessage.set(resuming
        ? 'L’envoi n’a pas pu être repris. Réessayez.'
        : 'L’envoi n’a pas pu être suspendu. Réessayez.');
    } finally {
      this.actionPending.set(null);
    }
  }

  statusLabel(status: CatalogAdminFairDigestStatus): string {
    return status.enabled && !status.suspended ? 'Envoi activé' : 'Envoi désactivé';
  }

  formatFairStart(value: string | null): string {
    const date = this.toValidDate(value);
    if (!date) {
      return 'Date à confirmer';
    }

    const parts = new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      timeZone: 'Europe/Paris',
      weekday: 'long',
    }).formatToParts(date);
    return `${capitalize(part(parts, 'weekday'))} ${part(parts, 'day')} ${part(parts, 'month')}`;
  }

  formatPlannedAt(value: string | null): string {
    const date = this.toValidDate(value);
    if (!date) {
      return 'Aucun envoi planifié';
    }

    const parts = new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      month: 'long',
      timeZone: 'Europe/Paris',
      weekday: 'long',
    }).formatToParts(date);
    const hour = part(parts, 'hour');
    const minute = part(parts, 'minute');
    return `${capitalize(part(parts, 'weekday'))} ${part(parts, 'day')} ${part(parts, 'month')}, ${hour}${minute === '00' ? ' h' : ` h ${minute}`}`;
  }

  formatCompletedAt(value: string): string {
    const date = this.toValidDate(value);
    if (!date) {
      return 'Date non renseignée';
    }
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      month: 'long',
      timeZone: 'Europe/Paris',
      year: 'numeric',
    }).format(date);
  }

  private canAct(): boolean {
    return this.auth.isAuthenticated()
      && this.auth.isAdministrator()
      && this.actionPending() === null;
  }

  private statusError(error: unknown): string {
    return error instanceof CatalogAuthenticationRedirectStartedError
      ? 'Redirection vers le fournisseur de connexion pour renouveler votre session…'
      : 'Le statut de l’e-mail avant la bourse n’a pas pu être chargé. Réessayez.';
  }

  private toValidDate(value: string | null): Date | null {
    if (!value) {
      return null;
    }
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
  }
}

function part(parts: Intl.DateTimeFormatPart[], type: Intl.DateTimeFormatPartTypes): string {
  return parts.find(item => item.type === type)?.value ?? '';
}

function capitalize(value: string): string {
  return value ? value.charAt(0).toLocaleUpperCase('fr-FR') + value.slice(1) : value;
}
