import {signal} from '@angular/core';
import {TestBed} from '@angular/core/testing';
import {LotoEventSnapshot, LotoPartieSnapshot} from '@vpd/ui';
import {HalloweenPreferences} from './halloween-preferences';
import {HalloweenFx} from './halloween-fx';

function partie(lastNumeros: number[] = [], isWon: boolean | null = null): LotoPartieSnapshot {
  return {
    id: 'partie-1',
    name: 'Partie 1',
    partieType: 'Standard',
    index: 0,
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
});
