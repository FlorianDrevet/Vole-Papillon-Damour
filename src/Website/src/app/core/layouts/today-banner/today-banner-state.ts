import {TodayEventModel} from '../../../shared/models/today-event.model';
import {parisDay} from '../../../shared/events/countdown';

export type TodayBannerStage = 'upcoming' | 'today' | 'live' | 'finished' | 'ongoing';

/** Fermeture mémorisée jusqu'à l'étape suivante, pour un rendez-vous donné (RG-46). */
export interface TodayBannerDismissal {
  eventId: string;
  stage: TodayBannerStage;
}

export const TODAY_BANNER_STORAGE_KEY = 'vpd.today-banner.dismissed';

function dayOrdinal(day: string): number {
  const [year, month, date] = day.split('-').map(Number);
  return Date.UTC(year, month - 1, date) / 86_400_000;
}

function daysUntil(today: TodayEventModel, now: Date): number {
  return dayOrdinal(parisDay(new Date(today.dateStart))) - dayOrdinal(parisDay(now));
}

export function todayBannerStage(today: TodayEventModel, now: Date): TodayBannerStage {
  if (today.eventType === 'Bingo' && today.liveStatus === 'Live') {
    return 'live';
  }
  if (today.eventType === 'Bingo' && today.liveStatus === 'Finished') {
    return 'finished';
  }

  const startDay = parisDay(new Date(today.dateStart));
  const currentDay = parisDay(now);
  if (startDay === currentDay) {
    return 'today';
  }
  if (startDay > currentDay) {
    return 'upcoming';
  }

  if (today.eventType === 'Books' && today.dateEnd !== null
    && parisDay(new Date(today.dateEnd)) >= currentDay) {
    return 'ongoing';
  }

  return 'finished';
}

export function todayBannerLabel(
  today: TodayEventModel,
  stage: TodayBannerStage,
  now: Date,
): string {
  const days = daysUntil(today, now);
  switch (stage) {
    case 'upcoming':
      return days === 1 ? 'Demain' : `Dans ${days} jours`;
    case 'today':
      return "Aujourd'hui";
    case 'live':
      return 'En direct';
    case 'finished':
      return 'Terminé';
    case 'ongoing':
      return 'En ce moment';
  }
}

/** « dimanche 1er novembre » à l'heure de Paris. */
function parisDate(iso: string): string {
  const parts = new Intl.DateTimeFormat('fr-FR', {
    timeZone: 'Europe/Paris',
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  }).formatToParts(new Date(iso));
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(item => item.type === type)?.value ?? '';
  const day = part('day');
  return `${part('weekday')} ${day === '1' ? '1er' : day} ${part('month')}`;
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

/** Ligne secondaire : date, horaires de porte ou fin d'une bourse en cours. */
export function todayBannerSchedule(
  today: TodayEventModel,
  stage: TodayBannerStage,
  now: Date,
): string {
  const parts: string[] = [];
  if (stage === 'upcoming' && daysUntil(today, now) === 1 && today.hourOpenDoors) {
    parts.push(`Ouverture des portes ${parisTime(today.hourOpenDoors)}`);
  } else if (stage === 'upcoming') {
    parts.push(parisDate(today.dateStart));
  } else if (stage === 'ongoing' && today.dateEnd !== null) {
    parts.push(`jusqu’au ${parisDate(today.dateEnd)}`);
  } else if (stage === 'today' && today.eventType === 'Bingo'
    && today.liveStatus === 'NotStarted' && today.hourOpenDoors) {
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

/** Une fermeture reste valable tant que l'événement et son étape ne changent pas. */
export function isTodayBannerDismissed(
  today: TodayEventModel,
  dismissal: TodayBannerDismissal | null,
  now: Date,
): boolean {
  return dismissal !== null
    && dismissal.eventId === today.id
    && dismissal.stage === todayBannerStage(today, now);
}
