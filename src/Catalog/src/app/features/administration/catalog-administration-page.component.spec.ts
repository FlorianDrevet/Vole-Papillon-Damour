import {HttpErrorResponse, provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting} from '@angular/common/http/testing';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {signal, WritableSignal} from '@angular/core';
import type {AccountInfo} from '@azure/msal-browser';
import {of, throwError} from 'rxjs';

import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogAdminApiService} from '../../core/catalog-admin-api.service';
import {CatalogDeadStockResponse} from '../../core/catalog.models';
import {CatalogAdministrationPageComponent} from './catalog-administration-page.component';
import {toDeadStockCsv} from './dead-stock-export';

describe('CatalogAdministrationPageComponent', () => {
  let fixture: ComponentFixture<CatalogAdministrationPageComponent>;
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
  let api: jasmine.SpyObj<CatalogAdminApiService>;

  const response: CatalogDeadStockResponse = {
    generatedAt: '2026-09-04T12:00:00Z',
    minAgeMonths: 6,
    minQuantity: 3,
    books: [{
      isbn13: '9782070408504',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1999,
      genre: 'Jeunesse',
      quantityAvailable: 7,
      firstAvailableAt: '2025-01-12T10:00:00Z',
    }],
  };

  const account = (name: string): AccountInfo => ({
    homeAccountId: 'home-account-id',
    environment: 'volepapillondamour.ciamlogin.com',
    tenantId: 'tenant-id',
    username: `${name.toLowerCase()}@example.test`,
    localAccountId: 'local-account-id',
    name,
  });

  beforeEach(async () => {
    auth = {
      account: signal<AccountInfo | null>(null),
      initialized: signal(true),
      isAuthenticated: signal(false),
      error: signal<string | null>(null),
      initialize: jasmine.createSpy('initialize'),
      login: jasmine.createSpy('login'),
      logout: jasmine.createSpy('logout'),
      getApiAccessToken: jasmine.createSpy('getApiAccessToken'),
    };
    auth.initialize.and.resolveTo();
    auth.login.and.resolveTo();
    auth.logout.and.resolveTo();
    auth.getApiAccessToken.and.resolveTo('access-token');

    api = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', ['getDeadStock']);
    api.getDeadStock.and.returnValue(of(response));

    await TestBed.configureTestingModule({
      declarations: [CatalogAdministrationPageComponent],
      imports: [FormsModule],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogAdminApiService, useValue: api},
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogAdministrationPageComponent);
  });

  it('offers a dedicated administrator login when signed out', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Accès administration');
    expect(fixture.nativeElement.textContent).toContain('Se connecter avec Microsoft');
    expect(fixture.nativeElement.querySelector('[data-testid="admin-login"]')).not.toBeNull();
    expect(api.getDeadStock).not.toHaveBeenCalled();
  });

  it('loads and renders the dead-stock list for an authenticated administrator', async () => {
    auth.account.set(account('Administrator'));
    auth.isAuthenticated.set(true);
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    expect(api.getDeadStock).toHaveBeenCalledWith('access-token', 6, 3);
    expect(fixture.nativeElement.textContent).toContain('Le Petit Prince');
    expect(fixture.nativeElement.textContent).toContain('7');
    expect(fixture.nativeElement.querySelector('table')).not.toBeNull();
  });

  it('explains when the signed-in account lacks the administration role', async () => {
    auth.account.set(account('Volunteer'));
    auth.isAuthenticated.set(true);
    api.getDeadStock.and.returnValue(throwError(() => new HttpErrorResponse({status: 403})));
    fixture.detectChanges();
    await fixture.componentInstance.initialize();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('ne possède pas les droits d’administration');
  });

  it('exports a valid CSV with escaped book fields', () => {
    const csv = toDeadStockCsv({
      ...response,
      books: [{
        ...response.books[0],
        title: 'Livre, "rare"',
        authors: 'Autrice\nAuteur',
      }],
    });

    expect(csv).toContain('ISBN;Titre;Auteur;Éditeur;Année;Genre;Exemplaires;Disponible depuis');
    expect(csv).toContain('9782070408504;"Livre, ""rare""";"Autrice\nAuteur";Gallimard;1999;Jeunesse;7;2025-01-12T10:00:00Z');
  });

  it('neutralizes spreadsheet formula prefixes in exported metadata', () => {
    const csv = toDeadStockCsv({
      ...response,
      books: [{...response.books[0], title: '=HYPERLINK("https://example.test")'}],
    });

    expect(csv).toContain(`;"'=HYPERLINK(""https://example.test"")"`);
  });
});
