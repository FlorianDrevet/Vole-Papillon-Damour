import type {AccountInfo} from '@azure/msal-browser';

type AccountTokenClaims = Record<string, unknown>;

const givenNameClaims = ['given_name', 'givenName'] as const;
const familyNameClaims = ['family_name', 'surname'] as const;

export function getCatalogAccountDisplayName(account: AccountInfo | null): string {
  if (!account) {
    return '';
  }

  const claims = account.idTokenClaims as AccountTokenClaims | undefined;
  const givenName = readClaim(claims, givenNameClaims);
  const familyName = readClaim(claims, familyNameClaims);

  if (givenName || familyName) {
    return [givenName, familyName].filter(Boolean).join(' ');
  }

  return normalize(account.name) || normalize(account.username);
}

function readClaim(
  claims: AccountTokenClaims | undefined,
  names: readonly string[],
): string {
  if (!claims) {
    return '';
  }

  for (const name of names) {
    const value = normalize(claims[name]);
    if (value) {
      return value;
    }
  }

  return '';
}

function normalize(value: unknown): string {
  if (typeof value !== 'string') {
    return '';
  }

  const normalized = value.trim();
  return isPlaceholder(normalized) ? '' : normalized;
}

function isPlaceholder(value: string): boolean {
  return ['unknown', 'undefined', 'null'].includes(value.toLowerCase());
}
