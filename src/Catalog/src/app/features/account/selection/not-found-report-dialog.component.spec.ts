import {ComponentFixture, TestBed} from '@angular/core/testing';

import {NotFoundReportDialogComponent} from './not-found-report-dialog.component';

describe('NotFoundReportDialogComponent', () => {
  let fixture: ComponentFixture<NotFoundReportDialogComponent>;

  const item = {
    title: 'Le Horla',
    authors: 'Guy de Maupassant',
    publisher: 'Folio',
    publicationYear: 2004,
    physicalFormat: 'Poche',
    coverUrl: null,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [NotFoundReportDialogComponent],
    }).compileComponents();
  });

  function render(mode: 'report' | 'login' = 'report', sending = false): ComponentFixture<NotFoundReportDialogComponent> {
    fixture = TestBed.createComponent(NotFoundReportDialogComponent);
    fixture.componentRef.setInput('item', item);
    fixture.componentRef.setInput('mode', mode);
    fixture.componentRef.setInput('sending', sending);
    fixture.detectChanges();
    return fixture;
  }

  function submitButton(): HTMLButtonElement {
    return fixture.nativeElement.querySelector('[data-testid="not-found-report-submit"]') as HTMLButtonElement;
  }

  it('emits trimmed comment and selected location', () => {
    const page = render();
    const submitted = spyOn(page.componentInstance.submitted, 'emit');
    const comment = page.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    comment.value = '  Rayon polar vide  ';
    comment.dispatchEvent(new Event('input', {bubbles: true}));
    (page.nativeElement.querySelector('input[value="Premises"]') as HTMLInputElement).click();
    submitButton().click();

    expect(submitted).toHaveBeenCalledWith({location: 'Premises', comment: 'Rayon polar vide'});
  });

  it('emits null location when none chosen', () => {
    const page = render();
    const submitted = spyOn(page.componentInstance.submitted, 'emit');

    submitButton().click();

    expect(submitted).toHaveBeenCalledWith({location: null, comment: null});
  });

  it('limits the comment to 280 characters', () => {
    const page = render();
    const comment = page.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    comment.value = 'x'.repeat(300);
    comment.dispatchEvent(new Event('input', {bubbles: true}));
    page.detectChanges();

    expect(comment.value.length).toBe(280);
    expect(page.nativeElement.textContent).toContain('280 / 280 caractères');
  });

  it('disables submit while sending', () => {
    const page = render('report', true);

    expect(submitButton().disabled).toBeTrue();
  });

  it('closes on Escape and restores focus', async () => {
    const trigger = document.createElement('button');
    document.body.append(trigger);
    trigger.focus();
    const page = render();
    const closed = spyOn(page.componentInstance.closed, 'emit');
    await page.whenStable();

    expect(document.activeElement).toBe(page.nativeElement.querySelector('[aria-label="Fermer"]'));
    const tabBackwards = new KeyboardEvent('keydown', {key: 'Tab', shiftKey: true, bubbles: true, cancelable: true});
    document.dispatchEvent(tabBackwards);
    expect(document.activeElement).toBe(submitButton());
    document.dispatchEvent(new KeyboardEvent('keydown', {key: 'Tab', bubbles: true, cancelable: true}));
    expect(document.activeElement).toBe(page.nativeElement.querySelector('[aria-label="Fermer"]'));
    document.dispatchEvent(new KeyboardEvent('keydown', {key: 'Escape', bubbles: true}));
    page.detectChanges();
    await page.whenStable();

    expect(closed).toHaveBeenCalled();
    expect(document.activeElement).toBe(trigger);
    page.destroy();
    trigger.remove();
  });

  it('login mode requests login', () => {
    const page = render('login');
    const loginRequested = spyOn(page.componentInstance.loginRequested, 'emit');

    (page.nativeElement.querySelector('[data-testid="not-found-report-login"]') as HTMLButtonElement).click();

    expect(loginRequested).toHaveBeenCalled();
  });
});
