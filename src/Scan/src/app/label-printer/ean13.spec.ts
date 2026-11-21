import {
  EAN13_MODULE_COUNT,
  ean13BarRects,
  ean13CheckDigit,
  ean13Modules,
  isEan13,
  isInternalCode,
} from './ean13';

describe('ean13', () => {
  it('calcule la clé de contrôle d\'un EAN-13 connu', () => {
    expect(ean13CheckDigit('400638133393')).toBe(1);
  });

  it('valide un EAN-13 avec sa clé et rejette une clé fausse', () => {
    expect(isEan13('4006381333931')).toBeTrue();
    expect(isEan13('4006381333932')).toBeFalse();
  });

  it('rejette les valeurs qui ne sont pas des chiffres de longueur 13', () => {
    expect(isEan13('')).toBeFalse();
    expect(isEan13(null)).toBeFalse();
    expect(isEan13('978207040850')).toBeFalse();
    expect(isEan13('97820704085AB')).toBeFalse();
  });

  it('reconnaît un code interne 2xx et refuse un ISBN 978', () => {
    expect(isInternalCode('2100000000005')).toBeTrue();
    expect(isInternalCode('9782070408504')).toBeFalse();
  });

  it('refuse un code 2xx dont la clé est fausse', () => {
    expect(isInternalCode('2100000000001')).toBeFalse();
  });

  it('encode 95 modules encadrés par les gardes de début et de fin', () => {
    const modules = ean13Modules('4006381333931');

    expect(modules.length).toBe(EAN13_MODULE_COUNT);
    expect(modules.slice(0, 3)).toBe('101');
    expect(modules.slice(-3)).toBe('101');
    expect(modules.slice(45, 50)).toBe('01010');
  });

  it('encode les six chiffres de gauche selon le motif de parité du premier chiffre', () => {
    // Premier chiffre 4 : motif LGLLGG pour les chiffres 2 à 7 (0, 0, 6, 3, 8, 1).
    const left = ean13Modules('4006381333931').slice(3, 45);

    expect(left).toBe('0001101' + '0100111' + '0101111' + '0111101' + '0001001' + '0110011');
  });

  it('regroupe les barres en rectangles couvrant tous les modules à 1', () => {
    const code = '4006381333931';
    const rects = ean13BarRects(code);
    const ones = ean13Modules(code).split('').filter(module => module === '1').length;

    expect(rects.every(rect => rect.width >= 1)).toBeTrue();
    expect(rects.reduce((total, rect) => total + rect.width, 0)).toBe(ones);
  });
});
