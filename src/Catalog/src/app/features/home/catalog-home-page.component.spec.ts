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
  });

  it('keeps the hero focused on the book fair and uses the association butterfly', () => {
    const hero = fixture.nativeElement.querySelector('.hero') as HTMLElement;
    const heroSearchButton = hero.querySelector('.hero-search button') as HTMLButtonElement;
    const butterfly = hero.querySelector('.hero-butterfly') as HTMLImageElement;

    expect(hero.textContent).toContain('Est-ce que ce livre sera');
    expect(hero.textContent).toContain('à la bourse aux livres');
    expect(hero.querySelector('.hero-date')).toBeNull();
    expect(hero.querySelector('.hero-orbit')).toBeNull();
    expect(butterfly.getAttribute('src')).toContain('papillon_without_back.png');
    expect(butterfly.getAttribute('alt')).toBe('');
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

  it('puts the next fair and its map card before the complete upcoming schedule', () => {
    const datesSection = fixture.nativeElement.querySelector('#prochaines-dates');

    expect(datesSection).not.toBeNull();
    expect(datesSection.querySelector('.next-fair-card')).not.toBeNull();
    expect(datesSection.querySelector('.fair-location-card iframe')?.getAttribute('title'))
      .toContain('Carte du lieu');
    expect(datesSection.querySelector('.fair-location-address')?.textContent)
      .toContain('46 route de Saint-Marcellin');
    expect(datesSection.querySelector('.upcoming-fairs')).not.toBeNull();
    expect(datesSection.querySelectorAll('.upcoming-fair-row').length).toBe(2);
    expect(datesSection.textContent).toContain('Bourse de mars');
    expect(datesSection.textContent).toContain('Bourse d’automne');
  });

  it('keeps legacy fair opening hours as civil UTC components', () => {
    expect(fixture.componentInstance.formatTime('2027-03-14T09:30:00Z')).toBe('9 h 30');
  });

  it('renders featured genre cards that open a filtered search', () => {
    const element = fixture.nativeElement as HTMLElement;
    const cards = Array.from(
      element.querySelectorAll<HTMLAnchorElement>('.genre-card'),
    );

    expect(cards.length).toBe(5);
    expect(cards[0].textContent).toContain('Romans');
    expect(cards[0].getAttribute('href')).toBe('/recherche?genre=Romans');
  });

  it('keeps featured genres in the hero selector when the API has no genre list', () => {
    fixture.componentInstance.genres.set([]);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const options = Array.from(
      element.querySelectorAll<HTMLOptionElement>('.hero-genre-select option'),
    ).map(option => option.value);

    expect(options).toContain('Romans');
    expect(options).toContain('Jeunesse');
  });
});
