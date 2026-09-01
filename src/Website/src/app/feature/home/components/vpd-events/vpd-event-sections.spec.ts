import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VpdEventSections } from './vpd-event-sections';
import { AxiosService } from '../../../../shared/services/axios.service';
import { MethodEnum } from '../../../../shared/enums/method.enum';
import { VpdEventEnum } from '../../../../shared/enums/vpdEvent.enum';

describe('VpdEventSections', () => {
  let component: VpdEventSections;
  let fixture: ComponentFixture<VpdEventSections>;
  let axiosServiceSpy: jasmine.SpyObj<AxiosService>;

  const createEventResponse = (id: string, eventType: string, dateStart: string) => ({
    id,
    eventType,
    dateStart,
    dateEnd: null,
    hourOpenDoors: null,
    name: `Évènement ${id}`,
    city: 'Verrières'
  });

  const configureRequests = (events: unknown) => {
    axiosServiceSpy.request.and.callFake((method: MethodEnum, url: string) => {
      if (method !== MethodEnum.GET || url !== '/asso-events') {
        return Promise.reject(new Error(`Unexpected request ${method} ${url}`));
      }

      return Promise.resolve(events);
    });
  };

  const createComponent = async () => {
    fixture = TestBed.createComponent(VpdEventSections);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    axiosServiceSpy = jasmine.createSpyObj<AxiosService>('AxiosService', ['request']);

    await TestBed.configureTestingModule({
      declarations: [VpdEventSections],
      providers: [
        { provide: AxiosService, useValue: axiosServiceSpy }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();
  });

  it('should keep the three closest events, whatever their type', async () => {
    configureRequests([
      createEventResponse('books-1', 'Books', '2026-06-10T10:00:00.000Z'),
      createEventResponse('bingo-1', 'Bingo', '2026-05-12T10:00:00.000Z'),
      createEventResponse('bingo-2', 'Bingo', '2026-05-20T10:00:00.000Z'),
      createEventResponse('bingo-3', 'Bingo', '2026-05-28T10:00:00.000Z')
    ]);

    await createComponent();

    expect(component.upcomingEvents().map(event => event.id)).toEqual(['bingo-1', 'bingo-2', 'bingo-3']);
  });

  it('should map dates and event type of the displayed events', async () => {
    configureRequests([createEventResponse('bingo-1', 'Bingo', '2026-05-12T10:00:00.000Z')]);

    await createComponent();

    const event = component.upcomingEvents()[0];
    expect(event.dateStart instanceof Date).toBeTrue();
    expect(event.eventType).toBe(VpdEventEnum.Bingo);
  });

  it('should keep upcomingEvents empty when the request fails', async () => {
    axiosServiceSpy.request.and.returnValue(Promise.reject(new Error('Request failed')));

    await createComponent();

    expect(component.upcomingEvents()).toEqual([]);
  });

  it('should render three skeleton cards while upcoming events are loading', async () => {
    let resolveRequest!: (events: unknown[]) => void;
    axiosServiceSpy.request.and.returnValue(new Promise(resolve => {
      resolveRequest = resolve;
    }));

    fixture = TestBed.createComponent(VpdEventSections);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.vpd-upcoming-event-skeleton').length).toBe(3);

    resolveRequest([]);
    await fixture.whenStable();
  });
});
