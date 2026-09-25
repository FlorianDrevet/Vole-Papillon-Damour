import {ComponentFixture, TestBed} from '@angular/core/testing';
import {signal, WritableSignal} from '@angular/core';
import {RouterModule} from '@angular/router';
import {of, throwError} from 'rxjs';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../../core/catalog-member-api.service';
import {CatalogPersonalRecommendation} from '../../../core/catalog.models';
import {RecommendationsBandComponent} from './recommendations-band.component';

describe('RecommendationsBandComponent', () => {
  let fixture: ComponentFixture<RecommendationsBandComponent>;
  let authenticated: WritableSignal<boolean>;
  let auth: {isAuthenticated: WritableSignal<boolean>; getApiAccessToken: jasmine.Spy};
  let api: jasmine.SpyObj<CatalogMemberApiService>;

  beforeEach(async () => {
    authenticated = signal(true);
    auth = {
      isAuthenticated: authenticated,
      getApiAccessToken: jasmine.createSpy('getApiAccessToken').and.resolveTo('member-token'),
    };
    api = jasmine.createSpyObj<CatalogMemberApiService>(
      'CatalogMemberApiService',
      ['getRecommendations'],
    );
    api.getRecommendations.and.returnValue(of({status: 'Disabled', items: []}));

    await TestBed.configureTestingModule({
      declarations: [RecommendationsBandComponent],
      imports: [RouterModule.forRoot([])],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogMemberApiService, useValue: api},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RecommendationsBandComponent);
  });

  it('does not request personal recommendations for a visitor', async () => {
    authenticated.set(false);
    render();
    await settle();

    expect(api.getRecommendations).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('[data-zone="R6-bandeau"]')).toBeNull();
  });

  it('shows four compact cards and a link to recommendation preferences', async () => {
    api.getRecommendations.and.returnValue(of({
      status: 'Enabled',
      items: recommendations(),
    }));
    render();
    await settle();

    const section = fixture.nativeElement.querySelector('[data-zone="R6-bandeau"]') as HTMLElement;
    expect(section).not.toBeNull();
    expect(section.querySelector('h2')?.textContent?.trim())
      .toBe('Des livres proches de ceux que vous avez achetés.');
    expect(section.querySelectorAll('[data-zone="R6-carte"]')).toHaveSize(4);
    expect(section.querySelector('[data-zone="R6-liste"]')).not.toBeNull();
    expect(section.querySelector('a[href="/compte?tab=preferences"]')?.textContent)
      .toContain('Gérer ces suggestions');
    expect(api.getRecommendations).toHaveBeenCalledOnceWith('member-token', 4);
  });

  it('shows the exact invitation and links to the member card when there are no purchases', async () => {
    api.getRecommendations.and.returnValue(of({status: 'NoPurchases', items: []}));
    render();
    await settle();

    const invitation = fixture.nativeElement.querySelector('[data-zone="R8-sans-achat"]') as HTMLElement;
    expect(invitation).not.toBeNull();
    expect(invitation.textContent).toContain('Présentez votre carte de compte à la caisse.');
    expect(invitation.textContent).toContain(
      'Vos prochains achats associés nous permettront de vous suggérer des livres proches. Les ventes anonymes ne sont jamais utilisées.',
    );
    expect(invitation.querySelector('a')?.getAttribute('href')).toBe('/compte?tab=card');
    expect(invitation.querySelector('a')?.textContent).toContain('Afficher ma carte');
  });

  it('hides the band when personal recommendations are disabled', async () => {
    api.getRecommendations.and.returnValue(of({status: 'Disabled', items: recommendations()}));
    render();
    await settle();

    expect(fixture.nativeElement.querySelector('[data-zone="R6-bandeau"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-zone="R8-sans-achat"]')).toBeNull();
  });

  it('hides the band when the recommendations request fails', async () => {
    api.getRecommendations.and.returnValue(throwError(() => new Error('offline')));
    render();
    await settle();

    expect(fixture.nativeElement.querySelector('[data-zone="R6-bandeau"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-zone="R8-sans-achat"]')).toBeNull();
  });

  function render(): void {
    fixture.detectChanges();
  }

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    await fixture.whenStable();
    fixture.detectChanges();
  }
});

function recommendations(): CatalogPersonalRecommendation[] {
  return [
    recommendation('9782266000001', 'Theme', 'Dune', 2),
    recommendation('9782266000002', 'NextTome', 'La Horde du Contrevent', 0, 1),
    recommendation('9782266000003', 'SameAuthor', 'Une vie', 1),
    recommendation('9782266000004', 'SameSeries', 'Les hommes qui n’aimaient pas les femmes', 2),
  ];
}

function recommendation(
  isbn13: string,
  reason: CatalogPersonalRecommendation['reason'],
  seedTitle: string,
  quantityAvailable: number,
  quantityAnnounced = 0,
): CatalogPersonalRecommendation {
  return {
    isbn13,
    title: `Livre ${isbn13.slice(-1)}`,
    authors: 'Autrice Exemple',
    publisher: 'Éditeur Exemple',
    publicationYear: 2025,
    physicalFormat: 'Poche',
    language: 'fr',
    genre: 'Roman',
    workId: null,
    coverUrl: null,
    quantityAvailable,
    quantityAnnounced,
    nextFairAt: quantityAnnounced ? '2026-10-10T00:00:00Z' : null,
    lastAvailableAt: '2026-09-20T10:00:00Z',
    firstSeenAt: '2026-09-19T10:00:00Z',
    updatedAt: '2026-09-20T10:00:00Z',
    isRare: false,
    reason,
    seedTitle,
  };
}
