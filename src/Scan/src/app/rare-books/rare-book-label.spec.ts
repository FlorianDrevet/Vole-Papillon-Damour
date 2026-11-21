import {rareBookLabelCode} from './rare-book-label';

// RG-173 / RG-174 : l'étiquette d'un exemplaire rare porte son code interne s'il existe, sinon son ISBN.
describe('rareBookLabelCode', () => {
  it('prend le code interne quand il existe, même si un ISBN a été ajouté ensuite', () => {
    expect(rareBookLabelCode({isbn13: '9782070363735', internalCode: '2000000000015'})).toBe('2000000000015');
  });

  it('prend l’ISBN quand il n’y a pas de code interne', () => {
    expect(rareBookLabelCode({isbn13: '9782070363735', internalCode: null})).toBe('9782070363735');
  });

  it('ne renvoie rien quand le livre n’a aucun code', () => {
    expect(rareBookLabelCode({isbn13: null})).toBeNull();
  });
});
