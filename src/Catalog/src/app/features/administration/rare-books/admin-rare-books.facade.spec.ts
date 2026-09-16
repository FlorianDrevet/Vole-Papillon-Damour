import {signal} from '@angular/core';
import {of, throwError} from 'rxjs';

import {CatalogAdminApiService} from '../../../core/catalog-admin-api.service';
import {CatalogAuthService} from '../../../core/catalog-auth.service';
import {
  CatalogAdminBook,
  CatalogAdminRareBook,
  CatalogAdminRareBookPage,
  CatalogAdminRareBookPublishResult,
} from '../../../core/catalog.models';
import {
  AdminRareBooksFacade,
  CatalogAdminRareBookFormValue,
} from './admin-rare-books.facade';

describe('AdminRareBooksFacade', () => {
  let facade: AdminRareBooksFacade;
  let api: jasmine.SpyObj<CatalogAdminApiService>;
  let auth: {
    isAuthenticated: ReturnType<typeof signal<boolean>>;
    getApiAccessToken: jasmine.Spy;
  };

  const book = (overrides: Partial<CatalogAdminRareBook> = {}): CatalogAdminRareBook => ({
    id: 'rare-book-id',
    slug: 'les-fleurs-du-mal',
    isbn13: '9782070363735',
    title: 'Les fleurs du mal',
    authorMention: 'Charles Baudelaire',
    publisher: 'Poulet-Malassis',
    publicationYear: 1857,
    shelf: 'Éditions anciennes',
    price: 40,
    condition: 'GoodWithFlaws',
    publicDescription: 'Quelques rousseurs.',
    binding: 'Demi-chagrin',
    dimensions: '18 × 12 cm',
    pageCount: 320,
    shelfLocation: 'Table rares',
    status: 'Draft',
    isSold: false,
    soldAt: null,
    soldAtFairId: null,
    soldInSessionId: null,
    priceSetBy: 'Conseil du 5 mars',
    createdAt: '2026-09-16T10:00:00Z',
    createdBy: 'creator-id',
    updatedAt: '2026-09-16T10:00:00Z',
    updatedBy: 'updater-id',
    rowVersion: 'version-1',
    photos: [],
    ...overrides,
  });

  const page = (books: CatalogAdminRareBook[], totalCount = books.length): CatalogAdminRareBookPage => ({
    generatedAt: '2026-09-16T12:00:00Z',
    books,
    totalCount,
    page: 1,
    pageSize: 50,
  });

  const formValue = (overrides: Partial<CatalogAdminRareBookFormValue> = {}): CatalogAdminRareBookFormValue => ({
    title: '  Nouveau titre  ',
    authorMention: ' Auteur ',
    publisher: '',
    publicationYear: 1901,
    shelf: 'Illustrés',
    price: 22.5,
    condition: 'AsNew',
    publicDescription: ' Description publique ',
    binding: '',
    dimensions: '18 × 12 cm',
    pageCount: 160,
    shelfLocation: '',
    priceSetBy: 'Association',
    isbn13: '',
    ...overrides,
  });

  const catalogueBook = (): CatalogAdminBook => ({
    isbn13: '9782070363735',
    workId: 'OL42W',
    title: 'Les fleurs du mal',
    authors: 'Charles Baudelaire',
    publisher: 'Poulet-Malassis',
    publicationYear: 1857,
    physicalFormat: 'Relié',
    language: 'fr',
    genre: 'Poésie',
    metadataStatus: 'Complete',
    metadataSource: 'OpenLibrary',
    manuallyEditedFields: null,
    quantityAvailable: 1,
    quantityAnnounced: 0,
    salesCount: 0,
    rejectionCount: 0,
    isRare: false,
    isHidden: false,
    redirectedToIsbn13: null,
    coverUrl: 'https://covers.example/flowers.jpg',
    firstSeenAt: '2026-09-16T10:00:00Z',
    lastAvailableAt: '2026-09-16T10:00:00Z',
    updatedAt: '2026-09-16T10:00:00Z',
    announcements: [],
    movements: [],
  });

  beforeEach(() => {
    api = jasmine.createSpyObj<CatalogAdminApiService>('CatalogAdminApiService', [
      'getRareBooks', 'getRareBook', 'createRareBook', 'updateRareBook',
      'publishRareBook', 'unpublishRareBook', 'deleteRareBook',
      'addRareBookPhoto', 'reorderRareBookPhotos', 'updateRareBookPhotoCaption',
      'deleteRareBookPhoto',
    ]);
    auth = {
      isAuthenticated: signal(true),
      getApiAccessToken: jasmine.createSpy('getApiAccessToken').and.resolveTo('access-token'),
    };

    const draft = book();
    const published = book({id: 'published-id', status: 'Published'});
    api.getRareBooks.and.callFake((_token: string, filters = {}) => of(
      filters.status === 'Published' ? page([published], 3) : page([draft], 1),
    ));
    api.getRareBook.and.returnValue(of(draft));
    api.createRareBook.and.returnValue(of(draft));
    api.updateRareBook.and.returnValue(of(draft));
    api.publishRareBook.and.returnValue(of({rareBook: book({status: 'Published'}), changed: true, warnings: []} as CatalogAdminRareBookPublishResult));
    api.unpublishRareBook.and.returnValue(of(book({status: 'Draft'})));
    api.deleteRareBook.and.returnValue(of(undefined));
    api.addRareBookPhoto.and.returnValue(of(draft));
    api.reorderRareBookPhotos.and.returnValue(of(draft));
    api.updateRareBookPhotoCaption.and.returnValue(of(draft));
    api.deleteRareBookPhoto.and.returnValue(of(undefined));

    facade = new AdminRareBooksFacade(auth as unknown as CatalogAuthService, api);
  });

  it('loads the filtered list and keeps a published counter for the navigation badge', async () => {
    await facade.load({search: 'Baudelaire', withoutPhoto: true});

    expect(api.getRareBooks).toHaveBeenCalledWith('access-token', {search: 'Baudelaire', withoutPhoto: true});
    expect(api.getRareBooks).toHaveBeenCalledWith('access-token', {status: 'Published', page: 1, pageSize: 1});
    expect(facade.page()?.books[0].title).toBe('Les fleurs du mal');
    expect(facade.publishedCount()).toBe(3);
    expect(facade.loading()).toBeFalse();
  });

  it('opens an existing book and maps an editor submission without empty optional values', async () => {
    const existing = book({status: 'Published'});
    api.getRareBook.and.returnValue(of(existing));

    await facade.open('rare-book-id');
    expect(facade.selectedBook()).toBe(existing);
    expect(facade.editorOpen()).toBeTrue();

    await facade.save(formValue());

    expect(api.updateRareBook).toHaveBeenCalledWith('access-token', 'rare-book-id', {
      title: 'Nouveau titre',
      authorMention: 'Auteur',
      publisher: null,
      publicationYear: 1901,
      shelf: 'Illustrés',
      price: 22.5,
      condition: 'AsNew',
      publicDescription: 'Description publique',
      binding: null,
      dimensions: '18 × 12 cm',
      pageCount: 160,
      shelfLocation: null,
      priceSetBy: 'Association',
      isbn13: null,
      rowVersion: 'version-1',
    });
    expect(facade.error()).toBeNull();
  });

  it('creates a draft from the editor and manages publication and deletion', async () => {
    facade.startCreate({title: 'Prérempli', isbn13: '9782070363735'});
    expect(facade.editorOpen()).toBeTrue();
    expect(facade.draft()?.title).toBe('Prérempli');

    await facade.save(formValue({title: 'Création', isbn13: '9782070363735'}));
    expect(api.createRareBook).toHaveBeenCalled();
    expect(api.updateRareBook).not.toHaveBeenCalled();

    await facade.publish();
    expect(api.publishRareBook).toHaveBeenCalledWith('access-token', 'rare-book-id');
    expect(facade.selectedBook()?.status).toBe('Published');

    await facade.deleteSelected();
    expect(api.deleteRareBook).toHaveBeenCalledWith('access-token', 'rare-book-id');
    expect(facade.editorOpen()).toBeFalse();
  });

  it('opens an existing rare fiche or starts a prefilled draft from an ordinary catalogue book', async () => {
    const ordinaryBook = catalogueBook();
    api.getRareBooks.and.returnValue(of(page([])));

    await facade.openOrCreateFromCatalogBook(ordinaryBook);

    expect(api.getRareBooks).toHaveBeenCalledWith('access-token', {
      search: ordinaryBook.isbn13,
      page: 1,
      pageSize: 10,
    });
    expect(facade.selectedBook()).toBeNull();
    expect(facade.draft()).toEqual(jasmine.objectContaining({
      title: ordinaryBook.title,
      authorMention: ordinaryBook.authors,
      publisher: ordinaryBook.publisher,
      publicationYear: ordinaryBook.publicationYear,
      isbn13: ordinaryBook.isbn13,
    }));
    expect((facade.draft() as unknown as Record<string, unknown>)['price']).toBe(0);

    const existing = book({status: 'Published'});
    api.getRareBooks.and.returnValue(of(page([existing])));
    api.getRareBook.and.returnValue(of(existing));

    await facade.openOrCreateFromCatalogBook(ordinaryBook);

    expect(api.getRareBook).toHaveBeenCalledWith('access-token', existing.id);
    expect(facade.selectedBook()).toBe(existing);
    expect(facade.draft()).toBeNull();
  });

  it('resolves the rare fiche relation used by the ordinary catalogue detail', async () => {
    const ordinaryBook = catalogueBook();
    const existing = book({status: 'Published'});
    api.getRareBooks.and.returnValue(of(page([existing])));

    await facade.resolveCatalogRelation(ordinaryBook.isbn13);

    expect(facade.catalogueRareBook()).toBe(existing);

    api.getRareBooks.and.returnValue(of(page([])));
    await facade.resolveCatalogRelation(ordinaryBook.isbn13);

    expect(facade.catalogueRareBook()).toBeNull();
  });

  it('updates the photo gallery and exposes API errors in French', async () => {
    const existing = book();
    api.getRareBook.and.returnValue(of(existing));
    await facade.open(existing.id);
    const file = new File(['photo'], 'photo.jpg', {type: 'image/jpeg'});

    await facade.addPhoto(file, 'Couverture');
    expect(api.addRareBookPhoto).toHaveBeenCalledWith('access-token', existing.id, file, 'Couverture');
    await facade.reorderPhotos(['photo-1', 'photo-2']);
    expect(api.reorderRareBookPhotos).toHaveBeenCalledWith('access-token', existing.id, ['photo-1', 'photo-2']);
    await facade.updatePhotoCaption('photo-1', 'Dos');
    expect(api.updateRareBookPhotoCaption).toHaveBeenCalledWith('access-token', 'photo-1', 'Dos');
    await facade.deletePhoto('photo-1');
    expect(api.deleteRareBookPhoto).toHaveBeenCalledWith('access-token', 'photo-1');

    api.getRareBook.and.returnValue(throwError(() => ({status: 403})));
    await facade.open(existing.id);
    expect(facade.error()).toBe('Le compte connecté ne possède pas les droits pour gérer les livres rares.');
  });
});
