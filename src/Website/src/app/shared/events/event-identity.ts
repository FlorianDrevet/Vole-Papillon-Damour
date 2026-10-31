import {VpdEventEnum} from '../enums/vpdEvent.enum';
import {VpdEventModel} from '../models/vpdEvent.model';

export type EventIdentity = 'halloween' | 'loto' | 'books' | 'other';

export function eventIdentity(event: Pick<VpdEventModel, 'eventType' | 'liveTheme'>): EventIdentity {
  if (event.eventType === VpdEventEnum.Bingo) {
    return event.liveTheme === 'Halloween' ? 'halloween' : 'loto';
  }
  if (event.eventType === VpdEventEnum.Books) {
    return 'books';
  }
  return 'other';
}
