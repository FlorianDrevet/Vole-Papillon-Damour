import {Component, computed, DestroyRef, HostListener, inject, OnInit, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {ActivatedRoute} from '@angular/router';
import {buildLotoTableauView, LotoCell} from '@vpd/ui';
import {VpdEventModel} from '../../../../shared/models/vpdEvent.model';
import {LotoFacadeService} from '../../../../shared/facades/loto.facade.service';
import {VpdEventsFacadeService} from '../../../../shared/facades/vpd-events.facade.service';
import {environment} from '../../../../../environments/environment';
import {LotoPaletteService} from './loto-palette.service';
import {reduceRegieKey, RegieAction, regieErrorMessage, shouldPauseAfterWin} from './loto-regie';

/** Format de référence de la toile : la scène est dessinée en 1920 × 1080 puis mise à l'échelle. */
const STAGE_WIDTH = 1920;
const STAGE_HEIGHT = 1080;
const CONTROLS_IDLE_MS = 3000;
const TOAST_MS = 4000;

type Overlay = 'none' | 'numero' | 'pause';

/**
 * Tableau du loto projeté en salle (E1), annonces plein écran (E2) et régie discrète
 * (E3) : un seul écran, recopié sur le vidéoprojecteur.
 */
@Component({
  selector: 'app-tableau',
  templateUrl: './tableau.component.html',
  // Styles : src/scss/_loto-projection.scss.
  host: {class: 'loto-projection'},
  standalone: false
})
export class TableauComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly eventFacade = inject(VpdEventsFacadeService);
  private readonly lotoFacade = inject(LotoFacadeService);
  protected readonly paletteService = inject(LotoPaletteService);

  protected readonly shortUrl = 'volepapillondamour.fr/loto';
  protected readonly assoEvent = signal<VpdEventModel | null>(null);
  protected readonly view = computed(() => buildLotoTableauView(this.assoEvent()));
  protected readonly overlay = signal<Overlay>('none');
  protected readonly announcedNumero = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly toast = signal<string | null>(null);
  protected readonly controlsVisible = signal(true);
  protected readonly keyBuffer = signal('');
  protected readonly stageScale = signal(1);
  protected readonly palette = this.paletteService.palette;

  /** Écran affiché : les annonces (E2) passent devant le tableau (E1). */
  protected readonly screen = computed(() => {
    const phase = this.view().phase;
    if (phase === 'loading') {
      return 'loading';
    }
    const overlay = this.overlay();
    if (overlay !== 'none') {
      return overlay;
    }
    return phase === 'playing' ? 'board' : phase;
  });

  private announceTimer: ReturnType<typeof setTimeout> | undefined;
  private controlsTimer: ReturnType<typeof setTimeout> | undefined;
  private toastTimer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.updateStageScale();
    this.showControls();
    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const id = params.get('id');
        if (id !== null) {
          void this.eventFacade.getEventById$(id).then(event => this.assoEvent.set(event));
        }
      });
    this.destroyRef.onDestroy(() => {
      clearTimeout(this.announceTimer);
      clearTimeout(this.controlsTimer);
      clearTimeout(this.toastTimer);
    });
  }

  @HostListener('window:resize')
  updateStageScale(): void {
    this.stageScale.set(Math.min(window.innerWidth / STAGE_WIDTH, window.innerHeight / STAGE_HEIGHT));
  }

  /** RG-04 : la barre et le curseur n'apparaissent que tant que la souris bouge. */
  @HostListener('document:mousemove')
  showControls(): void {
    this.controlsVisible.set(true);
    clearTimeout(this.controlsTimer);
    this.controlsTimer = setTimeout(() => this.controlsVisible.set(false), CONTROLS_IDLE_MS);
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.ctrlKey || event.metaKey || event.altKey) {
      return;
    }
    const result = reduceRegieKey(this.keyBuffer(), event.key);
    if (result.action !== null || result.buffer !== this.keyBuffer()) {
      event.preventDefault();
    }
    this.keyBuffer.set(result.buffer);
    if (result.action !== null) {
      void this.run(result.action);
    }
  }

  protected onCellClicked(cell: LotoCell): void {
    void this.run({type: 'draw', numero: cell.value});
  }

  protected async run(action: RegieAction): Promise<void> {
    switch (action.type) {
      case 'palette':
        this.paletteService.toggle();
        return;
      case 'fullscreen':
        await this.toggleFullscreen();
        return;
      case 'pause':
        this.overlay.set(this.overlay() === 'pause' ? 'none' : 'pause');
        return;
    }

    const event = this.assoEvent();
    if (event === null || this.busy()) {
      return;
    }
    const alreadyDrawn = action.type === 'draw' && this.view().cells[action.numero - 1].state !== 'idle';
    if (alreadyDrawn) {
      this.showToast(regieErrorMessage(action, null, true));
      return;
    }

    this.busy.set(true);
    try {
      const updated = await this.send(event, action);
      this.assoEvent.set(updated);
      if (action.type === 'draw') {
        this.announce(action.numero);
      } else if (action.type === 'line' && shouldPauseAfterWin(event, updated)) {
        this.overlay.set('pause');
      }
    } catch (error) {
      this.showToast(regieErrorMessage(action, error, false));
    } finally {
      this.busy.set(false);
    }
  }

  private send(event: VpdEventModel, action: RegieAction): Promise<VpdEventModel> {
    switch (action.type) {
      case 'draw':
        return this.lotoFacade.postNumberToPartie$(event.id, action.numero);
      case 'undo':
        return this.lotoFacade.deleteRollBack$(event.id);
      case 'line':
        return this.lotoFacade.postWin$(event.id);
      default:
        return this.lotoFacade.postBingoWin$(event.id, !event.bingoHasBeenWon);
    }
  }

  /** RG-07 : le numéro occupe l'écran ~2 s ; une nouvelle saisie remplace l'annonce. */
  private announce(numero: number): void {
    clearTimeout(this.announceTimer);
    this.announcedNumero.set(numero);
    this.overlay.set('numero');
    this.announceTimer = setTimeout(() => {
      if (this.overlay() === 'numero') {
        this.overlay.set('none');
      }
    }, environment.time_numero_modal);
  }

  private showToast(message: string): void {
    clearTimeout(this.toastTimer);
    this.toast.set(message);
    this.toastTimer = setTimeout(() => this.toast.set(null), TOAST_MS);
  }

  private async toggleFullscreen(): Promise<void> {
    try {
      if (document.fullscreenElement) {
        await document.exitFullscreen();
      } else {
        await document.documentElement.requestFullscreen();
      }
    } catch {
      this.showToast('Le plein écran n’est pas disponible dans ce navigateur.');
    }
  }
}
