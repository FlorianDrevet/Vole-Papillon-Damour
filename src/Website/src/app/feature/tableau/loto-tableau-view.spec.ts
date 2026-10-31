import {buildLotoTableauView, LotoEventSnapshot, LotoPartieSnapshot} from '@vpd/ui';

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
    lineParties: [
      {index: 0, numberLine: 'OneLine', lots: [
        {id: 'lot-a', name: 'Bon d’achat 30 €', isWon: null},
        {id: 'lot-b', name: 'Panier garni', isWon: null},
      ]},
      {index: 1, numberLine: 'TwoLine', lots: [{id: 'lot-c', name: 'Jambon', isWon: null}]},
    ],
    ...overrides,
  };
}

function loto(parties: LotoPartieSnapshot[], currentPartieIndex = 0): LotoEventSnapshot {
  return {id: 'loto', name: 'Grand loto', parties, currentPartieIndex, bingoNumeros: [14, 63], bingoHasBeenWon: false};
}

describe('buildLotoTableauView', () => {
  it('attend le chargement puis le premier numéro (RG-09)', () => {
    expect(buildLotoTableauView(null).phase).toBe('loading');
    expect(buildLotoTableauView(loto([])).phase).toBe('before');

    const view = buildLotoTableauView(loto([partie(0), partie(1)]));
    expect(view.phase).toBe('before');
    expect(view.firstPartie).toEqual({
      name: 'Partie 1',
      typeLabel: null,
      lines: [
        {label: '1 ligne', lots: ['Bon d’achat 30 €', 'Panier garni']},
        {label: '2 lignes', lots: ['Jambon']},
      ],
    });
  });

  it('décrit la partie en cours : dernier numéro, précédents, cases et lots', () => {
    const drawn = [3, 7, 12, 88, 31, 56, 75, 42];
    const view = buildLotoTableauView(loto([partie(0, {lastNumeros: drawn, liveNumeros: drawn}), partie(1)]));

    expect(view.phase).toBe('playing');
    expect(view.partieNumber).toBe(1);
    expect(view.partieCount).toBe(2);
    expect(view.lineLabel).toBe('1 ligne');
    expect(view.timesLabel).toBe('1ʳᵉ fois');
    expect(view.lastNumero).toBe(42);
    expect(view.previousNumeros).toEqual([75, 56, 31, 88, 12]);
    expect(view.cells.length).toBe(90);
    expect(view.cells[41]).toEqual({value: 42, state: 'last'});
    expect(view.cells[2]).toEqual({value: 3, state: 'drawn'});
    expect(view.cells[3]).toEqual({value: 4, state: 'idle'});
    expect(view.drawnCount).toBe(8);
    expect(view.nextPartie?.name).toBe('Partie 2');
    expect(view.showBingo).toBeTrue();
  });

  it('passe à « 2ᵉ fois » quand un lot de la ligne est gagné', () => {
    const current = partie(0, {lastNumeros: [5], liveNumeros: [5]});
    current.lineParties[0].lots[0].isWon = true;

    const view = buildLotoTableauView(loto([current]));

    expect(view.timesLabel).toBe('2ᵉ fois');
    expect(view.lots).toEqual([
      {id: 'lot-a', name: 'Bon d’achat 30 €', won: true},
      {id: 'lot-b', name: 'Panier garni', won: false},
    ]);
  });

  it('cercle les voisins du dernier numéro au +1 / −1, y compris de 90 à 1', () => {
    const current = partie(0, {
      partieType: 'PlusUnMoinsUn',
      lastNumeros: [18, 90],
      liveNumeros: [18, 17, 19, 90, 89, 1],
    });

    const view = buildLotoTableauView(loto([current]));

    expect(view.typeLabel).toBe('+1 / −1');
    expect(view.neighbours).toEqual([89, 1]);
    expect(view.cells[0].state).toBe('neighbour');
    expect(view.cells[88].state).toBe('neighbour');
    expect(view.cells[89].state).toBe('last');
    expect(view.cells[17].state).toBe('drawn');
  });

  it('montre la nouvelle partie vide après une ligne gagnée, puis la fin du loto', () => {
    const first = partie(0, {lastNumeros: [5, 9], liveNumeros: [5, 9]});

    const next = buildLotoTableauView(loto([first, partie(1)], 1));
    expect(next.phase).toBe('playing');
    expect(next.lastNumero).toBeNull();
    expect(next.previousNumeros).toEqual([]);

    expect(buildLotoTableauView(loto([first], 1)).phase).toBe('finished');
  });

  it('déduit la pause après une partie terminée et la quitte au premier numéro', () => {
    const previous = partie(0, {pauseAfter: true, lastNumeros: [12], liveNumeros: [12]});
    const next = partie(1);

    expect(buildLotoTableauView(loto([previous, next], 1)).intermission).toBeTrue();
    expect(buildLotoTableauView(loto([
      previous,
      partie(1, {lastNumeros: [27], liveNumeros: [27]}),
    ], 1)).intermission).toBeFalse();
    expect(buildLotoTableauView(loto([partie(0, {pauseAfter: true})])).intermission).toBeFalse();
  });

  it('masque le rappel du bingo pendant la partie Bingo', () => {
    const view = buildLotoTableauView(loto([partie(0, {partieType: 'Bingo', lastNumeros: [4], liveNumeros: [4]})]));

    expect(view.showBingo).toBeFalse();
  });

  it('affiche une partie carton plein seule malgré son index de ligne 2', () => {
    const current = partie(0, {
      partieType: 'CartonPlein',
      lineParties: [
        {index: 2, numberLine: 'CartonPlein', lots: [{id: 'lot-final', name: 'Séjour', isWon: null}]},
      ],
    });

    const view = buildLotoTableauView(loto([current]));

    expect(view.lineLabel).toBe('Carton plein');
    expect(view.lots).toEqual([{id: 'lot-final', name: 'Séjour', won: false}]);
  });
});
