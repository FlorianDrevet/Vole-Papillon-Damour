import {LotoPaletteService} from './loto-palette.service';

describe('LotoPaletteService', () => {
  afterEach(() => localStorage.removeItem(LotoPaletteService.storageKey));

  it('démarre en palette sombre et mémorise la bascule sur le poste (RG-22)', () => {
    localStorage.removeItem(LotoPaletteService.storageKey);
    const first = new LotoPaletteService();
    expect(first.palette()).toBe('dark');

    first.toggle();

    expect(first.palette()).toBe('light');
    expect(new LotoPaletteService().palette()).toBe('light');
  });
});
