import {ComponentFixture, TestBed} from '@angular/core/testing';
import {signal, WritableSignal} from '@angular/core';
import {ActivatedRoute, RouterModule} from '@angular/router';
import {Meta} from '@angular/platform-browser';
import type {AccountInfo} from '@azure/msal-browser';
import {of} from 'rxjs';

import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';
import {CatalogUnsubscribePageComponent} from './catalog-unsubscribe-page.component';

describe('CatalogUnsubscribePageComponent', () => {
  let fixture: ComponentFixture<CatalogUnsubscribePageComponent>;
  let auth: {
    account: WritableSignal<AccountInfo | null>;
    initialized: WritableSignal<boolean>;
    isAuthenticated: WritableSignal<boolean>;
    error: WritableSignal<string | null>;
    initialize: jasmine.Spy;
    login: jasmine.Spy;
    getApiAccessToken: jasmine.Spy;
  };
  let api: jasmine.SpyObj<CatalogMemberApiService>;
  const query = {purpose: null as string | null};

  beforeEach(async () => {
    auth = {
      account: signal(null),
      initialized: signal(true),
      isAuthenticated: signal(false),
      error: signal(null),
      initialize: jasmine.createSpy('initialize'),
      login: jasmine.createSpy('login'),
      getApiAccessToken: jasmine.createSpy('getApiAccessToken'),
    };
    auth.initialize.and.resolveTo();
    auth.login.and.resolveTo();
    auth.getApiAccessToken.and.resolveTo('member-token');
    api = jasmine.createSpyObj<CatalogMemberApiService>(
      'CatalogMemberApiService',
      ['setAlertStatus', 'getRecommendationPreference', 'setRecommendationPreference'],
    );
    api.setAlertStatus.and.returnValue(of({alertStatus: 'Suspended', bounceCount: 0, changed: true}));
    api.getRecommendationPreference.and.returnValue(of({enabled: true, minimumHearts: 4}));
    api.setRecommendationPreference.and.returnValue(of(void 0));
    query.purpose = null;

    await TestBed.configureTestingModule({
      declarations: [CatalogUnsubscribePageComponent],
      imports: [RouterModule.forRoot([])],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogMemberApiService, useValue: api},
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: {get: (key: string) => key === 'purpose' ? query.purpose : null},
            },
          },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(CatalogUnsubscribePageComponent);
  });

  it('asks for a login before touching the protected endpoint', async () => {
    fixture.detectChanges();
    await fixture.componentInstance.initialize();

    expect(auth.login).toHaveBeenCalledWith('/desinscription');
    expect(api.setAlertStatus).not.toHaveBeenCalled();
  });

  it('preserves the fair digest return query when authentication is required', async () => {
    query.purpose = 'fairDigest';
    fixture.detectChanges();
    await fixture.componentInstance.initialize();

    expect(auth.login).toHaveBeenCalledWith('/desinscription?purpose=fairDigest');
  });

  it('suspends alerts after the signed-in member confirms', async () => {
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.confirmUnsubscribe();

    expect(api.setAlertStatus).toHaveBeenCalledWith('member-token', false);
    expect(fixture.componentInstance.completed()).toBeTrue();
  });

  it('revokes only fair digest consent and keeps the recommendation settings', async () => {
    query.purpose = 'fairDigest';
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.confirmUnsubscribe();

    expect(api.getRecommendationPreference).toHaveBeenCalledWith('member-token');
    expect(api.setRecommendationPreference).toHaveBeenCalledOnceWith(
      'member-token', true, 4, false,
    );
    expect(api.setAlertStatus).not.toHaveBeenCalled();
    expect(fixture.componentInstance.completed()).toBeTrue();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(
      'Vous ne recevrez plus la sélection avant chaque bourse.',
    );
  });
});
