import {ComponentFixture, TestBed} from '@angular/core/testing';
import {of} from 'rxjs';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../../core/catalog-member-api.service';
import {CatalogPurchasesResponse, CatalogPurchasePassage} from '../../../core/catalog.models';
import {AccountPurchasesComponent} from './account-purchases.component';

describe('AccountPurchasesComponent', () => {
  let fixture: ComponentFixture<AccountPurchasesComponent>;
  let api: jasmine.SpyObj<CatalogMemberApiService>;
  let auth: {getApiAccessToken: jasmine.Spy};

  const passage = (overrides: Partial<CatalogPurchasePassage> = {}): CatalogPurchasePassage => ({
    id: 'passage-1',
    reference: 'A3D29D66',
    occurredAt: '2026-03-14T10:00:00Z',
    fairId: 'fair-1',
    fairLabel: 'Bourse aux livres · 14 mars 2026',
    activeBookCount: 2,
    lines: [
      {
        id: 'line-1',
        kind: 'edition',
        isbn13: '9782070612758',
        rareBookId: null,
        title: 'Le Petit Prince',
        authors: 'Antoine de Saint-Exupéry',
        publisher: 'Gallimard',
        publicationYear: 1946,
        physicalFormat: 'Poche',
        quantity: 1,
        state: 'Associated',
        currentCoverUrl: null,
      },
      {
        id: 'line-2',
        kind: 'edition',
        isbn13: '9782203001015',
        rareBookId: null,
        title: 'La Peste',
        authors: 'Albert Camus',
        publisher: 'Gallimard',
        publicationYear: 1947,
        physicalFormat: 'Broché',
        quantity: 1,
        state: 'Associated',
        currentCoverUrl: null,
      },
    ],
    ...overrides,
  });

  const response = (passages: CatalogPurchasePassage[], nextCursor: string | null = null): CatalogPurchasesResponse => ({
    passages,
    nextCursor,
  });

  beforeEach(async () => {
    auth = {getApiAccessToken: jasmine.createSpy('getApiAccessToken').and.resolveTo('member-token')};
    api = jasmine.createSpyObj<CatalogMemberApiService>('CatalogMemberApiService', ['getPurchases']);
    api.getPurchases.and.returnValue(of(response([passage()])));

    await TestBed.configureTestingModule({
      declarations: [AccountPurchasesComponent],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogMemberApiService, useValue: api},
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<AccountPurchasesComponent> {
    fixture = TestBed.createComponent(AccountPurchasesComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('groups lines by passage with date and count', async () => {
    const page = render();
    await settle(page);

    const text = page.nativeElement.textContent as string;
    expect(text).toContain('14 mars 2026 · Bourse aux livres');
    expect(text).toContain('2 livres');
    expect(text).toContain('Le Petit Prince · Gallimard · 1946');
    expect(text).toContain('Antoine de Saint-Exupéry');
    expect(text).toContain('Poche');
    expect(text).toContain('9782070612758');
  });

  it('shows cancelled lines with the "Annulée" badge', async () => {
    api.getPurchases.and.returnValue(of(response([passage({
      activeBookCount: 0,
      lines: passage().lines.map(line => ({...line, state: 'Cancelled'})),
    })])));
    const page = render();
    await settle(page);

    expect(page.nativeElement.textContent).toContain('Annulée');
    expect(page.nativeElement.querySelector('.purchase-line.cancelled')).not.toBeNull();
  });

  it('loads the next page only on demand', async () => {
    api.getPurchases.and.returnValues(
      of(response([passage()], 'cursor-2')),
      of(response([passage({id: 'passage-2', reference: 'B4E39A77'})])),
    );
    const page = render();
    await settle(page);

    expect(api.getPurchases).toHaveBeenCalledTimes(1);
    const button = page.nativeElement.querySelector('[data-testid="older-purchases"]') as HTMLButtonElement;
    button.click();
    await settle(page);

    expect(api.getPurchases).toHaveBeenCalledTimes(2);
    expect(api.getPurchases).toHaveBeenCalledWith('member-token', 'cursor-2');
    expect(page.nativeElement.textContent).toContain('B4E39A77');
  });

  it('always shows the anonymous-sales notice', async () => {
    api.getPurchases.and.returnValue(of(response([passage()])));
    const page = render();
    await settle(page);

    expect(page.nativeElement.textContent).toContain(
      'Seuls les passages associés à ce compte sont visibles ici. Les ventes anonymes ne peuvent pas être retrouvées automatiquement.',
    );
  });

  it('never renders a euro sign', async () => {
    const page = render();
    await settle(page);

    expect(page.nativeElement.textContent).not.toContain('€');
  });

  it('shows the empty state wording', async () => {
    api.getPurchases.and.returnValue(of(response([])));
    const page = render();
    await settle(page);

    expect(page.nativeElement.textContent).toContain("Aucun achat n'est encore associé à ce compte.");
    expect(page.nativeElement.textContent).toContain('Seuls les passages associés à ce compte sont visibles ici.');
  });

  it('reveals the passage reference and association contact for a reported error', async () => {
    const page = render();
    await settle(page);
    (page.nativeElement.querySelector('[data-testid="report-passage"]') as HTMLButtonElement).click();
    page.detectChanges();

    expect(page.nativeElement.textContent).toContain('Référence du passage : A3D29D66');
    expect(page.nativeElement.querySelector('a[href="mailto:volepapillondamour@sfr.fr"]')).not.toBeNull();
  });

  async function settle(page: ComponentFixture<AccountPurchasesComponent>): Promise<void> {
    await page.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    page.detectChanges();
  }
});
