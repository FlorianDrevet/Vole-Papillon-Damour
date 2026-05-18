import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VpdEventSections } from './vpd-event-sections';
import { AxiosService } from '../../../../shared/services/axios.service';
import { MethodEnum } from '../../../../shared/enums/method.enum';

describe('VpdEventSections', () => {
  let component: VpdEventSections;
  let fixture: ComponentFixture<VpdEventSections>;
  let axiosServiceSpy: jasmine.SpyObj<AxiosService>;

  const createEventResponse = (eventType: string) => ({
    date: '2026-05-12T10:00:00.000Z',
    eventType
  });

  const configureRequests = (failingUrl: string) => {
    axiosServiceSpy.request.and.callFake((method: MethodEnum, url: string) => {
      if (method !== MethodEnum.GET) {
        return Promise.reject(new Error('Unexpected method'));
      }

      if (url === failingUrl) {
        return Promise.reject(new Error(`Request failed for ${url}`));
      }

      switch (url) {
        case '/asso-events/next-bingo':
          return Promise.resolve(createEventResponse('Bingo'));
        case '/asso-events/next-books':
          return Promise.resolve(createEventResponse('Books'));
        case '/asso-events/next-other-event':
          return Promise.resolve([createEventResponse('Other')]);
        default:
          return Promise.reject(new Error(`Unexpected url ${url}`));
      }
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

  it('should keep lotoCard null when next bingo request fails', async () => {
    configureRequests('/asso-events/next-bingo');

    await createComponent();

    expect(component.lotoCard()).toBeNull();
  });

  it('should keep balCard null when next books request fails', async () => {
    configureRequests('/asso-events/next-books');

    await createComponent();

    expect(component.balCard()).toBeNull();
  });

  it('should keep otherCard empty when next other event request fails', async () => {
    configureRequests('/asso-events/next-other-event');

    await createComponent();

    expect(component.otherCard()).toEqual([]);
  });
});