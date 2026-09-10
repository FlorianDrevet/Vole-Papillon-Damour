import {ScanOutboxStatus} from './scan-offline.model';

export type ScanRecoveryAction = 'decide' | 'reattach' | 'transmit' | 'delete' | 'retry';

export function recoveryActionsFor(status: ScanOutboxStatus): readonly ScanRecoveryAction[] {
  switch (status) {
    case 'Pending':
      return ['decide'];
    case 'Kept':
    case 'Rejected':
      return ['transmit'];
    case 'CancelledLocal':
      return ['delete'];
    case 'NeedsDecision':
      return ['decide', 'delete'];
    case 'NeedsReattach':
      return ['reattach'];
    case 'RejectedByServer':
      return ['retry', 'delete'];
  }
}
