/**
 * Sommaire du récit de Maxence : une entrée par chapitre, dans l'ordre de lecture.
 *
 * Source unique partagée par la frise (`app-time-line`), le lecteur
 * (`HistoryComponent`) et l'entête de chaque chapitre (`app-history-container`),
 * pour que la numérotation « Chapitre n / N » et la frise ne puissent pas diverger.
 * Les titres reprennent mot pour mot ceux passés par les composants `year-20XX`.
 */
export interface HistoryChapter {
  /** Année d'ouverture du chapitre. Sert aussi d'ancre : `#date-<year>`. */
  year: number;
  title: string;
}

export const HISTORY_CHAPTERS: readonly HistoryChapter[] = [
  { year: 2004, title: 'Sa naissance' },
  { year: 2005, title: 'Maxence a 1 an' },
  { year: 2006, title: 'Maxence a 2 ans' },
  { year: 2007, title: 'Maxence a 3 ans' },
  { year: 2008, title: 'Maxence a 4 ans' },
  { year: 2009, title: 'Maxence a 5 ans' },
  { year: 2010, title: 'Maxence a 6 ans' },
  { year: 2011, title: 'Maxence a 7 ans' },
  { year: 2012, title: 'Maxence a 8 ans' },
  { year: 2013, title: 'Maxence a 9 ans' },
  { year: 2014, title: 'Maxence a 10 ans' },
  { year: 2015, title: 'Maxence a 10 ans' },
  { year: 2016, title: 'Maxence a 11 ans' },
];
