import {ComponentFixture, TestBed} from '@angular/core/testing';
import {signal, WritableSignal} from '@angular/core';
import {RouterModule} from '@angular/router';
import {of} from 'rxjs';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../../core/catalog-member-api.service';
import {CatalogPurchasesResponse, CatalogPurchasePassage} from '../../../core/catalog.models';
import {CatalogCoverDirective} from '../../../shared/catalog-cover.directive';
import {RecommendationsBandComponent} from '../recommendations-band/recommendations-band.component';
import {AccountPurchasesComponent} from './account-purchases.component';

describe('AccountPurchasesComponent', () => {
  let fixture: ComponentFixture<AccountPurchasesComponent>;
  let api: jasmine.SpyObj<CatalogMemberApiService>;
  let auth: {getApiAccessToken: jasmine.Spy; isAuthenticated: WritableSignal<boolean>};

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
    auth = {
      getApiAccessToken: jasmine.createSpy('getApiAccessToken').and.resolveTo('member-token'),
      isAuthenticated: signal(false),
    };
    api = jasmine.createSpyObj<CatalogMemberApiService>('CatalogMemberApiService', ['getPurchases', 'getRecommendations']);
    api.getPurchases.and.returnValue(of(response([passage()])));
    api.getRecommendations.and.returnValue(of({status: 'NoPurchases', items: []}));

    await TestBed.configureTestingModule({
      declarations: [AccountPurchasesComponent, CatalogCoverDirective, RecommendationsBandComponent],
      imports: [RouterModule.forRoot([])],
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

  it('loads purchase covers through the optimized catalog cover URL', async () => {
    api.getPurchases.and.returnValue(of(response([passage({
      lines: passage().lines.map((line, index) => ({
        ...line,
        currentCoverUrl: index === 0
          ? 'https://openapi.bnf.fr/couverture/image/image/recupererImage?ISBN=9782070612758&couverture=1'
          : null,
      })),
    })])));

    const page = render();
    await settle(page);

    expect(page.nativeElement.querySelector('.purchase-cover')?.getAttribute('src')).toBe(
      'https://livres.volepapillondamour.fr/cdn-cgi/image/width=660,height=990,fit=scale-down,quality=80,format=auto/https://openapi.bnf.fr/couverture/image/image/recupererImage?ISBN=9782070612758&couverture=1&taille=originale&largeur=660&hauteur=990',
    );
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

  it('keeps the recommendation card and omits the second empty purchase card', async () => {
    auth.isAuthenticated.set(true);
    api.getPurchases.and.returnValue(of(response([])));
    const page = render();
    await settle(page);

    expect(page.nativeElement.querySelector('.recommendations-band-empty')).not.toBeNull();
    expect(page.nativeElement.textContent).toContain('Pour prolonger vos lectures');
    expect(page.nativeElement.querySelector('.purchases-empty')).toBeNull();
    expect(page.nativeElement.textContent).not.toContain("Aucun achat n'est encore associé à ce compte.");
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
