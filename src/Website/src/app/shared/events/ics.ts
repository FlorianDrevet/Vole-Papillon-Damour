import {VpdEventModel} from '../models/vpdEvent.model';
import {parisDay} from './countdown';

function escapeText(value: string): string {
  return value
    .replace(/\\/g, '\\\\')
    .replace(/\r\n|\r|\n/g, '\\n')
    .replace(/;/g, '\\;')
    .replace(/,/g, '\\,');
}

function dateOnlyValue(day: string): string {
  return day.replace(/-/g, '');
}

function addCalendarDay(day: string): string {
  const [year, month, date] = day.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, date + 1)).toISOString().slice(0, 10);
}

function utcDateTimeValue(date: Date): string {
  return date.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}Z$/, 'Z');
}

function eventLocation(event: VpdEventModel): string {
  const street = [event.roadNumber, event.road].filter(Boolean).join(' ');
  const city = [event.cityCode, event.city].filter(Boolean).join(' ');
  return [street, city].filter(Boolean).join(', ');
}

export function toIcs(event: VpdEventModel, url: string): string {
  const lines = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//Vole Papillon d’Amour//Agenda//FR',
    'CALSCALE:GREGORIAN',
    'BEGIN:VEVENT',
    `UID:${event.id}`,
  ];

  if (event.hourOpenDoors === null && event.hourCloseDoors === null) {
    const start = parisDay(event.dateStart);
    const rawEnd = parisDay(event.dateEnd ?? event.dateStart);
    const end = rawEnd < start ? start : rawEnd;
    lines.push(
      `DTSTART;VALUE=DATE:${dateOnlyValue(start)}`,
      `DTEND;VALUE=DATE:${dateOnlyValue(addCalendarDay(end))}`,
    );
  } else {
    const start = event.hourOpenDoors ?? event.dateStart;
    const end = event.hourCloseDoors ?? event.dateEnd ?? event.dateStart;
    lines.push(`DTSTART:${utcDateTimeValue(start)}`, `DTEND:${utcDateTimeValue(end)}`);
  }

  lines.push(
    `SUMMARY:${escapeText(event.name)}`,
    `LOCATION:${escapeText(eventLocation(event))}`,
    `URL:${url.replace(/[\r\n]/g, '')}`,
    'END:VEVENT',
    'END:VCALENDAR',
  );
  return `${lines.join('\r\n')}\r\n`;
}
