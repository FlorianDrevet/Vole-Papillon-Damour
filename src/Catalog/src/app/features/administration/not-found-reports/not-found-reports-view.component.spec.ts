import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {of} from 'rxjs';
import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {
  CatalogClosedNotFoundReports,
  CatalogNotFoundQueue,
  CatalogNotFoundQueueTarget,
} from '../../../core/catalog.models';
import {NotFoundReportCloseDialogComponent} from './not-found-report-close-dialog.component';
import {NotFoundReportsViewComponent} from './not-found-reports-view.component';

describe('NotFoundReportsViewComponent', () => {
  let fixture: ComponentFixture<NotFoundReportsViewComponent>;
  let api: jasmine.SpyObj<CatalogAdminApiService>;

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
    quantityAvailable: 2,
    reportCount: 2,
    memberCount: 2,
    firstReportedAt: '2026-09-01T10:00:00Z',
    lastReportedAt: '2026-09-05T10:00:00Z',
    overdue: false,
    comments: [{text: 'Étagère du fond', location: 'Premises', reportedAt: '2026-09-05T10:00:00Z'}],
  };
  const queue: CatalogNotFoundQueue = {
    generatedAt: '2026-09-05T10:00:00Z',
    openTargetCount: 1,
    openReportCount: 2,
    overdueTargetCount: 0,
    page: 1,
    pageSize: 50,
    totalCount: 1,
    items: [target],
  };
  const closed: CatalogClosedNotFoundReports = {
    generatedAt: '2026-09-06T10:00:00Z',
    page: 1,
    pageSize: 50,
    totalCount: 1,
    items: [{
      closedAt: '2026-09-06T10:00:00Z',
      kind: 'edition',
      isbn13: target.isbn13,
      rareBookId: null,
      title: target.title,
      outcome: 'Withdrawn',
      reportCount: 2,
      withdrawnQuantity: 2,
      closedByName: 'Bénévole',
      note: 'Introuvable en rayon',
    }],
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', [
      'getNotFoundQueue', 'getClosedNotFoundReports', 'closeNotFound',
    ]);
    api.getNotFoundQueue.and.returnValue(of(queue));
    api.getClosedNotFoundReports.and.returnValue(of(closed));
    api.closeNotFound.and.returnValue(of({
      closedReportCount: 2,
      withdrawnQuantity: 2,
      quantityAvailable: 0,
      movementId: 'movement-1',
    }));

    await TestBed.configureTestingModule({
      declarations: [NotFoundReportsViewComponent, NotFoundReportCloseDialogComponent],
      imports: [FormsModule],
      providers: [{provide: CatalogAdminApiService, useValue: api}],
    }).compileComponents();
    fixture = TestBed.createComponent(NotFoundReportsViewComponent);
    fixture.componentRef.setInput('accessToken', 'access-token');
    fixture.componentRef.setInput('canManageEditions', true);
    fixture.componentRef.setInput('canManageRareBooks', false);
  });

  it('renders one row per target without any member identity', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const rows = root.querySelectorAll('[data-testid="not-found-report-row"]');
    const text = root.textContent as string;

    expect(rows.length).toBe(1);
    expect(text).toContain('2 signalements');
    expect(text).toContain('Étagère du fond');
    expect(text).not.toContain('member@example.test');
    expect(root.querySelector('[data-member-id]')).toBeNull();
  });

  it('renders singular report and member counts', async () => {
    api.getNotFoundQueue.and.returnValue(of({
      ...queue,
      openReportCount: 1,
      items: [{...target, reportCount: 1, memberCount: 1}],
    }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent as string;
    expect(text).toContain('1 signalement · 1 membre');
    expect(text).not.toContain('1 signalements · 1 membres');
  });

  it('closing a target reloads the queue and emits new counts', async () => {
    const countsChanged = jasmine.createSpy('countsChanged');
    fixture.componentInstance.countsChanged.subscribe(countsChanged);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    api.getNotFoundQueue.calls.reset();

    (fixture.nativeElement.querySelector('[data-testid="not-found-found"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const note = root.querySelector<HTMLTextAreaElement>('[name="closeNote"]')!;
    note.value = 'Présent sur le rayon';
    note.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    root.querySelector<HTMLButtonElement>('[data-testid="not-found-close-submit"]')!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.closeNotFound).toHaveBeenCalled();
    expect(api.getNotFoundQueue).toHaveBeenCalled();
    expect(countsChanged).toHaveBeenCalled();
  });

  it('closed tab renders outcomes', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('[data-testid="not-found-tab-closed"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Retiré du stock');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Introuvable en rayon');
  });
});
