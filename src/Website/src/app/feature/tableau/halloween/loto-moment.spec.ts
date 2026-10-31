import {LotoEventSnapshot, LotoPartieSnapshot} from '@vpd/ui';
import {detectLotoMoment} from './loto-moment';

function partie(index: number, overrides: Partial<LotoPartieSnapshot> = {}): LotoPartieSnapshot {
  return {
    id: `partie-${index}`,
    name: `Partie ${index + 1}`,
    partieType: 'Standard',
    index,
    pauseAfter: false,
    lastNumeros: [],
    liveNumeros: [],
    currentLineIndex: 0,
    lineParties: [{
      index: 0,
      numberLine: 'OneLine',
      lots: [
        {id: 'lot-a', name: 'Bon d’achat', isWon: null},
        {id: 'lot-b', name: 'Panier garni', isWon: null},
      ],
    }],
    ...overrides,
  };
}

function loto(overrides: Partial<LotoEventSnapshot> = {}): LotoEventSnapshot {
  return {
    id: 'loto',
    name: 'Grand loto',
    parties: [partie(0)],
    currentPartieIndex: 0,
    bingoNumeros: [],
    bingoHasBeenWon: false,
    ...overrides,
  };
}

describe('detectLotoMoment', () => {
  it('ignore un état identique et un autre événement', () => {
    const current = loto();

    expect(detectLotoMoment(current, current)).toBeNull();
    expect(detectLotoMoment(current, loto({id: 'autre-loto', currentPartieIndex: 1}))).toBeNull();
  });

  it('détecte un bingo gagné avant une ligne gagnée', () => {
    const previous = loto();
    const winningPartie = partie(0, {
      lineParties: [{
        index: 0,
        numberLine: 'OneLine',
        lots: [
          {id: 'lot-a', name: 'Bon d’achat', isWon: true},
          {id: 'lot-b', name: 'Panier garni', isWon: null},
        ],
      }],
    });

    expect(detectLotoMoment(previous, loto({
      parties: [winningPartie],
      bingoHasBeenWon: true,
    }))).toEqual({kind: 'bingo'});
  });

  it('nomme le lot gagné et indique si une nouvelle partie arrive dans le même état', () => {
    const wonPartie = partie(0, {
      lineParties: [{
        index: 0,
        numberLine: 'OneLine',
        lots: [
          {id: 'lot-a', name: 'Bon d’achat', isWon: null},
          {id: 'lot-b', name: 'Panier garni', isWon: true},
        ],
      }],
    });

    expect(detectLotoMoment(loto(), loto({parties: [wonPartie]}))).toEqual({
      kind: 'line', lotName: 'Panier garni', endsPartie: false,
    });
    expect(detectLotoMoment(loto(), loto({
      parties: [wonPartie, partie(1)],
      currentPartieIndex: 1,
    }))).toEqual({kind: 'line', lotName: 'Panier garni', endsPartie: true});
  });

  it('priorise la ligne gagnée sur le changement de partie', () => {
    const wonPartie = partie(0, {
      lineParties: [{
        index: 0,
        numberLine: 'OneLine',
        lots: [
          {id: 'lot-a', name: 'Bon d’achat', isWon: true},
          {id: 'lot-b', name: 'Panier garni', isWon: null},
        ],
      }],
    });

    expect(detectLotoMoment(loto(), loto({
      parties: [wonPartie, partie(1)],
      currentPartieIndex: 1,
    }))).toEqual({kind: 'line', lotName: 'Bon d’achat', endsPartie: true});
  });

  it('détecte la partie suivante quand aucun lot n’a été gagné dans le même état', () => {
    expect(detectLotoMoment(loto(), loto({
      parties: [partie(0, {lastNumeros: [12]}), partie(1)],
      currentPartieIndex: 1,
    }))).toEqual({kind: 'partie'});
  });

  it('retient le dernier numéro reçu quand plusieurs tirages ont été rattrapés', () => {
    expect(detectLotoMoment(loto({
      parties: [partie(0, {lastNumeros: [4]})],
    }), loto({
      parties: [partie(0, {lastNumeros: [4, 18, 72]})],
    }))).toEqual({kind: 'numero', value: 72});
  });

  it('détecte le numéro retiré lors d’une annulation', () => {
    expect(detectLotoMoment(loto({
      parties: [partie(0, {lastNumeros: [8, 9, 10]})],
    }), loto({
      parties: [partie(0, {lastNumeros: [8, 9]})],
    }))).toEqual({kind: 'undo', value: 10});
  });
});
