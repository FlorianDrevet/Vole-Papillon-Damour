import {ScanOutboxStatus} from './scan-offline.model';
import {recoveryActionsFor} from './scan-recovery.model';

describe('scan recovery actions', () => {
  it('has a user-visible exit for every outbox status', () => {
    const statuses: ScanOutboxStatus[] = [
      'Pending',
      'Kept',
      'Rejected',
      'CancelledLocal',
      'NeedsDecision',
      'NeedsReattach',
      'RejectedByServer',
    ];

    for (const status of statuses) {
      expect(recoveryActionsFor(status).length).withContext(status).toBeGreaterThan(0);
    }
  });
});
