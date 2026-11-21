import {ChangeDetectionStrategy, ChangeDetectorRef, Component, Inject} from '@angular/core';
import {ActivatedRoute} from '@angular/router';

import {normalizeIsbn} from '../scanner/isbn.util';
import {Ean13BarRect, ean13BarRects, EAN13_MODULE_COUNT, isEan13} from './ean13';
import {LABEL_PRINTER, LabelPrinter, MAX_LABEL_COPIES} from './label-printer';

// E7 — Aperçu et impression d'étiquette.
// Entrée : ?isbn=<ISBN> (réimpression d'un ISBN, RG-175) ou ?code=<EAN-13 interne> [&price=<prix>] (RG-179).
@Component({
  selector: 'app-label-preview',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
  template: `
    <main class="preview-page">
      <header class="preview-header">
        <span class="preview-eyebrow">Étiquette</span>
        <h1>Aperçu et impression</h1>
      </header>

      <div *ngIf="printer.simulated" class="preview-simulation" role="status" data-zone="E7-statut-simulation">
        Simulation — rien n'est imprimé
      </div>

      <p *ngIf="error" class="preview-error" role="alert">{{ error }}</p>

      <section *ngIf="code" class="preview-label" data-zone="E7-apercu-etiquette" aria-label="Aperçu de l'étiquette">
        <svg class="preview-barcode" [attr.viewBox]="'0 0 ' + moduleCount + ' 60'" role="img" [attr.aria-label]="'Code-barres ' + code">
          <rect *ngFor="let bar of bars" [attr.x]="bar.x" y="0" [attr.width]="bar.width" height="60" fill="#072b45"></rect>
        </svg>
        <span class="preview-digits" data-zone="E7-code-chiffres">{{ code }}</span>
        <span *ngIf="price" class="preview-price" data-zone="E7-prix">Prix ferme : {{ price }}</span>
      </section>

      <div *ngIf="code" class="preview-copies" data-zone="E7-nombre-etiquettes">
        <span>Étiquettes</span>
        <div class="preview-stepper">
          <button type="button" (click)="changeCopies(-1)" [disabled]="copies <= 1" aria-label="Retirer une étiquette">−</button>
          <strong aria-live="polite">{{ copies }}</strong>
          <button type="button" (click)="changeCopies(1)" [disabled]="copies >= maxCopies" aria-label="Ajouter une étiquette">+</button>
        </div>
      </div>

      <p *ngIf="status" class="preview-status" role="status">{{ status }}</p>

      <div class="preview-actions">
        <button *ngIf="code" type="button" class="preview-primary" data-zone="E7-bouton-imprimer" [disabled]="busy" (click)="print()">
          Imprimer {{ copies }} étiquette{{ copies > 1 ? 's' : '' }}
        </button>
        <a *ngIf="code && !connected" routerLink="/imprimante" class="preview-link" data-zone="E7-lien-imprimante">
          Connecter l'imprimante
        </a>
        <a routerLink="/accueil" class="preview-link">Retour à l'accueil</a>
      </div>
    </main>
  `,
  styles: [`
    .preview-page { display: flex; height: 100%; min-height: 0; flex: 1 1 auto; flex-direction: column; gap: 16px; overflow-y: auto; overscroll-behavior-y: contain; padding: max(16px, env(safe-area-inset-top)) 20px max(24px, env(safe-area-inset-bottom)); background: #f7fbfe; color: #072b45; box-sizing: border-box; -webkit-overflow-scrolling: touch; }
    .preview-header h1 { margin: 6px 0 0; font: 500 30px/1.1 Newsreader, Georgia, serif; }
    .preview-eyebrow { font: 500 11px/1.2 'IBM Plex Mono', ui-monospace, monospace; letter-spacing: .14em; text-transform: uppercase; color: #41617a; }
    .preview-simulation { padding: 12px 16px; border-left: 4px solid #dc6412; background: #fdf1e4; color: #6b3608; font-size: 14px; }
    .preview-error { margin: 0; padding: 12px 16px; border-left: 4px solid #b42318; background: #fdecea; color: #7a1b14; font-size: 14px; }
    .preview-label { display: flex; flex-direction: column; align-items: center; gap: 10px; padding: 20px; border: 1px solid #d9e9f4; border-radius: 4px; background: #ffffff; }
    .preview-barcode { width: 100%; max-width: 300px; height: 96px; }
    .preview-digits { font: 500 15px 'IBM Plex Mono', ui-monospace, monospace; letter-spacing: .08em; }
    .preview-price { font: 600 16px 'IBM Plex Mono', ui-monospace, monospace; color: #5f3a86; }
    .preview-copies { display: flex; align-items: center; justify-content: space-between; padding: 12px 20px; border: 1px solid #d9e9f4; border-radius: 4px; background: #ffffff; }
    .preview-copies > span { font: 500 11px 'IBM Plex Mono', ui-monospace, monospace; letter-spacing: .14em; text-transform: uppercase; color: #6d8ba2; }
    .preview-stepper { display: flex; align-items: center; border: 1px solid #d9e9f4; border-radius: 4px; }
    .preview-stepper button { width: 44px; height: 40px; border: 0; background: transparent; color: #0c6ea6; font-size: 22px; cursor: pointer; }
    .preview-stepper strong { width: 44px; text-align: center; font: 500 18px 'IBM Plex Mono', ui-monospace, monospace; }
    .preview-status { margin: 0; font-size: 15px; color: #41617a; }
    .preview-actions { display: flex; flex-direction: column; gap: 12px; margin-top: auto; }
    .preview-primary { min-height: 56px; border: 0; border-radius: 999px; background: #f0801c; color: #ffffff; font: inherit; font-weight: 700; letter-spacing: .05em; text-transform: uppercase; cursor: pointer; }
    .preview-primary:disabled { opacity: .5; cursor: not-allowed; }
    .preview-link { text-align: center; color: #0c6ea6; font-weight: 600; }
    .preview-link:focus-visible, .preview-primary:focus-visible, .preview-stepper button:focus-visible { outline: 3px solid #0c6ea6; outline-offset: 2px; }
  `],
})
export class LabelPreviewComponent {
  readonly maxCopies = MAX_LABEL_COPIES;
  readonly moduleCount = EAN13_MODULE_COUNT;

  code: string | null = null;
  price: string | null = null;
  bars: Ean13BarRect[] = [];
  copies = 1;
  busy = false;
  error: string | null = null;
  status: string | null = null;

  constructor(
    @Inject(LABEL_PRINTER) readonly printer: LabelPrinter,
    route: ActivatedRoute,
    private readonly cdr: ChangeDetectorRef,
  ) {
    const params = route.snapshot.queryParamMap;
    const isbn = params.get('isbn');
    const code = params.get('code');

    if (isbn !== null) {
      const normalized = normalizeIsbn(isbn);
      if (normalized === null) {
        this.error = 'ISBN invalide : impossible de préparer l\'étiquette.';
      } else {
        this.code = normalized;
      }
    } else if (code !== null) {
      if (isEan13(code)) {
        this.code = code;
        this.price = params.get('price');
      } else {
        this.error = 'Code invalide : une étiquette ne porte qu\'un ISBN-13 ou un code interne.';
      }
    } else {
      this.error = 'Aucune étiquette à préparer.';
    }

    if (this.code !== null) {
      this.bars = ean13BarRects(this.code);
    }
  }

  get connected(): boolean {
    return this.printer.status() === 'connected';
  }

  changeCopies(delta: number): void {
    this.copies = Math.min(this.maxCopies, Math.max(1, this.copies + delta));
  }

  async print(): Promise<void> {
    if (this.code === null) {
      return;
    }

    this.busy = true;
    this.status = null;

    try {
      await this.printer.print({code: this.code, price: this.price, copies: this.copies});
      this.status = this.printer.simulated
        ? 'Simulation : aucune étiquette n\'a été imprimée.'
        : `${this.copies} étiquette${this.copies > 1 ? 's' : ''} envoyée${this.copies > 1 ? 's' : ''} à l'imprimante.`;
    } catch (error) {
      this.status = error instanceof Error ? error.message : 'Impression impossible.';
    } finally {
      this.busy = false;
      // Zoneless : un résultat asynchrone doit demander explicitement un rendu.
      this.cdr.markForCheck();
    }
  }
}
