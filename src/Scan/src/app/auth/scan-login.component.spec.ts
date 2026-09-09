import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {AccountInfo, InteractionStatus} from '@azure/msal-browser';
import {BehaviorSubject, of, throwError} from 'rxjs';

import {ScanAuthService, ScanAuthState} from './scan-auth.service';
import {ScanLoginComponent} from './scan-login.component';

function accountFixture(overrides: Partial<AccountInfo> = {}): AccountInfo {
  return {
    homeAccountId: 'home-id',
    environment: 'login.microsoftonline.com',
    tenantId: 'tenant-id',
    localAccountId: 'local-id',
    username: 'claire.durand@volepapillondamour.fr',
    name: 'Claire Durand',
    ...overrides,
  } as AccountInfo;
}

describe('ScanLoginComponent', () => {
  let fixture: ComponentFixture<ScanLoginComponent>;
  let component: ScanLoginComponent;
  let authState: BehaviorSubject<ScanAuthState>;
  let interactionStatus: BehaviorSubject<InteractionStatus>;
  let auth: jasmine.SpyObj<ScanAuthService>;

  beforeEach(async () => {
    authState = new BehaviorSubject<ScanAuthState>({
      status: 'unauthenticated',
      account: null,
      roles: [],
      requiredRole: 'Tri ou Caisse',
    });
    interactionStatus = new BehaviorSubject<InteractionStatus>(InteractionStatus.None);
    auth = jasmine.createSpyObj<ScanAuthService>('ScanAuthService', ['login', 'logout'], {
      authState$: authState.asObservable(),
      interactionStatus$: interactionStatus.asObservable(),
    });
    auth.login.and.returnValue(of(undefined) as never);
    auth.logout.and.returnValue(of(undefined) as never);

    await TestBed.configureTestingModule({
      declarations: [ScanLoginComponent],
      imports: [CommonModule],
      providers: [{provide: ScanAuthService, useValue: auth}],
    }).compileComponents();

    fixture = TestBed.createComponent(ScanLoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('offers the volunteer login action', () => {
    expect(fixture.nativeElement.querySelector('.login-screen')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.login-welcome')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Tri des livres');
    expect(fixture.nativeElement.querySelector('.login-butterfly')?.getAttribute('src'))
      .toBe('assets/papillon_without_back.png');

    (fixture.nativeElement.querySelector('.login-primary') as HTMLButtonElement).click();

    expect(auth.login).toHaveBeenCalledOnceWith();
  });

  it('keeps the login action available while cached authorization is checking', () => {
    authState.next({
      status: 'checking',
      account: null,
      roles: [],
      requiredRole: 'Tri ou Caisse',
    });
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.login-primary') as HTMLButtonElement;
    expect(button.disabled).toBeFalse();

    button.click();

    expect(auth.login).toHaveBeenCalledOnceWith();
  });

  it('disables the login action while an MSAL interaction is in progress', () => {
    interactionStatus.next(InteractionStatus.HandleRedirect);
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.login-primary') as HTMLButtonElement;
    expect(button.disabled).toBeTrue();
  });

  it('explains the missing role and offers account switching', () => {
    authState.next({
      status: 'unauthorized',
      account: accountFixture(),
      roles: [],
      requiredRole: 'Tri ou Caisse',
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.login-unauthorized')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Rôle manquant');
    expect(fixture.nativeElement.textContent).toContain('Tri');
    expect(fixture.nativeElement.textContent).toContain('Caisse');
    expect(fixture.nativeElement.textContent).toContain('claire.durand@volepapillondamour.fr');
    (fixture.nativeElement.querySelector('.login-change-account') as HTMLButtonElement).click();

    expect(auth.logout).toHaveBeenCalledOnceWith();

    (fixture.nativeElement.querySelector('.login-retry') as HTMLButtonElement).click();

    expect(auth.login).toHaveBeenCalledOnceWith();
  });

  it('shows a visible error when the login redirect cannot start', () => {
    auth.login.and.returnValue(throwError(() => new Error('interaction_in_progress')) as never);

    (fixture.nativeElement.querySelector('.login-primary') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent)
      .toContain('Impossible de démarrer la connexion');
  });
});
