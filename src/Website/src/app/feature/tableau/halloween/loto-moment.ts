import {LotoEventSnapshot, LotoPartieSnapshot} from '@vpd/ui';

export type LotoMoment =
  | {kind: 'bingo'}
  | {kind: 'line'; lotName: string; endsPartie: boolean}
  | {kind: 'partie'}
  | {kind: 'numero'; value: number}
  | {kind: 'undo'; value: number};

/** Décrit le changement de jeu le plus important entre deux états reçus. */
export function detectLotoMoment(previous: LotoEventSnapshot, next: LotoEventSnapshot): LotoMoment | null {
  if (previous.id !== next.id) {
    return null;
  }

  if (!previous.bingoHasBeenWon && next.bingoHasBeenWon) {
    return {kind: 'bingo'};
  }

  const lotName = newlyWonLotName(previous.parties, next.parties);
  if (lotName !== null) {
    return {
      kind: 'line',
      lotName,
      endsPartie: next.currentPartieIndex > previous.currentPartieIndex,
    };
  }

  if (next.currentPartieIndex > previous.currentPartieIndex) {
    return {kind: 'partie'};
  }

  if (next.currentPartieIndex !== previous.currentPartieIndex) {
    return null;
  }

  const previousPartie = currentPartie(previous);
  const nextPartie = currentPartie(next);
  if (previousPartie === undefined || nextPartie === undefined || previousPartie.id !== nextPartie.id) {
    return null;
  }

  if (nextPartie.lastNumeros.length > previousPartie.lastNumeros.length) {
    const value = nextPartie.lastNumeros.at(-1);
    return value === undefined ? null : {kind: 'numero', value};
  }

  if (nextPartie.lastNumeros.length < previousPartie.lastNumeros.length) {
    const value = previousPartie.lastNumeros.at(-1);
    return value === undefined ? null : {kind: 'undo', value};
  }

  return null;
}

function currentPartie(event: LotoEventSnapshot): LotoPartieSnapshot | undefined {
  return (event.parties ?? []).find(partie => partie.index === event.currentPartieIndex);
}

function newlyWonLotName(
  previousParties: LotoPartieSnapshot[] | null,
  nextParties: LotoPartieSnapshot[] | null,
): string | null {
  const previousWonState = new Map<string, boolean | null>();
  for (const partie of previousParties ?? []) {
    for (const line of partie.lineParties) {
      for (const lot of line.lots) {
        previousWonState.set(`${partie.id}:${lot.id}`, lot.isWon);
      }
    }
  }

  for (const partie of nextParties ?? []) {
    for (const line of partie.lineParties) {
      for (const lot of line.lots) {
        if (previousWonState.get(`${partie.id}:${lot.id}`) === null && lot.isWon !== null) {
          return lot.name;
        }
      }
    }
  }

  return null;
}
