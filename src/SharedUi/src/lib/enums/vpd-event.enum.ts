/**
 * Aligne avec la string réellement renvoyée par l'API .NET.
 * Le BackOffice utilise déjà ce format ; le Website avait un enum numérique
 * qui ne pouvait jamais matcher la donnée renvoyée par le backend (bug).
 */
export enum VpdEventEnum {
  Bingo = 'Bingo',
  Books = 'Books',
  Other = 'Other',
  UNKNOWN = 'Unknown',
}
