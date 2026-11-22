import {ean13Modules} from '../ean13';
import {DOTS_PER_MM, InkImage, PRINTHEAD_WIDTH_MM} from './supvan-raster';

// Dessine l'étiquette (EAN-13 + chiffres + prix éventuel) en image monochrome.
// `widthMm` = largeur du rouleau (sens de la tête d'impression), `heightMm` = longueur d'une étiquette (sens d'avance).
export interface LabelRenderOptions {
  code: string;
  price: string | null;
  widthMm: number;
  heightMm: number;
  // Cadre de contrôle autour de l'étiquette (étiquette d'essai).
  border?: boolean;
  caption?: string | null;
}

const QUIET_ZONE_MODULES = 7;

export function renderLabel(options: LabelRenderOptions): InkImage {
  const width = Math.min(options.widthMm, PRINTHEAD_WIDTH_MM) * DOTS_PER_MM;
  const height = options.heightMm * DOTS_PER_MM;
  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext('2d', {willReadFrequently: true});
  if (!context) {
    throw new Error('Rendu de l\'étiquette impossible sur cet appareil.');
  }

  context.fillStyle = '#fff';
  context.fillRect(0, 0, width, height);
  context.fillStyle = '#000';
  context.textBaseline = 'alphabetic';
  context.textAlign = 'center';

  const modules = ean13Modules(options.code);
  const moduleDots = Math.max(1, Math.floor(width / (modules.length + 2 * QUIET_ZONE_MODULES)));
  const barcodeWidth = modules.length * moduleDots;
  const left = Math.floor((width - barcodeWidth) / 2);

  const margin = 6;
  const textHeight = Math.max(18, Math.min(30, Math.floor(height * 0.18)));
  const captionHeight = options.caption ? 14 : 0;
  const priceHeight = options.price ? textHeight : 0;
  const barsTop = margin + captionHeight;
  const barsHeight = Math.max(16, height - barsTop - margin - textHeight - priceHeight - 4);

  if (options.caption) {
    context.font = '12px sans-serif';
    context.fillText(options.caption, width / 2, margin + 11, width - 2 * margin);
  }

  for (let index = 0; index < modules.length; index += 1) {
    if (modules[index] === '1') {
      context.fillRect(left + index * moduleDots, barsTop, moduleDots, barsHeight);
    }
  }

  context.font = `bold ${textHeight}px monospace`;
  const digitsBaseline = barsTop + barsHeight + textHeight;
  context.fillText(options.code, width / 2, digitsBaseline, width - 2 * margin);

  if (options.price) {
    context.font = `bold ${textHeight}px sans-serif`;
    context.fillText(options.price, width / 2, digitsBaseline + priceHeight, width - 2 * margin);
  }

  if (options.border) {
    context.lineWidth = 2;
    context.strokeStyle = '#000';
    context.strokeRect(1, 1, width - 2, height - 2);
  }

  const pixels = context.getImageData(0, 0, width, height).data;
  const ink = new Uint8Array(width * height);
  // La T50M Pro imprime ce raster en miroir gauche-droite (constaté sur une étiquette réelle : barres et chiffres
  // inversés, le chiffre restant sous les barres ; une rotation de 180° ne corrige pas un miroir). On envoie donc
  // l'image déjà retournée gauche-droite, pour que l'étiquette sorte comme l'aperçu.
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const source = (y * width + x) * 4;
      const luminance = 0.3 * pixels[source] + 0.59 * pixels[source + 1] + 0.11 * pixels[source + 2];
      ink[y * width + (width - 1 - x)] = luminance < 140 ? 1 : 0;
    }
  }

  return {width, height, ink};
}

// Avertissement si le code-barres est trop serré pour être lu (module < 2 points = 0,25 mm).
export function barcodeModuleDots(widthMm: number): number {
  const width = Math.min(widthMm, PRINTHEAD_WIDTH_MM) * DOTS_PER_MM;
  return Math.floor(width / (95 + 2 * QUIET_ZONE_MODULES));
}
