import {ean13Modules} from '../ean13';
import {renderLabel} from './supvan-label-render';

// Constat sur une étiquette réelle (22/11/2026, photo d'une T50M Pro, rouleau 40×30 mm) : la T50M Pro imprime
// l'image qu'on lui envoie en MIROIR gauche-droite (barres et chiffres inversés, le chiffre restant sous les barres).
// L'image envoyée doit donc être le miroir gauche-droite de l'aperçu : c'est ce que vérifient ces tests.
describe('renderLabel — orientation de l\'image envoyée à la T50M Pro', () => {
  const CODE = '9782378583323';
  const options = {code: CODE, price: null, widthMm: 40, heightMm: 30};
  // Lignes d'essai : 8 est dans les barres juste sous la marge haute (6 points), 232 sous la ligne de base des chiffres.
  const BARS_ROW = 8;
  const BELOW_DIGITS_ROW = 232;

  function inkRow(image: {width: number; ink: Uint8Array}, y: number): Uint8Array {
    return image.ink.subarray(y * image.width, (y + 1) * image.width);
  }

  it('garde les barres en haut et le chiffre en bas (pas de retournement vertical)', () => {
    const image = renderLabel(options);

    expect(inkRow(image, BARS_ROW).some(dot => dot === 1)).withContext('barres sous la marge haute').toBeTrue();
    expect(inkRow(image, BELOW_DIGITS_ROW).some(dot => dot === 1)).withContext('sous les chiffres').toBeFalse();
  });

  it('envoie les barres en miroir gauche-droite de l\'aperçu', () => {
    const image = renderLabel(options);
    const modules = ean13Modules(CODE);
    const moduleDots = Math.max(1, Math.floor(image.width / (modules.length + 2 * 7)));
    const left = Math.floor((image.width - modules.length * moduleDots) / 2);
    const row = inkRow(image, BARS_ROW);

    const wrong: number[] = [];
    for (let index = 0; index < modules.length; index += 1) {
      for (let dot = 0; dot < moduleDots; dot += 1) {
        const previewX = left + index * moduleDots + dot;
        const expected = modules[index] === '1' ? 1 : 0;
        if (row[image.width - 1 - previewX] !== expected) {
          wrong.push(previewX);
        }
      }
    }

    expect(wrong).withContext('colonnes de l\'aperçu dont le miroir est faux').toEqual([]);
  });
});
