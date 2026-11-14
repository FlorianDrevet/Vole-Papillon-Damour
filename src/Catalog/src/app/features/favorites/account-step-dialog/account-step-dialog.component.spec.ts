import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RouterModule} from '@angular/router';

import {LocalFavoriteEntry} from '../../../core/favorites/local-favorites.store';
import {AccountStepDialogComponent} from './account-step-dialog.component';

describe('AccountStepDialogComponent', () => {
  let fixture: ComponentFixture<AccountStepDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [AccountStepDialogComponent],
      imports: [RouterModule.forRoot([])],
    }).compileComponents();

    fixture = TestBed.createComponent(AccountStepDialogComponent);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('entries', [favorite('L’Amie prodigieuse'), favorite('Dune')]);
    fixture.detectChanges();
  });

  it('starts with email consent unchecked and emits the selected value before registration', () => {
    const consent = fixture.nativeElement.querySelector('[data-zone="E4-consentement-email"] input') as HTMLInputElement;
    expect(consent.checked).toBeFalse();
    expect(fixture.nativeElement.querySelector('[data-zone="E4-recap-liste"]')?.textContent)
      .toContain('L’Amie prodigieuse');

    const register = jasmine.createSpy('register');
    fixture.componentInstance.registerRequested.subscribe(register);
    (fixture.nativeElement.querySelector('[data-zone="E4-bouton-continuer"]') as HTMLButtonElement).click();
    expect(register).toHaveBeenCalledOnceWith(false);

    consent.checked = true;
    consent.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-zone="E4-bouton-continuer"]') as HTMLButtonElement).click();
    expect(register).toHaveBeenCalledWith(true);
  });
});

function favorite(title: string): LocalFavoriteEntry {
  return {
    isbn13: title === 'Dune' ? '9782266000001' : '9782070612758',
    title,
    authors: 'Autrice',
    coverUrl: null,
    addedAt: '2026-11-12T10:00:00Z',
  };
}
