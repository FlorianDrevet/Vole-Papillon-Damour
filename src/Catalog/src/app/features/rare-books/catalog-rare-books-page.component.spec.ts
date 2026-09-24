import {ComponentFixture, TestBed} from '@angular/core/testing';
import {LOCALE_ID} from '@angular/core';
import {registerLocaleData} from '@angular/common';
import localeFr from '@angular/common/locales/fr';
import {ActivatedRoute, Router, convertToParamMap} from '@angular/router';
import {FormsModule} from '@angular/forms';
import {RouterModule} from '@angular/router';
import {of} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogFair, CatalogRareBookPage, CatalogRareBook} from '../../core/catalog.models';
import {CatalogRareBookCardComponent} from '../../shared/rare-book-card/rare-book-card.component';
import {CatalogRareBooksPageComponent} from './catalog-rare-books-page.component';
import {DesignSystemModule} from '@vpd/ui';

registerLocaleData(localeFr);

describe('CatalogRareBooksPageComponent', () => {
  let fixture: ComponentFixture<CatalogRareBooksPageComponent>;
  let api: jasmine.SpyObj<CatalogApiService>;
  let router: jasmine.SpyObj<Router>;

  const nextFair: CatalogFair = {
    id: 'fair-1',
    name: 'Bourse de septembre',
    dateStart: '2026-09-20T00:00:00Z',
    dateEnd: null,
    openAt: '2026-09-20T09:00:00Z',
    closeAt: '2026-09-20T18:00:00Z',
    roadNumber: 12,
    city: 'Paris',
    cityCode: 75001,
    road: 'Rue des livres',
  };

  const rareBook: CatalogRareBook = {
    id: 'rare-1',
    slug: 'les-fables',
    isbn13: null,
    title: 'Les Fables',
    authorMention: 'Jean de La Fontaine',
    publisher: 'Imprimerie royale',
    publicationYear: 1770,
    price: 60,
    condition: 'GoodWithFlaws',
    publicDescription: 'Exemplaire illustré.',
    status: 'Published',
    isSold: false,
    soldAt: null,
    photos: [],
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['getPublicRareBooks', 'getUpcomingFairs']);
    api.getPublicRareBooks.and.returnValue(of({
      generatedAt: '2026-09-17T10:00:00Z',
      books: [rareBook],
      totalCount: 1,
      page: 1,
      pageSize: 24,
    } satisfies CatalogRareBookPage));
    api.getUpcomingFairs.and.returnValue(of([nextFair]));
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    await TestBed.configureTestingModule({
      declarations: [CatalogRareBooksPageComponent, CatalogRareBookCardComponent],
      imports: [FormsModule, RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        {provide: CatalogApiService, useValue: api},
        {provide: Router, useValue: router},
        {provide: LOCALE_ID, useValue: 'fr-FR'},
        {provide: ActivatedRoute, useValue: {
          queryParamMap: of(convertToParamMap({})),
        }},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogRareBooksPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('loads a dedicated rare-book page and displays the firm price as reading information', () => {
    expect(api.getPublicRareBooks).toHaveBeenCalledWith({
      includeSold: true,
      sort: 'price-desc',
      page: 1,
      pageSize: 24,
    });
    expect(api.getUpcomingFairs).toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('.rare-book-card')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('60,00 €');
    expect(fixture.nativeElement.textContent).not.toContain('Ajouter au panier');
  });

  it('announces the next book fair above the rare selection', () => {
    expect(fixture.nativeElement.textContent).toContain('Prochaine bourse');
    expect(fixture.nativeElement.textContent).toContain('Bourse de septembre');
  });

  it('invites visitors to donate an old book without turning the catalogue into a shop', () => {
    const callout = fixture.nativeElement.querySelector('.rare-donation-callout') as HTMLElement;

    expect(callout).not.toBeNull();
    expect(callout.textContent).toContain('livre ancien à donner');
    expect(callout.querySelector('a')?.getAttribute('href')).toContain('mailto:');
  });

  it('does not show public shelf filters for rare books', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('.rare-shelf-chips')).toBeNull();
    expect(element.textContent).not.toContain('Rayons');
    expect(element.textContent).not.toContain('Éditions anciennes');
  });
});
