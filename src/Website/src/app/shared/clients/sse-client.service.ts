import {isPlatformBrowser} from '@angular/common';
import {Injectable, inject, OnDestroy, PLATFORM_ID, signal} from '@angular/core';
import {VpdEventModel} from "../models/vpdEvent.model";
import {VpdEventsFacadeService} from "../facades/vpd-events.facade.service";
import {environment} from "../../../environments/environment";

/** Pastille d'état du tableau en direct (E4, RG-13). */
export type LiveTableauConnection = 'connecting' | 'live' | 'reconnecting' | 'offline' | 'not-found';

/**
 * Tableau en direct d'un événement : état complet lu à l'ouverture, puis flux SSE.
 * Chaque message est un état complet portant sa révision : après une coupure, la
 * reconnexion renvoie l'état à jour (RG-12) et un message plus ancien est ignoré.
 *
 * Fourni par la page du tableau (pas en racine) : la connexion se ferme avec la page.
 */
@Injectable()
export class SseClientService implements OnDestroy {
  static readonly initialReconnectDelayMs = 500;
  static readonly maxReconnectDelayMs = 5000;
  static readonly reconnectingAfterMs = 5000;

  private readonly platformId = inject(PLATFORM_ID);
  private readonly events = inject(VpdEventsFacadeService);

  private eventSource: EventSource | undefined;
  private reconnectTimeoutId: ReturnType<typeof setTimeout> | undefined;
  private reconnectingTimeoutId: ReturnType<typeof setTimeout> | undefined;
  private reconnectDelayMs = SseClientService.initialReconnectDelayMs;
  private currentEventId: string | undefined;
  private listening = false;

  readonly eventAsso = signal<VpdEventModel | null>(null);
  readonly connection = signal<LiveTableauConnection>('connecting');

  async init(id: string): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.currentEventId = id;
    this.reconnectDelayMs = SseClientService.initialReconnectDelayMs;
    this.clearTimers();
    this.closeEventSource();
    this.eventAsso.set(null);
    this.connection.set('connecting');
    this.listenToBrowser();

    try {
      const snapshot = await this.events.getEventById(id);
      if (this.currentEventId === id) {
        this.accept(snapshot);
      }
    } catch (error) {
      if ((error as {response?: {status?: number}})?.response?.status === 404) {
        this.connection.set('not-found');
        return;
      }
      // Réseau indisponible : le flux SSE prendra le relais dès qu'il se connecte.
    }

    if (this.currentEventId === id) {
      this.openEventSource(id);
    }
  }

  private openEventSource(id: string): void {
    this.closeEventSource();

    const eventSource = new EventSource(`${environment.api_url}/asso-events/${id}/tableau/sse`);
    this.eventSource = eventSource;

    eventSource.onopen = () => {
      this.reconnectDelayMs = SseClientService.initialReconnectDelayMs;
      this.clearReconnectingTimeout();
      this.connection.set('live');
    };

    eventSource.onmessage = (event) => {
      try {
        this.accept(JSON.parse(event.data) as VpdEventModel);
        this.connection.set('live');
      } catch (error) {
        console.error('Invalid SSE event payload: ', error);
      }
    };

    eventSource.onerror = () => {
      this.closeEventSource();
      this.markDisconnected();
      this.scheduleReconnect(id);
    };
  }

  private accept(snapshot: VpdEventModel): void {
    const current = this.eventAsso();
    if (current !== null && current.id === snapshot.id &&
      (snapshot.liveRevision ?? 0) < (current.liveRevision ?? 0)) {
      return;
    }
    this.eventAsso.set(snapshot);
  }

  /** RG-13 : « Hors ligne » sans réseau, « Reconnexion… » après 5 s sans connexion. */
  private markDisconnected(): void {
    if (!navigator.onLine) {
      this.connection.set('offline');
      return;
    }
    if (this.reconnectingTimeoutId === undefined && this.connection() !== 'reconnecting') {
      this.reconnectingTimeoutId = setTimeout(() => {
        this.reconnectingTimeoutId = undefined;
        if (this.eventSource === undefined || this.eventSource.readyState !== EventSource.OPEN) {
          this.connection.set(navigator.onLine ? 'reconnecting' : 'offline');
        }
      }, SseClientService.reconnectingAfterMs);
    }
  }

  private scheduleReconnect(id: string): void {
    if (this.reconnectTimeoutId !== undefined) {
      return;
    }

    const reconnectDelayMs = this.reconnectDelayMs;
    this.reconnectDelayMs = Math.min(SseClientService.maxReconnectDelayMs, this.reconnectDelayMs * 2);

    this.reconnectTimeoutId = setTimeout(() => {
      this.reconnectTimeoutId = undefined;
      if (this.currentEventId === id) {
        this.openEventSource(id);
      }
    }, reconnectDelayMs);
  }

  private reconnectNow(): void {
    const id = this.currentEventId;
    if (id === undefined || this.connection() === 'not-found' || this.eventSource !== undefined) {
      return;
    }
    this.clearReconnectTimeout();
    this.reconnectDelayMs = SseClientService.initialReconnectDelayMs;
    this.openEventSource(id);
  }

  private readonly onOnline = () => this.reconnectNow();
  private readonly onOffline = () => {
    if (this.connection() !== 'not-found') {
      this.connection.set('offline');
    }
  };
  /** Un téléphone en veille coupe le flux : on le rouvre dès que la page revient. */
  private readonly onVisibilityChange = () => {
    if (document.visibilityState === 'visible') {
      this.reconnectNow();
    }
  };

  private listenToBrowser(): void {
    if (this.listening) {
      return;
    }
    this.listening = true;
    window.addEventListener('online', this.onOnline);
    window.addEventListener('offline', this.onOffline);
    document.addEventListener('visibilitychange', this.onVisibilityChange);
  }

  private closeEventSource(): void {
    this.eventSource?.close();
    this.eventSource = undefined;
  }

  private clearReconnectTimeout(): void {
    clearTimeout(this.reconnectTimeoutId);
    this.reconnectTimeoutId = undefined;
  }

  private clearReconnectingTimeout(): void {
    clearTimeout(this.reconnectingTimeoutId);
    this.reconnectingTimeoutId = undefined;
  }

  private clearTimers(): void {
    this.clearReconnectTimeout();
    this.clearReconnectingTimeout();
  }

  ngOnDestroy(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.currentEventId = undefined;
    this.clearTimers();
    this.closeEventSource();
    if (this.listening) {
      window.removeEventListener('online', this.onOnline);
      window.removeEventListener('offline', this.onOffline);
      document.removeEventListener('visibilitychange', this.onVisibilityChange);
    }
  }
}
