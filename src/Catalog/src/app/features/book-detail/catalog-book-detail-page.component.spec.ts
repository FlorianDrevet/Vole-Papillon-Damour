import {ActivatedRoute, convertToParamMap} from '@angular/router';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';
import {signal, WritableSignal} from '@angular/core';
import type {AccountInfo} from '@azure/msal-browser';
import {of} from 'rxjs';
import {DesignSystemModule} from '@vpd/ui';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';
import {CatalogBook} from '../../core/catalog.models';
import {CatalogBookDetailPageComponent} from './catalog-book-detail-page.component';

describe('CatalogBookDetailPageComponent', () => {
  let fixture: ComponentFixture<CatalogBookDetailPageComponent>;
  let auth: {
    account: WritableSignal<AccountInfo | null>;
    initialized: WritableSignal<boolean>;
    isAuthenticated: WritableSignal<boolean>;
    error: WritableSignal<string | null>;
    initialize: jasmine.Spy;
    login: jasmine.Spy;
    logout: jasmine.Spy;
    getApiAccessToken: jasmine.Spy;
  };
  let api: jasmine.SpyObj<CatalogApiService>;
  let memberApi: jasmine.SpyObj<CatalogMemberApiService>;

  const book: CatalogBook = {
    isbn13: '9782070363735',
    title: 'Livre à surveiller',
    authors: 'Une autrice',
    publisher: 'Un éditeur',
    publicationYear: 2020,
    physicalFormat: 'Poche',
    language: 'fr',
    genre: 'Roman',
    workId: 'work-42',
    coverUrl: null,
    quantityAvailable: 0,
    quantityAnnounced: 0,
    nextFairAt: null,
    lastAvailableAt: null,
    firstSeenAt: '2026-09-01T10:00:00Z',
    updatedAt: '2026-09-04T10:00:00Z',
    isRare: false,
  };

  const account = (): AccountInfo => ({
    homeAccountId: 'home-account-id',
    environment: 'volepapillondamour.ciamlogin.com',
    tenantId: 'tenant-id',
    username: 'member@example.test',
    localAccountId: 'local-account-id',
    name: 'Member',
  });

  beforeEach(async () => {
    auth = {
      account: signal<AccountInfo | null>(account()),
      initialized: signal(true),
      isAuthenticated: signal(true),
      error: signal<string | null>(null),
      initialize: jasmine.createSpy('initialize'),
      login: jasmine.createSpy('login'),
      logout: jasmine.createSpy('logout'),
      getApiAccessToken: jasmine.createSpy('getApiAccessToken'),
    };
    auth.initialize.and.resolveTo();
    auth.login.and.resolveTo();
    auth.getApiAccessToken.and.resolveTo('member-token');

    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['getBook']);
    api.getBook.and.returnValue(of(book));
    memberApi = jasmine.createSpyObj<CatalogMemberApiService>(
      'CatalogMemberApiService',
      ['addWatchlistItem'],
    );
    memberApi.addWatchlistItem.and.returnValue(of({
      id: 'item-1',
      scope: 'Work',
      workId: 'work-42',
      isbn13: null,
      addedAt: '2026-09-04T20:00:00Z',
    }));

    await TestBed.configureTestingModule({
      declarations: [CatalogBookDetailPageComponent],
      imports: [RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        {provide: CatalogApiService, useValue: api},
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogMemberApiService, useValue: memberApi},
        {
          provide: ActivatedRoute,
          useValue: {paramMap: of(convertToParamMap({slug: 'livre-9782070363735'}))},
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogBookDetailPageComponent);
  });

  it('lets an authenticated member follow the work from its book page', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const notifyButton = fixture.nativeElement.querySelector('.notify-button') as HTMLButtonElement;
    expect(notifyButton.disabled).toBeFalse();

    notifyButton.click();
    await fixture.whenStable();

    expect(memberApi.addWatchlistItem).toHaveBeenCalledWith('member-token', {
      scope: 'Work',
      workId: 'work-42',
      isbn13: null,
    });
    expect(fixture.nativeElement.textContent).toContain('ajouté à votre liste');
  });

  it('starts member login instead of sending an anonymous protected request', async () => {
    auth.isAuthenticated.set(false);
    auth.account.set(null);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const notifyButton = fixture.nativeElement.querySelector('.notify-button') as HTMLButtonElement;
    notifyButton.click();
    await fixture.whenStable();

    expect(auth.login).toHaveBeenCalledWith('/livres/livre-a-surveiller-une-autrice-9782070363735');
    expect(memberApi.addWatchlistItem).not.toHaveBeenCalled();
  });

  it('renders the generic book cover when the detail has no image', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('vpd-book-cover-placeholder')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.detail-cover span')).toBeNull();
  });
});
