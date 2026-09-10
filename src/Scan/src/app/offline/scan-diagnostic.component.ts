import {ChangeDetectorRef, Component, OnInit} from '@angular/core';

import {
  createScanDiagnosticExport,
  ScanDiagnosticExport,
  serializeScanDiagnosticExport,
} from './scan-diagnostic-export';
import {ScanLocalStoreService} from './scan-local-store.service';
import {ScanCatalogBook} from './scan-offline.model';

@Component({
  selector: 'app-scan-diagnostic',
  templateUrl: './scan-diagnostic.component.html',
  styleUrls: ['./scan-diagnostic.component.scss'],
  standalone: false,
})
export class ScanDiagnosticComponent implements OnInit {
  diagnostic: ScanDiagnosticExport | null = null;
  recentCatalog: ScanCatalogBook[] = [];
  loadError: string | null = null;
  copyError: string | null = null;
  copyConfirmation: string | null = null;
  copying = false;

  constructor(
    private readonly store: ScanLocalStoreService,
    private readonly changeDetector: ChangeDetectorRef,
  ) {}

  async ngOnInit(): Promise<void> {
    try {
      const state = await this.store.getDiagnosticState();
      this.recentCatalog = state.recentCatalog;
      this.diagnostic = createScanDiagnosticExport(state);
    } catch (error: unknown) {
      this.loadError = error instanceof Error
        ? error.message
        : 'Impossible de lire le journal local.';
    }
    this.changeDetector.markForCheck();
  }

  async copyJournal(): Promise<void> {
    if (!this.diagnostic || this.copying) {
      return;
    }

    this.copying = true;
    this.copyError = null;
    this.copyConfirmation = null;
    try {
      if (typeof navigator === 'undefined' || !navigator.clipboard?.writeText) {
        throw new Error('Le presse-papiers est indisponible dans ce navigateur.');
      }

      const state = await this.store.getDiagnosticState();
      this.recentCatalog = state.recentCatalog;
      this.diagnostic = createScanDiagnosticExport(state);
      await navigator.clipboard.writeText(serializeScanDiagnosticExport(state));
      this.copyConfirmation = 'Journal local copié dans le presse-papiers.';
    } catch (error: unknown) {
      this.copyError = error instanceof Error
        ? error.message
        : 'Impossible de copier le journal local.';
    } finally {
      this.copying = false;
      this.changeDetector.markForCheck();
    }
  }
}
