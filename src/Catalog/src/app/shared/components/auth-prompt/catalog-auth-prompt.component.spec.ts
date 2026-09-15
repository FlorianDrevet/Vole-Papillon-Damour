import {provideZonelessChangeDetection} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';

import {CatalogAuthPromptComponent} from './catalog-auth-prompt.component';

describe('CatalogAuthPromptComponent', () => {
  let fixture: ComponentFixture<CatalogAuthPromptComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [CatalogAuthPromptComponent],
      providers: [provideZonelessChangeDetection()],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogAuthPromptComponent);
  });

  it('renders nothing when closed', () => {
    fixture.componentInstance.open = false;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="dialog"]')).toBeNull();
  });

  it('explains why an account is needed and what it unlocks', () => {
    fixture.componentInstance.open = true;
    fixture.detectChanges();

    const content = fixture.nativeElement.textContent.replace(/\s+/g, ' ').trim();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).not.toBeNull();
    expect(content).toContain('liste de suivi');
    expect(content).toContain('Se connecter');
    expect(content).toContain('Créer un compte');
  });

  it('emits loginRequested when the login action is used', () => {
    fixture.componentInstance.open = true;
    fixture.detectChanges();
    const emitted = jasmine.createSpy('loginRequested');
    fixture.componentInstance.loginRequested.subscribe(emitted);

    (fixture.nativeElement.querySelector('[data-testid="auth-prompt-login"]') as HTMLButtonElement).click();

    expect(emitted).toHaveBeenCalled();
  });

  it('emits registerRequested when the registration action is used', () => {
    fixture.componentInstance.open = true;
    fixture.detectChanges();
    const emitted = jasmine.createSpy('registerRequested');
    fixture.componentInstance.registerRequested.subscribe(emitted);

    (fixture.nativeElement.querySelector('[data-testid="auth-prompt-register"]') as HTMLButtonElement).click();

    expect(emitted).toHaveBeenCalled();
  });

  it('emits closed when dismissed without choosing an action', () => {
    fixture.componentInstance.open = true;
    fixture.detectChanges();
    const emitted = jasmine.createSpy('closed');
    fixture.componentInstance.closed.subscribe(emitted);

    (fixture.nativeElement.querySelector('.auth-prompt-close') as HTMLButtonElement).click();

    expect(emitted).toHaveBeenCalled();
  });

  it('emits closed when the overlay backdrop is clicked', () => {
    fixture.componentInstance.open = true;
    fixture.detectChanges();
    const emitted = jasmine.createSpy('closed');
    fixture.componentInstance.closed.subscribe(emitted);

    (fixture.nativeElement.querySelector('.auth-prompt-overlay') as HTMLElement).click();

    expect(emitted).toHaveBeenCalled();
  });
});
