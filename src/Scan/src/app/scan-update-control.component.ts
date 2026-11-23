import {ChangeDetectionStrategy, Component, Signal} from '@angular/core';
import {ScanUpdateService, ScanUpdateState} from './scan-update.service';

@Component({
  selector: 'app-scan-update-control',
  templateUrl: './scan-update-control.component.html',
  styleUrl: './scan-update-control.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
})
export class ScanUpdateControlComponent {
  readonly state: Signal<ScanUpdateState>;
  readonly appVersion: string;

  constructor(private readonly updates: ScanUpdateService) {
    this.state = updates.state;
    this.appVersion = updates.appVersion;
  }

  get statusMessage(): string {
    const messages: Record<ScanUpdateState, string> = {
      checking: 'Vérification en cours…',
      'up-to-date': 'Application à jour',
      available: 'Une mise à jour est prête',
      error: 'Vérification impossible. Réessayez.',
      updating: 'Actualisation…',
      unsupported: 'Mise à jour indisponible en local',
    };

    return messages[this.state()];
  }

  get actionLabel(): string {
    const labels: Record<ScanUpdateState, string> = {
      checking: 'Vérification…',
      'up-to-date': 'Vérifier',
      available: 'Mettre à jour',
      error: 'Réessayer',
      updating: 'Actualisation…',
      unsupported: 'Indisponible',
    };

    return labels[this.state()];
  }

  get isActionDisabled(): boolean {
    const currentState = this.state();
    return currentState === 'checking' || currentState === 'updating' || currentState === 'unsupported';
  }

  handleAction(): void {
    if (this.state() === 'available') {
      this.updates.reloadForUpdate();
      return;
    }

    void this.updates.checkForUpdate();
  }
}
