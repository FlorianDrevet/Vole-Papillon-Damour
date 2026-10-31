import {signal} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {provideRouter} from '@angular/router';
import {buildLotoTableauView, LotoEventSnapshot, LotoPartieSnapshot} from '@vpd/ui';
import {LiveTableauConnection} from '../../../shared/clients/sse-client.service';
import {VpdEventModel} from '../../../shared/models/vpdEvent.model';
import {HalloweenPreferences} from './halloween-preferences';
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

  it('barre le lot gagné et affiche son libellé', async () => {
    const element = await render();

    const wonLot = element.querySelector('.lot-won');
    expect(wonLot?.querySelector('s')?.textContent).toBe('Bon d’achat');
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
});
