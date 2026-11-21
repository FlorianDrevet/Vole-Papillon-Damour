import {ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy} from '@angular/core';

import {ScanTelemetryService} from '../../offline/scan-telemetry.service';
import {SupvanLabelPrinter, webBluetooth, webSerial} from './supvan-printer';
import {describeStatus, statusErrors} from './supvan-protocol';

// Page de diagnostic matériel de la T50M Pro (Bluetooth série). Hors parcours bénévole :
// elle permet de vérifier la liaison, de lire l'état et de lancer une étiquette d'essai.
// Chaque ligne du journal part aussi en télémétrie (événement `label_printer_diagnostic`).
@Component({
  selector: 'app-supvan-diagnostic',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
  template: `
    <main class="diag-page">
      <header>
        <span class="diag-eyebrow">Matériel</span>
        <h1>Diagnostic de l'étiqueteuse</h1>
      </header>

      <section class="diag-card" data-zone="D1-prerequis">
        <strong>Avant de commencer</strong>
        <ol>
          <li>Allumer l'étiqueteuse (rouleau en place, capot fermé).</li>
          <li>L'appairer dans les réglages Bluetooth du téléphone (nom « T0117… »), si ce n'est pas déjà fait.</li>
          <li>Ouvrir cette page dans <b>Chrome</b>.</li>
        </ol>
        <p class="diag-compat" [class.diag-ko]="!supported && !supportedBle">
          Bluetooth série : {{ supported ? 'disponible' : 'non disponible' }}
          · Bluetooth basse conso. : {{ supportedBle ? 'disponible' : 'non disponible' }}
        </p>
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
          2 · Imprimer l'étiquette de test
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
    .diag-page { display: flex; flex-direction: column; gap: 16px; padding: 22px 20px 28px; background: #f7fbfe; color: #072b45; box-sizing: border-box; min-height: 100%; }
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
  `],
})
export class SupvanDiagnosticComponent implements OnDestroy {
  readonly supported = webSerial() !== null;
  readonly supportedBle = webBluetooth() !== null;
  readonly densities = Array.from({length: 15}, (_, index) => index + 1);
  density = 4;
  busy = false;
  summary: string | null = null;
  summaryIsError = false;
  journal = '';

  private readonly started = Date.now();
  private readonly printer = new SupvanLabelPrinter(line => this.write(line));

  constructor(private readonly cdr: ChangeDetectorRef, private readonly telemetry: ScanTelemetryService) {
    this.write(`Navigateur : ${navigator.userAgent}`);
    this.write(`Web Serial : ${this.supported ? 'oui' : 'non'} · Web Bluetooth : ${this.supportedBle ? 'oui' : 'non'} · contexte sécurisé : ${window.isSecureContext ? 'oui' : 'non'}`);
  }

  get connected(): boolean {
    return this.printer.status() === 'connected';
  }

  ngOnDestroy(): void {
    this.printer.disconnect();
  }

  setDensity(value: string): void {
    this.density = Number(value);
    this.printer.density = this.density;
  }

  async connect(transport: 'serial' | 'ble'): Promise<void> {
    await this.run('connexion', async () => {
      if (!this.connected) {
        this.write('Choisissez l\'étiqueteuse dans la fenêtre Bluetooth…');
        await this.printer.connect(transport);
        this.write(`Connecté : ${this.printer.deviceName}`);
      }

      const client = this.printer.supvanClient;
      if (!client) {
        throw new Error('Connexion perdue.');
      }

      const material = await client.queryMaterial();
      if (material) {
        this.write(`Rouleau : ${material.widthMm} × ${material.heightMm} mm, écart ${material.gapMm} mm, `
          + `restantes ${material.remaining ?? '?'}, type ${material.labelType}, uuid ${material.uuid}, code ${material.code}`);
      } else {
        this.write('Rouleau : réponse illisible.');
      }

      this.write(`Firmware : ${(await client.readFirmwareVersion()) ?? '?'} · protocole : ${(await client.readProtocolVersion()) ?? '?'}`);

      const status = await client.queryStatus();
      if (!status) {
        throw new Error('Statut illisible.');
      }
      this.write(`Statut : ${describeStatus(status)}`);

      const errors = statusErrors(status);
      this.summary = errors.length === 0
        ? `Liaison OK${material ? ` — rouleau ${material.widthMm}×${material.heightMm} mm` : ''}. Vous pouvez imprimer l'étiquette de test.`
        : `Liaison OK mais : ${errors.join(', ')}.`;
      this.summaryIsError = errors.length > 0;
    });
  }

  async printTest(): Promise<void> {
    await this.run('impression', async () => {
      this.printer.density = this.density;
      this.write(`Impression de l'étiquette de test (densité ${this.density})…`);
      await this.printer.test();
      this.write('Étiquette envoyée.');
      this.summary = 'Étiquette envoyée. Vérifiez : code-barres net, chiffres lisibles, étiquette complète.';
      this.summaryIsError = false;
    });
  }

  disconnect(): void {
    this.printer.disconnect();
    this.write('Déconnecté.');
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
      this.write(`ERREUR : ${message}`);
      this.summary = error instanceof Error ? error.message : 'Échec.';
      this.summaryIsError = true;
      this.telemetry.trackEvent('label_printer_diagnostic', {step, ok: 'false', error: message, journal: this.journal.slice(-6000)});
    } finally {
      this.busy = false;
      this.cdr.markForCheck();
    }
  }

  private write(line: string): void {
    const seconds = ((Date.now() - this.started) / 1000).toFixed(1).padStart(6, ' ');
    this.journal += `${seconds}s  ${line}\n`;
    this.cdr.markForCheck();
  }
}
