import {TestBed} from '@angular/core/testing';
import {ActivatedRouteSnapshot, provideRouter, RouterStateSnapshot, UrlTree} from '@angular/router';
import {lotoRedirectGuard} from './loto-redirect.guard';
import {VpdEventsFacadeService} from '../../shared/facades/vpd-events.facade.service';
import {TodayEventModel} from '../../shared/models/today-event.model';
import {VpdEventModel} from '../../shared/models/vpdEvent.model';

describe('lotoRedirectGuard', () => {
  let events: jasmine.SpyObj<VpdEventsFacadeService>;

  beforeEach(() => {
    events = jasmine.createSpyObj<VpdEventsFacadeService>('VpdEventsFacadeService', ['getTodayEvent', 'getLatestEventBingo']);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), {provide: VpdEventsFacadeService, useValue: events}]
    });
  });

  it('ouvre le tableau du loto du jour', async () => {
    events.getTodayEvent.and.resolveTo({id: 'loto-1', eventType: 'Bingo'} as TodayEventModel);

    expect(await redirect()).toBe('/evenement/loto-1/tableau');
  });

  it('mène à la fiche du prochain loto les autres jours', async () => {
    events.getTodayEvent.and.resolveTo({id: 'bourse', eventType: 'Books'} as TodayEventModel);
    events.getLatestEventBingo.and.resolveTo({id: 'loto-2'} as VpdEventModel);

    expect(await redirect()).toBe('/evenement/loto-2');
  });

  it('mène aux événements quand aucun loto n’est prévu', async () => {
    events.getTodayEvent.and.resolveTo(null);
    events.getLatestEventBingo.and.rejectWith({response: {status: 404}});

    expect(await redirect()).toBe('/evenement');
  });

  async function redirect(): Promise<string> {
    const result = await TestBed.runInInjectionContext(() =>
      lotoRedirectGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)) as UrlTree;
    return result.toString();
  }
});
