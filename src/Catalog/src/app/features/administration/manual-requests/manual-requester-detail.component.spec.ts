import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {of} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {CatalogApiService} from '../../../core/catalog-api.service';
import {CatalogManualRequester} from '../../../core/catalog.models';
import {ManualRequesterDetailComponent} from './manual-requester-detail.component';
import {ManualRequestAddDialogComponent} from './manual-request-add-dialog.component';
import {ManualRequestDecisionDialogComponent} from './manual-request-decision-dialog.component';

describe('ManualRequesterDetailComponent', () => {
  let fixture: ComponentFixture<ManualRequesterDetailComponent>;
  let api: jasmine.SpyObj<CatalogAdminApiService>;
  let catalogApi: jasmine.SpyObj<CatalogApiService>;

  const requester: CatalogManualRequester = {
    requesterId: 'requester-1', lastName: 'Martin', firstName: 'Jeanne', phone: '0612345678', erased: false,
    consentRecordedAt: '2026-09-03T09:00:00Z', consentRecordedByName: 'Martine',
    requests: [
      {
        requestId: 'request-1', requesterId: 'requester-1', requesterLastName: 'Martin', requesterFirstName: 'Jeanne',
        requesterPhone: '0612345678', requesterErased: false, scope: 'Edition', workId: null, isbn13: '9782070612758',
        title: 'Le Petit Prince', authors: 'Antoine de Saint-Exupéry', publisher: 'Gallimard', publicationYear: 1943,
        requestedAt: '2026-10-01T10:00:00Z', status: 'Found', foundOrigin: 'Scan', foundIsbn13: '9782070612758',
        foundPublisher: 'Gallimard', foundPublicationYear: 1943, foundAt: '2026-10-10T10:00:00Z',
        foundByName: 'Alice', quantityAvailable: 0, quantityAnnounced: 0, watcherCount: 2, releaseMode: 'AvailableNow',
        releaseFairStart: null, closedAt: null, closedByName: null,
      },
      {
        requestId: 'request-2', requesterId: 'requester-1', requesterLastName: 'Martin', requesterFirstName: 'Jeanne',
        requesterPhone: '0612345678', requesterErased: false, scope: 'Work', workId: 'OL123W', isbn13: null,
        title: 'L’Étranger', authors: 'Albert Camus', publisher: null, publicationYear: null,
        requestedAt: '2026-10-02T10:00:00Z', status: 'Searching', foundOrigin: null, foundIsbn13: null,
        foundPublisher: null, foundPublicationYear: null, foundAt: null, foundByName: null,
        quantityAvailable: 0, quantityAnnounced: 0, watcherCount: 0, releaseMode: null, releaseFairStart: null,
        closedAt: null, closedByName: null,
      },
    ],
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', [
      'getManualRequester', 'getManualRequestSummary', 'createManualRequester', 'updateManualRequester', 'deleteManualRequester',
      'pickManualRequest', 'markManualRequestGiven', 'refuseManualRequest', 'cancelManualRequest',
    ]);
    catalogApi = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['searchReferences']);
    catalogApi.searchReferences.and.returnValue(of({generatedAt: '', query: '', items: [], page: 1, pageSize: 20}));
    api.getManualRequester.and.returnValue(of(requester));
    api.getManualRequestSummary.and.returnValue(of({foundCount: 0, foundOverdueCount: 0}));
    api.createManualRequester.and.returnValue(of({requesterId: 'created-1'}));
    api.updateManualRequester.and.returnValue(of(undefined));
    api.deleteManualRequester.and.returnValue(of({cancelledRequestCount: 1, notifiedMemberCount: 2}));
    api.pickManualRequest.and.returnValue(of(undefined));
    api.markManualRequestGiven.and.returnValue(of(undefined));
    api.refuseManualRequest.and.returnValue(of({notifiedMemberCount: 0}));
    api.cancelManualRequest.and.returnValue(of({notifiedMemberCount: 0}));

    await TestBed.configureTestingModule({
      declarations: [ManualRequesterDetailComponent, ManualRequestAddDialogComponent, ManualRequestDecisionDialogComponent],
      imports: [FormsModule],
      providers: [
        {provide: CatalogAdminApiService, useValue: api},
        {provide: CatalogApiService, useValue: catalogApi},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ManualRequesterDetailComponent);
    fixture.componentRef.setInput('accessToken', 'access-token');
  });

  it('requires consent before creating a requester', async () => {
    fixture.componentRef.setInput('requesterId', 'nouveau');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.componentInstance.lastName = 'Martin';
    fixture.componentInstance.firstName = 'Jeanne';
    fixture.componentInstance.phone = '06 12 34 56 78';

    await fixture.componentInstance.save();
    fixture.detectChanges();

    expect(api.createManualRequester).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain("Sans l'accord de la personne, ses coordonnées ne peuvent pas être enregistrées.");
    expect(fixture.nativeElement.querySelector('[name="consentGiven"]')?.getAttribute('aria-invalid')).toBe('true');
  });

  it('creates the requester then asks to add a book', async () => {
    fixture.componentRef.setInput('requesterId', 'nouveau');
    fixture.detectChanges();
    await fixture.whenStable();
    const addBookRequested = jasmine.createSpy('addBookRequested');
    fixture.componentInstance.addBookRequested.subscribe(addBookRequested);
    fixture.componentInstance.lastName = 'Martin';
    fixture.componentInstance.firstName = 'Jeanne';
    fixture.componentInstance.phone = '06 12 34 56 78';
    fixture.componentInstance.consentGiven = true;

    await fixture.componentInstance.save();

    expect(api.createManualRequester).toHaveBeenCalledWith('access-token', {
      lastName: 'Martin', firstName: 'Jeanne', phone: '06 12 34 56 78', consentGiven: true,
    });
    expect(addBookRequested).toHaveBeenCalledWith('created-1');
  });

  it('shows the requester history and disables consent when editing', async () => {
    fixture.componentRef.setInput('requesterId', 'requester-1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    const consent = root.querySelector<HTMLInputElement>('[name="consentGiven"]');
    expect(root.textContent).toContain('Accord recueilli par Martine');
    expect(root.textContent).toContain('Le Petit Prince');
    expect(root.textContent).toContain('Trouvé · à remettre');
    expect(consent?.checked).toBeTrue();
    expect(consent?.disabled).toBeTrue();
  });

  it('deletes a requester only after explicit confirmation', async () => {
    fixture.componentRef.setInput('requesterId', 'requester-1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const closed = jasmine.createSpy('closed');
    fixture.componentInstance.closed.subscribe(closed);

    fixture.componentInstance.requestDelete();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Supprimer Jeanne MARTIN ?');
    expect(api.deleteManualRequester).not.toHaveBeenCalled();

    await fixture.componentInstance.confirmDelete();

    expect(api.deleteManualRequester).toHaveBeenCalledWith('access-token', 'requester-1');
    expect(closed).toHaveBeenCalled();
  });

  it('confirms canceling a found request and refreshes the administration badge', async () => {
    fixture.componentRef.setInput('requesterId', 'requester-1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const countsChanged = jasmine.createSpy('countsChanged');
    fixture.componentInstance.countsChanged.subscribe(countsChanged);

    fixture.componentInstance.cancelRequest(requester.requests[0]);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).toContain('Annuler la demande de Jeanne MARTIN ?');
    expect(api.cancelManualRequest).not.toHaveBeenCalled();
    (root.querySelector('[data-testid="manual-request-decision-confirm"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.cancelManualRequest).toHaveBeenCalledWith('access-token', 'request-1');
    expect(api.getManualRequestSummary).toHaveBeenCalledWith('access-token');
    expect(countsChanged).toHaveBeenCalledWith({foundCount: 0, foundOverdueCount: 0});
  });
});
