import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VpdEventEnum } from '../../../../shared/enums/vpdEvent.enum';
import { VpdEventModel } from '../../../../shared/models/vpdEvent.model';
import { EVENT_EDITORIAL_PHOTOS } from '../../../../shared/data/event-editorial-content';
import { GeneralInfosComponent } from './general-infos.component';

describe('GeneralInfosComponent', () => {
  let fixture: ComponentFixture<GeneralInfosComponent>;

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
      declarations: [GeneralInfosComponent],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(GeneralInfosComponent);
    fixture.componentRef.setInput('vpdEvent', event);
    fixture.detectChanges();
  });

  it('renders an editorial event photo gallery beside the description', () => {
    const gallery = fixture.nativeElement.querySelector('.event-photo-gallery');
    const images = gallery?.querySelectorAll('img') ?? [];

    expect(gallery).not.toBeNull();
    expect(images.length).toBeGreaterThan(1);
    expect(images[0]?.getAttribute('src')).toBe(EVENT_EDITORIAL_PHOTOS[VpdEventEnum.Books][0]);
    expect(images[0]?.getAttribute('src')).not.toBe(event.urlImage);
  });

  it('does not render an empty gallery when no editorial photos are configured', () => {
    fixture.componentRef.setInput('vpdEvent', {...event, eventType: VpdEventEnum.Other});
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.event-photo-gallery')).toBeNull();
  });
});
