import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {HttpErrorResponse} from '@angular/common/http';
import {of, throwError} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {CatalogApiService} from '../../../core/catalog-api.service';
import {CatalogBookReference, CatalogReferenceSearchResponse} from '../../../core/catalog.models';
import {ManualRequestAddDialogComponent} from './manual-request-add-dialog.component';

describe('ManualRequestAddDialogComponent', () => {
  let fixture: ComponentFixture<ManualRequestAddDialogComponent>;
  let adminApi: jasmine.SpyObj<CatalogAdminApiService>;
  let catalogApi: jasmine.SpyObj<CatalogApiService>;

  const workBook: CatalogBookReference = {
    isbn13: '9782070612758', workId: 'OL123W', title: 'Le Petit Prince',
    authors: 'Antoine de Saint-Exupéry', publisher: 'Gallimard', publicationYear: 2007,
    coverUrl: null, source: 'OpenLibrary',
  };
  const editionOnlyBook: CatalogBookReference = {...workBook, isbn13: '9782070584628', workId: null};
  const searchResponse: CatalogReferenceSearchResponse = {
    generatedAt: '2026-10-12T09:00:00Z', query: 'petit prince', items: [workBook], page: 1, pageSize: 20,
  };

  beforeEach(async () => {
    adminApi = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', [
      'getManualRequestStock', 'addManualBookRequest',
    ]);
    catalogApi = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['searchReferences']);
    adminApi.getManualRequestStock.and.returnValue(of({quantityAvailable: 0, quantityAnnounced: 0, nextFairStart: null}));
    adminApi.addManualBookRequest.and.returnValue(of({requestId: 'request-1', status: 'Searching'}));
    catalogApi.searchReferences.and.returnValue(of(searchResponse));

    await TestBed.configureTestingModule({
      declarations: [ManualRequestAddDialogComponent],
      imports: [FormsModule],
      providers: [
        {provide: CatalogAdminApiService, useValue: adminApi},
        {provide: CatalogApiService, useValue: catalogApi},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ManualRequestAddDialogComponent);
    fixture.componentRef.setInput('accessToken', 'access-token');
    fixture.componentRef.setInput('requesterId', 'requester-1');
    fixture.componentRef.setInput('requesterFirstName', 'Jeanne');
    fixture.componentRef.setInput('requesterLastName', 'Martin');
  });

  it('defaults to work scope for a reference attached to a work', async () => {
    fixture.detectChanges();
    await fixture.componentInstance.searchBooks();
    await fixture.componentInstance.selectBook(workBook);
    fixture.detectChanges();

    expect(fixture.componentInstance.scope()).toBe('Work');
    expect(adminApi.getManualRequestStock).toHaveBeenCalledWith('access-token', {scope: 'Work', workId: 'OL123W'});
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Toutes les éditions de cette œuvre · recommandé');
  });

  it('locks an edition-only reference to edition scope and explains why', async () => {
    fixture.detectChanges();
    await fixture.componentInstance.selectBook(editionOnlyBook);
    fixture.detectChanges();

    expect(fixture.componentInstance.scope()).toBe('Edition');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Ce livre ne peut être suivi que dans cette édition.');
    expect(fixture.nativeElement.querySelector('[data-testid="manual-request-scope-work"]')?.disabled).toBeTrue();
  });

  it('hides the stock panel when the selected target has no stock', async () => {
    fixture.detectChanges();
    await fixture.componentInstance.selectBook(workBook);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-testid="manual-request-stock"]')).toBeNull();
  });

  it('sends pickFromStock true when the administrator chooses to pick a copy', async () => {
    adminApi.getManualRequestStock.and.returnValue(of({quantityAvailable: 2, quantityAnnounced: 1, nextFairStart: '2027-03-14'}));
    fixture.detectChanges();
    await fixture.componentInstance.selectBook(workBook);
    await fixture.componentInstance.add(true);

    expect(adminApi.addManualBookRequest).toHaveBeenCalledWith('access-token', 'requester-1', jasmine.objectContaining({
      scope: 'Work', workId: 'OL123W', pickFromStock: true,
    }));
  });

  it('shows the reference service error', async () => {
    catalogApi.searchReferences.and.returnValue(throwError(() => new HttpErrorResponse({status: 503})));
    fixture.detectChanges();
    fixture.componentInstance.query = 'petit prince';
    await fixture.componentInstance.searchBooks();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('La recherche de livres est momentanément indisponible. Réessayez dans quelques minutes.');
  });

  it('shows the duplicate request error', async () => {
    adminApi.addManualBookRequest.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 409, error: {code: 'ManualRequest.DuplicateRequest'},
    })));
    fixture.detectChanges();
    await fixture.componentInstance.selectBook(workBook);
    await fixture.componentInstance.add(false);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Jeanne Martin cherche déjà ce livre.');
  });
});
