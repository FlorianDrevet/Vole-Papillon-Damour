import {ChangeDetectionStrategy, ChangeDetectorRef, Component, Inject} from '@angular/core';

import {LABEL_PRINTER, LabelPrinter} from './label-printer';

// E6 — Imprimante d'étiquettes : connexion, test, bandeau de simulation (RG-178).
@Component({
  selector: 'app-label-printer-page',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
  template: `
    <main class="printer-page">
      <header class="printer-header">
        <span class="printer-eyebrow">Matériel</span>
        <h1>Imprimante d'étiquettes</h1>
      </header>

      <div *ngIf="printer.simulated" class="printer-simulation" role="status" data-zone="E6-bandeau-simulation">
        Simulation — rien n'est imprimé
      </div>

      <section class="printer-state" data-zone="E6-etat" aria-labelledby="printer-state-title">
        <span class="printer-eyebrow">État</span>
        <strong id="printer-state-title">{{ connected ? 'Connectée' : 'Non connectée' }}</strong>
        <p>{{ connected ? 'Étiqueteuse liée à cet appareil.' : 'Aucune étiqueteuse liée à cet appareil.' }}</p>
      </section>

      <p class="printer-device" data-zone="E6-appareil">
        <span>Appareil</span> {{ printer.deviceName }}
      </p>

      <p *ngIf="message" class="printer-message" role="status">{{ message }}</p>

      <div class="printer-actions">
        <button type="button" class="printer-primary" data-zone="E6-bouton-connecter" (click)="toggleConnection()">
          {{ connected ? 'Déconnecter' : 'Connecter' }}
        </button>
        <button type="button" class="printer-secondary" data-zone="E6-bouton-tester" [disabled]="!connected" (click)="test()">
          Tester (étiquette d'essai)
        </button>
      </div>
      <a routerLink="/imprimante/diagnostic" class="printer-link" data-zone="E6-lien-diagnostic">Diagnostic matériel</a>
    </main>
  `,
  styles: [`
    .printer-page { display: flex; height: 100%; min-height: 0; flex: 1 1 auto; flex-direction: column; gap: 16px; overflow-y: auto; overscroll-behavior-y: contain; padding: max(16px, env(safe-area-inset-top)) 20px max(24px, env(safe-area-inset-bottom)); background: #f7fbfe; color: #072b45; box-sizing: border-box; -webkit-overflow-scrolling: touch; }
    .printer-header h1 { margin: 6px 0 0; font: 500 30px/1.1 Newsreader, Georgia, serif; }
    .printer-eyebrow { font: 500 11px/1.2 'IBM Plex Mono', ui-monospace, monospace; letter-spacing: .14em; text-transform: uppercase; color: #41617a; }
    .printer-simulation { padding: 12px 16px; border-left: 4px solid #dc6412; background: #fdf1e4; color: #6b3608; font-size: 14px; }
    .printer-state { display: flex; flex-direction: column; gap: 8px; padding: 18px 20px; border: 1px solid #d9e9f4; border-radius: 4px; background: #ffffff; }
    .printer-state strong { font-size: 20px; text-transform: uppercase; letter-spacing: .04em; }
    .printer-state p, .printer-device, .printer-message { margin: 0; font-size: 15px; color: #41617a; }
    .printer-device { display: flex; justify-content: space-between; padding: 14px 20px; border: 1px solid #d9e9f4; border-radius: 4px; background: #ffffff; color: #072b45; font-weight: 600; }
    .printer-device span { font: 500 11px 'IBM Plex Mono', ui-monospace, monospace; letter-spacing: .14em; text-transform: uppercase; color: #6d8ba2; font-weight: 500; }
    .printer-actions { display: flex; flex-direction: column; gap: 12px; margin-top: auto; }
    .printer-actions button { min-height: 52px; border-radius: 999px; font: inherit; font-weight: 700; letter-spacing: .05em; text-transform: uppercase; cursor: pointer; }
    .printer-primary { border: 0; background: #f0801c; color: #ffffff; }
    .printer-secondary { border: 1.5px solid #9dc2da; background: #ffffff; color: #0c6ea6; }
    .printer-link { text-align: center; color: #0c6ea6; font-weight: 600; }
    .printer-actions button:disabled { opacity: .5; cursor: not-allowed; }
    .printer-actions button:focus-visible { outline: 3px solid #0c6ea6; outline-offset: 2px; }
  `],
})
export class LabelPrinterPageComponent {
  message: string | null = null;

  constructor(
    @Inject(LABEL_PRINTER) readonly printer: LabelPrinter,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  get connected(): boolean {
    return this.printer.status() === 'connected';
  }

  async toggleConnection(): Promise<void> {
    this.message = null;

    if (this.connected) {
      this.printer.disconnect();
      this.message = 'Imprimante déconnectée.';
      return;
    }

    try {
      await this.printer.connect();
      this.message = 'Imprimante connectée.';
    } catch (error) {
      this.message = error instanceof Error ? error.message : 'Connexion impossible.';
    } finally {
      // Zoneless : un résultat asynchrone doit demander explicitement un rendu.
      this.cdr.markForCheck();
    }
  }

  async test(): Promise<void> {
    this.message = null;

    try {
      await this.printer.test();
      this.message = this.printer.simulated
        ? 'Test simulé : rien n\'a été imprimé.'
        : 'Étiquette d\'essai envoyée.';
    } catch (error) {
      this.message = error instanceof Error ? error.message : 'Test impossible.';
    } finally {
      this.cdr.markForCheck();
    }
  }
}
