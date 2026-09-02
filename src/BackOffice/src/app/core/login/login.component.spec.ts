import {ComponentFixture, TestBed} from '@angular/core/testing';
import {MatSnackBar} from '@angular/material/snack-bar';
import {MsalService} from '@azure/msal-angular';
import {of} from 'rxjs';

import {LoginComponent} from './login.component';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let msalService: jasmine.SpyObj<MsalService>;

  beforeEach(async () => {
    msalService = jasmine.createSpyObj<MsalService>('MsalService', ['loginRedirect']);
    msalService.loginRedirect.and.returnValue(of(undefined));

    await TestBed.configureTestingModule({
      declarations: [LoginComponent],
      providers: [
        {provide: MsalService, useValue: msalService},
        {provide: MatSnackBar, useValue: {open: jasmine.createSpy('open')}},
      ],
    })
      .overrideComponent(LoginComponent, {set: {template: ''}})
      .compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
  });

  it('starts the Entra redirect login with the API scope', () => {
    component.onLoginClick();

    expect(msalService.loginRedirect).toHaveBeenCalledWith({
      scopes: ['api://ebc68507-2c07-4bab-9448-2d6d489c6112/access_as_user'],
    });
  });
});
