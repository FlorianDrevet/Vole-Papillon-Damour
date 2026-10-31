import {
  isTodayBannerDismissed,
  parisTime,
  todayBannerLabel,
  todayBannerSchedule,
  todayBannerStage,
} from './today-banner-state';
import {TodayEventModel} from '../../../shared/models/today-event.model';

const loto: TodayEventModel = {
  id: 'loto-1',
  name: 'Grand loto de Vole Papillon d’Amour',
  eventType: 'Bingo',
  dateStart: '2026-11-01T12:00:00Z',
  dateEnd: null,
  hourOpenDoors: '2026-11-01T12:30:00Z',
  hourCloseDoors: '2026-11-01T17:30:00Z',
  city: 'Arras',
  liveStatus: 'NotStarted',
  lastNumero: null,
};

describe('today banner state', () => {
  it('announces a rendezvous at J-7 as upcoming', () => {
    expect(todayBannerStage(
      {...loto, dateStart: '2026-11-07T12:00:00Z'},
      new Date('2026-10-31T12:00:00Z'),
    )).toBe('upcoming');
  });

  it('uses a stage label for every banner step', () => {
    const now = new Date('2026-10-31T12:00:00Z');
    expect(todayBannerLabel({...loto, dateStart: '2026-11-05T12:00:00Z'}, 'upcoming', now))
      .toBe('Dans 5 jours');
    expect(todayBannerLabel(loto, 'upcoming', now)).toBe('Demain');
    expect(todayBannerLabel(loto, 'today', new Date('2026-11-01T12:00:00Z'))).toBe("Aujourd'hui");
    expect(todayBannerLabel(loto, 'live', now)).toBe('En direct');
    expect(todayBannerLabel(loto, 'finished', now)).toBe('Terminé');
    expect(todayBannerLabel(loto, 'ongoing', now)).toBe('En ce moment');
  });

  it('keeps an upcoming dismissal from J-5 through J-1, then shows the day-J step', () => {
    const event = {...loto, dateStart: '2026-11-05T12:00:00Z'};
    const dismissal = {eventId: 'loto-1', stage: 'upcoming' as const};

    expect(isTodayBannerDismissed(event, dismissal, new Date('2026-10-31T12:00:00Z'))).toBeTrue();
    expect(isTodayBannerDismissed(event, dismissal, new Date('2026-11-04T12:00:00Z'))).toBeTrue();
    expect(isTodayBannerDismissed(event, dismissal, new Date('2026-11-05T12:00:00Z'))).toBeFalse();
  });

  it('keeps a multi-day book event in the ongoing stage', () => {
    const books: TodayEventModel = {
      ...loto,
      id: 'books-1',
      eventType: 'Books',
      dateStart: '2026-11-11T00:00:00Z',
      dateEnd: '2026-11-16T00:00:00Z',
      liveStatus: null,
    };
    expect(todayBannerStage(books, new Date('2026-11-13T12:00:00Z'))).toBe('ongoing');
    expect(todayBannerLabel(books, 'ongoing', new Date('2026-11-13T12:00:00Z')))
      .toBe('En ce moment');
    expect(todayBannerSchedule(books, 'ongoing', new Date('2026-11-13T12:00:00Z')))
      .toBe('jusqu’au lundi 16 novembre · Arras');
  });

  it('shows the date before the event and opening hours tomorrow or today', () => {
    const now = new Date('2026-10-31T12:00:00Z');
    expect(todayBannerSchedule(loto, 'upcoming', now)).toBe('Ouverture des portes 13 h 30 · Arras');
    expect(todayBannerSchedule({...loto, dateStart: '2026-11-07T12:00:00Z'}, 'upcoming', now))
      .toBe('samedi 7 novembre · Arras');
    expect(todayBannerSchedule(
      {...loto, dateStart: '2026-10-31T12:00:00Z'},
      'today',
      now,
    )).toBe('Ouverture des portes 13 h 30 · Arras');
    expect(parisTime('2026-11-01T12:30:00Z')).toBe('13 h 30');
  });

  it('uses live and finished as distinct dismissal steps', () => {
    const event = {...loto, dateStart: '2026-11-01T12:00:00Z'};
    const closedToday = {eventId: 'loto-1', stage: 'today' as const};
    expect(isTodayBannerDismissed(event, closedToday, new Date('2026-11-01T12:00:00Z'))).toBeTrue();
    expect(isTodayBannerDismissed(
      {...event, liveStatus: 'Live'},
      closedToday,
      new Date('2026-11-01T12:00:00Z'),
    )).toBeFalse();
    expect(isTodayBannerDismissed(
      {...event, liveStatus: 'Finished'},
      {eventId: 'loto-1', stage: 'live'},
      new Date('2026-11-01T12:00:00Z'),
    )).toBeFalse();
  });
});
