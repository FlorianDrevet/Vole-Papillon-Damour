export const TEMPORARY_PASSWORD_MIN_LENGTH = 8;
export const TEMPORARY_PASSWORD_MAX_LENGTH = 256;
export const TEMPORARY_PASSWORD_GUIDANCE =
  '8 à 256 caractères et au moins 3 des 4 catégories : minuscule, majuscule, chiffre et symbole.';

export type TemporaryPasswordIssue = 'required' | 'tooShort' | 'tooLong' | 'categories';

const PASSWORD_CATEGORIES = [
  /\p{Ll}/u,
  /\p{Lu}/u,
  /\p{Nd}/u,
  /[\p{P}\p{S} ]/u,
];

export function getTemporaryPasswordIssue(password: string | null | undefined): TemporaryPasswordIssue | null {
  if (!password) {
    return 'required';
  }
  if (password.length < TEMPORARY_PASSWORD_MIN_LENGTH) {
    return 'tooShort';
  }
  if (password.length > TEMPORARY_PASSWORD_MAX_LENGTH) {
    return 'tooLong';
  }

  const categories = PASSWORD_CATEGORIES.filter(category => category.test(password)).length;
  return categories >= 3 ? null : 'categories';
}

export function temporaryPasswordCategoriesValidator(
  control: {readonly value: unknown},
): {passwordCategories: true} | null {
  const password = typeof control.value === 'string' ? control.value : '';
  if (!password) {
    return null;
  }

  const categories = PASSWORD_CATEGORIES.filter(category => category.test(password)).length;
  return categories >= 3 ? null : {passwordCategories: true};
}

export function temporaryPasswordIssueMessage(issue: TemporaryPasswordIssue): string {
  switch (issue) {
    case 'required':
      return 'Saisissez un mot de passe temporaire.';
    case 'tooShort':
      return 'Le mot de passe doit contenir au moins 8 caractères.';
    case 'tooLong':
      return 'Le mot de passe ne peut pas dépasser 256 caractères.';
    case 'categories':
      return 'Le mot de passe doit contenir au moins 3 des 4 catégories : minuscule, majuscule, chiffre et symbole.';
  }
}

export function isTemporaryPasswordPolicyProblemDetail(detail: unknown): detail is string {
  return typeof detail === 'string'
    && detail.includes('Le mot de passe temporaire ne respecte pas la politique Microsoft Entra.');
}
