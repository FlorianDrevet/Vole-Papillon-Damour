import {ComponentFixture, TestBed} from '@angular/core/testing';
import {provideRouter} from '@angular/router';
import {VpdEventEnum} from '../../../../shared/enums/vpdEvent.enum';
import {VpdEventModel} from '../../../../shared/models/vpdEvent.model';
import {AgendaFiltersComponent} from '../agenda-filters/agenda-filters.component';
import {AgendaListComponent} from './agenda-liste.component';

function event(
  id: string,
  eventType: VpdEventEnum,
  dateStart: string,
  options: {dateEnd?: string | null; liveTheme?: 'Classic' | 'Halloween'; name?: string} = {},
): VpdEventModel {
  return {
    id,
    eventType,
    liveTheme: options.liveTheme ?? 'Classic',
    name: options.name ?? `${eventType === VpdEventEnum.Bingo ? 'Loto' : 'Bourse aux livres'} ${id}`,
    description: '',
    dateStart: new Date(dateStart),
    dateEnd: options.dateEnd ? new Date(options.dateEnd) : null,
    hourOpenDoors: null,
    hourCloseDoors: null,
    urlImageMap: null,
    urlRegistration: null,
    urlImage: '',
    city: 'Arras',
    road: '',
    cityCode: '62000',
    roadNumber: '',
    parties: [],
    currentPartieIndex: 0,
    bingoHasBeenWon: false,
    bingoNumeros: [],
  };
}

describe('AgendaListComponent', () => {
  let fixture: ComponentFixture<AgendaListComponent>;

  beforeEach(async () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date('2026-10-31T12:00:00Z'));
    await TestBed.configureTestingModule({
      imports: [AgendaListComponent, AgendaFiltersComponent],
      providers: [provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(AgendaListComponent);
  });

  afterEach(() => jasmine.clock().uninstall());

  it('groups by the start month and shows a multi-day book fair only once', () => {
    fixture.componentRef.setInput('events', [
      event('halloween', VpdEventEnum.Bingo, '2026-11-01T12:00:00Z', {
        liveTheme: 'Halloween',
        name: 'Loto d’Halloween',
      }),
      event('books', VpdEventEnum.Books, '2026-11-11T00:00:00Z', {
        dateEnd: '2026-11-16T00:00:00Z',
        name: 'Bourse aux livres',
      }),
    ]);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('.agenda-month').length).toBe(1);
    expect(fixture.nativeElement.querySelector('.month-name')?.textContent).toContain('novembre');
    expect(fixture.nativeElement.querySelectorAll('.agenda-event--books').length).toBe(1);
    expect(fixture.nativeElement.querySelector('.agenda-event--books')?.textContent).toContain('11–16');
  });

  it('includes the pumpkin in a Halloween loto banner', () => {
    fixture.componentRef.setInput('events', [
      event('halloween', VpdEventEnum.Bingo, '2026-11-01T12:00:00Z', {
        liveTheme: 'Halloween',
        name: 'Loto d’Halloween',
      }),
    ]);
    fixture.detectChanges();

    const banner = fixture.nativeElement.querySelector('.agenda-event--halloween') as HTMLElement;
    expect(banner.querySelector('vpd-citrouille')).not.toBeNull();
    expect(banner.textContent).toContain('Loto d’Halloween');
  });
});

describe('AgendaFiltersComponent', () => {
  it('hides filters with no matching events and never displays event counts', async () => {
    await TestBed.configureTestingModule({
      imports: [AgendaFiltersComponent],
    }).compileComponents();
    const fixture = TestBed.createComponent(AgendaFiltersComponent);
    fixture.componentRef.setInput('events', [event('loto', VpdEventEnum.Bingo, '2026-11-01T12:00:00Z')]);
    fixture.detectChanges();

    const labels = (fixture.nativeElement.textContent as string).trim();
    expect(labels).toContain('Tout');
    expect(labels).toContain('Lotos');
    expect(labels).not.toContain('Bourses aux livres');
    expect(labels).not.toContain('Autres');
    expect(labels).not.toMatch(/\d/);
  });
});
