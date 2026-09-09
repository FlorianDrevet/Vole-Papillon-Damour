import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';

import {ScanRecoveryComponent} from './scan-recovery.component';
import {ScanTelemetryService} from './scan-telemetry.service';
import {ScanOutboxEntry} from './scan-offline.model';
import {ScanWorkflowService} from './scan-workflow.service';

describe('ScanRecoveryComponent', () => {
  let fixture: ComponentFixture<ScanRecoveryComponent>;
  let component: ScanRecoveryComponent;
  let workflow: jasmine.SpyObj<ScanWorkflowService>;
  let telemetry: jasmine.SpyObj<ScanTelemetryService>;

  beforeEach(async () => {
    workflow = jasmine.createSpyObj<ScanWorkflowService>('ScanWorkflowService', [
      'listSetAsideOutboxEntries',
      'getCatalogBook',
      'decide',
      'deleteOutboxEntry',
      'retryOutboxEntry',
      'reattachOutboxEntry',
    ]);
    telemetry = jasmine.createSpyObj<ScanTelemetryService>('ScanTelemetryService', ['trackEvent']);
    workflow.listSetAsideOutboxEntries.and.resolveTo([
      createEntry('NeedsDecision', 'gesture-undecided'),
      createEntry('RejectedByServer', 'gesture-rejected', false),
    ]);
    workflow.getCatalogBook.and.resolveTo({
      isbn13: '9782070363735',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      workId: null,
      qtyAvailable: 0,
      qtyAnnounced: 0,
      salesCount: 0,
      isWanted: false,
      isRare: false,
      updatedAt: '2026-09-09T09:38:00.000Z',
    });

    await TestBed.configureTestingModule({
      declarations: [ScanRecoveryComponent],
      imports: [CommonModule],
      providers: [
        {provide: ScanWorkflowService, useValue: workflow},
        {provide: ScanTelemetryService, useValue: telemetry},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ScanRecoveryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await component.ngOnInit();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('renders the cause and action set for both recovery cases', () => {
    const entries = fixture.nativeElement.querySelectorAll('.recovery-entry');

    expect(entries.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('décision');
    expect(fixture.nativeElement.textContent).toContain('serveur a refusé');
    expect(fixture.nativeElement.querySelector('.retry-entry')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.abandon-entry')).not.toBeNull();
  });

  it('applies a decision and traces an explicit deletion without account identity', async () => {
    await component.keep(component.entries[0]);
    await component.delete(component.entries[0]);

    expect(workflow.decide).toHaveBeenCalledOnceWith('gesture-undecided', true);
    expect(workflow.deleteOutboxEntry).toHaveBeenCalledWith('gesture-undecided');
    expect(telemetry.trackEvent).toHaveBeenCalledWith('scan_gesture_deleted', {
      clientGestureId: 'gesture-undecided',
      isbn13: '9782070363735',
    });
    expect(JSON.stringify(telemetry.trackEvent.calls.mostRecent().args[1]))
      .not.toContain('volunteerId');
  });

  it('retries a server rejection and offers an explicit abandon path', async () => {
    const rejected = component.entries[1];

    await component.retry(rejected);
    await component.abandon(rejected);

    expect(workflow.retryOutboxEntry).toHaveBeenCalledOnceWith('gesture-rejected');
    expect(workflow.deleteOutboxEntry).toHaveBeenCalledWith('gesture-rejected');
  });

  function createEntry(
    status: ScanOutboxEntry['status'],
    clientGestureId: string,
    kept: boolean | null = null,
  ): ScanOutboxEntry {
    return {
      clientGestureId,
      clientSessionId: 'session-1',
      isbn13: '9782070363735',
      occurredAt: '2026-09-09T09:38:00.000Z',
      createdAt: '2026-09-09T09:38:00.100Z',
      status,
      kept,
      catalogApplied: false,
      verdict: 'FirstCopy',
      quantityAvailable: 0,
      quantityAnnounced: 0,
      salesCount: 0,
      isRare: false,
      attemptCount: 5,
      lastAttemptAt: '2026-09-09T09:42:00.000Z',
      lastError: status === 'RejectedByServer' ? 'Validation failed' : null,
      lastFailureKind: status === 'RejectedByServer' ? 'permanent' : null,
      setAsideReason: status === 'RejectedByServer' ? 'server-refused' : 'undecided',
    };
  }
});
