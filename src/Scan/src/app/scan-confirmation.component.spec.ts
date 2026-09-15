import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';

import {ScanConfirmationComponent} from './scan-confirmation.component';
import {ScanConfirmationService} from './scan-confirmation.service';

describe('ScanConfirmationComponent', () => {
  let fixture: ComponentFixture<ScanConfirmationComponent>;
  let confirmation: ScanConfirmationService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ScanConfirmationComponent],
      imports: [CommonModule],
      providers: [ScanConfirmationService],
    }).compileComponents();

    fixture = TestBed.createComponent(ScanConfirmationComponent);
    confirmation = TestBed.inject(ScanConfirmationService);
    fixture.detectChanges();
  });

  it('keeps the copy and touch actions in a dedicated dialog content region', async () => {
    const request = confirmation.confirm({
      title: 'Terminer cette session de tri ?',
      message: 'La clôture sera synchronisée avec le serveur.',
      confirmLabel: 'Terminer la session',
      cancelLabel: 'Poursuivre le tri',
    });
    fixture.detectChanges();

    const dialog = fixture.nativeElement.querySelector('[role="alertdialog"]') as HTMLElement;
    const content = dialog.querySelector('.scan-confirmation-content') as HTMLElement;
    const actions = dialog.querySelector('.scan-confirmation-actions') as HTMLElement;

    expect(content).not.toBeNull();
    expect(content.querySelector('h2')?.textContent).toContain('Terminer cette session');
    expect(content.querySelector('p')?.textContent).toContain('synchronisée');
    expect(actions.querySelectorAll('button')).toHaveSize(2);
    expect(actions.querySelector('.scan-confirmation-confirm')?.textContent).toContain('Terminer la session');

    confirmation.resolve(false);
    await request;
  });
});
