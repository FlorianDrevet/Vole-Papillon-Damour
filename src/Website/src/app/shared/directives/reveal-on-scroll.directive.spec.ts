import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RevealOnScrollDirective } from './reveal-on-scroll.directive';

@Component({
  template: '<section appRevealOnScroll data-reveal-target></section>',
  standalone: false,
})
class RevealHostComponent {}

describe('RevealOnScrollDirective', () => {
  let fixture: ComponentFixture<RevealHostComponent>;
  let callback: IntersectionObserverCallback | undefined;
  let observer: IntersectionObserver;
  let previousObserver: typeof IntersectionObserver;

  beforeEach(async () => {
    previousObserver = window.IntersectionObserver;
    const observerMock = {
      observe: jasmine.createSpy('observe'),
      unobserve: jasmine.createSpy('unobserve'),
      disconnect: jasmine.createSpy('disconnect'),
      takeRecords: () => [],
    } as unknown as IntersectionObserver;
    observer = observerMock;

    function MockIntersectionObserver(intersectionCallback: IntersectionObserverCallback): IntersectionObserver {
      callback = intersectionCallback;
      return observerMock;
    }
    Object.defineProperty(window, 'IntersectionObserver', {
      configurable: true,
      value: MockIntersectionObserver as unknown as typeof IntersectionObserver,
    });

    await TestBed.configureTestingModule({
      declarations: [RevealHostComponent, RevealOnScrollDirective],
    }).compileComponents();
    fixture = TestBed.createComponent(RevealHostComponent);
  });

  afterEach(() => {
    Object.defineProperty(window, 'IntersectionObserver', { configurable: true, value: previousObserver });
  });

  it('reveals the element once it enters the viewport', () => {
    fixture.detectChanges();
    const target = fixture.nativeElement.querySelector('[data-reveal-target]') as HTMLElement;

    expect(target.getAttribute('data-reveal-state')).toBe('pending');

    callback?.([{ isIntersecting: true, target } as unknown as IntersectionObserverEntry], observer);

    expect(target.getAttribute('data-reveal-state')).toBe('visible');
    expect((observer as unknown as { unobserve: jasmine.Spy }).unobserve).toHaveBeenCalledWith(target);
  });

  it('does not hide content when reduced motion is enabled', () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: true } as MediaQueryList);
    fixture.detectChanges();

    const target = fixture.nativeElement.querySelector('[data-reveal-target]') as HTMLElement;

    expect(target.hasAttribute('data-reveal-state')).toBeFalse();
  });
});
