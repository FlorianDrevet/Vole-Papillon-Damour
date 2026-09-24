import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {CatalogNotFoundQueueTarget} from '../../../core/catalog.models';
import {NotFoundReportCloseDialogComponent} from './not-found-report-close-dialog.component';

describe('NotFoundReportCloseDialogComponent', () => {
  let fixture: ComponentFixture<NotFoundReportCloseDialogComponent>;

  const target: CatalogNotFoundQueueTarget = {
    kind: 'edition',
    isbn13: '9782070408504',
    rareBookId: null,
    title: 'Le Petit Prince',
    authors: 'Antoine de Saint-Exupéry',
    publisher: 'Gallimard',
    publicationYear: 1999,
    coverUrl: null,
    genre: 'Jeunesse',
    quantityAvailable: 5,
    reportCount: 3,
    memberCount: 3,
    firstReportedAt: '2026-09-01T10:00:00Z',
    lastReportedAt: '2026-09-05T10:00:00Z',
    overdue: false,
    comments: [],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [NotFoundReportCloseDialogComponent],
      imports: [FormsModule],
    }).compileComponents();
    fixture = TestBed.createComponent(NotFoundReportCloseDialogComponent);
    fixture.componentInstance.target = target;
    fixture.componentInstance.action = 'withdrawal';
    fixture.detectChanges();
  });

  it('withdraw dialog computes the quantity to withdraw from found copies', () => {
    fixture.componentInstance.foundCopies = 2;
    fixture.detectChanges();

    expect(fixture.componentInstance.withdrawalQuantity()).toBe(3);
    expect(fixture.nativeElement.textContent).toContain('3 exemplaires seront retirés');
  });

  it('withdraw dialog switches to found when all copies are found', () => {
    fixture.componentInstance.foundCopies = target.quantityAvailable;
    fixture.detectChanges();

    expect(fixture.componentInstance.effectiveAction()).toBe('found');
    expect(fixture.nativeElement.textContent).toContain('Confirmer : retrouvé');
  });

  it('withdraw dialog requires a note', () => {
    const submit = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('[data-testid="not-found-close-submit"]')!;

    expect(submit.disabled).toBeTrue();

    fixture.componentInstance.note = 'Contrôle effectué en rayon';
    fixture.detectChanges();

    expect(submit.disabled).toBeFalse();
  });

  it('rare target shows Retirer la fiche rare without quantity', () => {
    fixture.componentInstance.target = {...target, kind: 'rare', isbn13: null, rareBookId: 'rare-1'};
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Retirer la fiche rare');
    expect(fixture.nativeElement.querySelector('[name="foundCopies"]')).toBeNull();
  });
});
