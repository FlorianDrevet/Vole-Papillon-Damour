import {CatalogAdminFairEvolutionEntry, CatalogAdminFairsEvolution} from '../../core/catalog.models';
import {buildFairsEvolutionView, toFairsEvolutionCsv} from './fairs-evolution-view';

describe('fairs evolution view', () => {
  function fair(
    index: number,
    dateStart: string,
    soldQuantity: number,
    overrides: Partial<CatalogAdminFairEvolutionEntry> = {},
  ): CatalogAdminFairEvolutionEntry {
    return {
      fairId: `fair-${index}`,
      name: `Bourse ${index}`,
      dateStart,
      dateEnd: dateStart,
      soldQuantity,
      revenue: soldQuantity * 1.4,
      averageBasket: 1.4,
      variationPercent: null,
      daysOpen: 2,
      ...overrides,
    };
  }

  function evolution(fairs: CatalogAdminFairEvolutionEntry[]): CatalogAdminFairsEvolution {
    return {
      generatedAt: '2026-09-15T00:00:00Z',
      from: null,
      to: null,
      fairCount: fairs.length,
      totalSoldQuantity: fairs.reduce((total, item) => total + item.soldQuantity, 0),
      totalRevenue: 1000,
      averageBasket: 1.4,
      growthSinceFirstPercent: 100,
      seasons: [
        {season: 'Printemps', averageSoldQuantity: 400, fairCount: 1},
        {season: 'Été', averageSoldQuantity: 500, fairCount: 1},
        {season: 'Automne', averageSoldQuantity: 440, fairCount: 1},
        {season: 'Hiver', averageSoldQuantity: null, fairCount: 0},
      ],
      fairs,
    };
  }

  const fairs = [
    fair(1, '2023-03-10T00:00:00Z', 400),
    fair(2, '2023-06-10T00:00:00Z', 440),
    fair(3, '2023-11-10T00:00:00Z', 480),
    fair(4, '2024-06-10T00:00:00Z', 900, {daysOpen: 3}),
    fair(5, '2025-02-10T00:00:00Z', 800, {averageBasket: 1.45}),
  ];

  it('summarizes the period, the growth and the basket range for the cards', () => {
    const view = buildFairsEvolutionView(evolution(fairs));

    expect(view.periodLabel).toBe('Mars 2023 → février 2025');
    expect(view.growthLabel).toBe('400 livres en mars 2023, 800 en février 2025');
    expect(view.basketLabel).toContain('Stable entre');
    expect(view.chartTitle).toBe('Cinq bourses, d’une fois sur l’autre');
  });

  it('draws one bar per fair with the latest fair emphasized and a revenue line', () => {
    const chart = buildFairsEvolutionView(evolution(fairs)).chart!;

    expect(chart.bars.length).toBe(5);
    expect(chart.bars.map(bar => bar.isLatest)).toEqual([false, false, false, false, true]);
    expect(chart.bars[3].height).toBeGreaterThan(chart.bars[0].height);
    expect(chart.bars[0].month).toBe('mars');
    expect(chart.bars[0].year).toBe('2023');
    expect(chart.revenuePoints.length).toBe(5);
    expect(chart.quantityTicks[chart.quantityTicks.length - 1].label).toBe('0');
  });

  it('flags the only longer fair and leaves it out of the trend', () => {
    const view = buildFairsEvolutionView(evolution(fairs));

    expect(view.insights.map(insight => insight.label)).toEqual(['Tendance', 'Juin 2024', 'Recette']);
    expect(view.insights[0].after).toContain('hors juin 2024');
    expect(view.insights[1].strong).toBe('trois jours');
    expect(view.recentRows.find(row => row.fairId === 'fair-4')?.isLongest).toBeTrue();
  });

  it('lists the most recent fairs first and ignores empty seasons', () => {
    const view = buildFairsEvolutionView(evolution(fairs));

    expect(view.recentRows[0].fairId).toBe('fair-5');
    expect(view.recentRows[0].isLatest).toBeTrue();
    expect(view.seasons.map(season => season.season)).toEqual(['Printemps', 'Été', 'Automne']);
    expect(view.seasonCaption).toBe('Les trois saisons se tiennent en 20 %.');
  });

  it('handles a period without fairs', () => {
    const view = buildFairsEvolutionView(evolution([]));

    expect(view.chart).toBeNull();
    expect(view.periodLabel).toBeNull();
    expect(view.insights).toEqual([]);
    expect(view.chartTitle).toBe('Aucune bourse sur la période');
  });

  it('exports every fair of the period as CSV', () => {
    const csv = toFairsEvolutionCsv(evolution([fair(1, '2023-03-10T00:00:00Z', 400, {name: '=Bourse', revenue: 560})]));

    expect(csv.split('\r\n')[0]).toBe('Bourse;Début;Fin;Vendus;Écart (%);Recette (€);Panier moyen (€);Jours');
    expect(csv.split('\r\n')[1]).toBe('\'=Bourse;2023-03-10;2023-03-10;400;;560;1.4;2');
  });
});
