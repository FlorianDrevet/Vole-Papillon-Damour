import {NO_ERRORS_SCHEMA} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {NavigationEnd, NavigationStart, Router} from '@angular/router';
import {Subject} from 'rxjs';

import {DesignSystemModule} from '@vpd/ui';
import {AppComponent} from './app.component';

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;
  let routerEvents: Subject<NavigationStart | NavigationEnd>;

  beforeEach(async () => {
    routerEvents = new Subject<NavigationStart | NavigationEnd>();

    await TestBed.configureTestingModule({
      declarations: [AppComponent],
      imports: [DesignSystemModule],
      providers: [{
        provide: Router,
        useValue: {events: routerEvents.asObservable()},
      }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
  });

  it('shows the branded line loader only while a route transition is running', () => {
    routerEvents.next(new NavigationStart(1, '/actualite'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-loader="line"]')).not.toBeNull();

    routerEvents.next(new NavigationEnd(1, '/actualite', '/actualite'));
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[data-loader="line"]')).toBeNull();
  });
});
