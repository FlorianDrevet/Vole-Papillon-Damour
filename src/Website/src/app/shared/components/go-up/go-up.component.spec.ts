import { ComponentFixture, TestBed } from '@angular/core/testing';

import { GoUpComponent } from './go-up.component';

describe('GoUpComponent', () => {
  let fixture: ComponentFixture<GoUpComponent>;
  let scrollY: number;

  beforeEach(async () => {
    scrollY = 0;
    spyOnProperty(window, 'scrollY', 'get').and.callFake(() => scrollY);
    await TestBed.configureTestingModule({ declarations: [GoUpComponent] }).compileComponents();
    fixture = TestBed.createComponent(GoUpComponent);
  });

  it('keeps the back-to-top action out of the tab order at the top of the page', () => {
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    expect(button.tabIndex).toBe(-1);
    expect(button.getAttribute('aria-hidden')).toBe('true');
  });

  it('reveals the action after the page has been scrolled', () => {
    fixture.detectChanges();
    scrollY = 600;
    window.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;

    expect(button.tabIndex).toBe(0);
    expect(button.getAttribute('aria-hidden')).toBeNull();
  });
});
