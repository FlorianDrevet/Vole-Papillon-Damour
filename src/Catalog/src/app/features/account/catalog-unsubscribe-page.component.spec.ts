import {ComponentFixture, TestBed} from '@angular/core/testing';
import {signal, WritableSignal} from '@angular/core';
import {RouterModule} from '@angular/router';
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
    api = jasmine.createSpyObj<CatalogMemberApiService>('CatalogMemberApiService', ['setAlertStatus']);
    api.setAlertStatus.and.returnValue(of({alertStatus: 'Suspended', bounceCount: 0, changed: true}));

    await TestBed.configureTestingModule({
      declarations: [CatalogUnsubscribePageComponent],
      imports: [RouterModule.forRoot([])],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogMemberApiService, useValue: api},
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

  it('suspends alerts after the signed-in member confirms', async () => {
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    await fixture.componentInstance.confirmUnsubscribe();

    expect(api.setAlertStatus).toHaveBeenCalledWith('member-token', false);
    expect(fixture.componentInstance.completed()).toBeTrue();
  });
});
