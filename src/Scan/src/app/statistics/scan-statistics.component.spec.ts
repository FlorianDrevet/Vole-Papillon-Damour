import {ComponentFixture, TestBed} from '@angular/core/testing';
import {AccountInfo} from '@azure/msal-browser';
import {RouterModule} from '@angular/router';
import {of, throwError} from 'rxjs';

import {ScanAuthService} from '../auth/scan-auth.service';
import {ScanApiService} from '../offline/scan-api.service';
import {ScanLocalStoreService} from '../offline/scan-local-store.service';
import {ScanVolunteerStatisticsRecord} from '../offline/scan-offline.model';
import {ScanStatisticsComponent} from './scan-statistics.component';

describe('ScanStatisticsComponent', () => {
  let fixture: ComponentFixture<ScanStatisticsComponent>;
  let api: jasmine.SpyObj<ScanApiService>;
  let store: jasmine.SpyObj<ScanLocalStoreService>;
  let auth: jasmine.SpyObj<ScanAuthService>;

  beforeEach(async () => {
    api = jasmine.createSpyObj<ScanApiService>('ScanApiService', ['getVolunteerStatistics']);
    store = jasmine.createSpyObj<ScanLocalStoreService>(
      'ScanLocalStoreService',
      ['getVolunteerStatistics', 'saveVolunteerStatistics', 'countBlockingOutboxEntries'],
    );
    auth = jasmine.createSpyObj<ScanAuthService>(
      'ScanAuthService',
      [],
      {
        authState: {
          status: 'degraded',
          account: {
            homeAccountId: 'home-camille',
            environment: 'login.microsoftonline.com',
            tenantId: 'tenant-id',
            localAccountId: 'local-camille',
            username: 'camille@example.org',
            name: 'Camille',
          } as AccountInfo,
          roles: ['Tri'],
          requiredRole: 'Tri ou Caisse',
        },
        canSort: true,
        canSell: false,
        displayName: 'Camille',
        account$: of(null),
      },
    );
    api.getVolunteerStatistics.and.returnValue(
      throwError(() => new Error('offline')),
    );
    store.getVolunteerStatistics.and.resolveTo({
      key: 'volunteer-statistics',
      accountId: 'home-camille',
      fetchedAt: '2026-09-11T12:00:00.000Z',
      statistics: createStatistics(),
    });
    store.countBlockingOutboxEntries.and.resolveTo(3);
    store.saveVolunteerStatistics.and.resolveTo();

    await TestBed.configureTestingModule({
      declarations: [ScanStatisticsComponent],
      imports: [RouterModule.forRoot([])],
      providers: [
        {provide: ScanAuthService, useValue: auth},
        {provide: ScanApiService, useValue: api},
        {provide: ScanLocalStoreService, useValue: store},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ScanStatisticsComponent);
  });

  it('renders the cached tri snapshot and pending synchronization state when offline', async () => {
    fixture.detectChanges();
    await fixture.componentInstance.load();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Mes stats');
    expect(fixture.nativeElement.textContent).toContain('TRI');
    expect(fixture.nativeElement.textContent).toContain('3 gestes à synchroniser');
    expect(fixture.nativeElement.textContent).toContain('Livres scannés');
    expect(fixture.nativeElement.textContent).toContain('12');
    expect(fixture.nativeElement.textContent).toContain('Dernier instantané local');
    expect(store.getVolunteerStatistics).toHaveBeenCalledWith('home-camille');
  });

  function createStatistics(): ScanVolunteerStatisticsRecord['statistics'] {
    return {
      generatedAt: '2026-09-11T12:00:00.000Z',
      memberSince: '2024-03-01T12:00:00.000Z',
      scan: {
        scannedCount: 12,
        keptCount: 9,
        rejectedCount: 3,
        sessionCount: 2,
        durationMinutes: 80,
        firstSessionAt: '2024-03-01T12:00:00.000Z',
        medianTeamKeepRatePercent: 74,
        monthly: [{periodStart: '2026-09-01T00:00:00Z', kept: 9, rejected: 3}],
        timeSlots: [{dayOfWeek: 6, slot: 'morning', count: 12}],
        impact: {
          foundReaderCount: 5,
          newTitleCount: 2,
          rareCount: 1,
          alertItemCount: 3,
          foundReaderIsEstimated: true,
        },
        topGenres: [{name: 'Romans', quantity: 6}],
        recentSessions: [],
      },
      cash: {
        grossSoldQuantity: 0,
        soldQuantity: 0,
        saleMovementCount: 0,
        voidedSaleQuantity: 0,
        fairCount: 0,
        estimatedDurationMinutes: 0,
        estimatedCadencePerHour: null,
        medianTeamCadencePerHour: null,
        fairBreakdown: [],
        peak: {fairDate: null, localHour: null, quantity: 0},
        topBooks: [],
        topGenres: [],
        estimatedTriAndCashOverlap: 0,
        estimatedRevenueShare: null,
        revenueShare: null,
        durationIsEstimated: true,
        cadenceIsEstimated: true,
        revenueShareIsEstimated: true,
        triAndCashOverlapIsEstimated: true,
      },
    };
  }
});
