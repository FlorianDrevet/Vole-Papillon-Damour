import {ComponentFixture, TestBed} from '@angular/core/testing';
import {PLATFORM_ID} from '@angular/core';
import {of} from 'rxjs';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../../core/catalog-member-api.service';
import {AccountCardComponent} from './account-card.component';

describe('AccountCardComponent', () => {
  let fixture: ComponentFixture<AccountCardComponent>;
  let auth: jasmine.SpyObj<CatalogAuthService>;
  let api: jasmine.SpyObj<CatalogMemberApiService>;
  let purchases: jasmine.Spy;

  const card = (recoveryCode: string) => ({
    qrPayload: 'VPDC1.AAAA.BBBB',
    recoveryCode,
    displayLabel: 'Camille',
    issuedAt: '2026-09-23T10:00:00Z',
  });

  async function render(platformId = 'browser'): Promise<ComponentFixture<AccountCardComponent>> {
    await TestBed.configureTestingModule({
      declarations: [AccountCardComponent],
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        {provide: CatalogMemberApiService, useValue: api},
        {provide: PLATFORM_ID, useValue: platformId},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AccountCardComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise(resolve => setTimeout(resolve, 25));
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    auth = jasmine.createSpyObj<CatalogAuthService>('CatalogAuthService', ['getApiAccessToken']);
    auth.getApiAccessToken.and.resolveTo('member-token');
    purchases = jasmine.createSpy('getPurchases');
    api = {
      getCard: jasmine.createSpy('getCard').and.returnValue(of(card('LUNE-4271'))),
      rotateCard: jasmine.createSpy('rotateCard').and.returnValue(of(card('AUBE-5932'))),
      getPurchases: purchases,
    } as unknown as jasmine.SpyObj<CatalogMemberApiService>;
  });

  it('renders an accessible QR and the recovery code', async () => {
    await render();

    const qr = fixture.nativeElement.querySelector('[role="img"]') as HTMLElement;
    expect(qr.getAttribute('aria-label')).toContain('LUNE-4271');
    expect(qr.querySelector('svg')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Code de secours : LUNE-4271');
    expect(fixture.nativeElement.textContent).not.toContain('@');
  });

  it('does not call the purchases endpoint to display the card', async () => {
    await render();

    expect(purchases).not.toHaveBeenCalled();
    expect(api.getCard).toHaveBeenCalledTimes(1);
  });

  it('asks confirmation before rotating and shows the new recovery code', async () => {
    await render();

    const rotate = fixture.nativeElement.querySelector('[data-testid="rotate-card"]') as HTMLButtonElement;
    rotate.click();
    fixture.detectChanges();

    const confirmation = fixture.nativeElement.querySelector('[role="alertdialog"]') as HTMLElement;
    expect(confirmation.textContent).toContain("L'ancien QR code et l'ancien code de secours ne fonctionneront plus.");
    expect(api.rotateCard).not.toHaveBeenCalled();

    const confirm = fixture.nativeElement.querySelector('[data-testid="confirm-rotate-card"]') as HTMLButtonElement;
    confirm.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.rotateCard).toHaveBeenCalledOnceWith('member-token');
    expect(fixture.nativeElement.textContent).toContain('Code de secours : AUBE-5932');
  });

  it('renders a placeholder instead of a QR on the server', async () => {
    await render('server');

    expect(fixture.nativeElement.querySelector('svg')).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="card-qr-placeholder"]')).not.toBeNull();
    expect(api.getCard).not.toHaveBeenCalled();
  });

  it('keeps the card within a 342px column at a 390px viewport', async () => {
    await render();

    const host = fixture.nativeElement as HTMLElement;
    host.style.width = '342px';
    host.style.maxWidth = '342px';
    fixture.detectChanges();

    expect(host.scrollWidth).toBeLessThanOrEqual(342);
    const panel = host.querySelector('.member-card-panel') as HTMLElement;
    expect(panel.scrollWidth).toBeLessThanOrEqual(panel.clientWidth);
    expect((host.querySelector('.member-card') as HTMLElement).getBoundingClientRect().width).toBeLessThanOrEqual(342);
  });
});
