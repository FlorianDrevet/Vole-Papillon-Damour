import {signal} from '@angular/core';
import {TestBed} from '@angular/core/testing';
import {LotoEventSnapshot, LotoPartieSnapshot} from '@vpd/ui';
import {HalloweenPreferences} from './halloween-preferences';
import {HalloweenFx} from './halloween-fx';

function partie(lastNumeros: number[] = [], isWon: boolean | null = null, index = 0): LotoPartieSnapshot {
  return {
    id: `partie-${index + 1}`,
    name: `Partie ${index + 1}`,
    partieType: 'Standard',
    index,
    pauseAfter: false,
    lastNumeros,
    liveNumeros: lastNumeros,
    currentLineIndex: 0,
    lineParties: [{
      index: 0,
      numberLine: 'OneLine',
      lots: [{id: 'lot-1', name: 'Panier garni', isWon}],
    }],
  };
}

function loto(lastNumeros: number[] = [], isWon: boolean | null = null): LotoEventSnapshot {
  return {
    id: 'loto-1',
    name: 'Loto de test',
    parties: [partie(lastNumeros, isWon)],
    currentPartieIndex: 0,
    bingoNumeros: [],
    bingoHasBeenWon: false,
  };
}

function lotoWithTwoParties(currentPartieIndex: number, firstWon: boolean | null, secondNumeros: number[] = []): LotoEventSnapshot {
  return {
    ...loto(),
    parties: [partie([18], firstWon, 0), partie(secondNumeros, null, 1)],
    currentPartieIndex,
  };
}

describe('HalloweenFx', () => {
  let fx: HalloweenFx;
  let calm: ReturnType<typeof signal<boolean>>;
  let visible: ReturnType<typeof signal<boolean>>;
  let visibleSince: ReturnType<typeof signal<number>>;

  beforeEach(() => {
    calm = signal(false);
    visible = signal(true);
    visibleSince = signal(Date.now() - 4000);
    TestBed.configureTestingModule({
      providers: [
        HalloweenFx,
        {provide: HalloweenPreferences, useValue: {calm, visible, visibleSince}},
      ],
    });
    fx = TestBed.inject(HalloweenFx);
  });

  it('ne joue rien au premier état puis signale un nouveau numéro', () => {
    fx.accept(loto());
    expect(fx.moment()).toBeNull();

    fx.accept(loto([18]));
    expect(fx.moment()).toEqual({kind: 'numero', value: 18});
  });

  it('ignore les changements reçus en mode calme', () => {
    fx.accept(loto());
    calm.set(true);
    fx.accept(loto([18]));

    expect(fx.moment()).toBeNull();
    calm.set(false);
    fx.accept(loto([18]));
    expect(fx.moment()).toBeNull();
  });

  it('ignore les changements pendant les trois secondes suivant le retour visible', () => {
    jasmine.clock().install();
    try {
      jasmine.clock().mockDate();
      fx.accept(loto());
      visibleSince.set(Date.now());
      fx.accept(loto([18]));
      expect(fx.moment()).toBeNull();

      jasmine.clock().tick(3001);
      fx.accept(loto([18, 32]));
      expect(fx.moment()).toEqual({kind: 'numero', value: 32});
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('interrompt un numéro pour jouer la ligne gagnée après le fondu de 150 ms', () => {
    jasmine.clock().install();
    try {
      fx.accept(loto());
      fx.accept(loto([18]));
      const animation = {cancel: jasmine.createSpy('cancel')} as unknown as Animation;
      fx.registerAnimation(animation);

      fx.accept(loto([18], true));
      expect(animation.cancel).toHaveBeenCalledTimes(1);
      expect(fx.moment()).toBeNull();

      jasmine.clock().tick(150);
      expect(fx.moment()).toEqual({kind: 'line', lotName: 'Panier garni', endsPartie: false});
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('gèle les cases de la partie précédente puis les libère à deux secondes', () => {
    jasmine.clock().install();
    try {
      fx.accept(loto([18], true));
      fx.accept(lotoWithTwoParties(1, true));

      expect(fx.moment()).toEqual({kind: 'partie'});
      expect(fx.frozenCells()?.length).toBe(90);
      expect(fx.frozenCells()?.find(cell => cell.value === 18)?.state).toBe('last');

      jasmine.clock().tick(2000);
      expect(fx.frozenCells()).toBeNull();
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('enchaîne la ligne de fin de partie puis le fantôme sans figer la grille au-delà de 6,5 s', () => {
    jasmine.clock().install();
    try {
      fx.accept(loto([18], null));
      const next = {
        ...lotoWithTwoParties(1, true),
        parties: [partie([18], true, 0), partie([], null, 1)],
      };
      fx.accept(next);

      expect(fx.moment()).toEqual({kind: 'line', lotName: 'Panier garni', endsPartie: true});
      expect(fx.momentPhase()).toBe('main');
      expect(fx.frozenCells()?.length).toBe(90);

      jasmine.clock().tick(4200);
      expect(fx.momentPhase()).toBe('ghost');
      jasmine.clock().tick(2300);
      expect(fx.frozenCells()).toBeNull();
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('libère immédiatement une grille gelée si un numéro arrive dans la nouvelle partie', () => {
    jasmine.clock().install();
    try {
      fx.accept(loto([18], true));
      fx.accept(lotoWithTwoParties(1, true));
      expect(fx.frozenCells()).not.toBeNull();

      fx.accept(lotoWithTwoParties(1, true, [25]));
      expect(fx.frozenCells()).toBeNull();
      expect(fx.moment()).toEqual({kind: 'numero', value: 25});
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
