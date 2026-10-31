import {VpdEventEnum} from '../enums/vpdEvent.enum';
import {eventIdentity} from './event-identity';

describe('eventIdentity', () => {
  it('uses the Halloween identity for a themed loto', () => {
    expect(eventIdentity({eventType: VpdEventEnum.Bingo, liveTheme: 'Halloween'})).toBe('halloween');
  });

  it('uses the regular loto identity when no Halloween theme is selected', () => {
    expect(eventIdentity({eventType: VpdEventEnum.Bingo, liveTheme: 'Classic'})).toBe('loto');
  });

  it('uses the books identity for a book exchange', () => {
    expect(eventIdentity({eventType: VpdEventEnum.Books})).toBe('books');
  });

  it('uses the neutral identity for other event types', () => {
    expect(eventIdentity({eventType: VpdEventEnum.Other})).toBe('other');
  });
});
