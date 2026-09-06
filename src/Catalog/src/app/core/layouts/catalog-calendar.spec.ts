import {calendarDataUri, calendarFilename} from './catalog-calendar';

describe('catalog-calendar', () => {
  it('creates a downloadable calendar event with the fair name and UTC dates', () => {
    const uri = calendarDataUri({
      name: 'Bourse de mars',
      dateStart: '2026-03-14T00:00:00Z',
      dateEnd: '2026-03-15T00:00:00Z',
      openAt: '2026-03-14T09:30:00Z',
      closeAt: '2026-03-15T18:00:00Z',
    });

    expect(decodeURIComponent(uri)).toContain('SUMMARY:Bourse de mars');
    expect(decodeURIComponent(uri)).toContain('DTSTART:20260314T093000Z');
    expect(decodeURIComponent(uri)).toContain('DTEND:20260315T180000Z');
  });

  it('uses a stable French filename', () => {
    expect(calendarFilename('Bourse de mars')).toBe('bourse-de-mars.ics');
  });
});
