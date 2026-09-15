const SPELLED_NUMBERS = [
  'zéro', 'une', 'deux', 'trois', 'quatre', 'cinq', 'six', 'sept', 'huit', 'neuf', 'dix',
  'onze', 'douze', 'treize', 'quatorze', 'quinze', 'seize', 'dix-sept', 'dix-huit', 'dix-neuf', 'vingt',
];

const numberFormatter = new Intl.NumberFormat('fr-FR');

export function spellFrenchNumber(value: number): string {
  return SPELLED_NUMBERS[value] ?? numberFormatter.format(value);
}

export function capitalizeFrench(value: string): string {
  return value.charAt(0).toLocaleUpperCase('fr-FR') + value.slice(1);
}

export function toCsv(rows: (string | number | null | undefined)[][]): string {
  return rows.map(row => row.map(value => csvCell(value)).join(';')).join('\r\n') + '\r\n';
}

function csvCell(value: string | number | null | undefined): string {
  const text = value === null || value === undefined ? '' : String(value);
  const safeText = typeof value === 'string' && /^[=+\-@]/.test(text) ? `'${text}` : text;
  return /[;"\r\n]/.test(safeText)
    ? `"${safeText.replaceAll('"', '""')}"`
    : safeText;
}
