import {signal, WritableSignal} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';
import {of, throwError} from 'rxjs';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../../core/catalog-member-api.service';
import {
  CatalogSelectionAvailability,
  CatalogSelectionItem,
  CatalogSelectionResponse,
  CatalogSelectionStatus,
  CatalogNotFoundReportSummary,
} from '../../../core/catalog.models';
import {CatalogSelectionService, CatalogSelectionMode} from '../../../core/selection/catalog-selection.service';
import {LocalSelectionStore} from '../../../core/selection/local-selection.store';
import {AccountSelectionComponent} from './account-selection.component';

describe('AccountSelectionComponent', () => {
  let fixture: ComponentFixture<AccountSelectionComponent>;
  let auth: {isAuthenticated: WritableSignal<boolean>; getApiAccessToken: jasmine.Spy; login: jasmine.Spy};
  let api: jasmine.SpyObj<CatalogMemberApiService>;
  let selection: jasmine.SpyObj<CatalogSelectionService>;
  let snapshot: WritableSignal<CatalogSelectionResponse | null>;
  let mode: WritableSignal<CatalogSelectionMode>;
  let pendingMerge: WritableSignal<{localCount: number; accountCount: number; mergedCount: number} | null>;
  let local: LocalSelectionStore;

  const makeItem = (
    id: string,
    isbn13: string,
    status: CatalogSelectionStatus,
    availability: CatalogSelectionAvailability,
    notFoundReport: CatalogNotFoundReportSummary | null = null,
  ): CatalogSelectionItem => ({
    id,
    kind: 'edition',
    isbn13,
    rareBookId: null,
    rareBookSlug: null,
    title: `Livre ${isbn13}`,
    authors: 'Une autrice',
    publisher: 'Un éditeur',
    publicationYear: 2020,
    physicalFormat: 'Poche',
    coverUrl: null,
    availability,
    availabilityCheckedAt: '2026-09-23T10:00:00Z',
    status,
    notFoundReport,
    addedAt: '2026-09-20T10:00:00Z',
    purchasedAt: status === 'Purchased' ? '2026-09-21T10:00:00Z' : null,
  });

  const makeResponse = (items: CatalogSelectionItem[]): CatalogSelectionResponse => ({
    generatedAt: '2026-09-23T10:00:00Z',
    nextFair: {id: 'fair-1', startsAt: '2026-10-10T08:00:00Z'},
    items,
  });

  beforeEach(async () => {
    localStorage.removeItem('vpd.catalog.selection.v1');
    auth = {
      isAuthenticated: signal(true),
      getApiAccessToken: jasmine.createSpy('getApiAccessToken').and.resolveTo('member-token'),
      login: jasmine.createSpy('login').and.resolveTo(),
    };
    snapshot = signal<CatalogSelectionResponse | null>(makeResponse([]));
    mode = signal<CatalogSelectionMode>('synced');
    pendingMerge = signal<{localCount: number; accountCount: number; mergedCount: number} | null>(null);
    api = jasmine.createSpyObj<CatalogMemberApiService>('CatalogMemberApiService', [
      'reportNotFound', 'cancelNotFoundReport', 'removeSelectionItem',
    ]);
    api.reportNotFound.and.returnValue(of({
      reportId: 'report-1', reportedAt: '2026-09-24T10:00:00Z', alreadyOpen: false,
    }));
    api.cancelNotFoundReport.and.returnValue(of(void 0));
    api.removeSelectionItem.and.returnValue(of(void 0));
    selection = jasmine.createSpyObj<CatalogSelectionService>(
      'CatalogSelectionService',
      ['onSignedIn', 'refresh', 'confirmMerge', 'declineMerge', 'remove'],
      {snapshot, mode, pendingMerge, keys: signal<ReadonlySet<string>>(new Set())},
    );
    selection.onSignedIn.and.resolveTo();
    selection.refresh.and.callFake(async () => snapshot());
    selection.confirmMerge.and.resolveTo({added: 0, alreadyPresent: 0, rejected: []});
    selection.declineMerge.and.callFake(() => pendingMerge.set(null));
    selection.remove.and.resolveTo();

    await TestBed.configureTestingModule({
      declarations: [AccountSelectionComponent],
      imports: [RouterModule.forRoot([])],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogMemberApiService, useValue: api},
        {provide: CatalogSelectionService, useValue: selection},
      ],
    }).compileComponents();
    local = TestBed.inject(LocalSelectionStore);
  });

  function render(): ComponentFixture<AccountSelectionComponent> {
    fixture = TestBed.createComponent(AccountSelectionComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('shows the exact empty state and a route back to the catalogue', () => {
    const page = render();

    expect(page.nativeElement.textContent).toContain("Rien dans Ma sélection pour l'instant.");
    expect(page.nativeElement.textContent).toContain('Ajoutez un livre depuis le catalogue pour le retrouver à la bourse.');
    expect(page.nativeElement.querySelector('a[href="/recherche"]')).not.toBeNull();
  });

  it('does not render any personal status control', () => {
    snapshot.set(makeResponse([makeItem('one', '9780000000001', 'ToTake', 'Available')]));
    const page = render();

    expect(page.nativeElement.querySelector('.selection-status-control')).toBeNull();
    expect(page.nativeElement.querySelector('.selection-status-button')).toBeNull();
  });

  it('shows the report button only on available non-purchased lines', () => {
    const foundReport: CatalogNotFoundReportSummary = {
      id: 'found-report', status: 'Found', reportedAt: '2026-09-22T10:00:00Z', closedAt: '2026-09-23T10:00:00Z',
    };
    snapshot.set(makeResponse([
      makeItem('available', '9780000000001', 'ToTake', 'Available'),
      makeItem('purchased', '9780000000002', 'Purchased', 'Available'),
      makeItem('found', '9780000000003', 'ToTake', 'Available', foundReport),
    ]));
    const page = render();

    expect(page.nativeElement.querySelector('[data-testid="not-found-report-available"]')).not.toBeNull();
    expect(page.nativeElement.querySelector('[data-testid="not-found-report-purchased"]')).toBeNull();
    expect(page.nativeElement.querySelector('[data-testid="not-found-report-found"]')).not.toBeNull();
  });

  it('does not offer a report for an authenticated local entry that is not synchronized', () => {
    local.add({
      ref: {kind: 'edition', isbn13: '9782070612758'},
      title: 'Le Petit Prince',
      addedAt: '2026-09-20T10:00:00.000Z',
    });
    const page = render();

    expect(page.nativeElement.querySelector('[data-testid="not-found-report-edition:9782070612758"]')).toBeNull();
  });

  it('hides the report button on announced, out-of-stock, rare-sold and purchased lines', () => {
    snapshot.set(makeResponse([
      makeItem('announced', '9780000000001', 'ToTake', 'Announced'),
      makeItem('out-of-stock', '9780000000002', 'ToTake', 'OutOfStock'),
      {...makeItem('rare-sold', '9780000000003', 'ToTake', 'RareSold'), kind: 'rare', isbn13: null, rareBookId: 'rare-sold'},
      makeItem('purchased', '9780000000004', 'Purchased', 'Available'),
    ]));
    const page = render();

    for (const id of ['announced', 'out-of-stock', 'rare-sold', 'purchased']) {
      expect(page.nativeElement.querySelector(`[data-testid="not-found-report-${id}"]`)).toBeNull();
    }
  });

  it('shows the open report notice with a cancel action', () => {
    snapshot.set(makeResponse([makeItem('reported', '9780000000001', 'ToTake', 'Available', {
      id: 'report-1', status: 'Open', reportedAt: '2026-09-24T10:00:00Z', closedAt: null,
    })]));
    const page = render();

    expect(page.nativeElement.textContent).toContain('Signalé introuvable le');
    expect(page.nativeElement.textContent).toContain('Un bénévole va vérifier en rayon. Merci pour votre aide.');
    expect(page.nativeElement.querySelector('[data-testid="cancel-not-found-report-reported"]')).not.toBeNull();
  });

  it('cancelling a report calls the api and refreshes', async () => {
    snapshot.set(makeResponse([makeItem('reported', '9780000000001', 'ToTake', 'Available', {
      id: 'report-1', status: 'Open', reportedAt: '2026-09-24T10:00:00Z', closedAt: null,
    })]));
    const page = render();

    (page.nativeElement.querySelector('[data-testid="cancel-not-found-report-reported"]') as HTMLButtonElement).click();
    await page.whenStable();

    expect(api.cancelNotFoundReport).toHaveBeenCalledWith('member-token', 'report-1');
    expect(selection.refresh).toHaveBeenCalled();
  });

  it('shows the found message and the button again when a report was found', () => {
    snapshot.set(makeResponse([makeItem('found', '9780000000001', 'ToTake', 'Available', {
      id: 'report-1', status: 'Found', reportedAt: '2026-09-20T10:00:00Z', closedAt: '2026-09-23T10:00:00Z',
    })]));
    const page = render();

    expect(page.nativeElement.textContent).toContain('Un bénévole l’a retrouvé en rayon le');
    expect(page.nativeElement.querySelector('[data-testid="not-found-report-found"]')).not.toBeNull();
  });

  it('shows the withdrawn thank-you message', () => {
    snapshot.set(makeResponse([makeItem('withdrawn', '9780000000001', 'ToTake', 'OutOfStock', {
      id: 'report-1', status: 'Withdrawn', reportedAt: '2026-09-20T10:00:00Z', closedAt: '2026-09-23T10:00:00Z',
    })]));
    const page = render();

    expect(page.nativeElement.textContent).toContain('Retiré du catalogue après votre signalement. Merci !');
  });

  it('filters reported lines with the Signalés filter', () => {
    snapshot.set(makeResponse([
      makeItem('reported', '9780000000001', 'ToTake', 'Available', {
        id: 'report-1', status: 'Open', reportedAt: '2026-09-24T10:00:00Z', closedAt: null,
      }),
      makeItem('not-reported', '9780000000002', 'ToTake', 'Available'),
    ]));
    const page = render();

    (page.nativeElement.querySelector('[data-testid="selection-filter-reported"]') as HTMLButtonElement).click();
    page.detectChanges();

    expect(page.nativeElement.querySelectorAll('.selection-item').length).toBe(1);
    expect(page.nativeElement.textContent).toContain('Livre 9780000000001');
    expect(page.nativeElement.textContent).not.toContain('Livre 9780000000002');
  });

  it('no longer offers Prochaine visite or Pas trouvé filters', () => {
    snapshot.set(makeResponse([makeItem('one', '9780000000001', 'ToTake', 'Available')]));
    const page = render();

    const filters = Array.from(
      page.nativeElement.querySelectorAll('.selection-filter-button') as NodeListOf<HTMLButtonElement>,
    ).map(button => button.textContent);
    expect(filters.join(' ')).not.toMatch(/Prochaine visite|Pas trouvé/);
  });

  it('maps a 429 to the daily limit message', async () => {
    snapshot.set(makeResponse([makeItem('one', '9780000000001', 'ToTake', 'Available')]));
    api.reportNotFound.and.returnValue(throwError(() => new HttpErrorResponse({status: 429})));
    const page = render();

    const submitNotFoundReport = Reflect.get(page.componentInstance, 'submitNotFoundReport');
    expect(typeof submitNotFoundReport).toBe('function');
    if (typeof submitNotFoundReport !== 'function') {
      return;
    }

    await submitNotFoundReport.call(page.componentInstance, page.componentInstance.visibleItems()[0], {
      location: null,
      comment: null,
    });
    page.detectChanges();

    expect(page.nativeElement.textContent).toContain("Vous avez déjà beaucoup signalé aujourd'hui, merci !");
  });

  it('asks before merging and keeps local items when the merge is declined', () => {
    auth.isAuthenticated.set(true);
    mode.set('local');
    local.add({ref: {kind: 'edition', isbn13: '9780000000001'}, title: 'Livre local 1', addedAt: '2026-09-20T10:00:00Z'});
    local.add({ref: {kind: 'edition', isbn13: '9780000000002'}, title: 'Livre local 2', addedAt: '2026-09-20T10:00:00Z'});
    snapshot.set(makeResponse([makeItem('remote-1', '9780000000003', 'ToTake', 'Available')]));
    pendingMerge.set({localCount: 2, accountCount: 1, mergedCount: 3});
    const page = render();

    const dialog = page.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    expect(dialog.textContent).toContain('Après fusion : 3 livres, sans doublon.');
    (dialog.querySelector('[data-testid="selection-merge-decline"]') as HTMLButtonElement).click();
    page.detectChanges();

    expect(selection.declineMerge).toHaveBeenCalled();
    expect(local.list().length).toBe(2);
    expect(page.nativeElement.querySelector('[role="dialog"]')).toBeNull();
  });

  it('renders the freshness line with the generation date', () => {
    snapshot.set(makeResponse([makeItem('selection-1', '9780000000001', 'ToTake', 'Available')]));
    const page = render();

    expect(page.nativeElement.textContent).toContain('Disponibilité vérifiée le 23 septembre 2026');
    expect(page.nativeElement.textContent).toContain('Elle peut changer avant votre visite.');
  });

  it('shows loading when the account page is synchronizing the selection', () => {
    const page = render();
    page.componentRef.setInput('selectionLoading', true);
    page.detectChanges();

    expect(page.nativeElement.textContent).toContain('Chargement de Ma sélection…');
  });

  it('asks the account page to retry a failed synchronization', () => {
    const page = render();
    page.componentRef.setInput('selectionError', 'Ma sélection n’a pas pu être synchronisée.');
    page.detectChanges();
    const retryRequested = spyOn(page.componentInstance.retryRequested, 'emit');

    (page.nativeElement.querySelector('.selection-feedback button') as HTMLButtonElement).click();

    expect(retryRequested).toHaveBeenCalled();
  });
});
