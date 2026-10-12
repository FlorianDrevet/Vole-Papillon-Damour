import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {of, throwError} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {CatalogManualRequestItem, CatalogManualRequestQueue} from '../../../core/catalog.models';
import {ManualRequestsViewComponent} from './manual-requests-view.component';

describe('ManualRequestsViewComponent', () => {
  let fixture: ComponentFixture<ManualRequestsViewComponent>;
  let api: jasmine.SpyObj<CatalogAdminApiService>;

  const item: CatalogManualRequestItem = {
    requestId: 'request-1', requesterId: 'requester-1', requesterLastName: 'Martin', requesterFirstName: 'Jeanne',
    requesterPhone: '0612345678', requesterErased: false, scope: 'Edition', workId: null, isbn13: '9782070612758',
    title: 'Le Petit Prince', authors: 'Antoine de Saint-Exupéry', publisher: 'Gallimard', publicationYear: 1943,
    requestedAt: '2026-10-01T10:00:00Z', status: 'Found', foundOrigin: 'Scan', foundIsbn13: '9782070612758',
    foundPublisher: 'Gallimard', foundPublicationYear: 1943, foundAt: '2026-10-10T10:00:00Z',
    foundByName: 'Alice', quantityAvailable: 0, quantityAnnounced: 0, watcherCount: 2, releaseMode: 'AvailableNow',
    releaseFairStart: null, closedAt: null, closedByName: null,
  };

  const queue: CatalogManualRequestQueue = {
    items: [item], totalCount: 1, foundCount: 1, searchingCount: 0, closedCount: 0,
    foundOverdueCount: 0, page: 1, pageSize: 50,
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', [
      'getManualRequests', 'getManualRequestSummary', 'pickManualRequest', 'markManualRequestGiven',
      'refuseManualRequest', 'cancelManualRequest',
    ]);
    api.getManualRequests.and.returnValue(of(queue));
    api.pickManualRequest.and.returnValue(of(undefined));
    api.markManualRequestGiven.and.returnValue(of(undefined));
    api.refuseManualRequest.and.returnValue(of({notifiedMemberCount: 0}));
    api.cancelManualRequest.and.returnValue(of({notifiedMemberCount: 0}));

    await TestBed.configureTestingModule({
      declarations: [ManualRequestsViewComponent],
      imports: [FormsModule],
      providers: [{provide: CatalogAdminApiService, useValue: api}],
    }).compileComponents();

    fixture = TestBed.createComponent(ManualRequestsViewComponent);
    fixture.componentRef.setInput('accessToken', 'access-token');
  });

  it('renders the manual request heading, found row, privacy note and requester action', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).toContain('Demandes manuelles.');
    expect(root.textContent).toContain('Trouvés à remettre');
    expect(root.textContent).toContain('Le Petit Prince');
    expect(root.textContent).toContain('Trouvé au tri par Alice');
    expect(root.textContent).toContain('Demande : cette édition');
    expect(root.textContent).toContain('Ces livres ne sont ni disponibles ni annoncés');
    expect(root.querySelector('[data-testid="manual-request-new-requester"]')).not.toBeNull();
  });

  it('loads the selected queue tab', async () => {
    fixture.detectChanges();
    await fixture.whenStable();
    api.getManualRequests.calls.reset();

    (fixture.nativeElement.querySelector('[data-testid="manual-requests-tab-searching"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(api.getManualRequests).toHaveBeenCalledWith('access-token', {
      tab: 'searching', q: '', page: 1, pageSize: 50,
    });
  });

  it('shows the empty queue message', async () => {
    api.getManualRequests.and.returnValue(of({...queue, items: [], totalCount: 0, foundCount: 0}));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Aucun livre à remettre pour le moment.');
  });

  it('shows a retry action after a queue error', async () => {
    api.getManualRequests.and.returnValues(
      throwError(() => new Error('offline')),
      of({...queue, items: [], totalCount: 0, foundCount: 0}),
    );
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).toContain('Impossible de charger les demandes manuelles. Vérifiez la connexion puis réessayez.');
    (root.querySelector('[data-testid="manual-requests-retry"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(api.getManualRequests).toHaveBeenCalledTimes(2);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Aucun livre à remettre pour le moment.');
  });

  it('picks an available searching request and reloads the queue', async () => {
    const searching = {...item, status: 'Searching' as const, foundOrigin: null, foundIsbn13: null,
      foundAt: null, foundByName: null, quantityAvailable: 1, quantityAnnounced: 0, releaseMode: null};
    api.getManualRequests.and.returnValue(of({...queue, items: [searching], foundCount: 0, searchingCount: 1}));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    api.getManualRequests.calls.reset();

    (fixture.nativeElement.querySelector('[data-testid="manual-request-pick"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.pickManualRequest).toHaveBeenCalledWith('access-token', 'request-1');
    expect(api.getManualRequests).toHaveBeenCalledTimes(1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Exemplaire prélevé — demande trouvée');
  });
});
