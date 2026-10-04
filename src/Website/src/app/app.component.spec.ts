import {NO_ERRORS_SCHEMA} from '@angular/core';
import {DOCUMENT} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {NavigationCancel, NavigationEnd, NavigationError, NavigationStart, Router} from '@angular/router';
import {Subject} from 'rxjs';

import {DesignSystemModule} from '@vpd/ui';
import {AppComponent} from './app.component';

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;
  let routerEvents: Subject<NavigationStart | NavigationEnd | NavigationCancel | NavigationError>;
  let routerState: {events: Router['events']; navigated: boolean; url: string};
  let document: Document;

  beforeEach(async () => {
    routerEvents = new Subject<NavigationStart | NavigationEnd | NavigationCancel | NavigationError>();
    routerState = {events: routerEvents.asObservable(), navigated: false, url: '/'};

    await TestBed.configureTestingModule({
      declarations: [AppComponent],
      imports: [DesignSystemModule],
      providers: [{
        provide: Router,
        useValue: routerState,
      }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    document = TestBed.inject(DOCUMENT);
  });

  afterEach(() => {
    document.head.querySelectorAll('link[rel="canonical"]').forEach(link => link.remove());
  });

  it('shows the branded line loader only while a route transition is running', () => {
    fixture.detectChanges();
    routerEvents.next(new NavigationStart(1, '/actualite'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-loader="line"]')).not.toBeNull();

    routerEvents.next(new NavigationEnd(1, '/actualite', '/actualite'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-loader="line"]')).toBeNull();
  });

  it('Given_InitialNavigationCompleted_When_Initialized_Then_UsesCurrentRoute', () => {
    routerState.navigated = true;
    routerState.url = '/maxence/histoire';

    fixture.detectChanges();

    expect(canonical()?.href).toBe('https://volepapillondamour.fr/maxence/histoire');
  });

  it('Given_InitialNavigationPending_When_Initialized_Then_WaitsForResolvedRoute', () => {
    fixture.detectChanges();

    expect(canonical()).toBeNull();
  });

  it('Given_Redirection_When_NavigationEnds_Then_UsesRedirectDestination', () => {
    fixture.detectChanges();

    routerEvents.next(new NavigationEnd(1, '/association', '/association/presentation'));

    expect(canonical()?.href).toBe('https://volepapillondamour.fr/association/presentation');
  });

  [
    '/actualite/3e85f3a6-1f26-48c1-8371-106d1e179624',
    '/evenement/69e14da9-ccc3-43ce-859f-3ce838bd655c',
    '/accessibilite',
  ].forEach(path => {
    it(`Given_ContentRoute_${path}_When_NavigationEnds_Then_PreservesPagePath`, () => {
      fixture.detectChanges();

      routerEvents.next(new NavigationEnd(1, path, path));

      expect(canonical()?.href).toBe(`https://volepapillondamour.fr${path}`);
    });
  });

  it('Given_TrackingAndFragment_When_NavigationEnds_Then_RemovesBothFromCanonical', () => {
    fixture.detectChanges();
    const url = '/maxence/histoire?utm_source=facebook&fbclid=tracking#enfance';

    routerEvents.next(new NavigationEnd(1, url, url));

    expect(canonical()?.href).toBe('https://volepapillondamour.fr/maxence/histoire');
  });

  it('Given_TrailingSlash_When_NavigationEnds_Then_NormalizesPagePath', () => {
    fixture.detectChanges();

    routerEvents.next(new NavigationEnd(1, '/accessibilite/', '/accessibilite/'));

    expect(canonical()?.href).toBe('https://volepapillondamour.fr/accessibilite');
  });

  it('Given_RootRoute_When_NavigationEnds_Then_CanonicalPointsToHome', () => {
    fixture.detectChanges();

    routerEvents.next(new NavigationEnd(1, '/', '/'));

    expect(canonical()?.href).toBe('https://volepapillondamour.fr/accueil');
  });

  it('Given_PrerenderedCanonical_When_RoutesChange_Then_ReusesTheSingleLink', () => {
    const existing = document.createElement('link');
    existing.rel = 'canonical';
    existing.href = 'https://www.volepapillondamour.fr/accueil';
    document.head.appendChild(existing);
    fixture.detectChanges();

    routerEvents.next(new NavigationEnd(1, '/maxence/histoire', '/maxence/histoire'));
    routerEvents.next(new NavigationEnd(2, '/accessibilite', '/accessibilite'));

    expect(document.head.querySelectorAll('link[rel="canonical"]').length).toBe(1);
    expect(canonical()).toBe(existing);
    expect(existing.href).toBe('https://volepapillondamour.fr/accessibilite');
  });

  it('Given_FailedNavigation_When_CancelledOrErrored_Then_KeepsLastSuccessfulCanonical', () => {
    fixture.detectChanges();
    routerEvents.next(new NavigationEnd(1, '/accessibilite', '/accessibilite'));

    routerEvents.next(new NavigationStart(2, '/contact'));
    routerEvents.next(new NavigationCancel(2, '/contact', 'cancelled'));
    routerEvents.next(new NavigationStart(3, '/association'));
    routerEvents.next(new NavigationError(3, '/association', new Error('failed')));

    expect(canonical()?.href).toBe('https://volepapillondamour.fr/accessibilite');
    expect(fixture.componentInstance.isNavigating()).toBeFalse();
  });

  it('Given_DestroyedComponent_When_RouterEmits_Then_LeavesCanonicalUnchanged', () => {
    fixture.detectChanges();
    routerEvents.next(new NavigationEnd(1, '/accessibilite', '/accessibilite'));
    fixture.destroy();

    routerEvents.next(new NavigationEnd(2, '/contact', '/contact'));

    expect(canonical()?.href).toBe('https://volepapillondamour.fr/accessibilite');
  });

  function canonical(): HTMLLinkElement | null {
    return document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
  }
});
