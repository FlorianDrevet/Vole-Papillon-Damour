import {signal, WritableSignal} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {RouterModule, Router} from '@angular/router';
import {of} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogBook, CatalogFair, CatalogSearchResponse} from '../../core/catalog.models';
import {BookCardComponent} from '../../shared/book-card/book-card.component';
import {CookieConsentService} from '../../shared/services/cookie-consent.service';
import {DesignSystemModule} from '@vpd/ui';
import {CatalogHomePageComponent} from './catalog-home-page.component';

describe('CatalogHomePageComponent', () => {
  let fixture: ComponentFixture<CatalogHomePageComponent>;
  let api: jasmine.SpyObj<CatalogApiService>;
  let router: jasmine.SpyObj<Router>;
  let consent: {
    mapsEnabled: WritableSignal<boolean>;
    enableMaps: jasmine.Spy;
  };

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
    consent = {
      mapsEnabled: signal(true),
      enableMaps: jasmine.createSpy('enableMaps').and.callFake(() => consent.mapsEnabled.set(true)),
    };
    await TestBed.configureTestingModule({
      declarations: [CatalogHomePageComponent, BookCardComponent],
      imports: [FormsModule, RouterModule.forRoot([]), DesignSystemModule],
      providers: [
        {provide: CatalogApiService, useValue: api},
        {provide: CookieConsentService, useValue: consent},
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
    expect(fixture.nativeElement.querySelector('.home-account-callout')?.textContent).toContain('Avec un compte');
    expect(fixture.nativeElement.querySelector('.home-account-callout')?.textContent)
      .toContain('Suivez un livre, on vous prévient quand il arrive.');
    expect(fixture.nativeElement.querySelector('.home-account-callout')?.textContent).not.toContain('Votre sélection');
    expect(api.search).toHaveBeenCalledWith({availability: 'available', sort: 'recent', pageSize: 4});
  });

  it('places the genre browser and account guidance after the rare books section', () => {
    const element = fixture.nativeElement as HTMLElement;
    const rareSection = element.querySelector('.rare-section') as HTMLElement;
    const genresSection = element.querySelector('.genres-section') as HTMLElement;
    const accountSection = element.querySelector('.home-account-callout') as HTMLElement;

    expect(rareSection.compareDocumentPosition(genresSection) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(genresSection.compareDocumentPosition(accountSection) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(genresSection.querySelector('.eyebrow')?.textContent).toContain('Parcourir');
    expect(genresSection.querySelector('h2')?.textContent).toContain('Par genres');
    expect(genresSection.querySelector('.genre-card--all')).not.toBeNull();
    expect(accountSection.querySelectorAll('.home-account-step')).toHaveSize(3);
    expect(accountSection.querySelector('.callout-link')?.textContent).toContain('Créer mon compte');
  });

  it('uses the mockup card variant only for recent books', () => {
    const element = fixture.nativeElement as HTMLElement;
    const recentCard = element.querySelector('.book-grid[aria-label="Livres arrivés récemment"] app-book-card');
    const rareCard = element.querySelector('.book-grid[aria-label="Livres rares"] app-book-card');

    expect(recentCard?.getAttribute('variant')).toBe('home');
    expect(rareCard?.getAttribute('variant')).toBeNull();
  });

  it('renders recent availability as a colored status tag on the cover', () => {
    const status = fixture.nativeElement.querySelector(
      '.book-grid[aria-label="Livres arrivés récemment"] .book-card-status',
    ) as HTMLElement;

    expect(status.classList).toContain('status-available');
    expect(getComputedStyle(status).borderRadius).toBe('999px');
    expect(getComputedStyle(status).backgroundColor).not.toBe('rgba(0, 0, 0, 0)');
  });

  it('uses the 2a hero composition with the butterfly and a footer fair row', () => {
    const hero = fixture.nativeElement.querySelector('.hero') as HTMLElement;
    const heroSearchButton = hero.querySelector('.hero-search button') as HTMLButtonElement;
    const heroVisual = hero.querySelector('.hero-visual') as HTMLElement;
    const butterfly = hero.querySelector('.hero-butterfly') as HTMLImageElement;

    expect(hero.textContent).toContain('Est-ce que ce livre sera');
    expect(hero.textContent).toContain('à la bourse aux livres');
    expect(hero.querySelector('.hero-date')).toBeNull();
    expect(heroVisual).not.toBeNull();
    expect(butterfly).not.toBeNull();
    expect(butterfly.getAttribute('src')).toBe('images/papillon_without_back.png');
    expect(heroVisual.querySelectorAll('.hero-book').length).toBe(0);
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
    expect(teaser.textContent).toContain('Prochaine bourse');
    expect(teaser.textContent).toContain('10 octobre 2026');
    expect(teaser.textContent).toContain('Plus d’infos');
    expect(teaser.querySelector('.home-fair-teaser-time')).toBeNull();
    expect(teaser.textContent).not.toContain('46 route de Saint-Marcellin');
    expect(element.querySelector('.fair-section')).toBeNull();
    expect(element.querySelector('.next-fair-card')).toBeNull();
    expect(element.querySelector('.upcoming-fairs')).toBeNull();
  });

  it('highlights the next fair and renders the complete upcoming book-fair schedule', () => {
    fixture.componentInstance.upcomingOnly.set(true);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    const datesSection = element.querySelector('.fair-section') as HTMLElement;

    expect(element.querySelector('.hero')).toBeNull();
    expect(element.querySelector('.home-fair-teaser')).toBeNull();
    expect(datesSection).not.toBeNull();
    expect(datesSection.querySelector('.next-fair-card')).not.toBeNull();
    expect(datesSection.querySelector('.fair-location-card iframe')).not.toBeNull();
    expect(datesSection.querySelector('.fair-location-card iframe')?.getAttribute('src'))
      .toContain('https://www.google.com/maps?q=');
    expect(datesSection.querySelector('.fair-location-card iframe')?.getAttribute('src'))
      .toContain('&hl=fr&z=15&output=embed');
    expect(datesSection.querySelector<HTMLAnchorElement>('.fair-location-map-link')?.href)
      .toContain('https://www.google.com/maps/search/');
    expect(datesSection.querySelector('.fair-location-address')?.textContent)
      .toContain('46 route de Saint-Marcellin');
    expect(datesSection.querySelectorAll('.fair-detail-icon img')).toHaveSize(3);
    expect(datesSection.querySelector('.fair-detail-icon img')?.getAttribute('src'))
      .toBe('icons/calendar-icon.svg');
    expect(datesSection.querySelectorAll('.upcoming-fair')).toHaveSize(2);
    expect(datesSection.querySelector('.upcoming-fair--next h3')?.textContent)
      .toContain('Bourse d’automne');
    expect(datesSection.textContent).toContain('Bourse d’automne');
    expect(datesSection.textContent).toContain('Bourse de mars');
    expect(datesSection.querySelector('.upcoming-fair--next')).not.toBeNull();
    expect(element.querySelector('.selection-section')).toBeNull();
    expect(element.querySelector('.genres-section')).toBeNull();
    expect(element.querySelector('.home-account-callout')).toBeNull();
  });

  it('groups each upcoming date into scannable regions with clear mobile actions', () => {
    fixture.componentInstance.upcomingOnly.set(true);
    fixture.detectChanges();

    const firstFair = fixture.nativeElement.querySelector('.upcoming-fair') as HTMLElement;
    const primaryAction = firstFair.querySelector('.upcoming-fair-action--primary') as HTMLAnchorElement;

    expect(firstFair.querySelector('.upcoming-fair-main')).not.toBeNull();
    expect(firstFair.querySelector('.upcoming-fair-meta')).not.toBeNull();
    expect(firstFair.querySelector('.upcoming-fair-actions')).not.toBeNull();
    expect(primaryAction.textContent).toContain('Ajouter à l’agenda');
    expect(primaryAction.getAttribute('aria-label')).toBe('Ajouter Bourse d’automne à l’agenda');
  });

  it('offers an explicit Google Maps opt-in before embedding the map', () => {
    consent.mapsEnabled.set(false);
    fixture.componentInstance.upcomingOnly.set(true);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.fair-location-map')).toBeNull();
    const consentButton = element.querySelector('.fair-location-map-consent button') as HTMLButtonElement;
    expect(consentButton.textContent).toContain('Afficher la carte Google Maps');

    consentButton.click();
    fixture.detectChanges();

    expect(consent.enableMaps).toHaveBeenCalledOnceWith();
    expect(element.querySelector('.fair-location-map')).not.toBeNull();
  });

  it('keeps legacy fair opening hours as civil UTC components', () => {
    expect(fixture.componentInstance.formatTime('2027-03-14T09:30:00Z')).toBe('9 h 30');
  });

  it('renders only API genres as cards that open a filtered search', () => {
    const element = fixture.nativeElement as HTMLElement;
    const cards = Array.from(
      element.querySelectorAll<HTMLAnchorElement>('.genre-card'),
    );

    expect(cards.length).toBe(4);
    expect(cards.slice(0, 3).map(card => card.querySelector('strong')?.textContent?.trim()))
      .toEqual(['Jeunesse', 'Romans', 'Policier']);
    expect(cards[0].getAttribute('href')).toBe('/recherche?genre=Jeunesse');
    expect(cards[3].classList).toContain('genre-card--all');
    expect(cards[3].querySelector('strong')?.textContent?.trim()).toBe('Les 3 genres');
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
