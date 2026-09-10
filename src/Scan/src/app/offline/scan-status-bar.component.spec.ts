import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';

import {ScanStatusBarComponent} from './scan-status-bar.component';
import {ScanStatusService} from './scan-status.service';

describe('ScanStatusBarComponent', () => {
  let fixture: ComponentFixture<ScanStatusBarComponent>;
  let status: ScanStatusService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ScanStatusBarComponent],
      imports: [CommonModule],
      providers: [ScanStatusService],
    }).compileComponents();

    status = TestBed.inject(ScanStatusService);
    status.updateFromLocalState(
      {
        key: 'catalog-sync',
        watermark: 'watermark',
        updatedAt: '2026-09-09T09:00:00.000Z',
        nextFair: null,
      },
      {pendingDecisionCount: 1, pendingTransmissionCount: 2},
      {orphaned: 1, quarantined: 0},
      new Date('2026-09-09T10:00:00.000Z'),
    );

    fixture = TestBed.createComponent(ScanStatusBarComponent);
    fixture.detectChanges();
  });

  it('renders each active axis in its own message', () => {
    expect(fixture.nativeElement.querySelector('.status-catalog')?.textContent)
      .toContain('Catalogue à jour');
    expect(fixture.nativeElement.querySelector('.status-outbox')?.textContent)
      .toContain('2 livres en attente d’envoi');
    expect(fixture.nativeElement.querySelector('.status-decision')?.textContent)
      .toContain('1 livre attend une décision');
    expect(fixture.nativeElement.querySelector('.status-set-aside')?.textContent)
      .toContain('1 livre attend une reprise');
    expect(fixture.nativeElement.querySelectorAll('.status-message').length).toBe(4);
  });
});
