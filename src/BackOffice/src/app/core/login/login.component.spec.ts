import {NO_ERRORS_SCHEMA} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {ActivatedRoute, convertToParamMap} from '@angular/router';
import {Subject, throwError} from 'rxjs';

import {AuthSessionService} from '../../shared/auth/auth-session.service';
import {LoginComponent} from './login.component';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: any;
  let authSession: jasmine.SpyObj<AuthSessionService>;

  beforeEach(async () => {
    authSession = jasmine.createSpyObj<AuthSessionService>(
      'AuthSessionService',
      ['login', 'logout', 'resetSession'],
      {isAuthenticated: () => false} as Partial<AuthSessionService>,
    );
    authSession.login.and.returnValue(new Subject<void>());

    await TestBed.configureTestingModule({
      declarations: [LoginComponent],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [
        {provide: AuthSessionService, useValue: authSession},
        {
          provide: ActivatedRoute,
          useValue: {snapshot: {queryParamMap: convertToParamMap({})}},
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
  });

  it('starts the Entra redirect login', () => {
    component.onLoginClick();

    expect(authSession.login).toHaveBeenCalled();
  });

  it('keeps a recovery message on screen when the redirect cannot start', () => {
    authSession.login.and.returnValue(throwError(() => new Error('interaction_in_progress')));

    component.onLoginClick();

    expect(component.hasFailed()).toBeTrue();
  });

  it('shows a provider-neutral sign-in action and avoids viewport gutters in split panels', () => {
    fixture.detectChanges();

    const host = fixture.nativeElement as HTMLElement;
    const signInButton = host.querySelector('button[type="button"]');
    const panels = Array.from(host.querySelectorAll('section'));

    expect(signInButton?.textContent?.trim()).toBe('Connexion');
    expect(host.textContent).not.toContain('Microsoft');
    expect(panels.every(panel => !panel.classList.contains('px-shell'))).toBeTrue();
  });
});
