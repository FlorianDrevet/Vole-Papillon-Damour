import {Injectable, OnDestroy, OnInit, signal} from '@angular/core';
import {VpdEventModel} from "../models/vpdEvent.model";

@Injectable({
  providedIn: 'root'
})
export class SseClientService implements OnInit, OnDestroy {

  eventAsso = signal<VpdEventModel | null>(null);
  private eventSource: EventSource | undefined;

  ngOnInit(): void {
    this.eventSource = new EventSource('https://localhost:5001/api/notifications/stream');

    this.eventSource.onmessage = (event) => {
      const message: VpdEventModel = JSON.parse(event.data);
      this.eventAsso.set(message);
    };

    this.eventSource.onerror = (error) => {
      console.error("EventSource failed: ", error);
      this.eventSource?.close();
    };
  }

  ngOnDestroy(): void {
    this.eventSource?.close();
  }
}
