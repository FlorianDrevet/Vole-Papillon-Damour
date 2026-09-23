import {signal, WritableSignal} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';
import {of} from 'rxjs';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../../core/catalog-member-api.service';
import {
  CatalogSelectionAvailability,
  CatalogSelectionItem,
  CatalogSelectionResponse,
  CatalogSelectionStatus,
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
      'setSelectionStatus', 'removeSelectionItem',
    ]);
    api.setSelectionStatus.and.returnValue(of(void 0));
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

  it('filters Prochaine visite to ToTake and ToRevisit', () => {
    snapshot.set(makeResponse([
      makeItem('one', '9780000000001', 'ToTake', 'Available'),
      makeItem('two', '9780000000002', 'ToRevisit', 'Announced'),
      makeItem('three', '9780000000003', 'Purchased', 'OutOfStock'),
      makeItem('four', '9780000000004', 'NotFound', 'Unavailable'),
    ]));
    const page = render();

    (page.nativeElement.querySelector('[data-testid="selection-filter-next"]') as HTMLButtonElement).click();
    page.detectChanges();

    expect(page.nativeElement.querySelectorAll('.selection-item').length).toBe(2);
    expect(page.nativeElement.textContent).toContain('Livre 9780000000001');
    expect(page.nativeElement.textContent).toContain('Livre 9780000000002');
    expect(page.nativeElement.textContent).not.toContain('Livre 9780000000003');
  });

  it('changes a status through the API and re-renders the selected state', async () => {
    const item = makeItem('selection-1', '9780000000001', 'ToTake', 'Available');
    snapshot.set(makeResponse([item]));
    selection.refresh.and.callFake(async () => {
      const updated = {...item, status: 'NotFound' as const};
      snapshot.set(makeResponse([updated]));
      return snapshot();
    });
    const page = render();

    (page.nativeElement.querySelector('[data-testid="status-not-found"]') as HTMLButtonElement).click();
    await page.whenStable();
    page.detectChanges();

    expect(api.setSelectionStatus).toHaveBeenCalledWith('member-token', 'selection-1', 'NotFound');
    expect(page.nativeElement.querySelector('[data-testid="status-not-found"]')?.getAttribute('aria-pressed')).toBe('true');
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
