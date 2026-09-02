import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VpdEventEnum } from '../../../../shared/enums/vpdEvent.enum';
import { VpdEventModel } from '../../../../shared/models/vpdEvent.model';
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

  it('renders the event map and address in the rendez-vous card', () => {
    const card = fixture.nativeElement.querySelector('.event-location-card');
    const map = card?.querySelector('iframe');
    const address = card?.querySelector('.event-location-address');

    expect(card).not.toBeNull();
    expect(map).not.toBeNull();
    expect(map?.getAttribute('src') ?? '').toContain('output=embed');
    expect(address?.textContent ?? '').toContain('482 rue Pierre-Georges Latécoère');
    expect(address?.textContent ?? '').toContain('42160 Andrézieux-Bouthéon');
  });
});
