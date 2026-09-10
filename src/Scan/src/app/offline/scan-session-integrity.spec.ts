import {TestBed} from '@angular/core/testing';

import {ScanLocalStoreService} from './scan-local-store.service';
import {
  ScanSessionResponse,
} from './scan-offline.model';
import {ScanVerdictService} from './scan-verdict.service';
import {ScanWorkflowService} from './scan-workflow.service';

describe('scan session integrity', () => {
  let workflow: ScanWorkflowService;
  let store: ScanLocalStoreService;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [ScanLocalStoreService, ScanVerdictService, ScanWorkflowService],
    });
    workflow = TestBed.inject(ScanWorkflowService);
    store = TestBed.inject(ScanLocalStoreService);
    await clearState();
  });

  it('keeps the immutable client identity when the server reuses another session', async () => {
    const scan = await workflow.recordScan('9782070363735');
    const localSession = await workflow.getSession();
    const remoteSession = createRemoteSession('remote-session-1', true);

    await workflow.mergeRemoteSession(remoteSession);

    const merged = await workflow.getSession();
    const persisted = await store.getOutboxEntry(scan.entry.clientGestureId);

    expect(merged?.clientSessionId).toBe(localSession?.clientSessionId);
    expect(merged?.remoteSessionId).toBe('remote-session-1');
    expect(persisted?.clientSessionId).toBe(localSession?.clientSessionId);
    expect(persisted?.clientSessionId).not.toBe('remote-session-1');
  });

  it('does not create a local session while merging a remote response', async () => {
    await workflow.clearSession();

    await workflow.mergeRemoteSession(createRemoteSession('remote-session-2', false));

    expect(await workflow.getSession()).toBeNull();
  });

  it('derives session counters from the current client session outbox', async () => {
    const first = await workflow.recordScan('9782070363735');
    await workflow.decide(first.entry.clientGestureId, true);
    await workflow.recordScan('9780306406157');
    const session = await workflow.getSession();

    const counts = await workflow.getSessionCounts(session!.clientSessionId);

    expect(counts).toEqual({scannedCount: 2, keptCount: 1, rejectedCount: 0});
    expect(Object.prototype.hasOwnProperty.call(await store.getSession(), 'scannedCount')).toBeFalse();
  });

  function createRemoteSession(
    scanSessionId: string,
    reusedExistingSession: boolean,
  ): ScanSessionResponse {
    return {
      scanSessionId,
      volunteerId: 'volunteer-1',
      mode: 'AvailableNow',
      targetAssoEventsId: null,
      startedAt: '2026-09-03T08:00:00.000Z',
      lastScanAt: '2026-09-03T08:01:00.000Z',
      lastSyncAt: '2026-09-03T08:01:00.000Z',
      lateArrivals: false,
      endedAt: null,
      closeReason: null,
      status: 'InProgress',
      scannedCount: 0,
      keptCount: 0,
      rejectedCount: 0,
      reusedExistingSession,
    };
  }

  async function clearState(): Promise<void> {
    await store.clearAccountState();
    await store.clearSessionCloseRequests();
    for (const entry of await store.listOutboxEntries()) {
      await store.deleteOutboxEntry(entry.clientGestureId);
    }
  }
});
