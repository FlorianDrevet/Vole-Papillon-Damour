import {Injectable, signal} from '@angular/core';

/** RG-22 : sombre, claire ou contraste renforcé pour les essais sur la toile. */
export type LotoPalette = 'dark' | 'light' | 'projection';

@Injectable({
  providedIn: 'root'
})
export class LotoPaletteService {
  static readonly storageKey = 'vpd.loto.palette';

  readonly palette = signal<LotoPalette>(LotoPaletteService.read());

  toggle(): void {
    const next: LotoPalette = this.palette() === 'dark'
      ? 'light'
      : this.palette() === 'light' ? 'projection' : 'dark';
    this.palette.set(next);
    try {
      localStorage.setItem(LotoPaletteService.storageKey, next);
    } catch {
      // Stockage indisponible (navigation privée) : la palette vaut pour la session.
    }
  }

  private static read(): LotoPalette {
    try {
      const stored = localStorage.getItem(LotoPaletteService.storageKey);
      return stored === 'light' || stored === 'projection' ? stored : 'dark';
    } catch {
      return 'dark';
    }
  }
}
