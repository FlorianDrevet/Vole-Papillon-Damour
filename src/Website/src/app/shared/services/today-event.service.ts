import {isPlatformServer} from '@angular/common';
import {inject, Injectable, makeStateKey, NgZone, PendingTasks, PLATFORM_ID, signal, TransferState} from '@angular/core';
import {VpdEventsFacadeService} from '../facades/vpd-events.facade.service';
import {TodayEventModel} from '../models/today-event.model';

const TODAY_EVENT_KEY = makeStateKey<TodayEventModel | null>('vpd-today-event');
const REFRESH_MS = 60_000;
const SERVER_TIMEOUT_MS = 3000;

/**
 * L'événement du jour pour le bandeau (E5) et la fiche (E6). Lu au rendu serveur et
 * transmis au navigateur (pas de saut de mise en page, RG-20), puis rafraîchi chaque
 * minute tant que l'onglet est visible pour passer à « En direct ».
 */
@Injectable({
  providedIn: 'root'
})
export class TodayEventService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly transferState = inject(TransferState);
  private readonly pendingTasks = inject(PendingTasks);
  private readonly zone = inject(NgZone);
  private readonly events = inject(VpdEventsFacadeService);
  private started = false;

  readonly today = signal<TodayEventModel | null>(null);

  async start(): Promise<void> {
    if (this.started) {
      return;
    }
    this.started = true;

    if (isPlatformServer(this.platformId)) {
      // Rendu serveur ou pré-rendu au build : l'API ne doit jamais bloquer la page.
      const done = this.pendingTasks.add();
      try {
        await Promise.race([this.refresh(), new Promise(resolve => setTimeout(resolve, SERVER_TIMEOUT_MS))]);
      } finally {
        done();
      }
      this.transferState.set(TODAY_EVENT_KEY, this.today());
      return;
    }

    if (this.transferState.hasKey(TODAY_EVENT_KEY)) {
      this.today.set(this.transferState.get(TODAY_EVENT_KEY, null));
      this.transferState.remove(TODAY_EVENT_KEY);
    }
    // Une page pré-rendue au build porte un état périmé : on le relit tout de suite.
    void this.refresh();

    // Hors zone : un intervalle permanent empêcherait l'application d'être « stable ».
    this.zone.runOutsideAngular(() => setInterval(() => {
      if (document.visibilityState === 'visible') {
        void this.refresh();
      }
    }, REFRESH_MS));
  }

  async refresh(): Promise<void> {
    try {
      this.today.set(await this.events.getTodayEvent());
    } catch {
      // API injoignable : on garde le dernier état connu.
    }
  }
}
