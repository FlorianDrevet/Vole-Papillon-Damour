import {CommonModule} from '@angular/common';
import {Component} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {BehaviorSubject} from 'rxjs';

import {AppComponent} from './app.component';
import {ScanAuthService, ScanAuthState} from './auth/scan-auth.service';

@Component({
  selector: 'app-scan-login',
  template: '<div class="login-stub"></div>',
  standalone: false,
})
class LoginStubComponent {}

@Component({
  selector: 'app-scanner',
  template: '<div class="scanner-stub"></div>',
  standalone: false,
})
class ScannerStubComponent {}

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;
  let authState: BehaviorSubject<ScanAuthState>;

  beforeEach(async () => {
    authState = new BehaviorSubject<ScanAuthState>(createState('unauthenticated'));

    await TestBed.configureTestingModule({
      declarations: [
        AppComponent,
        LoginStubComponent,
        ScannerStubComponent,
      ],
      imports: [CommonModule],
      providers: [{
        provide: ScanAuthService,
        useValue: {authState$: authState.asObservable()},
      }],
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
  });

  it('shows the login surface until the account has the Tri role', () => {
    expect(fixture.nativeElement.querySelector('.login-stub')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.scanner-stub')).toBeNull();

    authState.next(createState('authorized'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.login-stub')).toBeNull();
    expect(fixture.nativeElement.querySelector('.scanner-stub')).not.toBeNull();
  });

  it('returns to the login surface when access expires', () => {
    authState.next(createState('authorized'));
    fixture.detectChanges();
    authState.next(createState('unauthenticated'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.login-stub')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.scanner-stub')).toBeNull();
  });

  function createState(status: ScanAuthState['status']): ScanAuthState {
    return {
      status,
      account: null,
      roles: [],
      requiredRole: 'Tri',
    };
  }
});
