import {signal} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {provideRouter} from '@angular/router';
import {buildLotoTableauView, LotoEventSnapshot, LotoPartieSnapshot} from '@vpd/ui';
import {LiveTableauConnection} from '../../../shared/clients/sse-client.service';
import {VpdEventModel} from '../../../shared/models/vpdEvent.model';
import {HalloweenPreferences} from '../../../shared/components/mascots/halloween-preferences';
import {TableauHalloweenComponent} from './tableau-halloween.component';

function loto(partieType = 'Standard', lotWon = true, bingoWon = false): LotoEventSnapshot {
  const lastNumeros = partieType === 'PlusUnMoinsUn' ? [10, 21] : [5, 11, 18, 27, 39, 90];
  const liveNumeros = partieType === 'PlusUnMoinsUn' ? [10, 9, 11, 21, 20, 22] : lastNumeros;
  const partie: LotoPartieSnapshot = {
    id: 'partie-1',
    name: 'Partie 1',
    partieType,
    index: 0,
    pauseAfter: false,
    lastNumeros,
    liveNumeros,
    currentLineIndex: 0,
    lineParties: [{
      index: 0,
      numberLine: 'OneLine',
      lots: [
        {id: 'lot-1', name: 'Bon d’achat', isWon: lotWon ? true : null},
        {id: 'lot-2', name: 'Panier garni', isWon: null},
      ],
    }],
  };

  return {
    id: 'loto-1',
    name: 'Loto de test',
    parties: [partie],
    currentPartieIndex: 0,
    bingoNumeros: [7, 19, 33, 58, 81],
    bingoHasBeenWon: bingoWon,
  };
}

describe('TableauHalloweenComponent', () => {
  let fixture: ComponentFixture<TableauHalloweenComponent>;
  let preferences: {
    calm: ReturnType<typeof signal<boolean>>;
    visible: ReturnType<typeof signal<boolean>>;
    visibleSince: ReturnType<typeof signal<number>>;
    toggleCalm: jasmine.Spy;
  };

  async function render(snapshot = loto()): Promise<HTMLElement> {
    fixture = TestBed.createComponent(TableauHalloweenComponent);
    fixture.componentRef.setInput('view', buildLotoTableauView(snapshot));
    fixture.componentRef.setInput('event', snapshot as unknown as VpdEventModel);
    fixture.componentRef.setInput('connection', 'live' as LiveTableauConnection);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(async () => {
    preferences = {
      calm: signal(false),
      visible: signal(true),
      visibleSince: signal(Date.now() - 5000),
      toggleCalm: jasmine.createSpy('toggleCalm').and.callFake(() => preferences.calm.update(value => !value)),
    };
    await TestBed.configureTestingModule({
      imports: [TableauHalloweenComponent],
      providers: [provideRouter([])],
    }).overrideProvider(HalloweenPreferences, {useValue: preferences as unknown as HalloweenPreferences})
      .compileComponents();
  });

  it('affiche le dernier numéro, cinq précédents et les 90 cases', async () => {
    const element = await render();

    expect(element.querySelector('[aria-live="polite"]')?.textContent).toContain('90');
    expect(element.querySelectorAll('.tomb').length).toBe(5);
    expect(Array.from(element.querySelectorAll('.tomb')).map(tomb => tomb.textContent?.trim()))
      .toEqual(['39', '27', '18', '11', '5']);
    expect(element.querySelectorAll('.cell').length).toBe(90);
    expect(element.querySelector('.cell.last')?.textContent?.trim()).toBe('90');
  });

  it('marque les voisins du +1 / −1 avec leur anneau', async () => {
    const element = await render(loto('PlusUnMoinsUn'));

    const neighbourCells = Array.from(element.querySelectorAll('.cell.nb')).map(cell => cell.textContent?.trim());
    expect(neighbourCells).toEqual(['20', '22']);
  });

  it('garde le lot gagné lisible, sans barre, avec son libellé', async () => {
    const element = await render();

    const wonLot = element.querySelector('.lot-won');
    expect(wonLot?.querySelector('s')).toBeNull();
    expect(wonLot?.querySelector('span')?.textContent).toBe('Bon d’achat');
    expect(wonLot?.textContent).toContain('gagné');
    expect(element.textContent).toContain('À gagner sur cette ligne');
  });

  it('affiche l’état de connexion sans capitales espacées', async () => {
    const element = await render();

    expect(element.textContent).toContain('En direct');
    expect(element.textContent).not.toContain('EN DIRECT');
  });

  it('suit la préférence calme et permet de l’inverser au clavier ou au toucher', async () => {
    const element = await render();

    expect(element.querySelector('.hw')?.classList.contains('calm')).toBeFalse();
    const button = element.querySelector<HTMLButtonElement>('[aria-label="Calmer les animations"]');
    expect(button?.textContent?.trim()).toBe('Calmer');
    expect(button?.getAttribute('aria-pressed')).toBe('false');

    button?.click();
    fixture.detectChanges();

    expect(preferences.toggleCalm).toHaveBeenCalledTimes(1);
    expect(element.querySelector('.hw')?.classList.contains('calm')).toBeTrue();
    const activeButton = element.querySelector<HTMLButtonElement>('[aria-label="Remettre les animations"]');
    expect(activeButton?.textContent?.trim()).toBe('Animer');
    expect(activeButton?.getAttribute('aria-pressed')).toBe('true');
  });

  it('met les animations en pause quand l’onglet est caché', async () => {
    const element = await render();
    preferences.visible.set(false);
    fixture.detectChanges();

    expect(element.querySelector('.hw')?.classList.contains('paused')).toBeTrue();
  });

  it('joue une ligne gagnée avec trois citrouilles, 26 bonbons et le nom du lot', async () => {
    const element = await render(loto('Standard', false));
    const next = loto('Standard', true);
    fixture.componentRef.setInput('view', buildLotoTableauView(next));
    fixture.componentRef.setInput('event', next as unknown as VpdEventModel);
    fixture.detectChanges();

    expect(element.querySelectorAll('.fx .pk-rire').length).toBe(3);
    expect(element.querySelectorAll('.fx .candy').length).toBe(26);
    expect(element.querySelector('.fx .banner-title')?.textContent?.trim()).toBe('Ligne gagnée !');
    expect(element.querySelector('.fx .banner-lot')?.textContent?.trim()).toBe('Bon d’achat');
  });

  it('joue un bingo avec 40 bonbons puis retire les effets après 5,2 secondes', async () => {
    jasmine.clock().install();
    try {
      const element = await render(loto('Standard', true, false));
      const next = loto('Standard', true, true);
      fixture.componentRef.setInput('view', buildLotoTableauView(next));
      fixture.componentRef.setInput('event', next as unknown as VpdEventModel);
      fixture.detectChanges();

      expect(element.querySelectorAll('.fx .candy').length).toBe(40);
      expect(element.querySelectorAll('.fx .bat').length).toBe(5);
      expect(element.querySelector('.fx .banner-title')?.textContent?.trim()).toBe('Bingo !');

      jasmine.clock().tick(5200);
      fixture.detectChanges();
      expect(element.querySelector('.fx')).toBeNull();
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('affiche l’état avant le début avec la citrouille endormie et la première partie', async () => {
    const before = loto('Standard', false);
    before.parties?.[0].lastNumeros.splice(0);
    before.parties?.[0].liveNumeros.splice(0);
    const element = await render(before);

    expect(element.querySelector('.state-before h1')?.textContent?.trim()).toBe('Le loto va commencer');
    expect(element.querySelector('.state-before .pk-dort')).not.toBeNull();
    expect(element.querySelector('.state-before .first-partie')?.textContent).toContain('Partie 1');
    expect(element.querySelector('.state-before .first-partie')?.textContent).toContain('Panier garni');
  });

  it('affiche la pause et ne montre pas la grille', async () => {
    const element = await render();
    fixture.componentRef.setInput('view', {
      ...buildLotoTableauView(loto()),
      intermission: true,
      partieNumber: 2,
      partieCount: 8,
      partieName: 'Les feux follets',
      lineLabel: '2 lignes',
      lots: [{id: 'lot-next', name: 'Coffret de chocolats', won: false}],
    });
    fixture.detectChanges();

    expect(element.querySelector('.state-pause h1')?.textContent?.trim()).toBe('Pause');
    expect(element.querySelector('.state-pause')?.textContent).toContain('On reprend dans quelques minutes.');
    expect(element.querySelector('.state-pause')?.textContent).toContain('Ensuite, partie 2 sur 8');
    expect(element.querySelector('.state-pause')?.textContent).toContain('Coffret de chocolats');
    expect(element.querySelector('.grid')).toBeNull();
  });

  it('affiche la fin du loto, cinq bonbons décoratifs et le lien vers les événements', async () => {
    const element = await render();
    fixture.componentRef.setInput('view', {
      ...buildLotoTableauView(loto()),
      phase: 'finished',
      showBingo: true,
      bingoWon: true,
    });
    fixture.detectChanges();

    expect(element.querySelector('.state-finished h1')?.textContent?.trim()).toBe('Loto terminé');
    expect(element.querySelector('.state-finished')?.textContent).toContain('Merci à tous !');
    expect(element.querySelector('.state-finished')?.textContent).toContain('Les bénéfices de cet après-midi soutiennent Maxence et sa famille.');
    expect(element.querySelectorAll('.end-candy').length).toBe(5);
    expect(element.querySelector('.state-finished a')?.textContent?.trim()).toBe('Voir les prochains événements');
  });

  it('affiche des silhouettes de chargement accessibles sans blocs génériques', async () => {
    const element = await render();
    fixture.componentRef.setInput('view', {...buildLotoTableauView(loto()), phase: 'loading'});
    fixture.detectChanges();

    expect(element.querySelector('.state-loading')?.getAttribute('aria-label')).toBe('Chargement du tableau de loto');
    expect(element.querySelectorAll('.loading-silhouette').length).toBeGreaterThan(0);
    expect(element.querySelector('.skeleton-gray')).toBeNull();
  });

  it('garde le dernier état pendant la reconnexion puis distingue le mode hors ligne', async () => {
    const element = await render(loto());
    fixture.componentRef.setInput('connection', 'reconnecting');
    fixture.detectChanges();

    expect(element.querySelector('.connection-alert')?.textContent).toContain('dès le retour du réseau.');
    expect(element.querySelectorAll('.cell').length).toBe(90);
    expect(element.querySelector('.cell.last')?.textContent?.trim()).toBe('90');

    fixture.componentRef.setInput('connection', 'offline');
    fixture.detectChanges();
    expect(element.querySelector('.status')?.textContent).toContain('Hors ligne');
  });

  it('affiche un tableau introuvable avec le fantôme lanterne et le lien des événements', async () => {
    const element = await render();
    fixture.componentRef.setInput('connection', 'not-found');
    fixture.detectChanges();

    expect(element.querySelector('.state-not-found .gh-lanterne')).not.toBeNull();
    expect(element.querySelector('.state-not-found h1')?.textContent?.trim()).toBe('Ce tableau n\'existe pas ou n\'est plus disponible.');
    expect(element.querySelector('.state-not-found')?.textContent).toContain('Vérifiez le lien, ou retrouvez le loto du jour depuis la page des événements.');
    expect(element.querySelector('.state-not-found a')?.textContent?.trim()).toBe('Voir les événements');
  });

  it('ouvre les yeux de la citrouille quand le premier numéro arrive', async () => {
    const before = loto('Standard', false);
    before.parties?.[0].lastNumeros.splice(0);
    before.parties?.[0].liveNumeros.splice(0);
    const element = await render(before);
    const next = loto('Standard', false);
    next.parties![0].lastNumeros = [18];
    next.parties![0].liveNumeros = [18];
    fixture.componentRef.setInput('view', buildLotoTableauView(next));
    fixture.componentRef.setInput('event', next as unknown as VpdEventModel);
    fixture.detectChanges();

    expect(element.querySelector('.hero-pk .pk-sourire')).not.toBeNull();
  });
});
