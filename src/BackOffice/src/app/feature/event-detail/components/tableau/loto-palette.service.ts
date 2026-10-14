import {Injectable, signal} from '@angular/core';

/** RG-22 : A « sombre » (tableau lumineux) ou B « claire », choisie sur place le jour J. */
export type LotoPalette = 'dark' | 'light';

@Injectable({
  providedIn: 'root'
})
export class LotoPaletteService {
  static readonly storageKey = 'vpd.loto.palette';

  readonly palette = signal<LotoPalette>(LotoPaletteService.read());

  toggle(): void {
    const next: LotoPalette = this.palette() === 'dark' ? 'light' : 'dark';
    this.palette.set(next);
    try {
      localStorage.setItem(LotoPaletteService.storageKey, next);
    } catch {
      // Stockage indisponible (navigation privée) : la palette vaut pour la session.
    }
  }

  private static read(): LotoPalette {
    try {
      return localStorage.getItem(LotoPaletteService.storageKey) === 'light' ? 'light' : 'dark';
    } catch {
      return 'dark';
    }
  }
}
