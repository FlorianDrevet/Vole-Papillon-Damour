import {isPlatformBrowser} from '@angular/common';
import {Component, DestroyRef, effect, EventEmitter, inject, Input, OnChanges, Output, PLATFORM_ID, SimpleChanges, signal} from '@angular/core';
import {RouterLink} from '@angular/router';
import {HalloweenPreferences} from '../../../feature/tableau/halloween/halloween-preferences';
import {VpdChauveSourisComponent} from '../../../feature/tableau/halloween/characters/vpd-chauve-souris.component';
import {VpdCitrouilleComponent} from '../../../feature/tableau/halloween/characters/vpd-citrouille.component';
import {VpdFantomeComponent} from '../../../feature/tableau/halloween/characters/vpd-fantome.component';
import {TodayEventModel} from '../../../shared/models/today-event.model';
import {TodayBannerVariant} from './today-banner-state';
import {TodayBannerHalloweenMotionService} from './today-banner-halloween-motion.service';

@Component({
  selector: 'app-today-banner-halloween',
  standalone: true,
  imports: [RouterLink, VpdChauveSourisComponent, VpdCitrouilleComponent, VpdFantomeComponent],
  templateUrl: './today-banner-halloween.component.html',
})
export class TodayBannerHalloweenComponent implements OnChanges {
  private readonly destroyRef = inject(DestroyRef);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly preferences = inject(HalloweenPreferences);
  private readonly motion = inject(TodayBannerHalloweenMotionService);
  private hasInitialEvent = false;
  private previousEventId: string | null = null;
  private previousLastNumero: number | null = null;
  private runnerTimer: ReturnType<typeof setTimeout> | undefined;
  private batsTimer: ReturnType<typeof setTimeout> | undefined;

  @Input({required: true}) today!: TodayEventModel;
  @Input({required: true}) variant!: TodayBannerVariant;
  @Input() schedule = '';
  @Output() close = new EventEmitter<void>();

  protected readonly calm = this.preferences.calm;
  protected readonly runnerActive = signal(false);
  protected readonly batsActive = signal(false);
  protected readonly animationKey = signal<'a' | 'b'>('a');

  constructor() {
    effect(() => {
      if (this.preferences.calm()) {
        this.runnerActive.set(false);
        this.batsActive.set(false);
        clearTimeout(this.runnerTimer);
        clearTimeout(this.batsTimer);
      }
    });
    this.destroyRef.onDestroy(() => {
      clearTimeout(this.runnerTimer);
      clearTimeout(this.batsTimer);
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['today']) {
      return;
    }

    if (!this.hasInitialEvent) {
      this.hasInitialEvent = true;
      this.previousEventId = this.today.id;
      this.previousLastNumero = this.today.lastNumero;
      if (isPlatformBrowser(this.platformId) && !this.preferences.calm() && this.motion.claimEntrance()) {
        this.playRunner();
      }
      return;
    }

    const sameEvent = this.previousEventId === this.today.id;
    const numberChanged = this.previousLastNumero !== this.today.lastNumero;
    this.previousEventId = this.today.id;
    this.previousLastNumero = this.today.lastNumero;
    if (sameEvent && numberChanged && this.today.lastNumero !== null
      && this.variant === 'live' && isPlatformBrowser(this.platformId) && !this.preferences.calm()) {
      this.playBats();
    }
  }

  private playRunner(): void {
    const duration = this.preferences.phone() ? 2200 : 2800;
    this.runnerActive.set(true);
    this.runnerTimer = setTimeout(() => this.runnerActive.set(false), duration);
  }

  private playBats(): void {
    this.animationKey.update(key => key === 'a' ? 'b' : 'a');
    this.batsActive.set(true);
    clearTimeout(this.batsTimer);
    this.batsTimer = setTimeout(() => this.batsActive.set(false), 1800);
  }
}
