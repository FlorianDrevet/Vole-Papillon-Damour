import {VpdEventModel} from '../../../../shared/models/vpdEvent.model';

/** Actions de la régie, déclenchées au clavier ou depuis la barre (E3). */
export type RegieAction =
  | {type: 'draw'; numero: number}
  | {type: 'undo'}
  | {type: 'line'}
  | {type: 'bingo'}
  | {type: 'pause'}
  | {type: 'palette'}
  | {type: 'fullscreen'};

export interface RegieKeyResult {
  /** Chiffres tapés en attente de « Entrée », affichés dans le témoin de saisie. */
  buffer: string;
  action: RegieAction | null;
}

/**
 * Clavier de la régie : chiffres puis Entrée pour tirer, Retour arrière pour effacer un
 * chiffre ou, sans saisie en cours, annuler le dernier numéro ; L, B, Espace, P, F.
 */
export function reduceRegieKey(buffer: string, key: string): RegieKeyResult {
  if (/^[0-9]$/.test(key)) {
    return {buffer: buffer.length >= 2 ? key : buffer + key, action: null};
  }

  switch (key) {
    case 'Enter': {
      const numero = Number(buffer);
      return {
        buffer: '',
        action: buffer !== '' && numero >= 1 && numero <= 90 ? {type: 'draw', numero} : null,
      };
    }
    case 'Escape':
      return {buffer: '', action: null};
    case 'Backspace':
      return buffer === ''
        ? {buffer: '', action: {type: 'undo'}}
        : {buffer: buffer.slice(0, -1), action: null};
    case 'l':
    case 'L':
      return {buffer, action: {type: 'line'}};
    case 'b':
    case 'B':
      return {buffer, action: {type: 'bingo'}};
    case ' ':
      return {buffer, action: {type: 'pause'}};
    case 'p':
    case 'P':
      return {buffer, action: {type: 'palette'}};
    case 'f':
    case 'F':
      return {buffer, action: {type: 'fullscreen'}};
    default:
      return {buffer, action: null};
  }
}

/** RG-08 : la ligne gagnée qui termine une partie « pause après » ouvre l'écran Pause. */
export function shouldPauseAfterWin(before: VpdEventModel, after: VpdEventModel): boolean {
  if (after.currentPartieIndex <= before.currentPartieIndex) {
    return false;
  }
  const finished = before.parties.find(partie => partie.index === before.currentPartieIndex);
  return finished?.pauseAfter ?? false;
}

/** RG-06 et messages du toast d'erreur (E3). */
export function regieErrorMessage(action: RegieAction, error: unknown, alreadyDrawn: boolean): string {
  if (action.type === 'draw' && alreadyDrawn) {
    return `Le numéro ${action.numero} est déjà sorti.`;
  }
  const status = (error as {response?: {status?: number}} | null)?.response?.status;
  if (status === 409) {
    return 'Une autre action vient d’être enregistrée sur ce tableau. Réessayez.';
  }
  return 'L’action n’a pas pu être enregistrée. Vérifiez la connexion et réessayez.';
}
