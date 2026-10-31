import {signal} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {provideRouter, RouterLink} from '@angular/router';
import {TodayEventModel} from '../../../shared/models/today-event.model';
import {TodayEventService} from '../../../shared/services/today-event.service';
import {HalloweenPreferences} from '../../../shared/components/mascots/halloween-preferences';
import {TodayBannerHalloweenComponent} from './today-banner-halloween.component';
import {TodayBannerComponent} from './today-banner.component';

function event(liveTheme?: 'Classic' | 'Halloween' | null): TodayEventModel {
  return {
    id: 'loto-1',
    name: 'Grand loto de Vole Papillon d’Amour',
    eventType: 'Bingo',
    dateStart: '2026-11-01T12:00:00Z',
    dateEnd: null,
    hourOpenDoors: '2026-11-01T11:30:00Z',
    hourCloseDoors: '2026-11-01T17:00:00Z',
    city: 'Arras',
    liveStatus: 'Live',
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
