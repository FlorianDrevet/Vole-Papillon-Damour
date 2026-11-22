import {InjectionToken} from '@angular/core';

import {isEan13} from './ean13';

// Interface unique vers l'étiqueteuse (RG-178). Les écrans n'appellent que celle-ci ;
// le pilote réel (T50M Pro) est dans ./supvan.
export type LabelPrinterStatus = 'disconnected' | 'connected';

// Mode de liaison : Bluetooth basse consommation (Chrome Android, Bluefy sur iPhone) ou Bluetooth série (Chrome).
export type LabelPrinterTransport = 'ble' | 'serial';

export interface LabelJob {
  // ISBN-13 ou code interne (EAN-13), RG-175 / RG-170.
  code: string;
  // Prix ferme : uniquement pour un livre rare (RG-179). Null pour un ordinaire.
  price: string | null;
  copies: number;
}

// Rouleau d'étiquettes lu dans l'imprimante.
export interface LabelRoll {
  widthMm: number;
  heightMm: number;
  remaining: number | null;
}

export interface LabelPrinter {
  readonly deviceName: string;
  readonly roll: LabelRoll | null;
  // Niveau de noirceur (1 à 15), mémorisé sur l'appareil.
  density: number;
  availableTransports(): LabelPrinterTransport[];
  preferredTransport(): LabelPrinterTransport | null;
  status(): LabelPrinterStatus;
  connect(transport?: LabelPrinterTransport): Promise<void>;
  disconnect(): void;
  print(job: LabelJob, onProgress?: (done: number, total: number) => void): Promise<void>;
  // Prévient quand l'état ou le journal change ; renvoie la fonction de désabonnement.
  subscribe(listener: () => void): () => void;
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
