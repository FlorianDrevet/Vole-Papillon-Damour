import {AfterViewChecked, Component, effect, ElementRef, inject, Input, OnChanges, PLATFORM_ID, SimpleChanges, ViewChild} from '@angular/core';
import {isPlatformBrowser} from '@angular/common';
import {RouterLink} from '@angular/router';
import {LotoEventSnapshot, LotoTableauView} from '@vpd/ui';
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
    if (changes['event']) {
      this.fx.accept(this.event as LotoEventSnapshot | null);
    }
  }

  ngAfterViewChecked(): void {
    const moment = this.fx.moment();
    const key = this.fx.animationKey();
    if (!isPlatformBrowser(this.platformId) || moment?.kind !== 'numero' || key === this.lastBatsKey) {
      return;
    }

    this.lastBatsKey = key;
    queueMicrotask(() => this.playBats(key));
  }

  protected toggleCalm(): void {
    this.preferences.toggleCalm();
    if (this.preferences.calm()) {
      this.fx.cancelCurrent();
    }
  }

  protected isUndoCell(value: number, key: 'a' | 'b'): boolean {
    const moment = this.fx.moment();
    return moment?.kind === 'undo' && moment.value === value && this.fx.animationKey() === key;
  }

  private playBats(key: 'a' | 'b'): void {
    const moment = this.fx.moment();
    if (moment?.kind !== 'numero' || this.fx.animationKey() !== key || this.preferences.calm()) {
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
