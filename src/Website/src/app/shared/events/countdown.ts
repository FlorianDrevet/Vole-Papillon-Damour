import {VpdEventModel} from '../models/vpdEvent.model';

/** Jour calendaire à Paris, au format `AAAA-MM-JJ`. */
export function parisDay(now: Date): string {
  return new Intl.DateTimeFormat('fr-CA', {
    timeZone: 'Europe/Paris',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}

/** Jour calendaire parisien sous forme d'ordinal indépendant du fuseau de la machine. */
function dayOrdinal(day: string): number {
  const [year, month, date] = day.split('-').map(Number);
  return Date.UTC(year, month - 1, date) / 86_400_000;
}

export function countdownLabel(
  event: Pick<VpdEventModel, 'dateStart' | 'dateEnd'>,
  now: Date,
  maxDays = 30,
): string | null {
  const today = parisDay(now);
  const start = parisDay(event.dateStart);
  const daysUntil = dayOrdinal(start) - dayOrdinal(today);

  if (daysUntil === 0) {
    return "Aujourd'hui";
  }

  if (daysUntil < 0) {
    const end = event.dateEnd === null ? null : parisDay(event.dateEnd);
    return end !== null && start < end && end >= today ? 'En ce moment' : null;
  }

  if (daysUntil > maxDays) {
    return null;
  }

  return daysUntil === 1 ? 'Demain' : `Dans ${daysUntil} jours`;
}
