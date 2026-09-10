import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';

import {ScanDiagnosticComponent} from './scan-diagnostic.component';
import {ScanDiagnosticStoreState} from './scan-diagnostic-export';
import {ScanLocalStoreService} from './scan-local-store.service';

describe('ScanDiagnosticComponent', () => {
  let fixture: ComponentFixture<ScanDiagnosticComponent>;
  let store: jasmine.SpyObj<ScanLocalStoreService>;

  beforeEach(async () => {
    store = jasmine.createSpyObj<ScanLocalStoreService>('ScanLocalStoreService', [
      'getDiagnosticState',
    ]);
    store.getDiagnosticState.and.resolveTo(createState());

    await TestBed.configureTestingModule({
      declarations: [ScanDiagnosticComponent],
      imports: [CommonModule],
      providers: [{provide: ScanLocalStoreService, useValue: store}],
    }).compileComponents();

    fixture = TestBed.createComponent(ScanDiagnosticComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('renders every outbox entry and highlights its real status', () => {
    const entries = fixture.nativeElement.querySelectorAll('.diagnostic-outbox-entry');

    expect(entries.length).toBe(2);
    expect(entries[0].getAttribute('data-status')).toBe('Orphaned');
    expect(entries[1].getAttribute('data-status')).toBe('Quarantined');
    expect(fixture.nativeElement.textContent).toContain('7');
  });

  it('shows a visible confirmation after copying the local journal', async () => {
    const writeText = jasmine.createSpy('writeText').and.resolveTo();
    const clipboard = Object.assign({}, navigator.clipboard, {writeText});
    spyOnProperty(navigator, 'clipboard', 'get').and.returnValue(clipboard);

    const button = fixture.nativeElement.querySelector('.copy-journal') as HTMLButtonElement;
    button.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(writeText).toHaveBeenCalledOnceWith(jasmine.stringContaining('Orphaned'));
    expect(fixture.nativeElement.querySelector('.copy-confirmation')?.textContent)
      .toContain('copié');
  });

  function createState(): ScanDiagnosticStoreState {
    return {
      catalogCount: 7,
      recentCatalog: [],
      session: null,
      closeRequests: [],
      outbox: [
        createOutboxEntry('Orphaned', 'gesture-1'),
        createOutboxEntry('Quarantined', 'gesture-2'),
      ],
      sales: [],
    };
  }

  function createOutboxEntry(
    status: 'Orphaned' | 'Quarantined',
    clientGestureId: string,
  ) {
    return {
      clientGestureId,
      clientSessionId: 'session-1',
      isbn13: '9782070363735',
      occurredAt: '2026-09-09T09:38:00.000Z',
      createdAt: '2026-09-09T09:38:00.100Z',
      status,
      kept: null,
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
