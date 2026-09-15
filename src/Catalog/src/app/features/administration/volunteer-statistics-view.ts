import {CatalogAdminVolunteerContribution} from '../../core/catalog.models';
import {spellFrenchNumber, toCsv} from './statistics-format';

export type VolunteerSortKey =
  | 'displayName'
  | 'sessionCount'
  | 'totalDurationMinutes'
  | 'scannedCount'
  | 'keptRatePercent'
  | 'soldQuantity'
  | 'lastActivityAt';

export type VolunteerStatisticsPeriod = '30-days' | 'year' | 'all' | 'fair';

export interface VolunteerSort {
  key: VolunteerSortKey;
  direction: 'asc' | 'desc';
}

export interface VolunteerColumn {
  label: string;
  sortKey: VolunteerSortKey | null;
  className: string;
}

export const DEFAULT_VOLUNTEER_SORT: VolunteerSort = {key: 'scannedCount', direction: 'desc'};

export const VOLUNTEER_COLUMNS: VolunteerColumn[] = [
  {label: 'Bénévole', sortKey: 'displayName', className: 'volunteer-col-name'},
  {label: 'Rôles', sortKey: null, className: 'volunteer-col-roles'},
  {label: 'Sess.', sortKey: 'sessionCount', className: 'volunteer-col-number'},
  {label: 'Temps', sortKey: 'totalDurationMinutes', className: 'volunteer-col-number'},
  {label: 'Scannés', sortKey: 'scannedCount', className: 'volunteer-col-bar'},
  {label: 'Gardés', sortKey: 'keptRatePercent', className: 'volunteer-col-number'},
  {label: 'Vendus depuis', sortKey: 'soldQuantity', className: 'volunteer-col-bar'},
  {label: 'Dernière act.', sortKey: 'lastActivityAt', className: 'volunteer-col-number'},
];

const numberFormatter = new Intl.NumberFormat('fr-FR');

export function sortVolunteers(
  volunteers: readonly CatalogAdminVolunteerContribution[],
  sort: VolunteerSort,
): CatalogAdminVolunteerContribution[] {
  const factor = sort.direction === 'asc' ? 1 : -1;
  return [...volunteers].sort((left, right) => {
    const comparison = compareVolunteers(left, right, sort.key);
    return comparison !== 0 ? comparison * factor : left.displayName.localeCompare(right.displayName, 'fr');
  });
}

export function nextVolunteerSort(current: VolunteerSort, key: VolunteerSortKey): VolunteerSort {
  if (current.key === key) {
    return {key, direction: current.direction === 'asc' ? 'desc' : 'asc'};
  }
  return {key, direction: key === 'displayName' ? 'asc' : 'desc'};
}

export function describeStatisticsPeriod(period: VolunteerStatisticsPeriod, fairName: string | null): string {
  switch (period) {
    case '30-days':
      return 'sur trente jours';
    case 'year':
      return 'sur douze mois';
    case 'all':
      return 'depuis toujours';
    case 'fair':
      return fairName ? `sur ${fairName}` : 'sur une bourse';
  }
}

export function describeVolunteerLead(activeVolunteerCount: number): string {
  if (activeVolunteerCount === 0) {
    return 'livres passés sous la douchette.';
  }
  const people = activeVolunteerCount === 1 ? 'une personne' : `${spellFrenchNumber(activeVolunteerCount)} personnes`;
  return `livres passés sous la douchette par ${people}.`;
}

export function describeVolunteerCount(activeVolunteerCount: number): string {
  if (activeVolunteerCount === 0) {
    return 'aucun bénévole actif';
  }
  return activeVolunteerCount === 1
    ? 'un bénévole actif'
    : `${spellFrenchNumber(activeVolunteerCount)} bénévoles actifs`;
}

export function formatRoundedHours(minutes: number | null | undefined): string {
  return `${numberFormatter.format(Math.round((minutes ?? 0) / 60))} h`;
}

export function toVolunteerStatisticsCsv(volunteers: readonly CatalogAdminVolunteerContribution[]): string {
  return toCsv([
    ['Bénévole', 'Rôles', 'Sessions', 'Temps (min)', 'Scannés', 'Gardés (%)', 'Vendus depuis', 'Dernière activité'],
    ...volunteers.map(volunteer => [
      volunteer.displayName,
      volunteer.roles.join(' · '),
      volunteer.sessionCount,
      volunteer.totalDurationMinutes,
      volunteer.scannedCount,
      volunteer.keptRatePercent,
      volunteer.soldQuantity,
      volunteer.lastActivityAt?.slice(0, 10),
    ]),
  ]);
}

function compareVolunteers(
  left: CatalogAdminVolunteerContribution,
  right: CatalogAdminVolunteerContribution,
  key: VolunteerSortKey,
): number {
  if (key === 'displayName') {
    return left.displayName.localeCompare(right.displayName, 'fr');
  }
  if (key === 'lastActivityAt') {
    return activityTime(left.lastActivityAt) - activityTime(right.lastActivityAt);
  }
  return (left[key] ?? -1) - (right[key] ?? -1);
}

function activityTime(value: string | null): number {
  return value ? new Date(value).getTime() : 0;
}
