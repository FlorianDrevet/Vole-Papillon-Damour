import {TodayEventModel} from '../../../shared/models/today-event.model';

export type TodayBannerVariant = 'before' | 'live' | 'finished' | 'other';

/** Fermeture du bandeau mémorisée sur l'appareil (RG-19). */
export interface TodayBannerDismissal {
  eventId: string;
  day: string;
  variant: TodayBannerVariant;
}

export const TODAY_BANNER_STORAGE_KEY = 'vpd.today-banner.dismissed';

export function todayBannerVariant(today: TodayEventModel): TodayBannerVariant {
  if (today.eventType !== 'Bingo') {
    return 'other';
  }
  switch (today.liveStatus) {
    case 'Live':
      return 'live';
    case 'Finished':
      return 'finished';
    default:
      return 'before';
  }
}

/**
 * RG-19 : fermé pour l'événement et la journée ; le passage à « En direct » le fait
 * réapparaître une fois.
 */
export function isTodayBannerDismissed(
  today: TodayEventModel,
  dismissal: TodayBannerDismissal | null,
  day: string,
): boolean {
  if (dismissal === null || dismissal.eventId !== today.id || dismissal.day !== day) {
    return false;
  }
  return !(todayBannerVariant(today) === 'live' && dismissal.variant !== 'live');
}

/** Jour calendaire à Paris, au format `AAAA-MM-JJ`. */
export function parisDay(now: Date): string {
  return new Intl.DateTimeFormat('fr-CA', {
    timeZone: 'Europe/Paris',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}

/** « 13 h 30 » à l'heure de Paris. */
export function parisTime(iso: string): string {
  const parts = new Intl.DateTimeFormat('fr-FR', {
    timeZone: 'Europe/Paris',
    hour: '2-digit',
    minute: '2-digit',
  }).formatToParts(new Date(iso));
  const hour = parts.find(part => part.type === 'hour')?.value ?? '';
  const minute = parts.find(part => part.type === 'minute')?.value ?? '';
  return `${hour} h ${minute}`;
}

/** Ligne secondaire du bandeau : horaires et ville. */
export function todayBannerSchedule(today: TodayEventModel): string {
  const parts: string[] = [];
  if (todayBannerVariant(today) === 'before' && today.hourOpenDoors) {
    parts.push(`Ouverture des portes ${parisTime(today.hourOpenDoors)}`);
  } else if (today.hourOpenDoors && today.hourCloseDoors) {
    parts.push(`${parisTime(today.hourOpenDoors)} – ${parisTime(today.hourCloseDoors)}`);
  } else if (today.hourOpenDoors) {
    parts.push(parisTime(today.hourOpenDoors));
  }
  if (today.city) {
    parts.push(today.city);
  }
  return parts.join(' · ');
}
