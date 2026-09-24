import {parseMemberCredential} from './scan-member-card';

describe('parseMemberCredential', () => {
  it('recognises a card QR', () => {
    expect(parseMemberCredential('VPDC1.AAAA.BBBB')).toEqual({
      kind: 'qr',
      value: 'VPDC1.AAAA.BBBB',
    });
  });

  it('recognises and normalises a recovery code', () => {
    expect(parseMemberCredential(' lune 4271 ')).toEqual({
      kind: 'recovery-code',
      value: 'LUNE-4271',
    });
  });

  it('ignores an ISBN', () => {
    expect(parseMemberCredential('9782070612758')).toBeNull();
  });
});
