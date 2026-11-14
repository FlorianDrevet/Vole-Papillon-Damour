import {ComponentFixture, TestBed} from '@angular/core/testing';
import {signal} from '@angular/core';
import {of} from 'rxjs';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {CatalogAdminFairDigestStatus} from '../../../core/catalog.models';
import {FairDigestAdminComponent} from './fair-digest-admin.component';

describe('FairDigestAdminComponent', () => {
  let fixture: ComponentFixture<FairDigestAdminComponent>;
  let auth: {
    isAuthenticated: ReturnType<typeof signal<boolean>>;
    isAdministrator: ReturnType<typeof signal<boolean>>;
    getApiAccessToken: jasmine.Spy;
  };
  let api: jasmine.SpyObj<CatalogAdminApiService>;

  const status = (): CatalogAdminFairDigestStatus => ({
    enabled: true,
    nextFairId: 'fair-id',
    nextFairName: 'Bourse de printemps',
    nextFairStartsAt: '2027-03-14T09:00:00Z',
    campaignId: 'campaign-id',
    plannedAtUtc: '2027-03-11T09:00:00Z',
    suspended: false,
    estimatedRecipients: 142,
    lastReport: {
      fairName: 'Bourse d’automne',
      completedAtUtc: '2026-10-03T10:00:00Z',
      sentCount: 120,
      skippedCount: 8,
      failedCount: 2,
    },
  });

  beforeEach(async () => {
    auth = {
      isAuthenticated: signal(true),
      isAdministrator: signal(true),
      getApiAccessToken: jasmine.createSpy('getApiAccessToken').and.resolveTo('admin-token'),
    };
    api = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', [
      'getFairDigestStatus', 'getFairDigestPreview', 'sendFairDigestTest',
      'suspendFairDigest', 'resumeFairDigest',
    ]);
    api.getFairDigestStatus.and.returnValue(of(status()));
    api.getFairDigestPreview.and.returnValue(of('<!doctype html><html><body>Aperçu</body></html>'));
    api.sendFairDigestTest.and.returnValue(of({email: 'admin@example.test', isExample: true}));
    api.suspendFairDigest.and.returnValue(of({
      campaignId: 'campaign-id',
      changed: true,
      suspendedAtUtc: '2026-11-14T10:00:00Z',
    }));
    api.resumeFairDigest.and.returnValue(of({
      campaignId: 'campaign-id',
      changed: true,
      suspendedAtUtc: null,
    }));

    await TestBed.configureTestingModule({
      declarations: [FairDigestAdminComponent],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogAdminApiService, useValue: api},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(FairDigestAdminComponent);
  });

  it('shows fair schedule, estimated recipients and the sandboxed email preview', async () => {
    fixture.detectChanges();
    await settle();
    fixture.detectChanges();

    expect(api.getFairDigestStatus).toHaveBeenCalledOnceWith('admin-token');
    expect(fixture.nativeElement.querySelector('[data-zone="E9-prochain-envoi"]')?.textContent)
      .toContain('Bourse de printemps');
    expect(fixture.nativeElement.querySelector('[data-zone="E9-destinataires"]')?.textContent)
      .toContain('142');
    expect(fixture.nativeElement.querySelector('[data-zone="E9-apercu"] iframe')?.getAttribute('sandbox'))
      .toBe('');
    expect(fixture.nativeElement.textContent).toContain('Aucun script');
  });

  it('changes the campaign action from suspend to resume after a successful suspension', async () => {
    fixture.detectChanges();
    await settle();
    fixture.detectChanges();

    const suspendButton = fixture.nativeElement.querySelector('[data-zone="E9-bouton-suspendre"]') as HTMLButtonElement;
    expect(suspendButton.textContent).toContain('Suspendre l’envoi pour cette bourse');
    suspendButton.click();
    await settle();
    fixture.detectChanges();

    expect(api.suspendFairDigest).toHaveBeenCalledOnceWith('admin-token');
    expect(fixture.nativeElement.querySelector('[data-zone="E9-bouton-suspendre"]')?.textContent)
      .toContain('Reprendre l’envoi');
  });

  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>(resolve => setTimeout(resolve, 0));
    await fixture.whenStable();
  }
});
