import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VpdAllEventsComponent } from './vpd-all-events.component';
import { VpdEventsFacadeService } from '../../shared/facades/vpd-events.facade.service';

describe('VpdAllEventsComponent', () => {
  let fixture: ComponentFixture<VpdAllEventsComponent>;
  let eventsFacadeSpy: jasmine.SpyObj<VpdEventsFacadeService>;

  beforeEach(async () => {
    eventsFacadeSpy = jasmine.createSpyObj<VpdEventsFacadeService>('VpdEventsFacadeService', ['getAllEvents$']);

    await TestBed.configureTestingModule({
      declarations: [VpdAllEventsComponent],
      providers: [{ provide: VpdEventsFacadeService, useValue: eventsFacadeSpy }],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();
  });

  it('should render event row skeletons while the agenda is loading', () => {
    eventsFacadeSpy.getAllEvents$.and.returnValue(new Promise(() => undefined));

    fixture = TestBed.createComponent(VpdAllEventsComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.vpd-all-event-skeleton').length).toBe(3);
  });
});
