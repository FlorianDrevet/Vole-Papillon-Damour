import {AfterViewChecked, Component, effect, ElementRef, inject, Input, OnChanges, PLATFORM_ID, SimpleChanges, ViewChild} from '@angular/core';
import {isPlatformBrowser} from '@angular/common';
import {RouterLink} from '@angular/router';
import {LotoCell, LotoEventSnapshot, LotoTableauView} from '@vpd/ui';
import {LiveTableauConnection} from '../../../shared/clients/sse-client.service';
import {VpdEventModel} from '../../../shared/models/vpdEvent.model';
import {HalloweenPreferences} from './halloween-preferences';
import {VpdChaudronComponent} from './characters/vpd-chaudron.component';
import {VpdCitrouilleComponent} from './characters/vpd-citrouille.component';
import {VpdFantomeComponent} from './characters/vpd-fantome.component';
import {VpdChauveSourisComponent} from './characters/vpd-chauve-souris.component';
import {HalloweenFx} from './halloween-fx';

const BAT_FLIGHTS: {duration: number; delay: number; frames: Keyframe[]}[] = [
  {duration: 1500, delay: 0, frames: [
    {transform: 'translate(-23.1vw,29.6vh) rotate(10deg)'},
    {offset: .35, transform: 'translate(23.1vw,20.7vh) rotate(-6deg)'},
    {offset: .7, transform: 'translate(66.7vw,14vh) rotate(5deg)'},
    {transform: 'translate(120.5vw,6.9vh) rotate(-12deg)'},
  ]},
  {duration: 1700, delay: 100, frames: [
    {transform: 'translate(120.5vw,39.1vh) scaleX(-1)'},
    {offset: .45, transform: 'translate(59vw,29.6vh) scaleX(-1) rotate(-6deg)'},
    {transform: 'translate(-25.6vw,23.2vh) scaleX(-1) rotate(8deg)'},
  ]},
  {duration: 1150, delay: 220, frames: [
    {transform: 'translate(-38.5vw,71.1vh) scale(1.15) rotate(6deg)'},
    {transform: 'translate(123.1vw,48.6vh) scale(1.15) rotate(-14deg)'},
  ]},
  {duration: 1800, delay: 320, frames: [
    {transform: 'translate(-12.8vw,14vh)'},
    {offset: .5, transform: 'translate(48.7vw,4.7vh) rotate(-8deg)'},
    {transform: 'translate(112.8vw,11.4vh)'},
  ]},
  {duration: 1500, delay: 420, frames: [
    {transform: 'translate(120.5vw,17.8vh) scaleX(-1)'},
    {offset: .55, transform: 'translate(41vw,8.3vh) scaleX(-1) rotate(10deg)'},
    {transform: 'translate(-23.1vw,2.8vh) scaleX(-1)'},
  ]},
];

const CANDY_COLORS = [
  ['#ff8c2e', '#ffb347'],
  ['#9bd85a', '#d7f2b0'],
  ['#8b5cc7', '#d2b8f2'],
  ['#f4e8cf', '#ff8c2e'],
] as const;

interface HalloweenCandy {
  index: number;
  kind: 'wrapped' | 'corn' | 'lollipop';
  leftVw: number;
  width: number;
  delayRatio: number;
  duration: number;
  spin: number;
  primary: string;
  secondary: string;
}

interface EndCandy {
  index: number;
  left: number;
  delay: number;
  turn: number;
  color: string;
}

function createCandies(): HalloweenCandy[] {
  return Array.from({length: 40}, (_, index) => {
    const kind = index % 3;
    const duration = 2.3 + ((index * 13) % 10) / 10;
    const colors = CANDY_COLORS[index % CANDY_COLORS.length];
    return {
      index,
      kind: kind === 0 ? 'wrapped' : kind === 1 ? 'corn' : 'lollipop',
      leftVw: ((((index * 73) % 350) + 6) / 390) * 100,
      width: kind === 0 ? 30 : kind === 1 ? 18 : 22,
      delayRatio: ((index * 37) % 100) / 100,
      duration,
      spin: duration * .62,
      primary: colors[0],
      secondary: colors[1],
    };
  });
}

function createEndCandies(): EndCandy[] {
  const colors = ['#ffb347', '#ff8c2e', '#d2b8f2', '#ffc94d', '#9bd85a'];
  return colors.map((color, index) => ({
    index,
    left: [8, 29, 51, 73, 90][index],
    delay: [.1, .5, .25, .4, .8][index],
    turn: [160, -240, 300, -120, 220][index],
    color,
  }));
}

@Component({
  selector: 'app-tableau-halloween',
  standalone: true,
  templateUrl: './tableau-halloween.component.html',
  styleUrl: './tableau-halloween.component.scss',
  imports: [RouterLink, VpdCitrouilleComponent, VpdChauveSourisComponent, VpdFantomeComponent, VpdChaudronComponent],
  providers: [HalloweenFx],
})
export class TableauHalloweenComponent implements OnChanges, AfterViewChecked {
  private readonly platformId = inject(PLATFORM_ID);
  protected readonly preferences = inject(HalloweenPreferences);
  protected readonly fx = inject(HalloweenFx);
  @Input({required: true}) view!: LotoTableauView;
  @Input({required: true}) event!: VpdEventModel | null;
  @Input({required: true}) connection!: LiveTableauConnection;
  @ViewChild('fxLayer') private fxLayer?: ElementRef<HTMLElement>;

  protected readonly tombTilts = [-2, 1.5, -1, 2, -1.5];
  protected readonly batSizes = [78, 54, 112, 40, 60];
  protected readonly candies = createCandies();
  protected readonly endCandies = createEndCandies();
  protected finishedCandyPlayed = false;
  private lastBatsKey: 'a' | 'b' | null = null;

  constructor() {
    effect(() => {
      if (this.preferences.calm()) {
        this.fx.cancelCurrent();
      }
      if (this.preferences.visible()) {
        this.fx.resumeAnimations();
      } else {
        this.fx.pauseAnimations();
      }
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['view'] && this.view?.phase === 'finished' && this.preferences.calm()) {
      this.finishedCandyPlayed = true;
    }
    if (changes['event']) {
      this.fx.accept(this.event as LotoEventSnapshot | null);
    }
  }

  ngAfterViewChecked(): void {
    const moment = this.fx.moment();
    const key = this.fx.animationKey();
    if (!isPlatformBrowser(this.platformId) || (moment?.kind !== 'numero' && moment?.kind !== 'bingo') || key === this.lastBatsKey) {
      return;
    }

    this.lastBatsKey = key;
    queueMicrotask(() => this.playBats(key));
  }

  protected toggleCalm(): void {
    this.preferences.toggleCalm();
    if (this.preferences.calm()) {
      this.fx.cancelCurrent();
      if (this.view?.phase === 'finished') {
        this.finishedCandyPlayed = true;
      }
    }
  }

  protected markFinishedCandyPlayed(): void {
    this.finishedCandyPlayed = true;
  }

  protected shouldShowFx(): boolean {
    return this.view?.phase === 'playing'
      && (this.fx.moment() !== null || this.fx.interrupting())
      && (!this.view.intermission || this.shouldDeferIntermission());
  }

  protected shouldDeferIntermission(): boolean {
    if (!this.view?.intermission) {
      return false;
    }
    const moment = this.fx.moment();
    return this.fx.interrupting() || moment?.kind === 'partie'
      || (moment?.kind === 'line' && moment.endsPartie);
  }

  protected isUndoCell(value: number, key: 'a' | 'b'): boolean {
    const moment = this.fx.moment();
    return moment?.kind === 'undo' && moment.value === value && this.fx.animationKey() === key;
  }

  protected lineLotName(): string | null {
    const moment = this.fx.moment();
    return moment?.kind === 'line' ? moment.lotName : null;
  }

  protected isLineEndingPartie(): boolean {
    const moment = this.fx.moment();
    return moment?.kind === 'line' && moment.endsPartie;
  }

  protected displayCells(): LotoCell[] {
    return this.fx.frozenCells() ?? this.view.cells;
  }

  protected shouldFadeCell(cell: LotoCell): boolean {
    if (this.fx.frozenCells() === null || cell.state === 'idle') {
      return false;
    }
    const moment = this.fx.moment();
    return moment?.kind === 'partie'
      || (moment?.kind === 'line' && moment.endsPartie && this.fx.momentPhase() === 'ghost');
  }

  protected cellOutDelay(value: number): string {
    return `${.45 + .164 * ((value - 1) % 10)}s`;
  }

  private playBats(key: 'a' | 'b'): void {
    const moment = this.fx.moment();
    if ((moment?.kind !== 'numero' && moment?.kind !== 'bingo')
      || this.fx.animationKey() !== key || this.preferences.calm()) {
      return;
    }

    const elements = this.fxLayer?.nativeElement.querySelectorAll<HTMLElement>('.bat');
    if (!elements) {
      return;
    }

    Array.from(elements).forEach((element, index) => {
      const flight = BAT_FLIGHTS[index];
      if (!flight || typeof element.animate !== 'function') {
        return;
      }
      const animation = element.animate(flight.frames, {
        duration: flight.duration,
        delay: flight.delay,
        easing: 'cubic-bezier(.42,0,.58,1)',
        fill: 'both',
      });
      this.fx.registerAnimation(animation);
    });
  }
}
