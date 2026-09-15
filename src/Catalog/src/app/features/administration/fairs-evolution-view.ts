import {CatalogAdminFairEvolutionEntry, CatalogAdminFairsEvolution} from '../../core/catalog.models';
import {capitalizeFrench as capitalize, spellFrenchNumber as spell, toCsv} from './statistics-format';

const CHART_WIDTH = 1090;
const CHART_HEIGHT = 296;
const PLOT_LEFT = 34;
const PLOT_RIGHT = 1026;
const PLOT_TOP = 20;
const PLOT_BASELINE = 240;
const MAX_BAR_WIDTH = 54;
const RECENT_FAIR_COUNT = 6;
const STABLE_SPREAD_RATIO = 0.15;

const monthYearFormatter = new Intl.DateTimeFormat('fr-FR', {month: 'long', year: 'numeric', timeZone: 'UTC'});
const shortMonthFormatter = new Intl.DateTimeFormat('fr-FR', {month: 'short', timeZone: 'UTC'});
const numberFormatter = new Intl.NumberFormat('fr-FR');
const moneyFormatter = new Intl.NumberFormat('fr-FR', {style: 'currency', currency: 'EUR'});
const roundMoneyFormatter = new Intl.NumberFormat('fr-FR', {
  style: 'currency',
  currency: 'EUR',
  maximumFractionDigits: 0,
});

export interface FairsEvolutionTick {
  y: number;
  label: string;
}

export interface FairsEvolutionBar {
  fairId: string;
  x: number;
  y: number;
  width: number;
  height: number;
  centerX: number;
  valueY: number;
  value: string;
  month: string;
  year: string;
  isLatest: boolean;
}

export interface FairsEvolutionChart {
  viewBox: string;
  plotLeft: number;
  plotRight: number;
  baseline: number;
  quantityTicks: FairsEvolutionTick[];
  revenueTicks: FairsEvolutionTick[];
  bars: FairsEvolutionBar[];
  revenuePoints: {x: number; y: number}[];
  revenuePolyline: string;
  ariaLabel: string;
}

export interface FairsEvolutionInsight {
  label: string;
  before: string;
  strong: string;
  after: string;
}

export interface FairsEvolutionRow {
  fairId: string;
  label: string;
  name: string;
  soldQuantity: number;
  variationPercent: number | null;
  revenue: number | null;
  averageBasket: number | null;
  daysOpen: number | null;
  isLatest: boolean;
  isLongest: boolean;
}

export interface FairsEvolutionView {
  periodLabel: string | null;
  averageSoldLabel: string | null;
  basketLabel: string | null;
  growthLabel: string | null;
  chartTitle: string;
  chart: FairsEvolutionChart | null;
  insights: FairsEvolutionInsight[];
  seasons: {season: string; averageSoldQuantity: number; widthPercent: number}[];
  seasonCaption: string | null;
  recentRows: FairsEvolutionRow[];
}

export function buildFairsEvolutionView(evolution: CatalogAdminFairsEvolution): FairsEvolutionView {
  const fairs = evolution.fairs;
  const first = fairs[0];
  const last = fairs[fairs.length - 1];
  const longest = findLongestFair(fairs);
  const baskets = fairs
    .map(fair => fair.averageBasket)
    .filter((value): value is number => value !== null);

  return {
    periodLabel: first && last
      ? first === last
        ? capitalize(monthYear(first.dateStart))
        : `${capitalize(monthYear(first.dateStart))} → ${monthYear(last.dateStart)}`
      : null,
    averageSoldLabel: fairs.length > 0
      ? `${numberFormatter.format(Math.round(evolution.totalSoldQuantity / fairs.length))} en moyenne par bourse`
      : null,
    basketLabel: basketLabel(baskets),
    growthLabel: first && last && first !== last
      ? `${numberFormatter.format(first.soldQuantity)} livres en ${monthYear(first.dateStart)}, `
        + `${numberFormatter.format(last.soldQuantity)} en ${monthYear(last.dateStart)}`
      : null,
    chartTitle: chartTitle(fairs.length),
    chart: fairs.length > 0 ? buildChart(fairs) : null,
    insights: buildInsights(fairs, longest, baskets),
    ...buildSeasons(evolution),
    recentRows: fairs
      .slice(-RECENT_FAIR_COUNT)
      .reverse()
      .map(fair => ({
        fairId: fair.fairId,
        label: capitalize(monthYear(fair.dateStart)),
        name: fair.name,
        soldQuantity: fair.soldQuantity,
        variationPercent: fair.variationPercent,
        revenue: fair.revenue,
        averageBasket: fair.averageBasket,
        daysOpen: fair.daysOpen,
        isLatest: fair === last,
        isLongest: fair === longest,
      })),
  };
}

export function toFairsEvolutionCsv(evolution: CatalogAdminFairsEvolution): string {
  const header = ['Bourse', 'Début', 'Fin', 'Vendus', 'Écart (%)', 'Recette (€)', 'Panier moyen (€)', 'Jours'];
  const rows = evolution.fairs.map(fair => [
    fair.name,
    fair.dateStart.slice(0, 10),
    fair.dateEnd?.slice(0, 10) ?? null,
    fair.soldQuantity,
    fair.variationPercent,
    fair.revenue,
    fair.averageBasket === null ? null : Math.round(fair.averageBasket * 100) / 100,
    fair.daysOpen,
  ]);

  return toCsv([header, ...rows]);
}

function buildChart(fairs: CatalogAdminFairEvolutionEntry[]): FairsEvolutionChart {
  const plotHeight = PLOT_BASELINE - PLOT_TOP;
  const quantityStep = niceStep(Math.max(...fairs.map(fair => fair.soldQuantity), 1) / 3);
  const quantityMax = quantityStep * Math.max(1, Math.ceil(Math.max(...fairs.map(fair => fair.soldQuantity), 1) / quantityStep));
  const revenues = fairs.map(fair => fair.revenue).filter((value): value is number => value !== null);
  const revenueStep = niceStep(Math.max(...revenues, 1) / 2);
  const revenueMax = revenueStep * Math.max(1, Math.ceil(Math.max(...revenues, 1) / revenueStep));
  const slot = (PLOT_RIGHT - PLOT_LEFT) / fairs.length;
  const barWidth = Math.min(MAX_BAR_WIDTH, slot * 0.6);
  const quantityY = (value: number) => PLOT_BASELINE - (value / quantityMax) * plotHeight;
  const revenueY = (value: number) => PLOT_BASELINE - (value / revenueMax) * plotHeight;

  const bars = fairs.map((fair, index) => {
    const centerX = round(PLOT_LEFT + slot * (index + 0.5));
    const y = round(quantityY(fair.soldQuantity));
    return {
      fairId: fair.fairId,
      x: round(centerX - barWidth / 2),
      y,
      width: round(barWidth),
      height: round(PLOT_BASELINE - y),
      centerX,
      valueY: round(y - 8),
      value: numberFormatter.format(fair.soldQuantity),
      month: shortMonthFormatter.format(new Date(fair.dateStart)),
      year: String(new Date(fair.dateStart).getUTCFullYear()),
      isLatest: index === fairs.length - 1,
    };
  });
  const revenuePoints = fairs
    .map((fair, index) => fair.revenue === null
      ? null
      : {x: bars[index].centerX, y: round(revenueY(fair.revenue))})
    .filter((point): point is {x: number; y: number} => point !== null);

  return {
    viewBox: `0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`,
    plotLeft: PLOT_LEFT,
    plotRight: PLOT_RIGHT,
    baseline: PLOT_BASELINE,
    quantityTicks: ticks(quantityMax, quantityStep).map(value => ({
      y: round(quantityY(value)),
      label: numberFormatter.format(value),
    })),
    revenueTicks: revenues.length === 0
      ? []
      : ticks(revenueMax, revenueStep).map(value => ({
        y: round(revenueY(value)),
        label: roundMoneyFormatter.format(value),
      })),
    bars,
    revenuePoints,
    revenuePolyline: revenuePoints.map(point => `${point.x},${point.y}`).join(' '),
    ariaLabel: `Livres vendus et recette par bourse, ${fairs.length} bourse${fairs.length > 1 ? 's' : ''} : `
      + fairs.map(fair => `${fair.name} ${numberFormatter.format(fair.soldQuantity)} livres`).join(', '),
  };
}

function buildInsights(
  fairs: CatalogAdminFairEvolutionEntry[],
  longest: CatalogAdminFairEvolutionEntry | null,
  baskets: number[],
): FairsEvolutionInsight[] {
  const insights: FairsEvolutionInsight[] = [];
  const trendFairs = fairs.filter(fair => fair !== longest);
  if (trendFairs.length >= 3) {
    const slope = linearSlope(trendFairs.map(fair => fair.soldQuantity));
    const roundedSlope = Math.abs(slope) >= 10 ? Math.round(slope / 5) * 5 : Math.round(slope);
    const exclusion = longest ? `, hors ${monthYear(longest.dateStart)}` : '';
    insights.push(roundedSlope === 0
      ? {label: 'Tendance', before: 'Un volume ', strong: 'stable d’une bourse à l’autre', after: `${exclusion}.`}
      : {
        label: 'Tendance',
        before: 'Environ ',
        strong: `${roundedSlope > 0 ? '+' : '−'} ${numberFormatter.format(Math.abs(roundedSlope))} livres par bourse`,
        after: ` sur la période${exclusion}.`,
      });
  }

  if (longest?.daysOpen) {
    insights.push({
      label: capitalize(monthYear(longest.dateStart)),
      before: `${numberFormatter.format(longest.soldQuantity)} livres, la seule bourse sur `,
      strong: `${spell(longest.daysOpen)} jours`,
      after: ' — à ne pas comparer telle quelle.',
    });
  }

  if (baskets.length >= 2) {
    const min = Math.min(...baskets);
    const max = Math.max(...baskets);
    insights.push(spreadRatio(baskets) <= STABLE_SPREAD_RATIO
      ? {label: 'Recette', before: 'Elle suit le volume de près : ', strong: 'le panier moyen ne bouge presque pas', after: '.'}
      : {
        label: 'Recette',
        before: 'Le panier moyen varie ',
        strong: `de ${moneyFormatter.format(min)} à ${moneyFormatter.format(max)}`,
        after: ' : la recette ne suit pas seulement le volume.',
      });
  }

  return insights;
}

function buildSeasons(evolution: CatalogAdminFairsEvolution): Pick<FairsEvolutionView, 'seasons' | 'seasonCaption'> {
  const seasons = evolution.seasons
    .filter((season): season is {season: string; averageSoldQuantity: number; fairCount: number} =>
      season.fairCount > 0 && season.averageSoldQuantity !== null);
  const max = Math.max(...seasons.map(season => season.averageSoldQuantity), 1);
  const values = seasons.map(season => season.averageSoldQuantity);
  const gapPercent = seasons.length >= 2 ? Math.round(((Math.max(...values) - Math.min(...values)) / max) * 100) : null;

  return {
    seasons: seasons.map(season => ({
      season: season.season,
      averageSoldQuantity: season.averageSoldQuantity,
      widthPercent: round((season.averageSoldQuantity / max) * 100),
    })),
    seasonCaption: gapPercent === null
      ? null
      : `Les ${spell(seasons.length)} saisons se tiennent en ${numberFormatter.format(gapPercent)} %.`,
  };
}

function findLongestFair(fairs: CatalogAdminFairEvolutionEntry[]): CatalogAdminFairEvolutionEntry | null {
  const durations = fairs.map(fair => fair.daysOpen).filter((value): value is number => value !== null);
  if (fairs.length < 3 || durations.length < 3) {
    return null;
  }

  const maxDays = Math.max(...durations);
  const longest = fairs.filter(fair => fair.daysOpen === maxDays);
  return longest.length === 1 && maxDays > Math.min(...durations) ? longest[0] : null;
}

function basketLabel(baskets: number[]): string | null {
  if (baskets.length < 2) {
    return baskets.length === 1 ? 'Sur la seule bourse avec recette' : null;
  }

  const min = moneyFormatter.format(Math.min(...baskets));
  const max = moneyFormatter.format(Math.max(...baskets));
  if (min === max) {
    return 'Identique sur chaque bourse';
  }
  return spreadRatio(baskets) <= STABLE_SPREAD_RATIO ? `Stable entre ${min} et ${max}` : `Entre ${min} et ${max}`;
}

function chartTitle(count: number): string {
  if (count === 0) {
    return 'Aucune bourse sur la période';
  }
  if (count === 1) {
    return 'Une bourse sur la période';
  }
  return `${capitalize(spell(count))} bourses, d’une fois sur l’autre`;
}

function spreadRatio(values: number[]): number {
  const mean = values.reduce((total, value) => total + value, 0) / values.length;
  return mean === 0 ? 0 : (Math.max(...values) - Math.min(...values)) / mean;
}

function linearSlope(values: number[]): number {
  const meanX = (values.length - 1) / 2;
  const meanY = values.reduce((total, value) => total + value, 0) / values.length;
  const numerator = values.reduce((total, value, index) => total + (index - meanX) * (value - meanY), 0);
  const denominator = values.reduce((total, _value, index) => total + (index - meanX) ** 2, 0);
  return denominator === 0 ? 0 : numerator / denominator;
}

function niceStep(rawStep: number): number {
  const magnitude = 10 ** Math.floor(Math.log10(Math.max(rawStep, 1)));
  const normalized = rawStep / magnitude;
  const factor = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 2.5 ? 2.5 : normalized <= 5 ? 5 : 10;
  return Math.max(1, factor * magnitude);
}

function ticks(max: number, step: number): number[] {
  const values: number[] = [];
  for (let value = max; value >= 0; value -= step) {
    values.push(Math.round(value * 100) / 100);
  }
  return values;
}

function monthYear(value: string): string {
  return monthYearFormatter.format(new Date(value));
}

function round(value: number): number {
  return Math.round(value * 10) / 10;
}
