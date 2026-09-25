import {ComponentFixture, TestBed} from '@angular/core/testing';
import {signal, WritableSignal} from '@angular/core';
import {RouterModule} from '@angular/router';
import type {AccountInfo} from '@azure/msal-browser';
import {Subject, of, throwError} from 'rxjs';
import {DesignSystemModule} from '@vpd/ui';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../../core/catalog-member-api.service';
import {
  CatalogPersonalRecommendation,
  CatalogRecommendationsResponse,
} from '../../../core/catalog.models';
import {BookCardComponent} from '../../../shared/book-card/book-card.component';
import {ForYouSectionComponent} from './for-you-section.component';

describe('ForYouSectionComponent', () => {
  let fixture: ComponentFixture<ForYouSectionComponent>;
  let auth: jasmine.SpyObj<CatalogAuthService>;
  let memberApi: jasmine.SpyObj<CatalogMemberApiService>;
  let accountState: WritableSignal<AccountInfo | null>;
  let authenticated: WritableSignal<boolean>;

  beforeEach(async () => {
    accountState = signal<AccountInfo | null>(null);
    authenticated = signal(false);
    auth = {
      account: accountState,
      isAuthenticated: authenticated,
      initialize: jasmine.createSpy('initialize'),
      tryGetApiAccessToken: jasmine.createSpy('tryGetApiAccessToken'),
    } as unknown as jasmine.SpyObj<CatalogAuthService>;
    auth.initialize.and.resolveTo();
    auth.tryGetApiAccessToken.and.resolveTo('member-token');

    memberApi = jasmine.createSpyObj<CatalogMemberApiService>(
      'CatalogMemberApiService',
      ['getRecommendations'],
    );
    memberApi.getRecommendations.and.returnValue(of({status: 'NoPurchases', items: []}));

    await TestBed.configureTestingModule({
      declarations: [ForYouSectionComponent, BookCardComponent],
      imports: [RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogMemberApiService, useValue: memberApi},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ForYouSectionComponent);
  });

  it('does not request or render personal recommendations for a visitor', async () => {
    fixture.detectChanges();
    await waitForRecommendations();
    fixture.detectChanges();

    expect(auth.tryGetApiAccessToken).not.toHaveBeenCalled();
    expect(memberApi.getRecommendations).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[data-zone="R4-section"]')).toBeNull();
  });

  it('shows four enabled recommendations and greets the member by first name', async () => {
    signIn();
    memberApi.getRecommendations.and.returnValue(of({
      status: 'Enabled',
      items: recommendations(),
    }));

    fixture.detectChanges();
    await waitForRecommendations();
    fixture.detectChanges();

    expect(auth.initialize).toHaveBeenCalled();
    expect(auth.isAuthenticated()).toBeTrue();
    expect(auth.tryGetApiAccessToken).toHaveBeenCalled();
    expect(memberApi.getRecommendations).toHaveBeenCalled();
    const section = fixture.nativeElement.querySelector('[data-zone="R4-section"]') as HTMLElement;
    expect(section).not.toBeNull();
    expect(section.querySelector('.for-you-eyebrow')?.textContent.trim()).toBe('Pour vous, Camille');
    expect(section.querySelector('h2')?.textContent.trim()).toBe("D'après vos derniers achats.");
    expect(section.querySelectorAll('[data-zone="R4-carte"]')).toHaveSize(4);
    expect(memberApi.getRecommendations).toHaveBeenCalledOnceWith('member-token', 4);
  });

  it('explains a following volume and a recommendation based on an earlier purchase', async () => {
    signIn();
    memberApi.getRecommendations.and.returnValue(of({
      status: 'Enabled',
      items: recommendations(),
    }));

    fixture.detectChanges();
    await waitForRecommendations();
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const reasons = Array.from(
      element.querySelectorAll<HTMLElement>('[data-zone="R4-raison"]'),
    ).map(reason => reason.textContent?.trim() ?? '');
    expect(reasons[0]).toBe("La suite de Les hommes qui n'aimaient pas les femmes");
    expect(reasons[1]).toBe('Parce que vous avez aimé Dune');
    expect(fixture.nativeElement.querySelector('[data-zone="R4-raison"] em')?.textContent)
      .toBe("Les hommes qui n'aimaient pas les femmes");
  });

  it('hides recommendations when the member has disabled them', async () => {
    signIn();
    memberApi.getRecommendations.and.returnValue(of({status: 'Disabled', items: recommendations()}));

    fixture.detectChanges();
    await waitForRecommendations();
    fixture.detectChanges();

    expect(memberApi.getRecommendations).toHaveBeenCalledOnceWith('member-token', 4);
    expect(fixture.nativeElement.querySelector('[data-zone="R4-section"]')).toBeNull();
  });

  it('hides recommendations when there are no associated purchases', async () => {
    signIn();
    memberApi.getRecommendations.and.returnValue(of({status: 'NoPurchases', items: []}));

    fixture.detectChanges();
    await waitForRecommendations();
    fixture.detectChanges();

    expect(memberApi.getRecommendations).toHaveBeenCalledOnceWith('member-token', 4);
    expect(fixture.nativeElement.querySelector('[data-zone="R4-section"]')).toBeNull();
  });

  it('requires at least three recommendations before showing the section', async () => {
    signIn();
    memberApi.getRecommendations.and.returnValue(of({
      status: 'Enabled',
      items: recommendations().slice(0, 2),
    }));

    fixture.detectChanges();
    await waitForRecommendations();
    fixture.detectChanges();

    expect(memberApi.getRecommendations).toHaveBeenCalledOnceWith('member-token', 4);
    expect(fixture.nativeElement.querySelector('[data-zone="R4-section"]')).toBeNull();
  });

  it('keeps the loading skeleton visible while recommendations are pending', async () => {
    signIn();
    const response = new Subject<CatalogRecommendationsResponse>();
    memberApi.getRecommendations.and.returnValue(response);

    fixture.detectChanges();
    await waitForRecommendations();
    fixture.detectChanges();

    expect(memberApi.getRecommendations).toHaveBeenCalledOnceWith('member-token', 4);
    const loading = fixture.nativeElement.querySelector('[data-zone="R8-chargement"]') as HTMLElement;
    expect(loading).not.toBeNull();
    expect(loading.getAttribute('aria-busy')).toBe('true');
    response.next({status: 'NoPurchases', items: []});
    response.complete();
  });

  it('hides the section when recommendation loading fails', async () => {
    signIn();
    memberApi.getRecommendations.and.returnValue(throwError(() => new Error('offline')));

    fixture.detectChanges();
    await waitForRecommendations();
    fixture.detectChanges();

    expect(memberApi.getRecommendations).toHaveBeenCalledOnceWith('member-token', 4);
    expect(fixture.nativeElement.querySelector('[data-zone="R4-section"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  it('links to the account preferences tab', async () => {
    signIn();
    memberApi.getRecommendations.and.returnValue(of({
      status: 'Enabled',
      items: recommendations(),
    }));

    fixture.detectChanges();
    await waitForRecommendations();
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('[data-zone="R4-pourquoi"]') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/compte?tab=preferences');
  });

  async function waitForRecommendations(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    await fixture.whenStable();
  }

  function signIn(): void {
    accountState.set({
      homeAccountId: 'member-id',
      environment: 'volepapillondamour.ciamlogin.com',
      tenantId: 'tenant-id',
      username: 'camille@example.test',
      localAccountId: 'local-member-id',
      name: 'Camille Dupont',
      idTokenClaims: {given_name: 'Camille'},
    });
    authenticated.set(true);
  }
});

function recommendations(): CatalogPersonalRecommendation[] {
  return [
    recommendation('9782070612758', 'NextTome', "Les hommes qui n'aimaient pas les femmes"),
    recommendation('9782266000001', 'Theme', 'Dune'),
    recommendation('9782266000002', 'SameAuthor', 'Bel-Ami'),
    recommendation('9782266000003', 'SameSeries', 'Les Trois Mousquetaires'),
  ];
}

function recommendation(
  isbn13: string,
  reason: CatalogPersonalRecommendation['reason'],
  seedTitle: string,
): CatalogPersonalRecommendation {
  return {
    isbn13,
    title: `Livre ${isbn13.slice(-1)}`,
    authors: 'Autrice',
    publisher: 'Éditeur',
    publicationYear: 2025,
    physicalFormat: 'Poche',
    language: 'fr',
    genre: 'Roman',
    workId: null,
    coverUrl: null,
    quantityAvailable: 1,
    quantityAnnounced: 0,
    nextFairAt: null,
    lastAvailableAt: '2026-09-10T10:00:00Z',
    firstSeenAt: '2026-09-01T10:00:00Z',
    updatedAt: '2026-09-10T10:00:00Z',
    isRare: false,
    reason,
    seedTitle,
  };
}
