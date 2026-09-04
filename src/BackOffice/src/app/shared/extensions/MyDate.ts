type ApiEventDate = string | Date | null | undefined;

/**
 * Les anciens évènements encodent l'heure civile dans les composantes UTC de
 * leur DateTimeOffset : 14:00Z représente donc 14:00 dans le formulaire.
 * Les date/time pickers Angular, eux, lisent les composantes locales d'un Date.
 */
function toLocalPickerValue(value: ApiEventDate, withTime: boolean): Date | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }

  const parsed = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return null;
  }

  return new Date(
    parsed.getUTCFullYear(),
    parsed.getUTCMonth(),
    parsed.getUTCDate(),
    withTime ? parsed.getUTCHours() : 0,
    withTime ? parsed.getUTCMinutes() : 0,
    withTime ? parsed.getUTCSeconds() : 0,
    withTime ? parsed.getUTCMilliseconds() : 0
  );
}

export function fromApiUtcDate(value: ApiEventDate): Date | null {
  return toLocalPickerValue(value, false);
}

export function fromApiUtcWallClock(value: ApiEventDate): Date | null {
  return toLocalPickerValue(value, true);
}

export class MyDate extends Date {
  toISOUtcString(): string {
    const userTimezoneOffset = this.getTimezoneOffset() * 60000;
    return new Date(this.getTime() - userTimezoneOffset).toISOString();
  }
}
