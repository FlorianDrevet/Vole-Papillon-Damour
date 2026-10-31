import {NO_ERRORS_SCHEMA, PLATFORM_ID} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {CommonModule} from '@angular/common';
import {By} from '@angular/platform-browser';
import {provideRouter} from '@angular/router';
import {DesignSystemModule} from '@vpd/ui';
import {VpdEventsFacadeService} from '../../shared/facades/vpd-events.facade.service';
import {AgendaFiltersComponent} from './components/agenda-filters/agenda-filters.component';
import {AgendaListComponent} from './components/agenda-liste/agenda-liste.component';
import {AgendaCalendarComponent} from './components/agenda-calendrier/agenda-calendrier.component';
import {AgendaViewToggleComponent} from './components/agenda-view-toggle/agenda-view-toggle.component';
import {FeaturedEventComponent} from './components/featured-event/featured-event.component';
import {NextEventsComponent} from './components/next-events/next-events.component';
import {SpecialEventComponent} from './components/special-event/special-event.component';
import {VpdEventsPageComponent} from './vpd-events-page.component';

function apiEvent(
  id: string,
  name: string,
  eventType: 'Bingo' | 'Books' | 'Other',
  dateStart: string,
  options: {dateEnd?: string | null; liveTheme?: 'Classic' | 'Halloween'; city?: string} = {},
) {
  return {
    id,
    name,
    eventType,
    liveTheme: options.liveTheme ?? 'Classic',
    description: '',
    dateStart,
    dateEnd: options.dateEnd ?? null,
    hourOpenDoors: '2026-11-01T11:00:00Z',
    hourCloseDoors: '2026-11-01T17:00:00Z',
    city: options.city ?? 'Arras',
    road: '',
    cityCode: '62000',
    roadNumber: '',
    urlImage: '',
    urlImageMap: null,
    urlRegistration: null,
    parties: [],
    currentPartieIndex: 0,
    bingoHasBeenWon: false,
    bingoNumeros: [],
  };
}

describe('VpdEventsPageComponent', () => {
  let fixture: ComponentFixture<VpdEventsPageComponent>;
  let eventsFacadeSpy: jasmine.SpyObj<VpdEventsFacadeService>;

  async function configureTestModule(platformId: 'browser' | 'server' = 'browser'): Promise<void> {
    eventsFacadeSpy = jasmine.createSpyObj<VpdEventsFacadeService>('VpdEventsFacadeService', ['getAllEvents$']);

    await TestBed.configureTestingModule({
      declarations: [VpdEventsPageComponent, SpecialEventComponent],
      imports: [
        CommonModule,
        DesignSystemModule,
        FeaturedEventComponent,
        NextEventsComponent,
        AgendaFiltersComponent,
        AgendaListComponent,
        AgendaCalendarComponent,
        AgendaViewToggleComponent,
      ],
      providers: [
        provideRouter([]),
        {provide: PLATFORM_ID, useValue: platformId},
        {provide: VpdEventsFacadeService, useValue: eventsFacadeSpy},
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();
  }

  beforeEach(async () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-10-31T12:00:00Z'));
    localStorage.removeItem('vpd.agenda.vue');
    await configureTestModule();
  });

  afterEach(() => {
    localStorage.removeItem('vpd.agenda.vue');
    jasmine.clock().uninstall();
  });

  async function render(events: object[]): Promise<void> {
    const response = Promise.resolve(events as never);
    eventsFacadeSpy.getAllEvents$.and.returnValue(response);
    fixture = TestBed.createComponent(VpdEventsPageComponent);
    fixture.detectChanges();
    await response;
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();
  }

  it('features tomorrow’s Halloween bingo and puts the books fair next', async () => {
    await render([
      apiEvent('books-1', 'Bourse aux livres', 'Books', '2026-11-11T00:00:00Z', {
        dateEnd: '2026-11-16T00:00:00Z',
      }),
      apiEvent('loto-1', 'Loto d’Halloween', 'Bingo', '2026-11-01T12:00:00Z', {
        liveTheme: 'Halloween',
      }),
    ]);

    expect(fixture.nativeElement.querySelector('h1')?.textContent?.trim()).toBe('Nos prochains rendez-vous');
    expect(fixture.nativeElement.querySelector('app-featured-event')?.textContent).toContain('Loto d’Halloween');
    expect(fixture.nativeElement.querySelector('app-featured-event')?.textContent).toContain('Demain');
    expect(fixture.nativeElement.querySelector('.halloween-scene')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('app-next-events')?.textContent).toContain('Bourse aux livres');
  });

  it('uses no Halloween scene for a classic loto', async () => {
    await render([
      apiEvent('loto-classic', 'Loto de Maxence', 'Bingo', '2026-11-03T12:00:00Z'),
      apiEvent('books-1', 'Bourse aux livres', 'Books', '2026-11-11T00:00:00Z', {
        dateEnd: '2026-11-16T00:00:00Z',
      }),
    ]);

    expect(fixture.nativeElement.querySelector('app-featured-event')?.textContent).toContain('Loto de Maxence');
    expect(fixture.nativeElement.querySelector('.halloween-scene')).toBeNull();
  });

  it('applies the Lotos filter to the list', async () => {
    await render([
      apiEvent('loto-1', 'Loto de Maxence', 'Bingo', '2026-11-03T12:00:00Z'),
      apiEvent('books-1', 'Bourse aux livres', 'Books', '2026-11-11T00:00:00Z', {
        dateEnd: '2026-11-16T00:00:00Z',
      }),
    ]);

    const listButton = [...fixture.nativeElement.querySelectorAll('button')]
      .find(button => button.textContent?.trim() === 'Liste') as HTMLButtonElement;
    listButton.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.agenda-event').length).toBe(2);
    const lotoFilter = [...fixture.nativeElement.querySelectorAll('button')]
      .find(button => button.textContent?.trim() === 'Lotos') as HTMLButtonElement;
    lotoFilter.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.agenda-event').length).toBe(1);
    expect(fixture.nativeElement.querySelector('.agenda-event--books')).toBeNull();
    expect(fixture.nativeElement.querySelector('.agenda-event--loto')).not.toBeNull();
  });

  it('opens in Calendar and remembers a choice of List', async () => {
    await render([
      apiEvent('loto-1', 'Loto de Maxence', 'Bingo', '2026-11-01T12:00:00Z'),
      apiEvent('books-1', 'Bourse aux livres', 'Books', '2026-11-11T00:00:00Z', {
        dateEnd: '2026-11-16T00:00:00Z',
      }),
    ]);

    expect(fixture.nativeElement.querySelector('.agenda-calendar')).not.toBeNull();
    const listButton = [...fixture.nativeElement.querySelectorAll('button')]
      .find(button => button.textContent?.trim() === 'Liste') as HTMLButtonElement;
    listButton.click();
    fixture.detectChanges();

    expect(localStorage.getItem('vpd.agenda.vue')).toBe('liste');
    expect(fixture.nativeElement.querySelector('.agenda-calendar')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-agenda-liste')).not.toBeNull();

    fixture.destroy();
    await render([
      apiEvent('loto-1', 'Loto de Maxence', 'Bingo', '2026-11-01T12:00:00Z'),
      apiEvent('books-1', 'Bourse aux livres', 'Books', '2026-11-11T00:00:00Z', {
        dateEnd: '2026-11-16T00:00:00Z',
      }),
    ]);

    expect(fixture.nativeElement.querySelector('.agenda-calendar')).toBeNull();
    expect(fixture.nativeElement.querySelector('app-agenda-liste')).not.toBeNull();
  });

  it('lights November 1 and November 11 through 16 in the calendar', async () => {
    await render([
      apiEvent('loto-1', 'Loto de Maxence', 'Bingo', '2026-11-01T12:00:00Z'),
      apiEvent('books-1', 'Bourse aux livres', 'Books', '2026-11-11T00:00:00Z', {
        dateEnd: '2026-11-16T00:00:00Z',
      }),
      apiEvent('jan-loto', 'Loto de janvier', 'Bingo', '2027-01-03T12:00:00Z'),
    ]);

    expect(fixture.nativeElement.querySelector('[data-date="2026-11-01"].calendar-event-day.light')).not.toBeNull();
    expect(fixture.nativeElement.querySelectorAll('[data-event-id="books-1"].calendar-event-day').length).toBe(6);
    expect(fixture.nativeElement.querySelector('[data-date="2026-11-16"][data-event-id="books-1"]')).not.toBeNull();
  });

  it('reuses the calendar months between unchanged change-detection passes', async () => {
    await render([
      apiEvent('loto-1', 'Loto de Maxence', 'Bingo', '2026-11-01T12:00:00Z'),
      apiEvent('books-1', 'Bourse aux livres', 'Books', '2026-11-11T00:00:00Z', {
        dateEnd: '2026-11-16T00:00:00Z',
      }),
      apiEvent('jan-loto', 'Loto de janvier', 'Bingo', '2027-01-03T12:00:00Z'),
    ]);

    const calendar = fixture.debugElement.query(By.directive(AgendaCalendarComponent))
      .componentInstance as unknown as {months: unknown};
    const firstMonths = calendar.months;

    fixture.detectChanges();

    expect(calendar.months).toBe(firstMonths);
  });

  it('shows the clicked January 3 loto beneath the calendar', async () => {
    await render([
      apiEvent('loto-1', 'Loto de Maxence', 'Bingo', '2026-11-01T12:00:00Z'),
      apiEvent('jan-loto', 'Loto de Maxence', 'Bingo', '2027-01-03T12:00:00Z'),
    ]);

    const januaryLoto = fixture.nativeElement.querySelector('[data-date="2027-01-03"][data-event-id="jan-loto"]') as HTMLButtonElement;
    januaryLoto.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.calendar-selected')?.textContent).toContain('Loto de Maxence');
  });

  it('keeps the original presentation and expands the rest of the loto text', async () => {
    await render([apiEvent('loto-1', 'Loto de Maxence', 'Bingo', '2026-11-03T12:00:00Z')]);

    const lotoCard = fixture.nativeElement.querySelector('app-special-event') as HTMLElement;
    const details = lotoCard.querySelector('[id^="special-event-details-"]') as HTMLElement;
    const readMore = lotoCard.querySelector('button') as HTMLButtonElement;
    expect(lotoCard.textContent).toContain('Une salle comble de plus de 500 personnes');
    expect(lotoCard.textContent).toContain('Lire la suite');
    expect(details.hidden).toBeTrue();

    readMore.click();
    fixture.detectChanges();
    expect(details.hidden).toBeFalse();
    expect(readMore.getAttribute('aria-expanded')).toBe('true');
  });

  it('keeps the title free of spaced capitals and arrow characters', async () => {
    await render([apiEvent('loto-1', 'Loto de Maxence', 'Bingo', '2026-11-03T12:00:00Z')]);

    const page = fixture.nativeElement.textContent as string;
    expect(page).not.toContain('→');
    expect(page).not.toContain('←');
    expect(fixture.nativeElement.querySelector('.uppercase.tracking-\\[\\.16em\\]')).toBeNull();
  });

  it('shows an editorial hero skeleton while events are loading', () => {
    eventsFacadeSpy.getAllEvents$.and.returnValue(new Promise(() => undefined));
    fixture = TestBed.createComponent(VpdEventsPageComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.hero-skeleton')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[role="status"]')?.textContent).toContain('Chargement des rendez-vous');
  });

  it('explains when no new event has been scheduled and links to past editions', async () => {
    await render([]);

    const page = fixture.nativeElement as HTMLElement;
    expect(page.textContent).toContain("Pas de nouvelle date pour l'instant.");
    expect(page.textContent).toContain("Les prochains rendez-vous apparaîtront ici dès qu'ils seront fixés.");
    expect([...page.querySelectorAll('a')].some(link => link.textContent?.trim() === 'Voir les éditions passées')).toBeTrue();
  });

  describe('SSR event loading', () => {
    beforeEach(async () => {
      await TestBed.resetTestingModule();
      await configureTestModule('server');
    });

    it('keeps the server render pending until the event request finishes', async () => {
      let resolveEvents!: (events: never[]) => void;
      eventsFacadeSpy.getAllEvents$.and.returnValue(new Promise(resolve => {
        resolveEvents = resolve;
      }));
      fixture = TestBed.createComponent(VpdEventsPageComponent);
      fixture.detectChanges();

      let stable = false;
      const stability = fixture.whenStable().then(() => stable = true);
      await Promise.resolve();
      await Promise.resolve();

      expect(stable).toBeFalse();
      resolveEvents([]);
      await stability;
      expect(stable).toBeTrue();
    });
  });
});
