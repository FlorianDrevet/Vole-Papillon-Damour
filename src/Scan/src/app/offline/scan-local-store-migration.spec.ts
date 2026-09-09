import {migrateLegacyScanOutboxEntry} from './scan-local-store-migration';

describe('scan IndexedDB migration', () => {
  it('preserves every legacy entry while assigning an actionable v3 status and reason', () => {
    const entries = [
      createLegacyEntry('gesture-undecided', 'Orphaned', null),
      createLegacyEntry('gesture-decided', 'Orphaned', true),
      createLegacyEntry('gesture-rejected', 'Quarantined', false),
      createLegacyEntry('gesture-kept', 'Kept', true),
    ];

    const migrated = entries.map(migrateLegacyScanOutboxEntry);

    expect(migrated).toHaveSize(entries.length);
    expect(migrated.map(entry => [entry.clientGestureId, entry.status, entry.setAsideReason]))
      .toEqual([
        ['gesture-undecided', 'NeedsDecision', 'undecided'],
        ['gesture-decided', 'NeedsReattach', 'no-session'],
        ['gesture-rejected', 'RejectedByServer', 'server-refused'],
        ['gesture-kept', 'Kept', null],
      ]);
  });

  function createLegacyEntry(
    clientGestureId: string,
    status: 'Orphaned' | 'Quarantined' | 'Kept',
    kept: boolean | null,
  ) {
    return {
      clientGestureId,
      scanSessionId: 'session-1',
      isbn13: '9782070363735',
      occurredAt: '2026-09-09T09:38:00.000Z',
      createdAt: '2026-09-09T09:38:00.100Z',
      status,
      kept,
      catalogApplied: false,
      verdict: 'FirstCopy' as const,
      quantityAvailable: 0,
      quantityAnnounced: 0,
      salesCount: 0,
      isRare: false,
      attemptCount: 0,
      lastAttemptAt: null,
      lastError: null,
      lastFailureKind: null,
    };
  }
});
