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
      ['getOverview', 'getAccounts', 'createAccount'],
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
});
