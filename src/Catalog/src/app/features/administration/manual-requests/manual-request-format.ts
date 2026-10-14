export function formatManualRequesterPhone(phone: string | null): string {
  if (!phone) {
    return '';
  }

  const normalized = phone.trim().replace(/[ .()-]/g, '');
  if (normalized.startsWith('+33') && normalized.length === 12) {
    return `+33 ${normalized[3]} ${groupDigits(normalized.slice(4))}`;
  }

  if (normalized.startsWith('+')) {
    return `+${groupDigits(normalized.slice(1))}`;
  }

  return groupDigits(normalized);
}

export function formatManualRequesterName(
  firstName: string | null,
  lastName: string | null,
  erased: boolean,
): string {
  if (erased) {
    return 'Demandeur effacé';
  }

  const first = firstName?.trim();
  const last = lastName?.trim().toLocaleUpperCase('fr-FR');
  return [first, last].filter(Boolean).join(' ');
}

export function manualRequesterPhoneHref(phone: string): string {
  const normalized = phone.trim().replace(/[\s().-]/g, '');
  return `tel:${normalized}`;
}

function groupDigits(value: string): string {
  return value.replace(/(.{2})(?=.)/g, '$1 ').trim();
}
