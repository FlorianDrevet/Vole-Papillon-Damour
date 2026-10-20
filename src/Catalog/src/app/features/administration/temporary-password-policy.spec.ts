import {
  getTemporaryPasswordIssue,
  isTemporaryPasswordPolicyProblemDetail,
} from '@vpd/ui';

describe('temporary password policy', () => {
  it('requires a password', () => {
    expect(getTemporaryPasswordIssue('')).toBe('required');
  });

  it('accepts 8 characters with exactly three required categories', () => {
    expect(getTemporaryPasswordIssue('Abcdef12')).toBeNull();
    expect(getTemporaryPasswordIssue('Abcdef1')).toBe('tooShort');
  });

  it('requires at least three categories among lowercase, uppercase, digit, and symbol', () => {
    expect(getTemporaryPasswordIssue('abcdef12')).toBe('categories');
    expect(getTemporaryPasswordIssue('Abcdef1!')).toBeNull();
  });

  it('accepts 256 characters and rejects 257', () => {
    expect(getTemporaryPasswordIssue(`Abcdef1!${'a'.repeat(248)}`)).toBeNull();
    expect(getTemporaryPasswordIssue(`Abcdef1!${'a'.repeat(249)}`)).toBe('tooLong');
  });

  it('treats a space as a symbol while excluding tabs', () => {
    expect(getTemporaryPasswordIssue('Abcdef  ')).toBeNull();
    expect(getTemporaryPasswordIssue('Abcdef\t\t')).toBe('categories');
  });

  it('recognizes only the Entra password policy detail', () => {
    expect(isTemporaryPasswordPolicyProblemDetail(
      'Le mot de passe temporaire ne respecte pas la politique Microsoft Entra. Choisissez un autre mot de passe.',
    )).toBeTrue();
    expect(isTemporaryPasswordPolicyProblemDetail('La requête ne peut pas être traitée.')).toBeFalse();
  });
});
