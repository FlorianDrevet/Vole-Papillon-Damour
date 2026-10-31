import {signal} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {provideRouter, RouterLink} from '@angular/router';
import {TodayEventModel} from '../../../shared/models/today-event.model';
import {TodayEventService} from '../../../shared/services/today-event.service';
import {HalloweenPreferences} from '../../../shared/components/mascots/halloween-preferences';
import {TodayBannerHalloweenComponent} from './today-banner-halloween.component';
import {TODAY_BANNER_STORAGE_KEY} from './today-banner-state';
import {TodayBannerComponent} from './today-banner.component';

function event(
  liveTheme?: 'Classic' | 'Halloween' | null,
  dateStart = '2026-11-01T12:00:00Z',
  liveStatus: TodayEventModel['liveStatus'] = 'Live',
): TodayEventModel {
  return {
    id: 'loto-1',
    name: 'Grand loto de Vole Papillon d’Amour',
    eventType: 'Bingo',
    dateStart,
    dateEnd: null,
    hourOpenDoors: '2026-11-01T11:30:00Z',
    hourCloseDoors: '2026-11-01T17:00:00Z',
    city: 'Arras',
    liveStatus,
    lastNumero: 42,
    ...(liveTheme === undefined ? {} : {liveTheme}),
  };
}

describe('TodayBannerComponent', () => {
  let today: ReturnType<typeof signal<TodayEventModel | null>>;
  let calm: ReturnType<typeof signal<boolean>>;
  let phone: ReturnType<typeof signal<boolean>>;
  let eventService: jasmine.SpyObj<TodayEventService>;
  let fixture: ComponentFixture<TodayBannerComponent>;

  async function createBanner(): Promise<ComponentFixture<TodayBannerComponent>> {
    fixture = TestBed.createComponent(TodayBannerComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(async () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-10-31T12:00:00Z'));
    localStorage.removeItem(TODAY_BANNER_STORAGE_KEY);
    today = signal<TodayEventModel | null>(event('Halloween'));
    calm = signal(false);
    phone = signal(false);
    eventService = jasmine.createSpyObj<TodayEventService>('TodayEventService', ['start'], {today});
    eventService.start.and.resolveTo();
    await TestBed.configureTestingModule({
      declarations: [TodayBannerComponent],
      imports: [RouterLink, TodayBannerHalloweenComponent],
      providers: [
        provideRouter([]),
        {provide: TodayEventService, useValue: eventService},
        {provide: HalloweenPreferences, useValue: {calm, phone}},
      ],
    }).compileComponents();
  });

  afterEach(() => {
    localStorage.removeItem(TODAY_BANNER_STORAGE_KEY);
    jasmine.clock().uninstall();
  });

  it('rend la variante Halloween avec l’état en direct et le dernier numéro', async () => {
    const banner = await createBanner();

    expect(banner.nativeElement.querySelector('app-today-banner-halloween')).not.toBeNull();
    expect(banner.nativeElement.textContent).toContain('En direct');
    expect(banner.nativeElement.textContent).toContain('Dernier numéro');
    expect(banner.nativeElement.querySelector('.last-chip')?.textContent?.trim()).toBe('42');
  });

  it('garde le template actuel pour un thème Classic ou absent', async () => {
    today.set(event('Classic'));
    const banner = await createBanner();

    expect(banner.nativeElement.querySelector('app-today-banner-halloween')).toBeNull();
    expect(banner.nativeElement.querySelector('.bg-ink')).not.toBeNull();

    today.set(event(undefined));
    banner.detectChanges();
    expect(banner.nativeElement.querySelector('app-today-banner-halloween')).toBeNull();
  });

  it('annonce le prochain rendez-vous et sa date cinq jours avant', async () => {
    today.set(event('Classic', '2026-11-05T12:00:00Z', 'NotStarted'));
    const banner = await createBanner();

    expect(banner.nativeElement.querySelector('.stage-label')?.textContent?.trim()).toBe('Dans 5 jours');
    expect(banner.nativeElement.textContent).toContain('jeudi 5 novembre · Arras');
  });

  it('shows the label for tomorrow, today, live, finished and an ongoing book event', async () => {
    today.set(event('Classic', '2026-11-01T12:00:00Z', 'NotStarted'));
    const banner = await createBanner();
    const label = () => banner.nativeElement.querySelector('.stage-label')?.textContent?.trim();

    expect(label()).toBe('Demain');
    today.set(event('Classic', '2026-10-31T12:00:00Z', 'NotStarted'));
    banner.detectChanges();
    expect(label()).toBe("Aujourd'hui");
    today.set(event('Classic', '2026-10-31T12:00:00Z', 'Live'));
    banner.detectChanges();
    expect(label()).toBe('En direct');
    today.set(event('Classic', '2026-10-31T12:00:00Z', 'Finished'));
    banner.detectChanges();
    expect(label()).toBe('Terminé');
    today.set({
      ...event(null, '2026-10-28T12:00:00Z', null),
      eventType: 'Books',
      dateEnd: '2026-11-02T12:00:00Z',
    });
    banner.detectChanges();
    expect(label()).toBe('En ce moment');
  });

  it('keeps an upcoming dismissal until the event reaches day J', async () => {
    today.set(event('Classic', '2026-11-05T12:00:00Z', 'NotStarted'));
    const banner = await createBanner();
    (banner.nativeElement.querySelector('[aria-label="Masquer le bandeau"]') as HTMLButtonElement).click();
    banner.detectChanges();

    expect(JSON.parse(localStorage.getItem(TODAY_BANNER_STORAGE_KEY) ?? '{}').stage).toBe('upcoming');
    jasmine.clock().mockDate(new Date('2026-11-04T12:00:00Z'));
    today.set(event('Classic', '2026-11-05T12:00:00Z', 'NotStarted'));
    banner.detectChanges();
    expect(banner.nativeElement.querySelector('.bg-ink')).toBeNull();

    jasmine.clock().mockDate(new Date('2026-11-05T12:00:00Z'));
    today.set(event('Classic', '2026-11-05T12:00:00Z', 'NotStarted'));
    banner.detectChanges();
    expect(banner.nativeElement.querySelector('.bg-ink')).not.toBeNull();
    expect(banner.nativeElement.querySelector('.stage-label')?.textContent?.trim()).toBe("Aujourd'hui");
  });

  it('joue le fantôme à la première apparition et le réserve pour ce chargement', async () => {
    const first = await createBanner();
    expect(first.nativeElement.querySelector('.runner .gh-course')).not.toBeNull();

    const second = await createBanner();
    expect(second.nativeElement.querySelector('.runner')).toBeNull();
  });

  it('fait passer deux chauves-souris quand un nouveau numéro arrive', async () => {
    const banner = await createBanner();
    expect(banner.nativeElement.querySelectorAll('.hbat').length).toBe(0);

    today.set({...event('Halloween'), lastNumero: 43});
    banner.detectChanges();

    expect(banner.nativeElement.querySelectorAll('.hbat').length).toBe(2);
    expect(banner.nativeElement.querySelector('.last-chip.chip-a, .last-chip.chip-b')).not.toBeNull();
  });

  it('fait traverser les chauves-souris sur toute la largeur du bandeau mobile', async () => {
    phone.set(true);
    const banner = await createBanner();

    today.set({...event('Halloween'), lastNumero: 43});
    banner.detectChanges();

    const bats = banner.nativeElement.querySelectorAll('.hbat');
    expect(bats.length).toBe(2);
    expect(bats[0].style.animationName).toBe('hbat-one-mobile-b');
    expect(bats[1].style.animationName).toBe('hbat-two-mobile-b');
  });

  it('ne joue ni le fantôme ni les chauves-souris en mode calme', async () => {
    calm.set(true);
    const banner = await createBanner();

    expect(banner.nativeElement.querySelector('.runner')).toBeNull();
    today.set({...event('Halloween'), lastNumero: 43});
    banner.detectChanges();
    expect(banner.nativeElement.querySelectorAll('.hbat').length).toBe(0);
  });
});
