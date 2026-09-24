import {ScanMemberCredential} from './scan-offline.model';

export function parseMemberCredential(raw: string): ScanMemberCredential | null {
  if (raw.startsWith('VPDC1.')) {
    return {kind: 'qr', value: raw};
  }

  const match = /^([A-Za-z]{3,6})[\s-]*(\d{4})$/.exec(raw.trim());
  if (!match) {
    return null;
  }

  return {kind: 'recovery-code', value: `${match[1].toUpperCase()}-${match[2]}`};
}
