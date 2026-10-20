import {ComponentFixture, TestBed} from '@angular/core/testing';
import {NO_ERRORS_SCHEMA} from '@angular/core';
import {ActivatedRoute} from '@angular/router';
import {of} from 'rxjs';

import {EventDetailComponent} from './event-detail.component';
import {VpdEventsFacadeService} from '../../shared/facades/vpd-events.facade.service';
import {VpdEventEnum} from '../../shared/enums/vpdEvent.enum';
import {MyDate} from '../../shared/extensions/MyDate';
import {VpdEventModel} from '../../shared/models/vpdEvent.model';

describe('EventDetailComponent', () => {
  let fixture: ComponentFixture<EventDetailComponent>;

  const event: VpdEventModel = {
    id: 'event-1',
    eventType: VpdEventEnum.Bingo,
    name: 'Loto de test',
    description: '',
    dateStart: new MyDate('2026-10-20T15:00:00.000Z'),
    dateEnd: null,
    hourOpenDoors: null,
    hourCloseDoors: null,
    urlImageMap: null,
    urlRegistration: null,
    urlImage: '',
    city: '',
    road: '',
    cityCode: '',
    roadNumber: '',
    parties: [],
    currentPartieIndex: 0,
    bingoHasBeenWon: false,
    bingoNumeros: [],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [EventDetailComponent],
      providers: [
        {provide: ActivatedRoute, useValue: {paramMap: of({get: () => null})}},
        {provide: VpdEventsFacadeService, useValue: {}},
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(EventDetailComponent);
    fixture.nativeElement.style.width = '2378px';
    fixture.componentInstance.vpdEvent.set(event);
    fixture.detectChanges();
  });

  it('keeps the capped shell gutters stable when the parent is ultra-wide', () => {
    const shell = fixture.nativeElement.querySelector('.max-w-shell') as HTMLElement;

    expect(shell).toBeTruthy();
    const styles = getComputedStyle(shell);
    const gutter = window.innerWidth >= 1024 ? 44 : 24;
    expect(styles.paddingLeft).toBe(`${gutter}px`);
    expect(shell.clientWidth - parseFloat(styles.paddingLeft) - parseFloat(styles.paddingRight)).toBe(1280 - 2 * gutter);
  });
});
