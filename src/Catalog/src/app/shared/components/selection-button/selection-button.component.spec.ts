import {signal, WritableSignal} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {Router} from '@angular/router';

import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {CatalogSelectionService} from '../../../core/selection/catalog-selection.service';
import {CatalogAuthPromptComponent} from '../auth-prompt/catalog-auth-prompt.component';
import {SelectionButtonComponent} from './selection-button.component';

describe('SelectionButtonComponent', () => {
  const ref = {kind: 'edition' as const, isbn13: '9782070612758'};
  let fixture: ComponentFixture<SelectionButtonComponent>;
  let keys: WritableSignal<ReadonlySet<string>>;
  let selection: jasmine.SpyObj<CatalogSelectionService>;
  let auth: jasmine.SpyObj<CatalogAuthService>;

  beforeEach(async () => {
    keys = signal(new Set<string>());
    selection = jasmine.createSpyObj<CatalogSelectionService>(
      'CatalogSelectionService',
      ['add', 'remove'],
      {keys, mode: signal<'local' | 'synced' | 'local-unsynced'>('local')},
    );
    selection.add.and.callFake(async () => {
      keys.set(new Set(['edition:9782070612758']));
    });
    selection.remove.and.callFake(async () => {
      keys.set(new Set());
    });
    auth = jasmine.createSpyObj<CatalogAuthService>('CatalogAuthService', ['login']);
    auth.login.and.resolveTo();

    await TestBed.configureTestingModule({
      declarations: [SelectionButtonComponent, CatalogAuthPromptComponent],
      providers: [
        {provide: CatalogSelectionService, useValue: selection},
        {provide: CatalogAuthService, useValue: auth},
        {provide: Router, useValue: {url: '/livre/le-petit-prince'}},
      ],
    }).compileComponents();
  });

  function render(): ComponentFixture<SelectionButtonComponent> {
    fixture = TestBed.createComponent(SelectionButtonComponent);
    fixture.componentRef.setInput('ref', ref);
    fixture.componentRef.setInput('title', 'Le Petit Prince');
    fixture.detectChanges();
    return fixture;
  }

  it('adds then shows the in-selection state with a remove action', async () => {
    const page = render();
    (page.nativeElement.querySelector('.selection-add-button') as HTMLButtonElement).click();
    await page.whenStable();
    page.detectChanges();

    expect(selection.add).toHaveBeenCalledWith(ref, 'Le Petit Prince');
    expect(page.nativeElement.textContent).toContain('Dans Ma sélection');
    expect(page.nativeElement.querySelector('.selection-remove-button')).not.toBeNull();
  });

  it('never uses reservation wording in button labels', () => {
    const page = render();
    const labels = Array.from(page.nativeElement.querySelectorAll('button'))
      .map(button => (button as HTMLButtonElement).textContent!.toLowerCase());

    for (const forbidden of ['panier', 'réserv', 'mettre de côté', 'commande']) {
      expect(labels.join(' ')).not.toContain(forbidden);
    }
  });

  it('opens the sign-in invitation only from its link and lets the visitor keep the local selection', async () => {
    keys.set(new Set(['edition:9782070612758']));
    const page = render();

    expect(page.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(page.nativeElement.textContent).toContain('Enregistrée sur cet appareil uniquement.');
    (page.nativeElement.querySelector('.selection-login-link') as HTMLAnchorElement).click();
    page.detectChanges();

    const dialog = page.nativeElement.querySelector('[role="dialog"]') as HTMLElement;
    expect(dialog.textContent).toContain('Retrouver cette sélection sur tous vos appareils ?');
    expect(dialog.textContent).toContain('Connectez-vous seulement si vous voulez le synchroniser avec votre compte.');
    (dialog.querySelector('[data-testid="auth-prompt-register"]') as HTMLButtonElement).click();
    page.detectChanges();

    expect(page.nativeElement.querySelector('[role="dialog"]')).toBeNull();
    expect(keys().has('edition:9782070612758')).toBeTrue();
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('starts sign-in from the invitation and returns to the current page', async () => {
    keys.set(new Set(['edition:9782070612758']));
    const page = render();
    (page.nativeElement.querySelector('.selection-login-link') as HTMLAnchorElement).click();
    page.detectChanges();
    (page.nativeElement.querySelector('[data-testid="auth-prompt-login"]') as HTMLButtonElement).click();
    await page.whenStable();

    expect(auth.login).toHaveBeenCalledWith('/livre/le-petit-prince');
  });

  it('shows an alert and keeps state when adding fails', async () => {
    selection.add.and.rejectWith(new Error('boom'));
    const page = render();
    (page.nativeElement.querySelector('.selection-add-button') as HTMLButtonElement).click();
    await page.whenStable();
    page.detectChanges();

    expect(page.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    expect(page.nativeElement.textContent).toContain('Ajouter à Ma sélection');
    expect(page.nativeElement.textContent).not.toContain('Dans Ma sélection');
  });

  it('disables the action and announces busy while an add is pending', async () => {
    let complete!: () => void;
    selection.add.and.returnValue(new Promise<void>(resolve => { complete = resolve; }));
    const page = render();
    (page.nativeElement.querySelector('.selection-add-button') as HTMLButtonElement).click();
    page.detectChanges();

    expect((page.nativeElement.querySelector('.selection-add-button') as HTMLButtonElement).disabled).toBeTrue();
    expect(page.nativeElement.querySelector('[aria-busy="true"]')).not.toBeNull();
    complete();
    await page.whenStable();
  });

  it('removes an item without switching it to another state', async () => {
    keys.set(new Set(['edition:9782070612758']));
    const page = render();
    (page.nativeElement.querySelector('.selection-remove-button') as HTMLButtonElement).click();
    await page.whenStable();
    page.detectChanges();

    expect(selection.remove).toHaveBeenCalledWith(ref);
    expect(keys().has('edition:9782070612758')).toBeFalse();
  });
});
