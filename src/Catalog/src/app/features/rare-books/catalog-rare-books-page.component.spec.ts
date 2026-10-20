import {ComponentFixture, TestBed} from '@angular/core/testing';
import {LOCALE_ID} from '@angular/core';
import {registerLocaleData} from '@angular/common';
import localeFr from '@angular/common/locales/fr';
import {ActivatedRoute, Router, convertToParamMap} from '@angular/router';
import {FormsModule} from '@angular/forms';
import {RouterModule} from '@angular/router';
import {BehaviorSubject, of} from 'rxjs';

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
  let routeParams: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

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
    routeParams = new BehaviorSubject(convertToParamMap({}));
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['getPublicRareBooks', 'getUpcomingFairs']);
    api.getPublicRareBooks.and.returnValue(of({
      generatedAt: '2026-09-17T10:00:00Z',
      books: [rareBook],
      totalCount: 49,
      page: 1,
      pageSize: 24,
    } satisfies CatalogRareBookPage));
    api.getUpcomingFairs.and.returnValue(of([] satisfies CatalogFair[]));
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);

    await TestBed.configureTestingModule({
      declarations: [CatalogRareBooksPageComponent, CatalogRareBookCardComponent],
      imports: [FormsModule, RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        {provide: CatalogApiService, useValue: api},
        {provide: Router, useValue: router},
        {provide: LOCALE_ID, useValue: 'fr-FR'},
        {provide: ActivatedRoute, useValue: {queryParamMap: routeParams}},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CatalogRareBooksPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('loads a dedicated rare-book page and displays the firm price as reading information', () => {
    expect(api.getPublicRareBooks).toHaveBeenCalledWith({
      includeSold: false,
      sort: 'price-desc',
      page: 1,
      pageSize: 24,
    });
    expect(fixture.nativeElement.querySelector('.rare-book-card')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('60,00 €');
    expect(fixture.nativeElement.textContent).not.toContain('Ajouter au panier');
  });

  it('does not show a next-fair card or a filter for sold copies', () => {
    expect(fixture.nativeElement.querySelector('.rare-next-fair')).toBeNull();
    expect(fixture.nativeElement.querySelector('.rare-sold-filter')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Voir les exemplaires partis');
    expect(api.getUpcomingFairs).not.toHaveBeenCalled();
  });

  it('keeps sold copies hidden when an old URL includes the sold-copy filter', () => {
    routeParams.next(convertToParamMap({includeSold: 'true'}));
    fixture.detectChanges();

    expect(api.getPublicRareBooks.calls.mostRecent().args[0]?.includeSold).toBeFalse();
  });

  it('uses the catalog sort menu style for rare-book sorting', () => {
    const element = fixture.nativeElement as HTMLElement;
    const trigger = element.querySelector('.rare-sort-trigger') as HTMLButtonElement | null;

    expect(element.querySelector('select')).toBeNull();
    expect(trigger).not.toBeNull();
    expect(trigger?.getAttribute('aria-haspopup')).toBe('menu');
    expect(trigger?.getAttribute('aria-expanded')).toBe('false');

    trigger?.click();
    fixture.detectChanges();

    const panel = element.querySelector('.rare-sort-panel') as HTMLElement | null;
    expect(panel?.getAttribute('role')).toBe('menu');
    expect(panel?.querySelectorAll('.rare-sort-option').length).toBe(3);
    expect(panel?.querySelector('.rare-sort-option--selected')?.textContent).toContain('Prix décroissant');
  });

  it('applies a selected rare-book sort and closes the menu', () => {
    const element = fixture.nativeElement as HTMLElement;
    const trigger = element.querySelector('.rare-sort-trigger') as HTMLButtonElement | null;

    trigger?.click();
    fixture.detectChanges();
    (element.querySelector('[data-sort="recent"]') as HTMLButtonElement | null)?.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.sort).toBe('recent');
    expect(element.querySelector('.rare-sort-panel')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({queryParams: {sort: 'recent'}}));
  });

  it('shows pagination when rare books span more than one page', () => {
    const pagination = fixture.nativeElement.querySelector('.rare-pagination') as HTMLElement | null;
    expect(pagination).not.toBeNull();
    expect(pagination?.textContent).toContain('Page 1 / 3');
  });

  it('navigates to the next rare-book page and keeps pagination in the route', () => {
    const nextButton = fixture.nativeElement.querySelector('.rare-pagination button:last-child') as HTMLButtonElement;
    nextButton.click();

    expect(fixture.componentInstance.page).toBe(2);
    expect(router.navigate).toHaveBeenCalledWith([], jasmine.objectContaining({
      relativeTo: jasmine.anything(),
      queryParams: {page: 2},
    }));
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
