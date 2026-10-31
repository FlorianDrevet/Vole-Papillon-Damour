import {
  isTodayBannerDismissed,
  parisTime,
  todayBannerSchedule,
  todayBannerVariant,
} from './today-banner-state';
import {TodayEventModel} from '../../../shared/models/today-event.model';
import {parisDay} from '../../../shared/events/countdown';

const loto: TodayEventModel = {
  id: 'loto-1',
  name: 'Grand loto de Vole Papillon d’Amour',
  eventType: 'Bingo',
  dateStart: '2026-11-15T13:00:00Z',
  dateEnd: null,
  hourOpenDoors: '2026-11-15T12:30:00Z',
  hourCloseDoors: '2026-11-15T17:30:00Z',
  city: 'Arras',
  liveStatus: 'NotStarted',
  lastNumero: null,
};

describe('today banner state', () => {
  it('choisit la variante selon le type et l’avancement (RG-18)', () => {
    expect(todayBannerVariant(loto)).toBe('before');
    expect(todayBannerVariant({...loto, liveStatus: 'Live', lastNumero: 42})).toBe('live');
    expect(todayBannerVariant({...loto, liveStatus: 'Finished'})).toBe('finished');
    expect(todayBannerVariant({...loto, eventType: 'Books', liveStatus: null})).toBe('other');
  });

  it('reste fermé pour la journée, sauf au passage en direct (RG-19)', () => {
    const closedBefore = {eventId: 'loto-1', day: '2026-11-15', variant: 'before' as const};

    expect(isTodayBannerDismissed(loto, closedBefore, '2026-11-15')).toBeTrue();
    expect(isTodayBannerDismissed(loto, closedBefore, '2026-11-16')).toBeFalse();
    expect(isTodayBannerDismissed({...loto, liveStatus: 'Live'}, closedBefore, '2026-11-15')).toBeFalse();
    expect(isTodayBannerDismissed(
      {...loto, liveStatus: 'Live'},
      {...closedBefore, variant: 'live'},
      '2026-11-15')).toBeTrue();
    expect(isTodayBannerDismissed(loto, null, '2026-11-15')).toBeFalse();
  });

  it('compte les jours et les heures à Paris (RG-16)', () => {
    expect(parisDay(new Date('2026-11-14T23:30:00Z'))).toBe('2026-11-15');
    expect(parisTime('2026-11-15T12:30:00Z')).toBe('13 h 30');
  });

  it('annonce l’ouverture des portes avant le loto, les horaires sinon', () => {
    expect(todayBannerSchedule(loto)).toBe('Ouverture des portes 13 h 30 · Arras');
    expect(todayBannerSchedule({...loto, eventType: 'Books', liveStatus: null}))
      .toBe('13 h 30 – 18 h 30 · Arras');
  });
});
