import { CommonModule } from '@angular/common';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { EMPTY } from 'rxjs';

import { VpdEventEnum } from '../../shared/enums/vpdEvent.enum';
import { VpdEventsFacadeService } from '../../shared/facades/vpd-events.facade.service';
import { VpdEventModel } from '../../shared/models/vpdEvent.model';
import { EventDetailComponent } from './event-detail.component';

describe('EventDetailComponent', () => {
  let fixture: ComponentFixture<EventDetailComponent>;

  const event: VpdEventModel = {
    id: 'event-1',
    name: 'Bourse aux livres',
    description: 'Un rendez-vous pour trouver des ouvrages à petits prix.',
    eventType: VpdEventEnum.Books,
    dateStart: new Date('2026-10-06T00:00:00.000Z'),
    dateEnd: new Date('2026-10-11T00:00:00.000Z'),
    hourOpenDoors: new Date('2026-10-06T14:00:00.000Z'),
    hourCloseDoors: new Date('2026-10-06T18:00:00.000Z'),
    urlImageMap: null,
    urlRegistration: null,
    urlImage: 'https://cdn.example.test/books.jpg',
    city: 'Andrézieux-Bouthéon',
    road: 'rue Pierre-Georges Latécoère',
    cityCode: '42160',
    roadNumber: '482',
    parties: [],
    currentPartieIndex: 0,
    bingoHasBeenWon: false,
    bingoNumeros: [],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [EventDetailComponent],
      imports: [CommonModule],
      providers: [
        { provide: ActivatedRoute, useValue: { paramMap: EMPTY } },
        { provide: VpdEventsFacadeService, useValue: {} },
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(EventDetailComponent);
    fixture.componentInstance.vpdEvent.set(event);
    fixture.componentInstance.isLoading.set(false);
    fixture.detectChanges();
  });

  it('renders the map card on the right and complete date and time details on the left', () => {
    const hero = fixture.nativeElement.querySelector('section.bg-ink');
    const metadata = hero?.querySelector('.event-hero-meta');
    const mapCard = hero?.querySelector('.event-location-card');
    const time = metadata?.querySelector('.event-hero-time');

    expect(mapCard).not.toBeNull();
    expect(mapCard.querySelector('iframe')).not.toBeNull();
    expect(mapCard.querySelector('.event-location-address').textContent).toContain('482 rue Pierre-Georges Latécoère');
    expect(mapCard.textContent).not.toContain('Début');
    expect(mapCard.textContent).not.toContain('Fin');
    expect(metadata?.querySelector('img[src="icons/calendar-icon.svg"]')).not.toBeNull();
    expect(metadata?.querySelector('img[src="icons/clock-icon.svg"]')).not.toBeNull();
    expect(metadata?.textContent).toContain('Début');
    expect(metadata?.textContent).toContain('14:00');
    expect(metadata?.textContent).toContain('Fin');
    expect(metadata?.textContent).toContain('18:00');
    expect(time).not.toBeNull();
    expect(time?.textContent).toContain('·');
    expect(getComputedStyle(time).whiteSpace).toBe('nowrap');
    expect(metadata?.textContent).not.toContain('42160');
    expect(metadata?.textContent).not.toContain('Voir l’itinéraire');
  });

  it('only renders the start time when no closing time is provided', () => {
    fixture.componentInstance.vpdEvent.set({...event, hourCloseDoors: null});
    fixture.detectChanges();

    const time = fixture.nativeElement.querySelector('.event-hero-time');

    expect(time?.textContent).toContain('Début');
    expect(time?.textContent).toContain('14:00');
    expect(time?.textContent).not.toContain('Fin');
    expect(time?.textContent).not.toContain('À confirmer');
  });
});
