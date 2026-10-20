export interface InvalidIsbnApplicationRow {
  rowNumber: number;
  value: string;
  reason: string;
}

export interface IsbnApplicationCsvPreview {
  dataRowCount: number;
  emptyRowCount: number;
  duplicateCount: number;
  invalidCount: number;
  invalidExamples: InvalidIsbnApplicationRow[];
  isbns: string[];
}

export class IsbnApplicationCsvFormatError extends Error {}

const MAX_INVALID_EXAMPLES = 10;

export function parseIsbnApplicationCsv(content: string): IsbnApplicationCsvPreview {
  const lines = content.replace(/^\uFEFF/, '').split(/\r\n|\n|\r/);
  const headerLineIndex = lines.findIndex(line => line.trim().length > 0);
  if (headerLineIndex < 0) {
    throw new IsbnApplicationCsvFormatError('Le fichier est vide.');
  }
  let lastNonEmptyLineIndex = lines.length - 1;
  while (lastNonEmptyLineIndex > headerLineIndex && lines[lastNonEmptyLineIndex].trim().length === 0) {
    lastNonEmptyLineIndex -= 1;
  }

  const header = parseSemicolonRow(lines[headerLineIndex]);
  if (header.error) {
    throw new IsbnApplicationCsvFormatError(`En-tête invalide à la ligne ${headerLineIndex + 1}.`);
  }

  const headerNames = header.fields.map(value => value.trim());
  const isbnColumn = headerNames.indexOf('ISBN');
  const photoCountColumn = headerNames.indexOf('Nombre_de_photos');
  if (headerNames.length !== 2 || isbnColumn < 0 || photoCountColumn < 0 || isbnColumn === photoCountColumn) {
    throw new IsbnApplicationCsvFormatError(
      'Le fichier doit avoir les colonnes « ISBN » et « Nombre_de_photos », séparées par un point-virgule.',
    );
  }

  const uniqueIsbns = new Set<string>();
  const invalidExamples: InvalidIsbnApplicationRow[] = [];
  let dataRowCount = 0;
  let emptyRowCount = 0;
  let duplicateCount = 0;
  let invalidCount = 0;

  for (let index = headerLineIndex + 1; index < lines.length; index += 1) {
    const line = lines[index];
    if (line.trim().length === 0) {
      if (index < lastNonEmptyLineIndex) {
        emptyRowCount += 1;
      }
      continue;
    }

    dataRowCount += 1;
    const rowNumber = index + 1;
    const row = parseSemicolonRow(line);
    const rawIsbn = row.fields[isbnColumn] ?? '';
    const canonicalIsbn = row.error || row.fields.length !== 2
      ? null
      : canonicalizeIsbn(rawIsbn);

    if (!canonicalIsbn) {
      invalidCount += 1;
      if (invalidExamples.length < MAX_INVALID_EXAMPLES) {
        invalidExamples.push({
          rowNumber,
          value: rawIsbn.trim() || '—',
          reason: row.error || row.fields.length !== 2
            ? 'La ligne ne contient pas les deux colonnes attendues.'
            : 'ISBN-10 ou ISBN-13 invalide.',
        });
      }
      continue;
    }

    if (uniqueIsbns.has(canonicalIsbn)) {
      duplicateCount += 1;
      continue;
    }

    uniqueIsbns.add(canonicalIsbn);
  }

  return {
    dataRowCount,
    emptyRowCount,
    duplicateCount,
    invalidCount,
    invalidExamples,
    isbns: [...uniqueIsbns],
  };
}

function canonicalizeIsbn(value: string): string | null {
  const normalized = value.replace(/[\s-]/g, '').toUpperCase();
  if (/^\d{13}$/.test(normalized)) {
    if (!normalized.startsWith('978') && !normalized.startsWith('979')) {
      return null;
    }

    const checksum = [...normalized].reduce(
      (sum, digit, index) => sum + Number(digit) * (index % 2 === 0 ? 1 : 3),
      0,
    );
    return checksum % 10 === 0 ? normalized : null;
  }

  if (!/^\d{9}[\dX]$/.test(normalized)) {
    return null;
  }

  const isbn10Checksum = [...normalized].reduce(
    (sum, digit, index) => sum + (digit === 'X' ? 10 : Number(digit)) * (10 - index),
    0,
  );
  if (isbn10Checksum % 11 !== 0) {
    return null;
  }

  const body = `978${normalized.slice(0, 9)}`;
  const isbn13Checksum = [...body].reduce(
    (sum, digit, index) => sum + Number(digit) * (index % 2 === 0 ? 1 : 3),
    0,
  );
  return `${body}${(10 - (isbn13Checksum % 10)) % 10}`;
}

function parseSemicolonRow(line: string): {fields: string[]; error: boolean} {
  const fields: string[] = [];
  let field = '';
  let inQuotes = false;
  let afterQuote = false;

  for (let index = 0; index < line.length; index += 1) {
    const character = line[index];
    if (character === '"') {
      if (inQuotes && line[index + 1] === '"') {
        field += '"';
        index += 1;
      } else if (inQuotes) {
        inQuotes = false;
        afterQuote = true;
      } else if (field.length === 0 || field.trim().length === 0) {
        field = '';
        inQuotes = true;
      } else {
        return {fields, error: true};
      }
      continue;
    }

    if (character === ';' && !inQuotes) {
      fields.push(field.trim());
      field = '';
      afterQuote = false;
      continue;
    }

    if (!inQuotes && afterQuote && character.trim().length > 0) {
      return {fields, error: true};
    }

    field += character;
  }

  if (inQuotes) {
    return {fields, error: true};
  }

  fields.push(field.trim());
  return {fields, error: false};
}
