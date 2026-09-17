import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {of} from 'rxjs';

import {CatalogApiService} from '../../../core/catalog-api.service';
import {CatalogAdminRareBook} from '../../../core/catalog.models';
import {CatalogAdminRareBookFormValue} from './admin-rare-books.facade';
import {
  AdminRareBookFormComponent,
} from './admin-rare-book-form.component';

describe('AdminRareBookFormComponent', () => {
  let fixture: ComponentFixture<AdminRareBookFormComponent>;
  let component: AdminRareBookFormComponent;
  let catalogApi: jasmine.SpyObj<CatalogApiService>;

  const book: CatalogAdminRareBook = {
    id: 'rare-book-id',
    slug: 'le-livre',
    isbn13: '9782070363735',
    title: 'Le livre',
    authorMention: 'Auteur',
    publisher: 'Éditeur',
    publicationYear: 1920,
    shelf: 'Éditions anciennes',
    price: 35,
    condition: 'GoodWithFlaws',
    publicDescription: 'Défauts décrits ici.',
    binding: 'Reliure',
    dimensions: '18 × 12 cm',
    pageCount: 240,
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
  };

  beforeEach(async () => {
    catalogApi = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['searchReferences']);
    catalogApi.searchReferences.and.returnValue(of({
      generatedAt: '',
      query: 'Baudelaire',
      items: [{
        isbn13: '9782070363735',
        workId: 'OL42W',
        title: 'Les fleurs du mal',
        authors: 'Charles Baudelaire',
        publisher: 'Poulet-Malassis',
        publicationYear: 1857,
        genre: null,
        coverUrl: 'https://covers.example/flowers.jpg',
        source: 'OpenLibrary',
      }],
      page: 1,
      pageSize: 20,
    }));

    await TestBed.configureTestingModule({
      declarations: [AdminRareBookFormComponent],
      imports: [FormsModule],
      providers: [{provide: CatalogApiService, useValue: catalogApi}],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminRareBookFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders the identity, price, condition and public description controls', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[data-testid="rare-title"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="rare-author"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="rare-shelf"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="rare-price"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="rare-condition-good"]')).not.toBeNull();
    expect(element.querySelector('[data-testid="rare-description"]')).not.toBeNull();
    expect(element.textContent).toContain('0 / 1200');
    expect(element.textContent).toContain('Sans ISBN, la fiche vit seule');
  });

  it('normalizes a valid editor submission and keeps an ISBN optional', () => {
    const emitted: {value: CatalogAdminRareBookFormValue; rowVersion: string | null}[] = [];
    component.submitted.subscribe(value => emitted.push(value));
    component.form = {
      title: '  Titre rare  ',
      authorMention: ' Auteur ',
      publisher: '',
      publicationYear: 1900,
      shelf: 'Illustrés',
      price: 20,
      condition: 'Worn',
      publicDescription: ' Description ',
      binding: '',
      dimensions: '18 × 12 cm',
      pageCount: 200,
      shelfLocation: '',
      priceSetBy: 'Conseil',
      isbn13: '',
    };

    component.submit();

    expect(emitted).toEqual([{
      value: {
        title: 'Titre rare',
        authorMention: 'Auteur',
        publisher: '',
        publicationYear: 1900,
        shelf: 'Illustrés',
        price: 20,
        condition: 'Worn',
        publicDescription: 'Description',
        binding: '',
        dimensions: '18 × 12 cm',
        pageCount: 200,
        shelfLocation: '',
        priceSetBy: 'Conseil',
        isbn13: '',
      },
      rowVersion: null,
    }]);
    expect(component.validationError()).toBeNull();
  });

  it('loads a bibliographic reference and applies metadata without importing its cover as a photo', async () => {
    component.referenceQuery = 'Baudelaire';
    await component.searchReferences();
    expect(catalogApi.searchReferences).toHaveBeenCalledWith('Baudelaire', 1, 20);

    component.useReference(component.referenceResults()[0]);
    expect(component.form.title).toBe('Les fleurs du mal');
    expect(component.form.authorMention).toBe('Charles Baudelaire');
    expect(component.form.isbn13).toBe('9782070363735');
    expect((component.form as unknown as Record<string, unknown>)['coverUrl']).toBeUndefined();
    expect(component.referenceError()).toBeNull();
  });

  it('hydrates an existing book while preserving its concurrency version', () => {
    component.book = book;
    component.ngOnChanges({
      book: {
        currentValue: book,
        previousValue: null,
        firstChange: true,
        isFirstChange: () => true,
      },
    });
    fixture.detectChanges();

    expect(component.form.title).toBe('Le livre');
    expect(component.rowVersion()).toBe('version-1');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Modifier la fiche rare');
  });
});
