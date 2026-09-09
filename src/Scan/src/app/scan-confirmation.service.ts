import {Injectable, Signal, WritableSignal, signal} from '@angular/core';

export interface ScanConfirmationRequest {
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel: string;
}

@Injectable({providedIn: 'root'})
export class ScanConfirmationService {
  private readonly requestState: WritableSignal<ScanConfirmationRequest | null> = signal(null);
  private resolver: ((confirmed: boolean) => void) | null = null;

  readonly request: Signal<ScanConfirmationRequest | null> = this.requestState.asReadonly();

  confirm(request: ScanConfirmationRequest): Promise<boolean> {
    this.resolver?.(false);
    this.requestState.set(request);
    return new Promise<boolean>(resolve => {
      this.resolver = resolve;
    });
  }

  resolve(confirmed: boolean): void {
    const resolver = this.resolver;
    this.resolver = null;
    this.requestState.set(null);
    resolver?.(confirmed);
  }
}
