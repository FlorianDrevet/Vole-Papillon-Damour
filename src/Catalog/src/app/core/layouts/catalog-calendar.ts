export interface CatalogCalendarEvent {
  name: string;
  dateStart: string;
  dateEnd: string | null;
  openAt: string;
  closeAt: string | null;
  location?: string;
}

export function calendarDataUri(event: CatalogCalendarEvent): string {
  const start = toIcsDate(event.openAt || event.dateStart);
  const end = toIcsDate(event.closeAt || event.dateEnd || event.openAt || event.dateStart);
  const uid = `${event.name}-${start}`.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[^a-zA-Z0-9]+/g, '-').toLowerCase();
  const lines = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//Vole Papillon d’Amour//Catalogue//FR',
    'BEGIN:VEVENT',
    `UID:${uid}@volepapillondamour.fr`,
    `SUMMARY:${escapeIcsText(event.name)}`,
    `DTSTART:${start}`,
    `DTEND:${end}`,
    ...(event.location ? [`LOCATION:${escapeIcsText(event.location)}`] : []),
    'END:VEVENT',
    'END:VCALENDAR',
  ];

  return `data:text/calendar;charset=utf-8,${encodeURIComponent(lines.join('\r\n'))}`;
}

export function calendarFilename(name: string): string {
  const slug = name
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');

  return `${slug || 'bourse-aux-livres'}.ics`;
}

function toIcsDate(value: string): string {
  return new Date(value).toISOString().replace(/[-:]/g, '').replace(/\.\d{3}Z$/, 'Z');
}

function escapeIcsText(value: string): string {
  return value.replace(/\\/g, '\\\\').replace(/;/g, '\\;').replace(/,/g, '\\,').replace(/\r?\n/g, '\\n');
}
