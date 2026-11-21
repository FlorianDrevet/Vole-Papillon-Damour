import {InjectionToken} from '@angular/core';

import {isEan13} from './ean13';

// Interface unique vers l'étiqueteuse (RG-178). Le pilote réel (T50M Pro) implémentera
// cette interface ; les écrans n'appellent que celle-ci.
export type LabelPrinterStatus = 'disconnected' | 'connected';

export interface LabelJob {
  // ISBN-13 ou code interne (EAN-13), RG-175 / RG-170.
  code: string;
  // Prix ferme : uniquement pour un livre rare (RG-179). Null pour un ordinaire.
  price: string | null;
  copies: number;
}

export interface LabelPrinter {
  readonly simulated: boolean;
  readonly deviceName: string;
  status(): LabelPrinterStatus;
  connect(): Promise<void>;
  disconnect(): void;
  test(): Promise<void>;
  print(job: LabelJob): Promise<void>;
}

export const LABEL_PRINTER = new InjectionToken<LabelPrinter>('LABEL_PRINTER');

export const MAX_LABEL_COPIES = 50;

export function validateLabelJob(job: LabelJob): string | null {
  if (!isEan13(job.code)) {
    return 'Code invalide : une étiquette ne porte qu\'un ISBN-13 ou un code interne.';
  }

  if (!Number.isInteger(job.copies) || job.copies < 1 || job.copies > MAX_LABEL_COPIES) {
    return `Nombre d'étiquettes : entre 1 et ${MAX_LABEL_COPIES}.`;
  }

  return null;
}

// Implémentation simulée : aucune impression réelle, aucune trace écrite (RG-177, RG-178).
export class SimulatedLabelPrinter implements LabelPrinter {
  readonly simulated = true;
  readonly deviceName = 'T50M Pro (simulation)';

  private currentStatus: LabelPrinterStatus = 'disconnected';

  status(): LabelPrinterStatus {
    return this.currentStatus;
  }

  async connect(): Promise<void> {
    this.currentStatus = 'connected';
  }

  disconnect(): void {
    this.currentStatus = 'disconnected';
  }

  async test(): Promise<void> {
    this.assertConnected();
  }

  async print(job: LabelJob): Promise<void> {
    this.assertConnected();

    const error = validateLabelJob(job);
    if (error) {
      throw new Error(error);
    }
  }

  private assertConnected(): void {
    if (this.currentStatus !== 'connected') {
      throw new Error('Imprimante non connectée.');
    }
  }
}
