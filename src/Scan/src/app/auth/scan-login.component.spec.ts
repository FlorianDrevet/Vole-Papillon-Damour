import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {BehaviorSubject, of, throwError} from 'rxjs';

import {ScanAuthService, ScanAuthState} from './scan-auth.service';
import {ScanLoginComponent} from './scan-login.component';

describe('ScanLoginComponent', () => {
  let fixture: ComponentFixture<ScanLoginComponent>;
  let component: ScanLoginComponent;
  let authState: BehaviorSubject<ScanAuthState>;
  let auth: jasmine.SpyObj<ScanAuthService>;

  beforeEach(async () => {
    authState = new BehaviorSubject<ScanAuthState>({
      status: 'unauthenticated',
      account: null,
      roles: [],
      requiredRole: 'Tri',
    });
    auth = jasmine.createSpyObj<ScanAuthService>('ScanAuthService', ['login', 'logout'], {
      authState$: authState.asObservable(),
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
    expect(fixture.nativeElement.textContent).toContain('Accès bénévole');

    (fixture.nativeElement.querySelector('.login-primary') as HTMLButtonElement).click();

    expect(auth.login).toHaveBeenCalledOnceWith();
  });

  it('explains the missing role and offers account switching', () => {
    authState.next({
      status: 'unauthorized',
      account: null,
      roles: ['Caisse'],
      requiredRole: 'Tri',
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('rôle Tri');
    (fixture.nativeElement.querySelector('.login-secondary') as HTMLButtonElement).click();

    expect(auth.logout).toHaveBeenCalledOnceWith();
  });

  it('shows a visible error when the login redirect cannot start', () => {
    auth.login.and.returnValue(throwError(() => new Error('interaction_in_progress')) as never);

    (fixture.nativeElement.querySelector('.login-primary') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent)
      .toContain('Impossible de démarrer la connexion');
  });
});
