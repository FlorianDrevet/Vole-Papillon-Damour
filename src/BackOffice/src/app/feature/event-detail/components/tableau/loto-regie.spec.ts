import {reduceRegieKey, regieErrorMessage, shouldPauseAfterWin} from './loto-regie';
import {VpdEventModel, VpdEventPartieModel} from '../../../../shared/models/vpdEvent.model';
import {PartieTypeEnum} from '../../../../shared/enums/partieType.enum';

describe('reduceRegieKey', () => {
  it('tire le numéro tapé à l’appui sur Entrée', () => {
    let state = reduceRegieKey('', '4');
    state = reduceRegieKey(state.buffer, '2');
    expect(state as unknown).toEqual({buffer: '42', action: null, handled: true});

    expect(reduceRegieKey(state.buffer, 'Enter') as unknown)
      .toEqual({buffer: '', action: {type: 'draw', numero: 42}, handled: true});
  });

  it('recommence la saisie après deux chiffres et refuse un numéro hors 1–90', () => {
    expect(reduceRegieKey('42', '7').buffer).toBe('7');
    expect(reduceRegieKey('95', 'Enter').action).toBeNull();
    expect(reduceRegieKey('0', 'Enter').action).toBeNull();
    expect(reduceRegieKey('', 'Enter').action).toBeNull();
  });

  it('efface un chiffre, ou annule le dernier numéro sans saisie en cours', () => {
    expect(reduceRegieKey('42', 'Backspace') as unknown).toEqual({buffer: '4', action: null, handled: true});
    expect(reduceRegieKey('', 'Backspace') as unknown)
      .toEqual({buffer: '', action: {type: 'undo'}, handled: true});
  });

  it('associe les raccourcis de la barre de régie', () => {
    expect(reduceRegieKey('', 'l').action).toEqual({type: 'line'});
    expect(reduceRegieKey('', 'B').action).toEqual({type: 'bingo'});
    expect(reduceRegieKey('', ' ').action).toEqual({type: 'pause'});
    expect(reduceRegieKey('', 'p').action).toEqual({type: 'palette'});
    expect(reduceRegieKey('', 'F').action).toEqual({type: 'fullscreen'});
    expect(reduceRegieKey('', 'g').action as unknown).toEqual({type: 'grid'});
    expect(reduceRegieKey('4', 'Escape') as unknown).toEqual({buffer: '', action: null, handled: true});
    expect(reduceRegieKey('4', 'x') as unknown).toEqual({buffer: '4', action: null, handled: false});
  });
});

describe('shouldPauseAfterWin', () => {
  it('ouvre la pause quand la ligne gagnée termine une partie « pause après »', () => {
    const before = loto(0, [partie(0, true), partie(1, false)]);

    expect(shouldPauseAfterWin(before, loto(1, before.parties))).toBeTrue();
    expect(shouldPauseAfterWin(before, loto(0, before.parties))).toBeFalse();
  });

  it('n’ouvre pas la pause pour une partie sans « pause après »', () => {
    const before = loto(0, [partie(0, false), partie(1, true)]);

    expect(shouldPauseAfterWin(before, loto(1, before.parties))).toBeFalse();
  });
});

describe('regieErrorMessage', () => {
  it('signale un numéro déjà sorti (RG-06)', () => {
    expect(regieErrorMessage({type: 'draw', numero: 42}, {response: {status: 409}}, true))
      .toBe('Le numéro 42 est déjà sorti.');
  });

  it('distingue un conflit d’une coupure réseau', () => {
    expect(regieErrorMessage({type: 'line'}, {response: {status: 409}}, false))
      .toBe('Une autre action vient d’être enregistrée sur ce tableau. Réessayez.');
    expect(regieErrorMessage({type: 'undo'}, new Error('Network Error'), false))
      .toBe('L’action n’a pas pu être enregistrée. Vérifiez la connexion et réessayez.');
  });
});

function partie(index: number, pauseAfter: boolean): VpdEventPartieModel {
  return {
    id: `partie-${index}`,
    name: `Partie ${index + 1}`,
    partieType: PartieTypeEnum.STANDARD,
    index,
    pauseAfter,
    addedBingoNumber: null,
    lastNumeros: [],
    liveNumeros: [],
    lineParties: [],
    currentLineIndex: 0,
  };
}

function loto(currentPartieIndex: number, parties: VpdEventPartieModel[]): VpdEventModel {
  return {currentPartieIndex, parties} as unknown as VpdEventModel;
}
