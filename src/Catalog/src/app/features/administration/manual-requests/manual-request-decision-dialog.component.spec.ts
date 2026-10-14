import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';

import {CatalogManualRequestItem} from '../../../core/catalog.models';
import {ManualRequestDecisionDialogComponent} from './manual-request-decision-dialog.component';

describe('ManualRequestDecisionDialogComponent', () => {
  let fixture: ComponentFixture<ManualRequestDecisionDialogComponent>;

  const item: CatalogManualRequestItem = {
    requestId: 'request-1', requesterId: 'requester-1', requesterLastName: 'Martin', requesterFirstName: 'Jeanne',
    requesterPhone: '0612345678', requesterErased: false, scope: 'Work', workId: 'OL123W', isbn13: '9782070612758',
    title: 'Le Petit Prince', authors: 'Antoine de Saint-Exupéry', publisher: 'Gallimard', publicationYear: 2007,
    requestedAt: '2026-09-03T09:00:00Z', status: 'Found', foundOrigin: 'Scan', foundIsbn13: '9782070584628',
    foundPublisher: 'Folio', foundPublicationYear: 1999, foundAt: '2026-10-10T10:00:00Z', foundByName: 'Alice',
    quantityAvailable: 0, quantityAnnounced: 0, watcherCount: 3, releaseMode: 'NextFair', releaseFairStart: '2027-03-14',
    closedAt: null, closedByName: null,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ManualRequestDecisionDialogComponent],
      imports: [FormsModule],
    }).compileComponents();

    fixture = TestBed.createComponent(ManualRequestDecisionDialogComponent);
    fixture.componentRef.setInput('item', item);
    fixture.componentRef.setInput('requesterFirstName', 'Jeanne');
    fixture.componentRef.setInput('requesterLastName', 'Martin');
  });

  it('shows the given confirmation and emits when confirmed', () => {
    fixture.componentRef.setInput('mode', 'given');
    fixture.detectChanges();
    const confirmed = jasmine.createSpy('confirmed');
    fixture.componentInstance.confirmed.subscribe(confirmed);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Le Petit Prince a été remis à Jeanne MARTIN ?');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain("L'exemplaire sort définitivement du stock de la bourse.");
    (fixture.nativeElement.querySelector('[data-testid="manual-request-decision-confirm"]') as HTMLButtonElement).click();

    expect(confirmed).toHaveBeenCalledWith({keepSearching: false});
  });

  it('shows release consequences and emits the keep-searching choice on refusal', () => {
    fixture.componentRef.setInput('mode', 'refuse');
    fixture.detectChanges();
    const confirmed = jasmine.createSpy('confirmed');
    fixture.componentInstance.confirmed.subscribe(confirmed);

    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).toContain('Jeanne MARTIN ne prend pas Le Petit Prince');
    expect(root.textContent).toContain('3 membres qui le suivent seront prévenus par e-mail dès maintenant.');
    expect(root.textContent).toContain('Annoncé · bourse du 14 mars');
    expect(root.textContent).toContain('Mode de la session de scan du 10 oct. 2026. Non modifiable.');
    (root.querySelector('[data-testid="manual-request-keep-searching"]') as HTMLInputElement).click();
    (root.querySelector('[data-testid="manual-request-decision-confirm"]') as HTMLButtonElement).click();

    expect(confirmed).toHaveBeenCalledWith({keepSearching: true});
  });

  it('explains canceling a found request and offers to keep it', () => {
    fixture.componentRef.setInput('mode', 'cancel-found');
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.textContent).toContain('Annuler la demande de Jeanne MARTIN ?');
    expect(root.textContent).toContain('Le livre a déjà été mis de côté.');
    expect(root.textContent).toContain('3 membres qui le suivent seront prévenus dès maintenant.');
    expect(root.querySelector('[data-testid="manual-request-decision-close"]')).not.toBeNull();
    expect(root.querySelector('[data-testid="manual-request-decision-confirm"]')?.textContent).toContain('Annuler la demande');
  });

  it('closes on Escape and keeps focus inside the dialog when tabbing', () => {
    fixture.componentRef.setInput('mode', 'given');
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const dialog = root.querySelector<HTMLElement>('[role="dialog"]')!;
    const close = root.querySelector<HTMLButtonElement>('[data-testid="manual-request-decision-close"]')!;
    const confirmed = jasmine.createSpy('confirmed');
    fixture.componentInstance.closed.subscribe(confirmed);
    document.body.focus();

    const tab = new KeyboardEvent('keydown', {key: 'Tab', cancelable: true});
    fixture.componentInstance.onDocumentKeydown(tab);
    expect(tab.defaultPrevented).toBeTrue();
    expect(dialog.contains(document.activeElement)).toBeTrue();

    const escape = new KeyboardEvent('keydown', {key: 'Escape', cancelable: true});
    fixture.componentInstance.onDocumentKeydown(escape);
    expect(escape.defaultPrevented).toBeTrue();
    expect(confirmed).toHaveBeenCalled();
    expect(close).toBeTruthy();
  });

  it('returns focus to the action that opened the dialog when it closes', async () => {
    const trigger = document.createElement('button');
    document.body.appendChild(trigger);
    trigger.focus();
    fixture.componentRef.setInput('mode', 'given');
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="dialog"]').contains(document.activeElement)).toBeTrue();
    fixture.destroy();
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(document.activeElement).toBe(trigger);
    trigger.remove();
  });
});
