import {Injectable, signal} from '@angular/core';
import {SwUpdate} from '@angular/service-worker';
import {environment} from '../environments/environment';

export type ScanUpdateState =
  | 'checking'
  | 'up-to-date'
  | 'available'
  | 'error'
  | 'updating'
  | 'unsupported';

@Injectable({providedIn: 'root'})
export class ScanUpdateService {
  readonly appVersion = environment.appVersion;
  readonly state = signal<ScanUpdateState>('checking');

  private checkTask: Promise<void> | null = null;

  constructor(private readonly swUpdate: SwUpdate) {
    if (!swUpdate.isEnabled) {
      this.state.set('unsupported');
      return;
    }

    swUpdate.versionUpdates.subscribe(event => {
      if (event.type === 'VERSION_READY') {
        this.state.set('available');
      } else if (event.type === 'VERSION_INSTALLATION_FAILED') {
        this.state.set('error');
      }
    });

    void this.checkForUpdate();
  }

  checkForUpdate(): Promise<void> {
    if (!this.swUpdate.isEnabled) {
      this.state.set('unsupported');
      return Promise.resolve();
    }

    if (this.checkTask) {
      return this.checkTask;
    }

    this.state.set('checking');
    this.checkTask = this.swUpdate.checkForUpdate()
      .then(updateAvailable => {
        this.state.set(updateAvailable || this.state() === 'available' ? 'available' : 'up-to-date');
      })
      .catch(() => {
        this.state.set('error');
      })
      .finally(() => {
        this.checkTask = null;
      });

    return this.checkTask;
  }

  reloadForUpdate(): void {
    if (this.state() !== 'available' || typeof window === 'undefined') {
      return;
    }

    this.state.set('updating');
    window.location.reload();
  }
}
