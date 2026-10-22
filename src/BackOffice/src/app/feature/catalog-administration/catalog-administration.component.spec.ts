import {NO_ERRORS_SCHEMA} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';

import {CatalogAdminFacadeService} from '../../shared/facades/catalog-admin.facade.service';
import {CatalogAdministrationComponent} from './catalog-administration.component';

describe('CatalogAdministrationComponent account creation password', () => {
  let fixture: ComponentFixture<CatalogAdministrationComponent>;
  let facade: jasmine.SpyObj<CatalogAdminFacadeService>;

  beforeEach(async () => {
    facade = jasmine.createSpyObj<CatalogAdminFacadeService>(
      'CatalogAdminFacadeService',
      ['getOverview', 'getAccounts', 'createAccount', 'resetAccountPassword'],
    );
    facade.getOverview.and.resolveTo({} as Awaited<ReturnType<CatalogAdminFacadeService['getOverview']>>);
    facade.getAccounts.and.resolveTo({
      generatedAt: '',
      accounts: [],
      totalCount: 0,
      page: 1,
      pageSize: 25,
    });
    facade.createAccount.and.resolveTo({} as Awaited<ReturnType<CatalogAdminFacadeService['createAccount']>>);
    (facade as any).resetAccountPassword.and.resolveTo({temporaryPassword: 'TempPass9!'});

    await TestBed.configureTestingModule({
      declarations: [CatalogAdministrationComponent],
      imports: [FormsModule, ReactiveFormsModule],
      providers: [{provide: CatalogAdminFacadeService, useValue: facade}],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogAdministrationComponent);
  });

  function openCreateAccount(): any {
    const component = fixture.componentInstance as any;
    component.activeTab.set('accounts');
    component.showCreateAccount.set(true);
    fixture.detectChanges();
    return component;
  }

  function openAccounts(accounts: any[]): any {
    const component = fixture.componentInstance as any;
    component.activeTab.set('accounts');
    component.accounts.set(accounts);
    fixture.detectChanges();
    return component;
  }

  it('explains the temporary password requirements beside the field', () => {
    openCreateAccount();

    const form = (fixture.nativeElement as HTMLElement).querySelector<HTMLFormElement>('.admin-panel form, form.admin-panel');
    expect(form?.textContent).toContain('8 à 256 caractères');
    expect(form?.textContent).toContain('au moins 3 des 4 catégories');
  });

  it('rejects a password with only two categories before sending the account request', () => {
    const component = openCreateAccount();
    component.createAccountForm.patchValue({
      email: 'marie@example.test',
      firstName: 'Marie',
      lastName: 'Tri',
      temporaryPassword: 'abcdef12',
      roles: ['Tri'],
    });
    component.createAccountForm.controls.temporaryPassword.markAsTouched();

    component.createAccount();
    fixture.detectChanges();

    expect(facade.createAccount).not.toHaveBeenCalled();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('au moins 3 des 4 catégories');
  });

  it('displays the Entra password policy detail returned by account creation', async () => {
    const component = openCreateAccount();
    const detail = 'Le mot de passe temporaire ne respecte pas la politique Microsoft Entra. Choisissez un autre mot de passe.';
    facade.createAccount.and.rejectWith({response: {data: {detail, status: 400}}});
    component.createAccountForm.patchValue({
      email: 'marie@example.test',
      firstName: 'Marie',
      lastName: 'Tri',
      temporaryPassword: 'Abcdef1!',
      roles: ['Tri'],
    });

    component.createAccount();
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(detail);
  });

  it('shows password reset only for accounts that support it', () => {
    openAccounts([
      {externalId: 'entra-1', email: 'marie@example.test', displayName: 'Marie Tri', accountEnabled: true, roles: ['Tri'], canResetPassword: true},
      {externalId: 'entra-2', email: 'paul@example.test', displayName: 'Paul Caisse', accountEnabled: true, roles: ['Caisse'], canResetPassword: false},
    ]);

    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'))
      .filter(button => button.textContent?.includes('Réinitialiser le mot de passe'));

    expect(buttons.length).toBe(1);
    expect(buttons[0].closest('tr')?.textContent).toContain('marie@example.test');
  });

  it('does not reset the password when the administrator cancels confirmation', () => {
    const account = {externalId: 'entra-1', email: 'marie@example.test', canResetPassword: true};
    const component = openAccounts([account]);
    spyOn(window, 'confirm').and.returnValue(false);

    component.resetAccountPassword(account);

    expect((facade as any).resetAccountPassword).not.toHaveBeenCalled();
  });

  it('shows the temporary password once and erases it when closed', async () => {
    const account = {externalId: 'entra-1', email: 'marie@example.test', canResetPassword: true};
    const component = openAccounts([account]);
    spyOn(window, 'confirm').and.returnValue(true);

    component.resetAccountPassword(account);
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect((facade as any).resetAccountPassword).toHaveBeenCalledOnceWith('entra-1');
    expect(page.textContent).toContain('marie@example.test');
    expect(page.textContent).toContain('TempPass9!');
    expect(page.textContent).toContain('devra le changer à sa prochaine connexion');

    const closeButton = Array.from(page.querySelectorAll('button'))
      .find(button => button.textContent?.includes('Fermer et effacer'));
    expect(closeButton).toBeDefined();
    closeButton?.click();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('TempPass9!');
  });
});
