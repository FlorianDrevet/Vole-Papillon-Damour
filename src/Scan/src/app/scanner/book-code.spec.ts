import {normalizeBookCode} from './isbn.util';

describe('normalizeBookCode', () => {
  it('accepts an ISBN-13 and an ISBN-10', () => {
    expect(normalizeBookCode('978-2-07-036373-5')).toBe('9782070363735');
    expect(normalizeBookCode('0-306-40615-2')).toBe('9780306406157');
  });

  it('accepts a valid internal code (prefix 2, RG-170)', () => {
    expect(normalizeBookCode('2000000000015')).toBe('2000000000015');
  });

  it('rejects an internal code whose check digit is wrong', () => {
    expect(normalizeBookCode('2000000000016')).toBeNull();
  });

  it('rejects an empty value and a non-book barcode', () => {
    expect(normalizeBookCode('')).toBeNull();
    expect(normalizeBookCode(null)).toBeNull();
    expect(normalizeBookCode('1234')).toBeNull();
  });
});