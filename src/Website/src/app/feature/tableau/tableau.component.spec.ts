import {signal, WritableSignal} from '@angular/core';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {ActivatedRoute, convertToParamMap} from '@angular/router';
import {of} from 'rxjs';
import {VpdEventModel} from '../../shared/models/vpdEvent.model';
import {HalloweenPreferences} from './halloween/halloween-preferences';
import {SseClientService} from '../../shared/clients/sse-client.service';
import {TableauComponent} from './tableau.component';

describe('TableauComponent theme selection', () => {
  let fixture: ComponentFixture<TableauComponent>;
  let eventAsso: WritableSignal<VpdEventModel | null>;
  let phone: WritableSignal<boolean>;

  beforeEach(async () => {
    eventAsso = signal<VpdEventModel | null>(null);
    phone = signal(false);

    await TestBed.configureTestingModule({
      declarations: [TableauComponent],
      providers: [{
        provide: ActivatedRoute,
        useValue: {paramMap: of(convertToParamMap({}))},
      }],
    }).overrideComponent(TableauComponent, {
      set: {
        template: '',
        providers: [
          {provide: SseClientService, useValue: {eventAsso, connection: signal('live'), init: jasmine.createSpy('init')}},
          {provide: HalloweenPreferences, useValue: {phone}},
        ],
      },
    }).compileComponents();

    fixture = TestBed.createComponent(TableauComponent);
    fixture.detectChanges();
  });

  it('enables the theme for a Halloween event on a phone', () => {
    eventAsso.set({liveTheme: 'Halloween'} as unknown as VpdEventModel);
    phone.set(true);

    expect((fixture.componentInstance as any).halloween()).toBeTrue();
  });

  it('keeps the classic theme above phone width', () => {
    eventAsso.set({liveTheme: 'Halloween'} as unknown as VpdEventModel);
    phone.set(false);

    expect((fixture.componentInstance as any).halloween()).toBeFalse();
  });

  it('keeps the classic theme when an event has no Halloween setting', () => {
    eventAsso.set({} as unknown as VpdEventModel);
    phone.set(true);

    expect((fixture.componentInstance as any).halloween()).toBeFalse();
  });
});
