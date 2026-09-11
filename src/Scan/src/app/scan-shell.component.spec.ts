import {CommonModule} from '@angular/common';
import {NO_ERRORS_SCHEMA} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterTestingModule} from '@angular/router/testing';
import {BehaviorSubject} from 'rxjs';
import {DesignSystemModule} from '@vpd/ui';

import {ScanAuthService, ScanAuthState} from './auth/scan-auth.service';
import {ScanShellComponent} from './scan-shell.component';

describe('ScanShellComponent', () => {
  let fixture: ComponentFixture<ScanShellComponent>;
  let authState: ScanAuthState;
  let authState$: BehaviorSubject<ScanAuthState>;

  const createAuthState = (status: ScanAuthState['status']): ScanAuthState => ({
    status,
    account: null,
    roles: [],
    requiredRole: 'Tri ou Caisse',
  });

  beforeEach(async () => {
    authState = createAuthState('authorized');
    authState$ = new BehaviorSubject(authState);

    await TestBed.configureTestingModule({
      declarations: [ScanShellComponent],
      imports: [CommonModule, RouterTestingModule, DesignSystemModule],
      providers: [{
        provide: ScanAuthService,
        useValue: {authState, authState$: authState$.asObservable()},
      }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(ScanShellComponent);
  });

  it('keeps the authenticated shell free of global header and synchronization chrome', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.scan-global-header')).toBeNull();
    expect(fixture.nativeElement.querySelector('scan-status-bar')).toBeNull();
  });

  it('shows only the login surface while authorization is not available', () => {
    authState.status = 'unauthenticated';
    authState$.next(authState);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-scan-login')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeNull();
  });

  it('shows the 1b flight loader while the initial authorization check is pending', () => {
    authState$.next(createAuthState('checking'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-loader="flight"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('app-scan-login')).toBeNull();
  });
});
