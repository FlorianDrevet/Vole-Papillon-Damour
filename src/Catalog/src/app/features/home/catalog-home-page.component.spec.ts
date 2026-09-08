import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {RouterModule, Router} from '@angular/router';
import {of} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogBook, CatalogFair, CatalogSearchResponse} from '../../core/catalog.models';
import {BookCardComponent} from '../../shared/book-card/book-card.component';
import {DesignSystemModule} from '@vpd/ui';
import {CatalogHomePageComponent} from './catalog-home-page.component';

describe('CatalogHomePageComponent', () => {
  let fixture: ComponentFixture<CatalogHomePageComponent>;
  let api: jasmine.SpyObj<CatalogApiService>;
  let router: jasmine.SpyObj<Router>;

  const book: CatalogBook = {
    isbn13: '9782070612758',
    title: 'Le Petit Prince',
    authors: 'Antoine de Saint-Exupéry',
    publisher: 'Gallimard',
    publicationYear: 1999,
    physicalFormat: 'Poche',
    language: 'fr',
    genre: 'Jeunesse',
    workId: null,
    coverUrl: null,
    quantityAvailable: 3,
    quantityAnnounced: 0,
    nextFairAt: null,
    lastAvailableAt: '2026-09-04T10:00:00Z',
    firstSeenAt: '2026-09-04T10:00:00Z',
    updatedAt: '2026-09-04T10:00:00Z',
    isRare: false,
  };

  const searchResponse: CatalogSearchResponse = {
    generatedAt: '2026-09-06T08:00:00Z',
    books: [book],
    totalCount: 412,
    page: 1,
    pageSize: 3,
    genres: ['Jeunesse', 'Romans', 'Policier'],
  };

  const fair: CatalogFair = {
    id: 'fair-1',
    name: 'Bourse de mars',
    dateStart: '2027-03-14T00:00:00Z',
    dateEnd: '2027-03-15T00:00:00Z',
    openAt: '2027-03-14T09:30:00Z',
    closeAt: '2027-03-15T18:00:00Z',
    roadNumber: 46,
    city: 'Saint-Just-Saint-Rambert',
    cityCode: 42170,
    road: 'route de Saint-Marcellin',
  };

  const nextFair: CatalogFair = {
    ...fair,
    id: 'fair-2',
    name: 'Bourse d’automne',
    dateStart: '2026-10-10T00:00:00Z',
    dateEnd: '2026-10-11T00:00:00Z',
    openAt: '2026-10-10T09:30:00Z',
    closeAt: '2026-10-11T18:00:00Z',
  };

  beforeEach(async () => {
    api = jasmine.createSpyObj<CatalogApiService>('CatalogApiService', ['search', 'getUpcomingFairs']);
    api.search.and.returnValue(of(searchResponse));
    api.getUpcomingFairs.and.returnValue(of([fair, nextFair]));
    await TestBed.configureTestingModule({
      declarations: [CatalogHomePageComponent, BookCardComponent],
      imports: [FormsModule, RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        {provide: CatalogApiService, useValue: api},
      ],
    }).compileComponents();

    router = TestBed.inject(Router) as jasmine.SpyObj<Router>;
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(CatalogHomePageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('puts the genre selector and availability count in the hero search', () => {
    expect(fixture.nativeElement.querySelector('.hero-genre-select')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.hero-count')?.textContent).toContain('412');
    expect(fixture.nativeElement.querySelector('.hero-count')?.textContent).toContain('titres disponibles en ce moment');
    expect(fixture.nativeElement.querySelector('.home-account-callout')).not.toBeNull();
    expect(api.search).toHaveBeenCalledWith({availability: 'available', sort: 'recent', pageSize: 4});
  });

  it('keeps the hero focused on the book fair and uses a composed book visual', () => {
    const hero = fixture.nativeElement.querySelector('.hero') as HTMLElement;
    const heroSearchButton = hero.querySelector('.hero-search button') as HTMLButtonElement;
    const heroVisual = hero.querySelector('.hero-visual') as HTMLElement;

    expect(hero.textContent).toContain('Est-ce que ce livre sera');
    expect(hero.textContent).toContain('à la bourse aux livres');
    expect(hero.querySelector('.hero-date')).toBeNull();
    expect(heroVisual).not.toBeNull();
    expect(hero.querySelector('.hero-butterfly')).toBeNull();
    expect(heroVisual.querySelectorAll('.hero-book').length).toBeGreaterThan(0);
    expect(heroSearchButton.textContent?.trim()).toBe('Rechercher');
    expect(heroSearchButton.querySelector('span')).toBeNull();
  });

  it('keeps the selected genre when sending a search from the hero', () => {
    fixture.componentInstance.search = 'petit prince';
    fixture.componentInstance.heroGenre = 'Jeunesse';

    fixture.componentInstance.submitSearch();

    expect(router.navigate).toHaveBeenCalledWith(['/recherche'], {
      queryParams: {q: 'petit prince', genre: 'Jeunesse'},
    });
  });

  it('keeps the availability scope when opening the counted catalogue', () => {
    fixture.componentInstance.showAll();

    expect(router.navigate).toHaveBeenCalledWith(['/catalogue'], {
      queryParams: {availability: 'available'},
    });
  });

  it('shows a compact next fair teaser on the home page without the full schedule', () => {
    const element = fixture.nativeElement as HTMLElement;
    const teaser = element.querySelector('.home-fair-teaser') as HTMLElement;

    expect(teaser).not.toBeNull();
    expect(teaser.textContent).toContain('Prochaine bourse aux livres');
    expect(teaser.textContent).toContain('10 octobre 2026');
    expect(teaser.textContent).toContain('9 h 30');
    expect(teaser.textContent).not.toContain('46 route de Saint-Marcellin');
    expect(element.querySelector('.fair-section')).toBeNull();
    expect(element.querySelector('.next-fair-card')).toBeNull();
    expect(element.querySelector('.upcoming-fairs')).toBeNull();
  });

  it('keeps the upcoming dates page focused on the next fair only', () => {
    fixture.componentInstance.upcomingOnly.set(true);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const datesSection = element.querySelector('.fair-section') as HTMLElement;

    expect(element.querySelector('.hero')).toBeNull();
    expect(element.querySelector('.home-fair-teaser')).toBeNull();
    expect(datesSection).not.toBeNull();
    expect(datesSection.querySelector('.next-fair-card')).not.toBeNull();
    expect(datesSection.querySelector('.fair-location-card iframe')).toBeNull();
    expect(datesSection.querySelector('.fair-location-placeholder')?.textContent)
      .toContain('Carte consultable dans Google Maps');
    expect(datesSection.querySelector<HTMLAnchorElement>('.fair-location-map-link')?.href)
      .toContain('https://www.google.com/maps/search/');
    expect(datesSection.querySelector('.fair-location-address')?.textContent)
      .toContain('46 route de Saint-Marcellin');
    expect(datesSection.querySelector('.upcoming-fairs')).toBeNull();
    expect(datesSection.textContent).toContain('Bourse d’automne');
    expect(datesSection.textContent).not.toContain('Bourse de mars');
    expect(element.querySelector('.selection-section')).toBeNull();
    expect(element.querySelector('.genres-section')).toBeNull();
    expect(element.querySelector('.home-account-callout')).toBeNull();
  });

  it('keeps legacy fair opening hours as civil UTC components', () => {
    expect(fixture.componentInstance.formatTime('2027-03-14T09:30:00Z')).toBe('9 h 30');
  });

  it('renders only API genres as cards that open a filtered search', () => {
    const element = fixture.nativeElement as HTMLElement;
    const cards = Array.from(
      element.querySelectorAll<HTMLAnchorElement>('.genre-card'),
    );

    expect(cards.length).toBe(3);
    expect(cards.map(card => card.querySelector('strong')?.textContent?.trim()))
      .toEqual(['Jeunesse', 'Romans', 'Policier']);
    expect(cards[0].getAttribute('href')).toBe('/recherche?genre=Jeunesse');
  });

  it('does not invent genre options or a genre section when the API has no genres', () => {
    fixture.componentInstance.genres.set([]);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const options = Array.from(
      element.querySelectorAll<HTMLOptionElement>('.hero-genre-select option'),
    ).map(option => option.value);

    expect(options).toEqual(['']);
    expect(element.querySelector('.genres-section')).toBeNull();
  });
});
