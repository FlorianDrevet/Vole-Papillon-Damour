import {VpdEventModel} from '../models/vpdEvent.model';
import {parisDay} from './countdown';

export type CalendarRangePosition = 'one' | 'start' | 'middle' | 'end';

export interface CalendarEventOccurrence {
  event: VpdEventModel;
  position: CalendarRangePosition;
}

export interface CalendarDayCell {
  day: number | null;
  date: string | null;
  events: CalendarEventOccurrence[];
}

export interface AgendaMonth {
  key: string;
  year: number;
  month: number;
  label: string;
  events: VpdEventModel[];
}

function monthKey(date: Date): string {
  return parisDay(date).slice(0, 7);
}

function monthLabel(year: number, month: number): string {
  return new Intl.DateTimeFormat('fr-FR', {
    month: 'long',
    year: 'numeric',
    timeZone: 'UTC',
  }).format(new Date(Date.UTC(year, month, 1)));
}

function makeMonth(key: string, events: VpdEventModel[]): AgendaMonth {
  const [year, monthNumber] = key.split('-').map(Number);
  const month = monthNumber - 1;

  return {key, year, month, label: monthLabel(year, month), events};
}

export function groupByStartMonth(events: VpdEventModel[]): AgendaMonth[] {
  const grouped = new Map<string, VpdEventModel[]>();
  for (const event of events) {
    const key = monthKey(event.dateStart);
    const monthEvents = grouped.get(key) ?? [];
    monthEvents.push(event);
    grouped.set(key, monthEvents);
  }

  return [...grouped.entries()]
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([key, monthEvents]) => makeMonth(key, monthEvents.sort((a, b) => a.dateStart.getTime() - b.dateStart.getTime())));
}

export function calendarMonth(year: number, month: number, events: VpdEventModel[]): CalendarDayCell[] {
  if (month < 0 || month > 11) {
    throw new RangeError('month must be between 0 and 11');
  }

  const firstWeekday = new Date(Date.UTC(year, month, 1)).getUTCDay();
  const leadingEmptyCells = (firstWeekday + 6) % 7;
  const daysInMonth = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  const cells: CalendarDayCell[] = [];

  for (let index = 0; index < leadingEmptyCells; index += 1) {
    cells.push({day: null, date: null, events: []});
  }

  for (let day = 1; day <= daysInMonth; day += 1) {
    const date = `${year}-${String(month + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
    const occurrences = events.flatMap(event => {
      const start = parisDay(event.dateStart);
      const rawEnd = parisDay(event.dateEnd ?? event.dateStart);
      const end = rawEnd < start ? start : rawEnd;
      if (date < start || date > end) {
        return [];
      }

      const position: CalendarRangePosition = start === end
        ? 'one'
        : date === start
          ? 'start'
          : date === end
            ? 'end'
            : 'middle';
      return [{event, position}];
    });
    cells.push({day, date, events: occurrences});
  }

  while (cells.length % 7 !== 0) {
    cells.push({day: null, date: null, events: []});
  }
  return cells;
}

export function agendaMonths(events: VpdEventModel[]): AgendaMonth[] {
  const groupedMonths = groupByStartMonth(events);
  if (groupedMonths.length === 0) {
    return [];
  }

  const eventsByMonth = new Map(groupedMonths.map(group => [group.key, group.events]));
  const first = groupedMonths[0];
  const last = groupedMonths[groupedMonths.length - 1];
  const months: AgendaMonth[] = [];
  const cursor = new Date(Date.UTC(first.year, first.month, 1));
  const lastMonth = Date.UTC(last.year, last.month, 1);

  while (cursor.getTime() <= lastMonth) {
    const key = `${cursor.getUTCFullYear()}-${String(cursor.getUTCMonth() + 1).padStart(2, '0')}`;
    months.push(makeMonth(key, eventsByMonth.get(key) ?? []));
    cursor.setUTCMonth(cursor.getUTCMonth() + 1);
  }
  return months;
}
