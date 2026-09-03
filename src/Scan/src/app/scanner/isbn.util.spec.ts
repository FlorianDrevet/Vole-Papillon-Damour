import {normalizeIsbn} from './isbn.util';

describe('normalizeIsbn', () => {
  it('converts a valid ISBN-10 to ISBN-13', () => {
    expect(normalizeIsbn('0-306-40615-2')).toBe('9780306406157');
  });

  it('removes separators from a valid ISBN-13', () => {
    expect(normalizeIsbn('978-2-07-036373-5')).toBe('9782070363735');
  });

  it('rejects an invalid ISBN or a non-book barcode', () => {
    expect(normalizeIsbn('9782070363736')).toBeNull();
    expect(normalizeIsbn('4006381333931')).toBeNull();
  });
});
