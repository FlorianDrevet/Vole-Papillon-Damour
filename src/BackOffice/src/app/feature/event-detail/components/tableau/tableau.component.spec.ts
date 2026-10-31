import {CommonModule} from '@angular/common';
import {NO_ERRORS_SCHEMA} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {ActivatedRoute, convertToParamMap} from '@angular/router';
import {of} from 'rxjs';
import {VpdEventModel} from '../../../../shared/models/vpdEvent.model';
import {LotoFacadeService} from '../../../../shared/facades/loto.facade.service';
import {VpdEventsFacadeService} from '../../../../shared/facades/vpd-events.facade.service';
import {LotoPaletteService} from './loto-palette.service';
import {TableauComponent} from './tableau.component';

interface TableauTestApi {
  assoEvent: {set(value: VpdEventModel | null): void};
  screen: () => string;
  view: () => {lastNumero: number | null; drawnCount: number};
  controlsVisible: () => boolean;
  palette: () => string;
  onKeydown: (event: KeyboardEvent) => void;
}

describe('TableauComponent keyboard controls', () => {
  let fixture: ComponentFixture<TableauComponent>;
  let component: TableauTestApi;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [TableauComponent],
      imports: [CommonModule],
      providers: [
        {provide: ActivatedRoute, useValue: {paramMap: of(convertToParamMap({}))}},
        {provide: VpdEventsFacadeService, useValue: {getEventById$: jasmine.createSpy()}},
        {provide: LotoFacadeService, useValue: {}},
        LotoPaletteService,
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(TableauComponent);
    component = fixture.componentInstance as unknown as TableauTestApi;
    component.assoEvent.set({
      id: 'event-1',
      name: 'Loto',
      parties: [],
      currentPartieIndex: 0,
      bingoNumeros: [],
      bingoHasBeenWon: false,
    } as unknown as VpdEventModel);
    fixture.detectChanges();
  });

  afterEach(() => {
    fixture.destroy();
    localStorage.removeItem(LotoPaletteService.storageKey);
  });

  it('opens the number grid with G before the first draw, without drawing a number', () => {
    expect(component.screen()).toBe('before');

    component.onKeydown(new KeyboardEvent('keydown', {key: 'g'}));
    fixture.detectChanges();

    expect(component.screen()).toBe('board');
    expect(component.view().lastNumero).toBeNull();
    expect(component.view().drawnCount).toBe(0);
  });

  it('keeps the help bar hidden for a recognized shortcut and shows it for an unknown key', () => {
    component.onKeydown(new KeyboardEvent('keydown', {key: 'p'}));
    fixture.detectChanges();

    expect(component.palette()).toBe('light');
    expect(component.controlsVisible()).toBeFalse();
    expect(fixture.nativeElement.querySelector('.bar')).toBeNull();

    component.onKeydown(new KeyboardEvent('keydown', {key: 'x'}));
    fixture.detectChanges();

    expect(component.controlsVisible()).toBeTrue();
    expect(fixture.nativeElement.querySelector('.bar')).not.toBeNull();
  });

  it('does not block native Enter activation when there is no number to submit', () => {
    const event = new KeyboardEvent('keydown', {key: 'Enter', cancelable: true});

    component.onKeydown(event);

    expect(event.defaultPrevented).toBeFalse();
    expect(component.controlsVisible()).toBeFalse();
  });

  it('applies the high-contrast projection palette after two P presses', () => {
    component.onKeydown(new KeyboardEvent('keydown', {key: 'p'}));
    component.onKeydown(new KeyboardEvent('keydown', {key: 'p'}));
    fixture.detectChanges();

    const viewport = fixture.nativeElement.querySelector('.viewport') as HTMLElement;
    const styles = getComputedStyle(viewport);
    expect(viewport.classList.contains('pal-c')).toBeTrue();
    expect(styles.getPropertyValue('--cell-num').trim()).toBe('#ffffff');
    expect(styles.getPropertyValue('--on').trim()).toBe('#ffe066');
  });

  it('gives the help toolbar room to wrap without compressing its contents', () => {
    component.onKeydown(new KeyboardEvent('keydown', {key: 'x'}));
    fixture.detectChanges();

    const toolbar = fixture.nativeElement.querySelector('.bar') as HTMLElement;
    const styles = getComputedStyle(toolbar);
    expect(parseFloat(styles.width)).toBeGreaterThan(1700);
    expect(styles.flexWrap).toBe('wrap');
  });
});
