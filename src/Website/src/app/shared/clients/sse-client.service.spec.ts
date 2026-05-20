import {PLATFORM_ID} from '@angular/core';
import {TestBed} from '@angular/core/testing';

import {SseClientService} from './sse-client.service';
import {NumberLineEnum} from '../enums/numberLine.enum';
import {PartieTypeEnum} from '../enums/partieType.enum';
import {VpdEventEnum} from '../enums/vpdEvent.enum';
import {VpdEventModel} from '../models/vpdEvent.model';

class FakeEventSource {
  static readonly instances: FakeEventSource[] = [];

  onmessage: ((event: MessageEvent<string>) => void) | null = null;
  onopen: (() => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  close = jasmine.createSpy('close');

  constructor(public readonly url: string) {
    FakeEventSource.instances.push(this);
  }

  emitMessage(data: string): void {
    this.onmessage?.({data} as MessageEvent<string>);
  }

  emitError(): void {
    this.onerror?.(new Event('error'));
  }
}

const liveEvent: VpdEventModel = {
  eventType: VpdEventEnum.Bingo,
  id: 'event-1',
  name: 'Live loto',
  description: 'Live event',
  dateStart: new Date('2026-05-19T20:00:00Z'),
  dateEnd: null,
  hourOpenDoors: null,
  hourCloseDoors: null,
  urlImageMap: null,
  urlRegistration: null,
  urlImage: 'https://example.com/loto.jpg',
  city: 'Arras',
  road: 'Rue du loto',
  cityCode: '62000',
  roadNumber: '12',
  currentPartieIndex: 0,
  bingoHasBeenWon: false,
  bingoNumeros: [25],
  parties: [
    {
      id: 'partie-1',
      name: 'Partie 1',
      partieType: PartieTypeEnum.STANDARD,
      index: 0,
      pauseAfter: false,
      addedBingoNumber: 25,
      lastNumeros: [25],
      liveNumeros: [25],
      currentLineIndex: 0,
      lineParties: [
        {
          id: 'line-1',
          index: 0,
          numberLine: NumberLineEnum.ONELINE,
          lots: [
            {
              id: 'lot-1',
              name: 'Lot 1',
              urlImage: 'lot.jpg',
              index: 0,
              isWon: null
            }
          ]
        }
      ]
    }
  ]
};

describe('SseClientService', () => {
  const originalEventSource = globalThis.EventSource;
  let consoleErrorSpy: jasmine.Spy;

  beforeEach(() => {
    FakeEventSource.instances.length = 0;
    consoleErrorSpy = spyOn(console, 'error');
    (globalThis as unknown as { EventSource: typeof FakeEventSource }).EventSource = FakeEventSource;
  });

  afterEach(() => {
    globalThis.EventSource = originalEventSource;
    TestBed.resetTestingModule();
  });

  it('should create an event-scoped EventSource and update the event signal when a valid message arrives', () => {
    const service = createService('browser');

    service.init('event-1');
    FakeEventSource.instances[0].emitMessage(JSON.stringify(liveEvent));

    expect(FakeEventSource.instances[0].url).toContain('/asso-events/event-1/tableau/sse');
    expect(service.eventAsso()?.id).toBe(liveEvent.id);
    expect(service.eventAsso()?.parties[0].liveNumeros).toEqual([25]);
  });

  it('should keep the previous event state when a malformed message arrives', () => {
    const service = createService('browser');

    service.init('event-1');
    FakeEventSource.instances[0].emitMessage(JSON.stringify(liveEvent));
    FakeEventSource.instances[0].emitMessage('{malformed-json');

    expect(service.eventAsso()?.id).toBe(liveEvent.id);
    expect(service.eventAsso()?.parties[0].lastNumeros).toEqual([25]);
    expect(consoleErrorSpy).toHaveBeenCalled();
  });

  it('should reconnect with backoff after an EventSource error', () => {
    jasmine.clock().install();
    const service = createService('browser');

    try {
      service.init('event-1');
      const firstConnection = FakeEventSource.instances[0];
      firstConnection.emitError();

      expect(firstConnection.close).toHaveBeenCalled();
      expect(FakeEventSource.instances.length).toBe(1);

      jasmine.clock().tick(249);
      expect(FakeEventSource.instances.length).toBe(1);

      jasmine.clock().tick(1);
      expect(FakeEventSource.instances.length).toBe(2);

      const secondConnection = FakeEventSource.instances[1];
      secondConnection.emitError();
      jasmine.clock().tick(499);
      expect(FakeEventSource.instances.length).toBe(2);

      jasmine.clock().tick(1);
      expect(FakeEventSource.instances.length).toBe(3);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('should close the previous connection before opening a new one', () => {
    const service = createService('browser');

    service.init('event-1');
    const firstConnection = FakeEventSource.instances[0];
    service.init('event-2');

    expect(firstConnection.close).toHaveBeenCalled();
    expect(FakeEventSource.instances[1].url).toContain('/asso-events/event-2/tableau/sse');
  });

  it('should not create an EventSource while rendering on the server', () => {
    const service = createService('server');

    service.init('event-1');

    expect(FakeEventSource.instances.length).toBe(0);
  });
});

function createService(platformId: 'browser' | 'server'): SseClientService {
  TestBed.configureTestingModule({
    providers: [
      SseClientService,
      {provide: PLATFORM_ID, useValue: platformId}
    ]
  });

  return TestBed.inject(SseClientService);
}
