import {PLATFORM_ID} from '@angular/core';
import {TestBed} from '@angular/core/testing';

import {SseClientService} from './sse-client.service';
import {NumberLineEnum} from '../enums/numberLine.enum';
import {PartieTypeEnum} from '../enums/partieType.enum';
import {VpdEventEnum} from '../enums/vpdEvent.enum';
import {VpdEventModel} from '../models/vpdEvent.model';
import {VpdEventsFacadeService} from '../facades/vpd-events.facade.service';

class FakeEventSource {
  static readonly OPEN = 1;
  static readonly instances: FakeEventSource[] = [];

  readyState = 0;
  onmessage: ((event: MessageEvent<string>) => void) | null = null;
  onopen: (() => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  close = jasmine.createSpy('close');

  constructor(public readonly url: string) {
    FakeEventSource.instances.push(this);
  }

  emitOpen(): void {
    this.readyState = FakeEventSource.OPEN;
    this.onopen?.();
  }

  emitMessage(data: string): void {
    this.onmessage?.({data} as MessageEvent<string>);
  }

  emitError(): void {
    this.readyState = 2;
    this.onerror?.(new Event('error'));
  }
}

function liveEvent(liveRevision: number, liveNumeros: number[]): VpdEventModel {
  return {
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
    bingoNumeros: [],
    liveRevision,
    parties: [
      {
        id: 'partie-1',
        name: 'Partie 1',
        partieType: PartieTypeEnum.STANDARD,
        index: 0,
        pauseAfter: false,
        addedBingoNumber: null,
        lastNumeros: liveNumeros,
        liveNumeros,
        currentLineIndex: 0,
        lineParties: [
          {
            id: 'line-1',
            index: 0,
            numberLine: NumberLineEnum.ONELINE,
            lots: [{id: 'lot-1', name: 'Lot 1', urlImage: 'lot.jpg', index: 0, isWon: null}]
          }
        ]
      }
    ]
  };
}

describe('SseClientService', () => {
  const originalEventSource = globalThis.EventSource;
  let events: jasmine.SpyObj<VpdEventsFacadeService>;

  beforeEach(() => {
    FakeEventSource.instances.length = 0;
    spyOn(console, 'error');
    (globalThis as unknown as {EventSource: typeof FakeEventSource}).EventSource = FakeEventSource;
    events = jasmine.createSpyObj<VpdEventsFacadeService>('VpdEventsFacadeService', ['getEventById']);
    events.getEventById.and.resolveTo(liveEvent(1, [25]));
  });

  afterEach(() => {
    globalThis.EventSource = originalEventSource;
    TestBed.resetTestingModule();
  });

  it('affiche l’état lu à l’ouverture puis suit le flux de l’événement', async () => {
    const service = createService('browser', events);

    await service.init('event-1');
    expect(service.eventAsso()?.parties[0].liveNumeros).toEqual([25]);
    expect(FakeEventSource.instances[0].url).toContain('/asso-events/event-1/tableau/sse');

    FakeEventSource.instances[0].emitOpen();
    FakeEventSource.instances[0].emitMessage(JSON.stringify(liveEvent(2, [25, 42])));

    expect(service.connection()).toBe('live');
    expect(service.eventAsso()?.parties[0].liveNumeros).toEqual([25, 42]);
  });

  it('ignore un état plus ancien que celui affiché', async () => {
    events.getEventById.and.resolveTo(liveEvent(5, [25, 42, 7]));
    const service = createService('browser', events);

    await service.init('event-1');
    FakeEventSource.instances[0].emitMessage(JSON.stringify(liveEvent(4, [25, 42])));

    expect(service.eventAsso()?.liveRevision).toBe(5);
  });

  it('garde le dernier état sur un message illisible', async () => {
    const service = createService('browser', events);

    await service.init('event-1');
    FakeEventSource.instances[0].emitMessage('{malformed-json');

    expect(service.eventAsso()?.parties[0].lastNumeros).toEqual([25]);
    expect(console.error).toHaveBeenCalled();
  });

  it('signale un tableau introuvable sans ouvrir de flux (RG-15)', async () => {
    events.getEventById.and.rejectWith({response: {status: 404}});
    const service = createService('browser', events);

    await service.init('unknown');

    expect(service.connection()).toBe('not-found');
    expect(FakeEventSource.instances.length).toBe(0);
  });

  it('se reconnecte avec un délai croissant et affiche « Reconnexion… » après 5 s (RG-12, RG-13)', async () => {
    const service = createService('browser', events);
    await service.init('event-1');
    jasmine.clock().install();

    try {
      FakeEventSource.instances[0].emitOpen();
      FakeEventSource.instances[0].emitError();
      expect(FakeEventSource.instances[0].close).toHaveBeenCalled();
      expect(service.connection()).toBe('live');

      jasmine.clock().tick(499);
      expect(FakeEventSource.instances.length).toBe(1);
      jasmine.clock().tick(1);
      expect(FakeEventSource.instances.length).toBe(2);

      FakeEventSource.instances[1].emitError();
      jasmine.clock().tick(999);
      expect(FakeEventSource.instances.length).toBe(2);
      jasmine.clock().tick(1);
      expect(FakeEventSource.instances.length).toBe(3);

      jasmine.clock().tick(3500);
      expect(service.connection()).toBe('reconnecting');

      FakeEventSource.instances[2].emitOpen();
      FakeEventSource.instances[2].emitMessage(JSON.stringify(liveEvent(3, [25, 42, 7])));
      expect(service.connection()).toBe('live');
      expect(service.eventAsso()?.parties[0].liveNumeros).toEqual([25, 42, 7]);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('ferme le flux précédent en changeant d’événement', async () => {
    const service = createService('browser', events);

    await service.init('event-1');
    const firstConnection = FakeEventSource.instances[0];
    await service.init('event-2');

    expect(firstConnection.close).toHaveBeenCalled();
    expect(FakeEventSource.instances[1].url).toContain('/asso-events/event-2/tableau/sse');
  });

  it('n’ouvre aucun flux pendant le rendu serveur', async () => {
    const service = createService('server', events);

    await service.init('event-1');

    expect(FakeEventSource.instances.length).toBe(0);
    expect(events.getEventById).not.toHaveBeenCalled();
  });
});

function createService(platformId: 'browser' | 'server', events: VpdEventsFacadeService): SseClientService {
  TestBed.configureTestingModule({
    providers: [
      SseClientService,
      {provide: PLATFORM_ID, useValue: platformId},
      {provide: VpdEventsFacadeService, useValue: events}
    ]
  });

  return TestBed.inject(SseClientService);
}
