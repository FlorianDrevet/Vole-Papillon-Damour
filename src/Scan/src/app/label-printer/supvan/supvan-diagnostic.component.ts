import {ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy} from '@angular/core';
import {Location} from '@angular/common';

import {ScanTelemetryService} from '../../offline/scan-telemetry.service';
import {SupvanLabelPrinter, webBluetooth, webSerial} from './supvan-printer';
import {MAX_DENSITY} from './supvan-raster';
import {describeStatus, statusErrors} from './supvan-protocol';

// Page de dépannage de la T50M Pro. Hors parcours bénévole : elle permet de vérifier la liaison,
// de lire l'état de l'imprimante et d'imprimer une étiquette de contrôle.
// Elle partage l'imprimante de l'application (connexion et journal) ; chaque action part en télémétrie
// (événement `label_printer_diagnostic`, en plus des événements `label_printer_*` du pilote).
@Component({
  selector: 'app-supvan-diagnostic',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
  template: `
    <main class="diag-page">
      <button type="button" class="diag-back" data-zone="D1-retour" (click)="goBack()" aria-label="Retour à la page précédente">
        <svg aria-hidden="true" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
          <path d="M14.5 5 7.5 12l7 7"></path>
          <path d="M8 12h12"></path>
        </svg>
        <span>Retour</span>
      </button>

      <header>
        <span class="diag-eyebrow">Matériel</span>
        <h1>Dépannage de l'imprimante</h1>
      </header>

      <section class="diag-card" data-zone="D1-prerequis">
        <strong>Avant de commencer</strong>
        <ol>
          <li>Allumer l'étiqueteuse (rouleau en place, capot fermé).</li>
          <li>L'appairer dans les réglages Bluetooth du téléphone (nom « T0117… »), si ce n'est pas déjà fait.</li>
          <li>Ouvrir cette page dans <b>Chrome</b> (Android) ou dans <b>Bluefy</b> (iPhone).</li>
        </ol>
        <p class="diag-compat" [class.diag-ko]="!supported && !supportedBle">
          Bluetooth série : {{ supported ? 'disponible' : 'non disponible' }}
          · Bluetooth basse conso. : {{ supportedBle ? 'disponible' : 'non disponible' }}
        </p>
      </section>

      <section *ngIf="onIos && !supportedBle" class="diag-card" data-zone="D1b-iphone">
        <strong>Sur iPhone</strong>
        <p>Safari et Chrome n'autorisent pas le Bluetooth sur iPhone. Installez l'application gratuite
          <b>Bluefy</b> (App Store), ouvrez cette même adresse dedans, reconnectez-vous, puis utilisez le bouton
          « 1 bis · BLE ».</p>
      </section>

      <div class="diag-actions">
        <button type="button" class="diag-primary" data-zone="D2-connecter" [disabled]="busy || !supported" (click)="connect('serial')">
          {{ connected ? 'Relancer le diagnostic' : '1 · Connecter (Bluetooth série)' }}
        </button>
        <button type="button" class="diag-secondary" data-zone="D2b-connecter-ble" [disabled]="busy || !supportedBle" (click)="connect('ble')">
          1 bis · Connecter (Bluetooth basse conso. BLE)
        </button>
        <label class="diag-density">
          Densité d'impression
          <select [value]="density" (change)="setDensity($any($event.target).value)">
            <option *ngFor="let level of densities" [value]="level" [selected]="level === density">{{ level }}{{ level === 4 ? ' (défaut)' : '' }}</option>
          </select>
        </label>
        <button type="button" class="diag-secondary" data-zone="D3-imprimer" [disabled]="busy || !connected" (click)="printTest()">
          2 · Imprimer une étiquette de contrôle
        </button>
        <button type="button" class="diag-secondary" data-zone="D4-copier" (click)="copyReport()">Copier le rapport</button>
        <button type="button" class="diag-link" [disabled]="!connected" (click)="disconnect()">Déconnecter</button>
      </div>

      <p *ngIf="summary" class="diag-summary" [class.diag-ko]="summaryIsError" role="status">{{ summary }}</p>

      <section class="diag-card" data-zone="D5-journal">
        <strong>Journal</strong>
        <pre>{{ journal }}</pre>
      </section>
    </main>
  `,
  styles: [`
    .diag-page { display: flex; height: 100vh; height: 100dvh; min-height: 0; flex-direction: column; gap: 16px; overflow-y: auto; overscroll-behavior-y: contain; padding: max(16px, env(safe-area-inset-top)) 20px max(24px, env(safe-area-inset-bottom)); background: #f7fbfe; color: #072b45; box-sizing: border-box; -webkit-overflow-scrolling: touch; }
    .diag-back { display: inline-flex; min-height: 44px; align-items: center; align-self: flex-start; gap: 8px; padding: 0; border: 0; background: transparent; color: #0c6ea6; font: inherit; font-size: 14px; font-weight: 600; cursor: pointer; }
    .diag-back svg { width: 20px; height: 20px; }
    h1 { margin: 6px 0 0; font: 500 28px/1.1 Newsreader, Georgia, serif; }
    .diag-eyebrow { font: 500 11px/1.2 'IBM Plex Mono', ui-monospace, monospace; letter-spacing: .14em; text-transform: uppercase; color: #41617a; }
    .diag-card { display: flex; flex-direction: column; gap: 8px; padding: 16px 18px; border: 1px solid #d9e9f4; border-radius: 4px; background: #fff; font-size: 15px; }
    .diag-card ol { margin: 0; padding-left: 20px; }
    .diag-card pre { margin: 0; max-height: 360px; overflow: auto; white-space: pre-wrap; word-break: break-all; font: 12px/1.45 'IBM Plex Mono', ui-monospace, monospace; }
    .diag-compat { margin: 0; font-weight: 600; color: #1b6e3c; }
    .diag-ko { color: #b42318; }
    .diag-summary { margin: 0; padding: 12px 16px; border-left: 4px solid #1b6e3c; background: #eaf6ee; font-weight: 600; }
    .diag-summary.diag-ko { border-left-color: #b42318; background: #fdecea; }
    .diag-actions { display: flex; flex-direction: column; gap: 12px; }
    .diag-actions button { min-height: 52px; border-radius: 999px; font: inherit; font-weight: 700; cursor: pointer; }
    .diag-primary { border: 0; background: #f0801c; color: #fff; }
    .diag-secondary { border: 1.5px solid #9dc2da; background: #fff; color: #0c6ea6; }
    .diag-link { border: 0; background: transparent; color: #0c6ea6; min-height: 40px !important; }
    .diag-actions button:disabled { opacity: .5; cursor: not-allowed; }
    .diag-density { display: flex; justify-content: space-between; align-items: center; font-size: 14px; }
    .diag-density select { min-height: 40px; padding: 0 8px; }
    .diag-back:focus-visible { outline: 3px solid #0c6ea6; outline-offset: 2px; }
  `],
})
export class SupvanDiagnosticComponent implements OnDestroy {
  readonly supported = webSerial() !== null;
  readonly supportedBle = webBluetooth() !== null;
  readonly onIos = /iPad|iPhone|iPod/.test(navigator.userAgent)
    || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  readonly densities = Array.from({length: MAX_DENSITY}, (_, index) => index + 1);
  busy = false;
  summary: string | null = null;
  summaryIsError = false;

  private readonly unsubscribe: () => void;

  constructor(
    private readonly cdr: ChangeDetectorRef,
    private readonly telemetry: ScanTelemetryService,
    private readonly printer: SupvanLabelPrinter,
    private readonly location: Location,
  ) {
    // Zoneless : chaque ligne du journal et chaque changement d'état doit redessiner la page.
    this.unsubscribe = printer.subscribe(() => this.cdr.markForCheck());
  }

  get journal(): string {
    return this.printer.journal;
  }

  get density(): number {
    return this.printer.density;
  }

  get connected(): boolean {
    return this.printer.status() === 'connected';
  }

  goBack(): void {
    this.location.back();
  }

  ngOnDestroy(): void {
    // La connexion reste ouverte : l'imprimante est partagée avec le reste de l'application.
    this.unsubscribe();
  }

  setDensity(value: string): void {
    this.printer.density = Number(value);
  }

  async connect(transport: 'serial' | 'ble'): Promise<void> {
    await this.run('connexion', async () => {
      if (!this.connected) {
        this.printer.log('Choisissez l\'étiqueteuse dans la fenêtre Bluetooth…');
        await this.printer.connect(transport);
        this.printer.log(`Connecté : ${this.printer.deviceName}`);
      }

      const client = this.printer.supvanClient;
      if (!client) {
        throw new Error('Connexion perdue.');
      }

      const material = await client.queryMaterial();
      if (material) {
        this.printer.log(`Rouleau : ${material.widthMm} × ${material.heightMm} mm, écart ${material.gapMm} mm, `
          + `restantes ${material.remaining ?? '?'}, type ${material.labelType}, uuid ${material.uuid}, code ${material.code}`);
      } else {
        this.printer.log('Rouleau : réponse illisible.');
      }

      this.printer.log(`Firmware : ${(await client.readFirmwareVersion()) ?? '?'} · protocole : ${(await client.readProtocolVersion()) ?? '?'}`);

      const status = await client.queryStatus();
      if (!status) {
        throw new Error('Statut illisible.');
      }
      this.printer.log(`Statut : ${describeStatus(status)}`);

      const errors = statusErrors(status);
      this.summary = errors.length === 0
        ? `Liaison OK${material ? ` — rouleau ${material.widthMm}×${material.heightMm} mm` : ''}. Vous pouvez imprimer l'étiquette de test.`
        : `Liaison OK mais : ${errors.join(', ')}.`;
      this.summaryIsError = errors.length > 0;
    });
  }

  async printTest(): Promise<void> {
    await this.run('impression', async () => {
      this.printer.log(`Impression de l'étiquette de contrôle (densité ${this.density})…`);
      await this.printer.printDiagnosticLabel();
      this.printer.log('Étiquette envoyée.');
      this.summary = 'Étiquette de contrôle envoyée. Vérifiez : cadre complet, code-barres net, chiffres lisibles.';
      this.summaryIsError = false;
    });
  }

  disconnect(): void {
    this.printer.disconnect();
    this.summary = null;
    this.cdr.markForCheck();
  }

  async copyReport(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.journal);
      this.summary = 'Rapport copié dans le presse-papiers.';
      this.summaryIsError = false;
    } catch {
      this.summary = 'Copie impossible : faites une capture d\'écran du journal.';
      this.summaryIsError = true;
    }
    this.cdr.markForCheck();
  }

  private async run(step: string, action: () => Promise<void>): Promise<void> {
    this.busy = true;
    this.summary = null;
    this.cdr.markForCheck();
    try {
      await action();
      this.telemetry.trackEvent('label_printer_diagnostic', {step, ok: 'true', journal: this.journal.slice(-6000)});
    } catch (error) {
      const message = error instanceof Error ? `${error.name}: ${error.message}` : String(error);
      this.printer.log(`ERREUR : ${message}`);
      this.summary = error instanceof Error ? error.message : 'Échec.';
      this.summaryIsError = true;
      this.telemetry.trackEvent('label_printer_diagnostic', {step, ok: 'false', error: message, journal: this.journal.slice(-6000)});
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }
}
