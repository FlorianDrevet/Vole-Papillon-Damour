/**
 * Ce que le tableau du loto affiche, déduit de l'état de l'événement renvoyé par l'API
 * (`EventResponse`). Partagé par la projection du BackOffice et le tableau mobile du
 * Website : les deux écrans lisent les mêmes règles (dernier numéro, voisins du
 * +1 / −1, compteur « 1ʳᵉ fois / 2ᵉ fois », états avant / pendant / après le jeu).
 *
 * Les interfaces ne reprennent que les champs utilisés : les modèles `VpdEventModel`
 * des deux applications y sont assignables tels quels.
 */

export interface LotoLotSnapshot {
  id: string;
  name: string;
  isWon: boolean | null;
}

export interface LotoLineSnapshot {
  index: number;
  numberLine: string;
  lots: LotoLotSnapshot[];
}

export interface LotoPartieSnapshot {
  id: string;
  name: string;
  partieType: string;
  index: number;
  pauseAfter: boolean;
  lastNumeros: number[];
  liveNumeros: number[];
  /** Position in lineParties after sorting by each line's index. */
  currentLineIndex: number;
  lineParties: LotoLineSnapshot[];
}

export interface LotoEventSnapshot {
  id: string;
  name: string;
  parties: LotoPartieSnapshot[] | null;
  currentPartieIndex: number;
  bingoNumeros: number[];
  bingoHasBeenWon: boolean;
}

export type LotoPhase = 'loading' | 'before' | 'playing' | 'finished';
export type LotoCellState = 'idle' | 'drawn' | 'last' | 'neighbour';

export interface LotoCell {
  value: number;
  state: LotoCellState;
}

export interface LotoLot {
  id: string;
  name: string;
  won: boolean;
}

export interface LotoPartieSummary {
  name: string;
  typeLabel: string | null;
  lines: { label: string; lots: string[] }[];
}

export interface LotoTableauView {
  phase: LotoPhase;
  intermission: boolean;
  eventName: string;
  partieNumber: number;
  partieCount: number;
  partieName: string;
  lineLabel: string | null;
  timesLabel: string | null;
  typeLabel: string | null;
  lastNumero: number | null;
  previousNumeros: number[];
  neighbours: number[];
  cells: LotoCell[];
  drawnCount: number;
  lots: LotoLot[];
  showBingo: boolean;
  bingoNumeros: number[];
  bingoWon: boolean;
  firstPartie: LotoPartieSummary | null;
  nextPartie: LotoPartieSummary | null;
}

const LINE_LABELS: Record<string, string> = {
  OneLine: '1 ligne',
  TwoLine: '2 lignes',
  CartonPlein: 'Carton plein',
};

const TYPE_LABELS: Record<string, string> = {
  Americaine: 'Américaine',
  PlusUnMoinsUn: '+1 / −1',
};

export const LOTO_NUMBERS: readonly number[] = Array.from({length: 90}, (_, index) => index + 1);

export function lotoLineLabel(numberLine: string): string {
  return LINE_LABELS[numberLine] ?? numberLine;
}

export function lotoTypeLabel(partieType: string): string | null {
  return TYPE_LABELS[partieType] ?? null;
}

export function buildLotoTableauView(event: LotoEventSnapshot | null): LotoTableauView {
  const parties = [...(event?.parties ?? [])].sort((a, b) => a.index - b.index);
  const current = event === null ? undefined : parties.find(partie => partie.index === event.currentPartieIndex);
  const phase = resolvePhase(event, parties, current);
  const line = current === undefined
    ? undefined
    : [...current.lineParties].sort((a, b) => a.index - b.index)[current.currentLineIndex];
  const lastNumero = current?.lastNumeros.at(-1) ?? null;
  const neighbours = current ? neighboursOf(current) : [];
  const previousPartie = current === undefined
    ? undefined
    : parties.find(partie => partie.index === current.index - 1);
  const live = new Set(current?.liveNumeros ?? []);
  const neighbourSet = new Set(neighbours);
  const next = current === undefined ? undefined : parties.find(partie => partie.index === current.index + 1);

  return {
    phase,
    intermission: current !== undefined
      && current.index > 0
      && current.lastNumeros.length === 0
      && previousPartie?.pauseAfter === true,
    eventName: event?.name ?? '',
    partieNumber: (current?.index ?? 0) + 1,
    partieCount: parties.length,
    partieName: current?.name ?? '',
    lineLabel: line ? lotoLineLabel(line.numberLine) : null,
    timesLabel: line ? timesLabelOf(line) : null,
    typeLabel: current ? lotoTypeLabel(current.partieType) : null,
    lastNumero,
    previousNumeros: (current?.lastNumeros ?? []).slice(0, -1).reverse().slice(0, 5),
    neighbours,
    cells: LOTO_NUMBERS.map(value => ({
      value,
      state: value === lastNumero
        ? 'last'
        : neighbourSet.has(value)
          ? 'neighbour'
          : live.has(value) ? 'drawn' : 'idle',
    })),
    drawnCount: live.size,
    lots: (line?.lots ?? []).map(lot => ({id: lot.id, name: lot.name, won: lot.isWon !== null})),
    showBingo: current?.partieType !== 'Bingo',
    bingoNumeros: event?.bingoNumeros ?? [],
    bingoWon: event?.bingoHasBeenWon ?? false,
    firstPartie: parties[0] ? summaryOf(parties[0]) : null,
    nextPartie: next ? summaryOf(next) : null,
  };
}

function resolvePhase(
  event: LotoEventSnapshot | null,
  parties: LotoPartieSnapshot[],
  current: LotoPartieSnapshot | undefined,
): LotoPhase {
  if (event === null) {
    return 'loading';
  }
  if (parties.length === 0) {
    return 'before';
  }
  if (current === undefined) {
    return event.currentPartieIndex >= parties.length ? 'finished' : 'before';
  }
  const anyNumberDrawn = parties.some(partie => partie.lastNumeros.length > 0);
  return anyNumberDrawn ? 'playing' : 'before';
}

/**
 * Au +1 / −1, chaque tirage allume aussi ses deux voisins (90 et 1 se touchent). Les
 * voisins du dernier numéro sont les numéros allumés après lui dans `liveNumeros`.
 */
function neighboursOf(partie: LotoPartieSnapshot): number[] {
  const lastNumero = partie.lastNumeros.at(-1);
  if (partie.partieType !== 'PlusUnMoinsUn' || lastNumero === undefined) {
    return [];
  }
  const position = partie.liveNumeros.lastIndexOf(lastNumero);
  return position < 0 ? [] : partie.liveNumeros.slice(position + 1);
}

/** En « 1 ligne », deux lots se gagnent l'un après l'autre sur la même ligne. */
function timesLabelOf(line: LotoLineSnapshot): string | null {
  if (line.numberLine !== 'OneLine') {
    return null;
  }
  const stillWinnable = line.lots.filter(lot => lot.isWon === null).length;
  if (stillWinnable === 2) {
    return '1ʳᵉ fois';
  }
  return stillWinnable === 1 ? '2ᵉ fois' : null;
}

function summaryOf(partie: LotoPartieSnapshot): LotoPartieSummary {
  return {
    name: partie.name,
    typeLabel: lotoTypeLabel(partie.partieType),
    lines: [...partie.lineParties]
      .sort((a, b) => a.index - b.index)
      .map(line => ({label: lotoLineLabel(line.numberLine), lots: line.lots.map(lot => lot.name)})),
  };
}
