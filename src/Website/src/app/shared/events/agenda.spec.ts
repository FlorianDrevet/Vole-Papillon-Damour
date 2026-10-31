import {VpdEventEnum} from '../enums/vpdEvent.enum';
import {VpdEventModel} from '../models/vpdEvent.model';
import {agendaMonths, calendarMonth, groupByStartMonth} from './agenda';

describe('agenda helpers', () => {
  it('starts November 2026 on Sunday with six empty Monday-first cells', () => {
    const cells = calendarMonth(2026, 10, []);

    expect(cells.slice(0, 6).every(cell => cell.date === null)).toBeTrue();
    expect(cells[6].day).toBe(1);
  });

  it('marks the start, middle and end of a multi-day book exchange', () => {
    const books = createEvent('2026-11-11T12:00:00Z', '2026-11-16T12:00:00Z');
    const cells = calendarMonth(2026, 10, [books]);
    const positions = cells.flatMap(cell => cell.events)
      .filter(occurrence => occurrence.event.id === books.id)
      .map(occurrence => occurrence.position);

    expect(positions).toEqual(['start', 'middle', 'middle', 'middle', 'middle', 'end']);
  });

  it('groups by start month in chronological order and fills months between events', () => {
    const january = createEvent('2027-01-03T12:00:00Z');
    const november = createEvent('2026-11-11T12:00:00Z');
    const groups = groupByStartMonth([january, november]);
    const months = agendaMonths([january, november]);

    expect(groups.map(group => group.key)).toEqual(['2026-11', '2027-01']);
    expect(months.map(month => month.key)).toEqual(['2026-11', '2026-12', '2027-01']);
    expect(months[1].events).toEqual([]);
  });
});

function createEvent(dateStart: string, dateEnd: string | null = null): VpdEventModel {
  return {
    eventType: VpdEventEnum.Books,
    liveTheme: 'Classic',
    id: `${dateStart}-${dateEnd ?? 'single'}`,
    name: 'Bourse aux livres',
    description: '',
    dateStart: new Date(dateStart),
    dateEnd: dateEnd ? new Date(dateEnd) : null,
    hourOpenDoors: null,
    hourCloseDoors: null,
    urlImageMap: null,
    urlRegistration: null,
    urlImage: '',
    city: 'Saint-Just-Saint-Rambert',
    road: 'Rue du Papillon',
    cityCode: '42170',
    roadNumber: '12',
    parties: [],
    currentPartieIndex: 0,
    bingoHasBeenWon: false,
    bingoNumeros: [],
  };
}
