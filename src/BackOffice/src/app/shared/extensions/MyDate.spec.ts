import { MyDate, fromApiUtcDate, fromApiUtcWallClock } from './MyDate';

describe('event date conversion', () => {
  it('converts an API UTC wall-clock time into a local picker value without changing the displayed hour', () => {
    const pickerValue = fromApiUtcWallClock('2026-10-05T14:00:00.000Z');

    expect(pickerValue).not.toBeNull();
    expect(pickerValue?.getFullYear()).toBe(2026);
    expect(pickerValue?.getMonth()).toBe(9);
    expect(pickerValue?.getDate()).toBe(5);
    expect(pickerValue?.getHours()).toBe(14);
    expect(pickerValue?.getMinutes()).toBe(0);
    expect(new MyDate(pickerValue!).toISOUtcString()).toBe('2026-10-05T14:00:00.000Z');
  });

  it('converts an API date into a local calendar value without date drift', () => {
    const pickerValue = fromApiUtcDate('2026-10-05T04:00:00.000Z');

    expect(pickerValue).not.toBeNull();
    expect(pickerValue?.getFullYear()).toBe(2026);
    expect(pickerValue?.getMonth()).toBe(9);
    expect(pickerValue?.getDate()).toBe(5);
    expect(pickerValue?.getHours()).toBe(0);
    expect(new MyDate(pickerValue!).toISOUtcString()).toBe('2026-10-05T00:00:00.000Z');
  });
});
