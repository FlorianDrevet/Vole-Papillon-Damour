import {ChangeDetectionStrategy, ChangeDetectorRef, Component, Inject, OnDestroy} from '@angular/core';

import {LABEL_PRINTER, LabelPrinter, LabelPrinterTransport} from './label-printer';
import {MAX_DENSITY} from './supvan/supvan-raster';

// E6 — Imprimante d'étiquettes : connexion, rouleau détecté, noirceur d'impression.
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

      <section *ngIf="!canConnect" class="printer-warning" role="alert" data-zone="E6-navigateur">
        <strong>Ce navigateur ne peut pas parler à l'imprimante.</strong>
        <p>Sur Android, ouvrez l'application dans <b>Chrome</b>. Sur iPhone, ouvrez-la dans <b>Bluefy</b> (gratuit sur l'App Store).</p>
      </section>

      <section class="printer-state" data-zone="E6-etat" aria-labelledby="printer-state-title">
        <span class="printer-eyebrow">État</span>
        <strong id="printer-state-title">{{ connected ? 'Connectée' : 'Non connectée' }}</strong>
        <p>{{ connected ? 'Étiqueteuse liée à cet appareil.' : 'Allumez l\\'étiqueteuse, approchez le téléphone, puis appuyez sur « Connecter ».' }}</p>
      </section>

      <p class="printer-device" data-zone="E6-appareil">
        <span>Appareil</span> {{ connected ? printer.deviceName : '—' }}
      </p>

      <p *ngIf="connected" class="printer-device" data-zone="E6-rouleau">
        <span>Rouleau</span>
        {{ printer.roll ? printer.roll.widthMm + ' × ' + printer.roll.heightMm + ' mm' + (printer.roll.remaining !== null ? ' · ' + printer.roll.remaining + ' restantes' : '') : 'non détecté' }}
      </p>

      <section class="printer-density" data-zone="E6-noirceur" aria-labelledby="printer-density-title">
        <div>
          <strong id="printer-density-title">Noirceur d'impression</strong>
          <p>Augmentez si le code-barres est pâle, diminuez s'il bave.</p>
        </div>
        <div class="printer-stepper">
          <button type="button" (click)="changeDensity(-1)" [disabled]="printer.density <= 1" aria-label="Moins foncé">−</button>
          <strong aria-live="polite">{{ printer.density }}</strong>
          <button type="button" (click)="changeDensity(1)" [disabled]="printer.density >= maxDensity" aria-label="Plus foncé">+</button>
        </div>
      </section>

      <p *ngIf="message" class="printer-message" [class.printer-error]="messageIsError" role="status">{{ message }}</p>

      <div class="printer-actions">
        <button type="button" class="printer-primary" data-zone="E6-bouton-connecter" [disabled]="busy || (!connected && !canConnect)" (click)="toggleConnection()">
          {{ busy ? 'Connexion…' : connected ? 'Déconnecter' : 'Connecter' }}
        </button>
        <button *ngIf="!connected && otherTransport" type="button" class="printer-secondary" data-zone="E6-autre-mode" [disabled]="busy" (click)="connectWith(otherTransport)">
          {{ otherTransport === 'serial' ? 'Essayer le Bluetooth série' : 'Essayer le Bluetooth basse consommation' }}
        </button>
      </div>
      <a routerLink="/imprimante/diagnostic" class="printer-link" data-zone="E6-lien-diagnostic">Dépannage de l'imprimante</a>
    </main>
  `,
  styles: [`
    .printer-page { display: flex; height: 100%; min-height: 0; flex: 1 1 auto; flex-direction: column; gap: 16px; overflow-y: auto; overscroll-behavior-y: contain; padding: max(16px, env(safe-area-inset-top)) 20px max(24px, env(safe-area-inset-bottom)); background: #f7fbfe; color: #072b45; box-sizing: border-box; -webkit-overflow-scrolling: touch; }
    .printer-header h1 { margin: 6px 0 0; font: 500 30px/1.1 Newsreader, Georgia, serif; }
    .printer-eyebrow { font: 500 11px/1.2 'IBM Plex Mono', ui-monospace, monospace; letter-spacing: .14em; text-transform: uppercase; color: #41617a; }
    .printer-warning { display: flex; flex-direction: column; gap: 6px; padding: 12px 16px; border-left: 4px solid #b42318; background: #fdecea; color: #7a1b14; font-size: 14px; }
    .printer-warning p { margin: 0; }
    .printer-state { display: flex; flex-direction: column; gap: 8px; padding: 18px 20px; border: 1px solid #d9e9f4; border-radius: 4px; background: #ffffff; }
    .printer-state strong { font-size: 20px; text-transform: uppercase; letter-spacing: .04em; }
    .printer-state p, .printer-device, .printer-message { margin: 0; font-size: 15px; color: #41617a; }
    .printer-device { display: flex; justify-content: space-between; gap: 12px; padding: 14px 20px; border: 1px solid #d9e9f4; border-radius: 4px; background: #ffffff; color: #072b45; font-weight: 600; }
    .printer-device span { font: 500 11px 'IBM Plex Mono', ui-monospace, monospace; letter-spacing: .14em; text-transform: uppercase; color: #6d8ba2; font-weight: 500; }
    .printer-density { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 14px 20px; border: 1px solid #d9e9f4; border-radius: 4px; background: #ffffff; }
    .printer-density p { margin: 4px 0 0; font-size: 13px; color: #41617a; }
    .printer-stepper { display: flex; align-items: center; flex: none; border: 1px solid #d9e9f4; border-radius: 4px; }
    .printer-stepper button { width: 44px; height: 40px; border: 0; background: transparent; color: #0c6ea6; font-size: 22px; cursor: pointer; }
    .printer-stepper button:disabled { opacity: .4; cursor: not-allowed; }
    .printer-stepper strong { width: 40px; text-align: center; font: 500 18px 'IBM Plex Mono', ui-monospace, monospace; }
    .printer-error { color: #b42318; font-weight: 600; }
    .printer-actions { display: flex; flex-direction: column; gap: 12px; margin-top: auto; }
    .printer-actions button { min-height: 52px; border-radius: 999px; font: inherit; font-weight: 700; letter-spacing: .05em; text-transform: uppercase; cursor: pointer; }
    .printer-primary { border: 0; background: #f0801c; color: #ffffff; }
    .printer-secondary { border: 1.5px solid #9dc2da; background: #ffffff; color: #0c6ea6; }
    .printer-link { text-align: center; color: #0c6ea6; font-weight: 600; }
    .printer-actions button:disabled { opacity: .5; cursor: not-allowed; }
    .printer-actions button:focus-visible, .printer-stepper button:focus-visible { outline: 3px solid #0c6ea6; outline-offset: 2px; }
  `],
})
export class LabelPrinterPageComponent implements OnDestroy {
  readonly maxDensity = MAX_DENSITY;
  readonly canConnect: boolean;
  readonly otherTransport: LabelPrinterTransport | null;

  busy = false;
  message: string | null = null;
  messageIsError = false;

  private readonly unsubscribe: () => void;

  constructor(
    @Inject(LABEL_PRINTER) readonly printer: LabelPrinter,
    private readonly cdr: ChangeDetectorRef,
  ) {
    const preferred = printer.preferredTransport();
    this.canConnect = preferred !== null;
    this.otherTransport = printer.availableTransports().find(transport => transport !== preferred) ?? null;
    // Zoneless : une coupure de liaison ou un changement d'état doit redessiner l'écran.
    this.unsubscribe = printer.subscribe(() => this.cdr.markForCheck());
  }

  get connected(): boolean {
    return this.printer.status() === 'connected';
  }

  ngOnDestroy(): void {
    this.unsubscribe();
  }

  changeDensity(delta: number): void {
    this.printer.density = this.printer.density + delta;
    this.cdr.markForCheck();
  }

  async toggleConnection(): Promise<void> {
    if (this.connected) {
      this.printer.disconnect();
      this.show('Imprimante déconnectée.', false);
      return;
    }

    await this.connectWith(this.printer.preferredTransport());
  }

  async connectWith(transport: LabelPrinterTransport | null): Promise<void> {
    this.busy = true;
    this.message = null;
    this.cdr.markForCheck();

    try {
      await this.printer.connect(transport ?? undefined);
      const roll = this.printer.roll;
      this.show(roll
        ? `Imprimante connectée — rouleau ${roll.widthMm} × ${roll.heightMm} mm.`
        : 'Imprimante connectée, mais le rouleau n\'est pas détecté : vérifiez qu\'il est en place, capot fermé.', roll === null);
    } catch (error) {
      this.show(error instanceof Error ? error.message : 'Connexion impossible.', true);
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  private show(message: string, isError: boolean): void {
    this.message = message;
    this.messageIsError = isError;
    this.cdr.markForCheck();
  }
}
