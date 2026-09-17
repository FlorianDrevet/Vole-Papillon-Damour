import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {signal, WritableSignal} from '@angular/core';

import {
  CatalogAdminRareBook,
  CatalogAdminRareBookPage,
} from '../../../core/catalog.models';
import {AdminRareBookFormComponent} from './admin-rare-book-form.component';
import {AdminRareBookPhotosComponent} from './admin-rare-book-photos.component';
import {AdminRareBooksComponent} from './admin-rare-books.component';
import {AdminRareBooksFacade} from './admin-rare-books.facade';

describe('AdminRareBooksComponent', () => {
  let fixture: ComponentFixture<AdminRareBooksComponent>;
  let component: AdminRareBooksComponent;
  let facade: {
    page: WritableSignal<CatalogAdminRareBookPage | null>;
    selectedBook: WritableSignal<CatalogAdminRareBook | null>;
    editorOpen: WritableSignal<boolean>;
    draft: WritableSignal<null>;
    loading: WritableSignal<boolean>;
    error: WritableSignal<string | null>;
    success: WritableSignal<string | null>;
    publishedCount: WritableSignal<number>;
    publishWarnings: WritableSignal<string[]>;
    load: jasmine.Spy;
    open: jasmine.Spy;
    startCreate: jasmine.Spy;
    closeEditor: jasmine.Spy;
    save: jasmine.Spy;
    publish: jasmine.Spy;
    unpublish: jasmine.Spy;
    deleteSelected: jasmine.Spy;
    addPhoto: jasmine.Spy;
    reorderPhotos: jasmine.Spy;
    updatePhotoCaption: jasmine.Spy;
    deletePhoto: jasmine.Spy;
  };

  const books = (): CatalogAdminRareBookPage => ({
    generatedAt: '2026-09-16T12:00:00Z',
    books: [{
      id: 'rare-book-id',
      slug: 'le-livre',
      isbn13: null,
      title: 'Le livre rare',
      authorMention: 'Auteur',
      publisher: 'Éditeur',
      publicationYear: 1920,
      shelf: 'Éditions anciennes',
      price: 35,
      condition: 'GoodWithFlaws',
      publicDescription: null,
      binding: null,
      dimensions: null,
      pageCount: null,
      shelfLocation: 'Table rares',
      status: 'Draft',
      isSold: false,
      soldAt: null,
      soldAtFairId: null,
      soldInSessionId: null,
      priceSetBy: 'Association',
      createdAt: '2026-09-16T10:00:00Z',
      createdBy: 'creator-id',
      updatedAt: '2026-09-16T10:00:00Z',
      updatedBy: 'updater-id',
      rowVersion: 'version-1',
      photos: [],
    }],
    totalCount: 4,
    page: 1,
    pageSize: 50,
  });

  beforeEach(async () => {
    const page = books();
    page.books.push({...page.books[0], id: 'published-rare-book-id', slug: 'livre-publie', title: 'Le livre publié', status: 'Published'});
    facade = {
      page: signal(page),
      selectedBook: signal(null),
      editorOpen: signal(false),
      draft: signal(null),
      loading: signal(false),
      error: signal(null),
      success: signal(null),
      publishedCount: signal(2),
      publishWarnings: signal<string[]>([]),
      load: jasmine.createSpy('load').and.resolveTo(),
      open: jasmine.createSpy('open').and.resolveTo(),
      startCreate: jasmine.createSpy('startCreate'),
      closeEditor: jasmine.createSpy('closeEditor'),
      save: jasmine.createSpy('save').and.resolveTo(),
      publish: jasmine.createSpy('publish').and.resolveTo(),
      unpublish: jasmine.createSpy('unpublish').and.resolveTo(),
      deleteSelected: jasmine.createSpy('deleteSelected').and.resolveTo(),
      addPhoto: jasmine.createSpy('addPhoto').and.resolveTo(),
      reorderPhotos: jasmine.createSpy('reorderPhotos').and.resolveTo(),
      updatePhotoCaption: jasmine.createSpy('updatePhotoCaption').and.resolveTo(),
      deletePhoto: jasmine.createSpy('deletePhoto').and.resolveTo(),
    };

    await TestBed.configureTestingModule({
      declarations: [
        AdminRareBooksComponent,
        AdminRareBookFormComponent,
        AdminRareBookPhotosComponent,
      ],
      imports: [FormsModule],
      providers: [{provide: AdminRareBooksFacade, useValue: facade}],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminRareBooksComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('shows the draft queue, no-photo queue and the dense rare-book list', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-testid="rare-books-list"]')).not.toBeNull();
    expect(element.textContent).toContain('1 fiche en brouillon attend une relecture');
    expect(element.textContent).toContain('1 fiche publiée sans photo');
    expect(element.textContent).toContain('Le livre rare');
    expect(element.textContent).toContain('sans ISBN');
    expect(element.textContent).toContain('35,00 €');
    expect(element.textContent).not.toContain('panier');
  });

  it('forwards all list filters and opens the create form', async () => {
    component.search = 'Baudelaire';
    component.statusFilter = 'Published';
    component.availabilityFilter = 'sold';
    component.isbnFilter = 'without';
    component.withoutPhoto = true;
    component.minPrice = 10;
    component.maxPrice = 80;
    await component.applyFilters();

    expect(facade.load).toHaveBeenCalledWith({
      search: 'Baudelaire',
      status: 'Published',
      availability: 'sold',
      hasIsbn: false,
      minPrice: 10,
      maxPrice: 80,
      withoutPhoto: true,
      page: 1,
      pageSize: 50,
    });

    component.startCreate();
    expect(facade.startCreate).toHaveBeenCalled();
  });

  it('offers the public fiche link for a published rare book', () => {
    facade.selectedBook.set((facade.page()?.books ?? [])[1]);
    facade.editorOpen.set(true);
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('[data-testid="rare-public-link"]') as HTMLAnchorElement;
    expect(link).not.toBeNull();
    expect(link.getAttribute('href')).toBe('/livres-rares/livre-publie');
    expect(link.textContent).toContain('Voir la fiche publique');
  });
});
