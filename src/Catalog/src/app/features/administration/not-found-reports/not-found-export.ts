import {CatalogNotFoundQueueTarget} from '../../../core/catalog.models';

export function toNotFoundCsv(targets: CatalogNotFoundQueueTarget[]): string {
  const header = [
    'Titre',
    'Auteur',
    'ISBN ou fiche rare',
    'Rayon',
    'Stock',
    'Signalements',
    'Dernier signalement',
    'Trouvé ?',
  ];
  const rows = targets.map(target => [
    target.title,
    target.authors,
    target.kind === 'edition' ? target.isbn13 : `Fiche rare ${target.rareBookId ?? ''}`,
    target.genre,
    target.kind === 'rare' ? 'Exemplaire unique' : target.quantityAvailable,
    target.reportCount,
    target.lastReportedAt,
    '',
  ]);

  return [header, ...rows]
    .map(row => row.map(csvCell).join(';'))
    .join('\r\n') + '\r\n';
}

function csvCell(value: unknown): string {
  const text = value === null || value === undefined ? '' : String(value);
  const safeText = typeof value === 'string' && /^[=+\-@]/.test(text) ? `'${text}` : text;
  return /[;"\r\n]/.test(safeText)
    ? `"${safeText.replaceAll('"', '""')}"`
    : safeText;
}
