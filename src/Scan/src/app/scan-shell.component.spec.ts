import {CommonModule} from '@angular/common';
import {NO_ERRORS_SCHEMA} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterTestingModule} from '@angular/router/testing';

import {ScanAuthService} from './auth/scan-auth.service';
import {ScanShellComponent} from './scan-shell.component';

describe('ScanShellComponent', () => {
  let fixture: ComponentFixture<ScanShellComponent>;
  let authState: {status: 'authorized' | 'unauthenticated'};

  beforeEach(async () => {
    authState = {status: 'authorized'};

    await TestBed.configureTestingModule({
      declarations: [ScanShellComponent],
      imports: [CommonModule, RouterTestingModule],
      providers: [{
        provide: ScanAuthService,
        useValue: {authState},
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
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-scan-login')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeNull();
  });
});
