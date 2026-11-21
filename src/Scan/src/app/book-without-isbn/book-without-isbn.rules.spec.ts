import {
  emptyOrdinaryForm,
  emptyRareForm,
  toOrdinaryPayload,
  toRarePayload,
  validateOrdinaryForm,
  validatePhoto,
  validateRareForm,
} from './book-without-isbn.rules';

describe('book without ISBN rules', () => {
  it('requires a title and a quantity between 1 and 100 000 for an ordinary book', () => {
    expect(validateOrdinaryForm({...emptyOrdinaryForm(), title: '  '})).toBe('Le titre est obligatoire.');
    expect(validateOrdinaryForm({...emptyOrdinaryForm(), title: 'Atlas', quantity: '0'})).toContain('quantité');
    expect(validateOrdinaryForm({...emptyOrdinaryForm(), title: 'Atlas', quantity: '2'})).toBeNull();
  });

  it('rejects an invalid publication year and page count', () => {
    expect(validateOrdinaryForm({...emptyOrdinaryForm(), title: 'Atlas', publicationYear: '12.5'})).toContain('année');
    expect(validateOrdinaryForm({...emptyOrdinaryForm(), title: 'Atlas', pages: '0'})).toContain('pages');
  });

  it('requires a positive fixed price for a rare book, with a comma accepted as decimal separator', () => {
    const base = {...emptyRareForm(), title: 'Rare'};
    expect(validateRareForm({...base, price: ''})).toContain('prix');
    expect(validateRareForm({...base, price: '0'})).toContain('prix');
    expect(validateRareForm({...base, price: '12,5'})).toBeNull();
  });

  it('sends empty optional fields as null and no ISBN for a book without ISBN (RG-170)', () => {
    const payload = toOrdinaryPayload({...emptyOrdinaryForm(), title: '  Atlas  ', authors: '  ', pages: '120'});

    expect(payload.title).toBe('Atlas');
    expect(payload.authors).toBeNull();
    expect(payload.pages).toBe(120);
    expect(payload.quantityAvailable).toBe(1);
  });

  it('sends a rare book without ISBN and with its client gesture identifier', () => {
    const payload = toRarePayload({...emptyRareForm(), title: 'Rare', price: '25,00'}, 'gesture-1');

    expect(payload.isbn13).toBeNull();
    expect(payload.price).toBe(25);
    expect(payload.clientGestureId).toBe('gesture-1');
  });

  it('accepts JPEG, PNG and WebP photos up to 8 MB only', () => {
    expect(validatePhoto({type: 'image/jpeg', size: 1024})).toBeNull();
    expect(validatePhoto({type: 'image/gif', size: 1024})).toContain('JPEG');
    expect(validatePhoto({type: 'image/png', size: 9 * 1024 * 1024})).toContain('8 Mo');
  });
});