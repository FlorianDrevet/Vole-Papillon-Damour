import {
  emptyOrdinaryBookForm,
  toOrdinaryBookRequest,
  validateBookPhoto,
  validateOrdinaryBookForm,
} from './books-without-isbn.rules';

describe('admin books without ISBN rules', () => {
  it('requires a title and a quantity from 1 to 100 000', () => {
    expect(validateOrdinaryBookForm({...emptyOrdinaryBookForm(), title: ' '})).toBe('Le titre est obligatoire.');
    expect(validateOrdinaryBookForm({...emptyOrdinaryBookForm(), title: 'Atlas', quantity: '0'})).toContain('quantité');
    expect(validateOrdinaryBookForm({...emptyOrdinaryBookForm(), title: 'Atlas', quantity: '3'})).toBeNull();
  });

  it('rejects a non-integer year or page count', () => {
    expect(validateOrdinaryBookForm({...emptyOrdinaryBookForm(), title: 'Atlas', publicationYear: '1.5'})).toContain('année');
    expect(validateOrdinaryBookForm({...emptyOrdinaryBookForm(), title: 'Atlas', pages: 'abc'})).toContain('pages');
  });

  it('sends empty optional fields as null', () => {
    const request = toOrdinaryBookRequest({...emptyOrdinaryBookForm(), title: ' Atlas ', authors: '  ', pages: '96'});

    expect(request.title).toBe('Atlas');
    expect(request.authors).toBeNull();
    expect(request.pages).toBe(96);
  });

  it('accepts only JPEG, PNG and WebP photos up to 8 MB', () => {
    expect(validateBookPhoto({type: 'image/webp', size: 2048})).toBeNull();
    expect(validateBookPhoto({type: 'image/gif', size: 2048})).toContain('JPEG');
    expect(validateBookPhoto({type: 'image/jpeg', size: 9 * 1024 * 1024})).toContain('8 Mo');
  });
});