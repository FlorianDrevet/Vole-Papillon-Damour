import {isPlatformBrowser} from '@angular/common';
import {Injectable, inject, OnDestroy, PLATFORM_ID, signal} from '@angular/core';
import {VpdEventModel} from "../models/vpdEvent.model";
import {environment} from "../../../environments/environment";

@Injectable({
  providedIn: 'root'
})
export class SseClientService implements OnDestroy{
  private static readonly initialReconnectDelayMs = 250;
  private static readonly maxReconnectDelayMs = 5000;

  private readonly platformId = inject(PLATFORM_ID);

  private eventSource: EventSource | undefined;
  private reconnectTimeoutId: ReturnType<typeof setTimeout> | undefined;
  private reconnectDelayMs = SseClientService.initialReconnectDelayMs;
  private currentEventId: string | undefined;
  eventAsso = signal<VpdEventModel | null>(null);

  init(id: string): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.currentEventId = id;
    this.reconnectDelayMs = SseClientService.initialReconnectDelayMs;
    this.clearReconnectTimeout();
    this.openEventSource(id);
  }

  private openEventSource(id: string): void {
    this.closeEventSource();

    this.eventSource = new EventSource(`${environment.api_url}/asso-events/${id}/tableau/sse`);

    this.eventSource.onmessage = (event) => {
      try {
        const message: VpdEventModel = JSON.parse(event.data);
        this.eventAsso.set(message);
      } catch (error) {
        console.error('Invalid SSE event payload: ', error);
      }
    };

    this.eventSource.onopen = () => {
      this.reconnectDelayMs = SseClientService.initialReconnectDelayMs;
      console.log('Connection to server opened.');
    };

    this.eventSource.onerror = (error) => {
      console.error("EventSource failed: ", error);
      this.closeEventSource();
      this.scheduleReconnect(id);
    };
  }

  private scheduleReconnect(id: string): void {
    if (this.reconnectTimeoutId !== undefined) {
      return;
    }

    const reconnectDelayMs = this.reconnectDelayMs;
    this.reconnectDelayMs = Math.min(
      SseClientService.maxReconnectDelayMs,
      this.reconnectDelayMs * 2
    );

    this.reconnectTimeoutId = setTimeout(() => {
      this.reconnectTimeoutId = undefined;
      if (this.currentEventId === id) {
        this.openEventSource(id);
      }
    }, reconnectDelayMs);
  }

  private closeEventSource(): void {
    this.eventSource?.close();
    this.eventSource = undefined;
  }

  private clearReconnectTimeout(): void {
    if (this.reconnectTimeoutId === undefined) {
      return;
    }

    clearTimeout(this.reconnectTimeoutId);
    this.reconnectTimeoutId = undefined;
  }

  ngOnDestroy(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.clearReconnectTimeout();
    this.closeEventSource();
  }
}
