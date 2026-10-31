import {VpdEventModel} from '../models/vpdEvent.model';
import {countdownLabel, parisDay} from './countdown';

describe('countdownLabel', () => {
  it('labels the next Paris calendar day as tomorrow', () => {
    const event = {dateStart: new Date('2026-11-01T12:00:00Z'), dateEnd: null} as VpdEventModel;

    expect(countdownLabel(event, new Date('2026-10-31T12:00:00Z'))).toBe('Demain');
  });

  it('labels an event already in progress as now', () => {
    const event = {
      dateStart: new Date('2026-11-11T12:00:00Z'),
      dateEnd: new Date('2026-11-16T12:00:00Z'),
    } as VpdEventModel;

    expect(countdownLabel(event, new Date('2026-11-13T12:00:00Z'))).toBe('En ce moment');
  });

  it('uses Paris midnight when the UTC date has already changed', () => {
    const event = {dateStart: new Date('2026-10-31T23:30:00Z'), dateEnd: null} as VpdEventModel;

    expect(countdownLabel(event, new Date('2026-10-31T23:45:00Z'))).toBe("Aujourd'hui");
    expect(parisDay(new Date('2026-10-31T23:45:00Z'))).toBe('2026-11-01');
  });

  it('accepts the ISO strings returned by the API', () => {
    const event = {
      dateStart: '2026-11-01T14:00:00+00:00',
      dateEnd: null,
    } as unknown as VpdEventModel;

    expect(countdownLabel(event, new Date('2026-10-31T12:00:00Z'))).toBe('Demain');
  });

  it('returns null when the event is beyond the countdown window', () => {
    const event = {dateStart: new Date('2026-12-01T12:00:00Z'), dateEnd: null} as VpdEventModel;

    expect(countdownLabel(event, new Date('2026-10-31T12:00:00Z'))).toBeNull();
  });
});
