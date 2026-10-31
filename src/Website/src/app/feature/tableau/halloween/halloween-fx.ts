import {DestroyRef, inject, Injectable, signal} from '@angular/core';
import {buildLotoTableauView, LotoCell, LotoEventSnapshot} from '@vpd/ui';
import {HalloweenPreferences} from '../../../shared/components/mascots/halloween-preferences';
import {detectLotoMoment, LotoMoment} from './loto-moment';

const INTERRUPT_FADE_MS = 150;

/** Déclenche les effets uniquement pour les changements reçus pendant que la page est ouverte. */
@Injectable()
export class HalloweenFx {
  private readonly preferences = inject(HalloweenPreferences);
  private readonly destroyRef = inject(DestroyRef);
  private previous: LotoEventSnapshot | null = null;
  private activeAnimations = new Set<Pick<Animation, 'cancel'>>();
  private momentTimeout: ReturnType<typeof setTimeout> | undefined;
  private interruptTimeout: ReturnType<typeof setTimeout> | undefined;
  private phaseTimeout: ReturnType<typeof setTimeout> | undefined;
  private freezeTimeout: ReturnType<typeof setTimeout> | undefined;
  private pendingMoment: LotoMoment | null = null;

  readonly moment = signal<LotoMoment | null>(null);
  readonly frozenCells = signal<LotoCell[] | null>(null);
  readonly animationKey = signal<'a' | 'b'>('a');
  readonly previousLastNumero = signal<number | null>(null);
  readonly interrupting = signal(false);
  readonly momentPhase = signal<'main' | 'ghost' | 'announce'>('main');

  constructor() {
    this.destroyRef.onDestroy(() => this.cancelCurrent());
  }

  accept(snapshot: LotoEventSnapshot | null): void {
    if (snapshot === null) {
      this.previous = null;
      this.previousLastNumero.set(null);
      this.cancelCurrent();
      return;
    }

    const previous = this.previous;
    this.previous = snapshot;
    this.previousLastNumero.set(previous?.id === snapshot.id ? currentLastNumero(previous) : null);
    if (previous === null || previous.id !== snapshot.id) {
      this.cancelCurrent();
      return;
    }

    if (this.preferences.calm() || !this.preferences.visible()
      || Date.now() - this.preferences.visibleSince() < 3000) {
      this.cancelCurrent();
      return;
    }

    const moment = detectLotoMoment(previous, snapshot);
    if (moment !== null) {
      const freezesPreviousGrid = moment.kind === 'partie' || (moment.kind === 'line' && moment.endsPartie);
      const frozenCells = freezesPreviousGrid ? buildLotoTableauView(previous).cells : null;
      this.play(moment, frozenCells);
    }
  }

  registerAnimation(animation: Pick<Animation, 'cancel'>): void {
    this.activeAnimations.add(animation);
  }

  pauseAnimations(): void {
    for (const animation of this.activeAnimations) {
      if ('pause' in animation && typeof animation.pause === 'function') {
        animation.pause();
      }
    }
  }

  resumeAnimations(): void {
    for (const animation of this.activeAnimations) {
      if ('play' in animation && typeof animation.play === 'function') {
        animation.play();
      }
    }
  }

  cancelCurrent(): void {
    clearTimeout(this.momentTimeout);
    clearTimeout(this.interruptTimeout);
    clearTimeout(this.phaseTimeout);
    clearTimeout(this.freezeTimeout);
    this.momentTimeout = undefined;
    this.interruptTimeout = undefined;
    this.phaseTimeout = undefined;
    this.freezeTimeout = undefined;
    this.pendingMoment = null;
    for (const animation of this.activeAnimations) {
      animation.cancel();
    }
    this.activeAnimations.clear();
    this.moment.set(null);
    this.frozenCells.set(null);
    this.interrupting.set(false);
    this.momentPhase.set('main');
  }

  private play(next: LotoMoment, frozenCells: LotoCell[] | null): void {
    const current = this.pendingMoment ?? this.moment();
    const newNumberReleasesGrid = next.kind === 'numero'
      && (current?.kind === 'partie' || (current?.kind === 'line' && current.endsPartie));
    if (current !== null && rank(next) < rank(current) && !newNumberReleasesGrid) {
      return;
    }

    if (newNumberReleasesGrid) {
      this.cancelCurrent();
      this.start(next, null);
      return;
    }

    if (current !== null) {
      this.cancelAnimations();
      clearTimeout(this.momentTimeout);
      clearTimeout(this.interruptTimeout);
      this.momentTimeout = undefined;
      this.moment.set(null);
      this.interrupting.set(true);
      this.pendingMoment = next;
      this.frozenCells.set(frozenCells);
      this.interruptTimeout = setTimeout(() => {
        this.interruptTimeout = undefined;
        this.pendingMoment = null;
        this.interrupting.set(false);
        this.start(next, frozenCells);
      }, INTERRUPT_FADE_MS);
      return;
    }

    this.start(next, frozenCells);
  }

  private start(moment: LotoMoment, frozenCells: LotoCell[] | null): void {
    this.animationKey.update(key => key === 'a' ? 'b' : 'a');
    this.moment.set(moment);
    this.frozenCells.set(frozenCells);
    this.momentPhase.set(moment.kind === 'partie' ? 'ghost' : 'main');

    if (moment.kind === 'partie') {
      this.freezeTimeout = setTimeout(() => this.frozenCells.set(null), 2000);
      this.phaseTimeout = setTimeout(() => this.momentPhase.set('announce'), 2200);
    } else if (moment.kind === 'line' && moment.endsPartie) {
      this.freezeTimeout = setTimeout(() => this.frozenCells.set(null), 6500);
      this.phaseTimeout = setTimeout(() => this.momentPhase.set('ghost'), 4200);
    }

    this.momentTimeout = setTimeout(() => {
      this.momentTimeout = undefined;
      this.cancelAnimations();
      this.moment.set(null);
      this.frozenCells.set(null);
      this.momentPhase.set('main');
    }, durationOf(moment));
  }

  private cancelAnimations(): void {
    for (const animation of this.activeAnimations) {
      animation.cancel();
    }
    this.activeAnimations.clear();
  }
}

function currentLastNumero(event: LotoEventSnapshot): number | null {
  const current = (event.parties ?? []).find(partie => partie.index === event.currentPartieIndex);
  return current?.lastNumeros.at(-1) ?? null;
}

function durationOf(moment: LotoMoment): number {
  switch (moment.kind) {
    case 'numero': return 1600;
    case 'undo': return 800;
    case 'line': return moment.endsPartie ? 6800 : 4200;
    case 'bingo': return 5200;
    case 'partie': return 4000;
  }
}

function rank(moment: LotoMoment): number {
  switch (moment.kind) {
    case 'bingo': return 5;
    case 'line': return 4;
    case 'partie': return 3;
    case 'numero': return 2;
    case 'undo': return 1;
  }
}
