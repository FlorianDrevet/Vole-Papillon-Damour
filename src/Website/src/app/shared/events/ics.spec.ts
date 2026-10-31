import {VpdEventEnum} from '../enums/vpdEvent.enum';
import {VpdEventModel} from '../models/vpdEvent.model';
import {toIcs} from './ics';

describe('toIcs', () => {
  it('uses an exclusive end date for an all-day multi-day event', () => {
    const books = createEvent({
      dateStart: new Date('2026-11-11T00:00:00+01:00'),
      dateEnd: new Date('2026-11-16T00:00:00+01:00'),
    });

    const calendar = toIcs(books, 'https://example.test/evenement/1');

    expect(calendar).toContain('DTSTART;VALUE=DATE:20261111');
    expect(calendar).toContain('DTEND;VALUE=DATE:20261117');
    expect(calendar).toContain('URL:https://example.test/evenement/1');
  });

  it('escapes special characters in the title and place', () => {
    const books = createEvent({
      name: 'Bourse, livres; édition',
      road: 'Rue; du Papier',
      city: 'Saint-Just, Saint-Rambert',
    });

    const calendar = toIcs(books, 'https://example.test/evenement/1');

    expect(calendar).toContain('SUMMARY:Bourse\\, livres\\; édition');
    expect(calendar).toContain('LOCATION:12 Rue\\; du Papier\\, 42170 Saint-Just\\, Saint-Rambert');
  });
});

function createEvent(overrides: Partial<VpdEventModel> = {}): VpdEventModel {
  return {
    eventType: VpdEventEnum.Books,
    liveTheme: 'Classic',
    id: 'bourse-2026-11',
    name: 'Bourse aux livres',
    description: '',
    dateStart: new Date('2026-11-11T00:00:00+01:00'),
    dateEnd: new Date('2026-11-16T00:00:00+01:00'),
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
    ...overrides,
  };
}
