import {parseIsbnToAdd} from './add-isbn-to-book';

describe('parseIsbnToAdd (RG-174)', () => {
  it('convertit un ISBN-10 valide en ISBN-13', () => {
    expect(parseIsbnToAdd('2-07-036373-2')).toEqual({valid: true, isbn13: '9782070363735'});
  });

  it('accepte un ISBN-10 dont la clé est X', () => {
    expect(parseIsbnToAdd('0-8044-2957-X')).toEqual({valid: true, isbn13: '9780804429573'});
  });

  it('accepte un ISBN-13 saisi avec des espaces', () => {
    expect(parseIsbnToAdd('978 2070 363735')).toEqual({valid: true, isbn13: '9782070363735'});
  });

  it('refuse un code interne (préfixe 2) : ce n’est pas un ISBN', () => {
    expect(parseIsbnToAdd('2000000000015')).toEqual({valid: false, reason: 'internal'});
  });

  it('refuse un ISBN-13 dont la clé est fausse', () => {
    expect(parseIsbnToAdd('9782070363736')).toEqual({valid: false, reason: 'invalid'});
  });

  it('refuse un ISBN-10 dont la clé est fausse', () => {
    expect(parseIsbnToAdd('2070363733')).toEqual({valid: false, reason: 'invalid'});
  });

  it('refuse une saisie vide', () => {
    expect(parseIsbnToAdd('   ')).toEqual({valid: false, reason: 'empty'});
  });
});
