import {Injectable, input, OnDestroy, OnInit, signal} from '@angular/core';
import {VpdEventModel} from "../models/vpdEvent.model";
import {environment} from "../../../environments/environment";

@Injectable({
  providedIn: 'root'
})
export class SseClientService implements OnDestroy{

  private eventSource: EventSource | undefined;
  eventAsso = signal<VpdEventModel | null>(null);

  init(id: string): void {
    if (this.eventSource !== undefined) {
      this.eventSource.close();
    }

    this.eventSource = new EventSource(`${environment.api_url}/asso-events/${id}/tableau/sse`);

    this.eventSource.onmessage = (event) => {
      const message: VpdEventModel = JSON.parse(event.data);
      this.eventAsso.set(message);
    };

    this.eventSource.onopen = () => {
      console.log('Connection to server opened.');
    };

    this.eventSource.onerror = (error) => {
      console.error("EventSource failed: ", error);
      this.eventSource?.close();
      this.init(id);
    };
  }

  ngOnDestroy(): void {
    this.eventSource?.close();
  }
}
