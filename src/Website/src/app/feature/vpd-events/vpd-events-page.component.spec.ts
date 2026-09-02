import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VpdEventsPageComponent } from './vpd-events-page.component';
import { VpdEventsFacadeService } from '../../shared/facades/vpd-events.facade.service';

describe('VpdEventsPageComponent', () => {
  let fixture: ComponentFixture<VpdEventsPageComponent>;
  let eventsFacadeSpy: jasmine.SpyObj<VpdEventsFacadeService>;

  beforeEach(async () => {
    eventsFacadeSpy = jasmine.createSpyObj<VpdEventsFacadeService>('VpdEventsFacadeService', ['getAllEvents$']);

    await TestBed.configureTestingModule({
      declarations: [VpdEventsPageComponent],
      providers: [{ provide: VpdEventsFacadeService, useValue: eventsFacadeSpy }],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();
  });

  it('should keep the event page structure visible with skeletons while events are loading', () => {
    eventsFacadeSpy.getAllEvents$.and.returnValue(new Promise(() => undefined));

    fixture = TestBed.createComponent(VpdEventsPageComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.vpd-events-other-skeleton').length).toBe(3);
    expect(fixture.nativeElement.querySelectorAll('.vpd-event-date-skeleton').length).toBe(2);
  });
});
