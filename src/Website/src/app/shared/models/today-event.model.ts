/** Réponse de `GET /asso-events/today` : l'événement du jour, loto en priorité. */
export interface TodayEventModel {
  id: string;
  name: string;
  eventType: 'Bingo' | 'Books' | 'Other' | 'Unknown';
  dateStart: string;
  dateEnd: string | null;
  hourOpenDoors: string | null;
  hourCloseDoors: string | null;
  city: string;
  /** Pour un loto seulement. */
  liveStatus: 'NotStarted' | 'Live' | 'Finished' | null;
  lastNumero: number | null;
}
