import {CatalogAdminVolunteerContribution} from '../../core/catalog.models';
import {
  DEFAULT_VOLUNTEER_SORT,
  describeStatisticsPeriod,
  describeVolunteerCount,
  describeVolunteerLead,
  formatRoundedHours,
  nextVolunteerSort,
  sortVolunteers,
  toVolunteerStatisticsCsv,
} from './volunteer-statistics-view';

describe('volunteer statistics view', () => {
  function volunteer(
    displayName: string,
    overrides: Partial<CatalogAdminVolunteerContribution> = {},
  ): CatalogAdminVolunteerContribution {
    return {
      volunteerId: displayName,
      displayName,
      roles: ['Tri'],
      sessionCount: 1,
      scanDurationMinutes: 60,
      cashDurationMinutes: 0,
      totalDurationMinutes: 60,
      scannedCount: 10,
      keptCount: 8,
      keptRatePercent: 80,
      soldQuantity: 4,
      waitingQuantity: 4,
      waitingOverYearQuantity: 0,
      flowRatePercent: 50,
      firstActivityAt: null,
      lastActivityAt: null,
      dominantGenre: null,
      ...overrides,
    };
  }

  const volunteers = [
    volunteer('Bernard', {scannedCount: 40, sessionCount: 2, lastActivityAt: '2026-09-01T10:00:00Z'}),
    volunteer('Alice', {scannedCount: 90, sessionCount: 1, lastActivityAt: null}),
    volunteer('Chloé', {scannedCount: 40, sessionCount: 5, keptRatePercent: null, lastActivityAt: '2026-09-09T10:00:00Z'}),
  ];

  it('sorts by scanned books first and breaks ties by name', () => {
    expect(sortVolunteers(volunteers, DEFAULT_VOLUNTEER_SORT).map(item => item.displayName))
      .toEqual(['Alice', 'Bernard', 'Chloé']);
  });

  it('toggles the direction on the same column and starts new columns descending', () => {
    const bySessions = nextVolunteerSort(DEFAULT_VOLUNTEER_SORT, 'sessionCount');
    expect(bySessions).toEqual({key: 'sessionCount', direction: 'desc'});
    expect(sortVolunteers(volunteers, bySessions).map(item => item.displayName)).toEqual(['Chloé', 'Bernard', 'Alice']);

    const ascending = nextVolunteerSort(bySessions, 'sessionCount');
    expect(sortVolunteers(volunteers, ascending).map(item => item.displayName)).toEqual(['Alice', 'Bernard', 'Chloé']);
    expect(nextVolunteerSort(ascending, 'displayName').direction).toBe('asc');
  });

  it('puts volunteers without activity or kept rate last when sorting descending', () => {
    expect(sortVolunteers(volunteers, {key: 'lastActivityAt', direction: 'desc'}).map(item => item.displayName))
      .toEqual(['Chloé', 'Bernard', 'Alice']);
    expect(sortVolunteers(volunteers, {key: 'keptRatePercent', direction: 'desc'})[2].displayName).toBe('Chloé');
  });

  it('describes the team summary in words', () => {
    expect(describeVolunteerLead(15)).toBe('livres passés sous la douchette par quinze personnes.');
    expect(describeVolunteerLead(1)).toBe('livres passés sous la douchette par une personne.');
    expect(describeVolunteerCount(2)).toBe('deux bénévoles actifs');
    expect(describeStatisticsPeriod('year', null)).toBe('sur douze mois');
    expect(describeStatisticsPeriod('fair', 'Bourse de juin')).toBe('sur Bourse de juin');
    expect(formatRoundedHours(8340)).toBe('139 h');
  });

  it('exports the volunteers in the displayed order', () => {
    const csv = toVolunteerStatisticsCsv([volunteer('=Alice', {roles: ['Tri', 'Caisse'], lastActivityAt: '2026-09-09T10:00:00Z'})]);

    expect(csv.split('\r\n')[0]).toBe('Bénévole;Rôles;Sessions;Temps (min);Scannés;Gardés (%);Vendus depuis;Dernière activité');
    expect(csv.split('\r\n')[1]).toBe('\'=Alice;Tri · Caisse;1;60;10;80;4;2026-09-09');
  });
});
