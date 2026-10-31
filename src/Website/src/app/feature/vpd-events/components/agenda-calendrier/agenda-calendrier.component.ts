import {CommonModule} from '@angular/common';
import {Component, EventEmitter, inject, Input, OnChanges, Output, SimpleChanges} from '@angular/core';
import {AgendaMonth, CalendarDayCell, CalendarEventOccurrence, agendaMonths, calendarMonth} from '../../../../shared/events/agenda';
import {eventIdentity, EventIdentity} from '../../../../shared/events/event-identity';
import {VpdEventEnum} from '../../../../shared/enums/vpdEvent.enum';
import {VpdEventModel} from '../../../../shared/models/vpdEvent.model';
import {HalloweenPreferences} from '../../../../shared/components/mascots/halloween-preferences';
import {VpdBouleBingoComponent} from '../../../../shared/components/mascots/boule-bingo.component';
import {VpdCitrouilleComponent} from '../../../../shared/components/mascots/vpd-citrouille.component';
import {VpdPapillonComponent} from '../../../../shared/components/mascots/papillon.component';
import {AgendaListComponent} from '../agenda-liste/agenda-liste.component';

type AgendaMonthView = AgendaMonth & {cells: CalendarDayCell[]};

@Component({
  selector: 'app-agenda-calendrier',
  standalone: true,
  imports: [
    CommonModule,
    VpdBouleBingoComponent,
    VpdCitrouilleComponent,
    VpdPapillonComponent,
    AgendaListComponent,
  ],
  templateUrl: './agenda-calendrier.component.html',
  styleUrl: './agenda-calendrier.component.scss',
})
export class AgendaCalendarComponent implements OnChanges {
  private readonly preferences = inject(HalloweenPreferences);

  @Input() events: VpdEventModel[] = [];
  @Input() selectedEventId: string | null = null;
  @Input() selectedDate: string | null = null;
  @Output() eventSelected = new EventEmitter<{eventId: string; date: string}>();

  protected readonly calm = this.preferences.calm;
  protected readonly visible = this.preferences.visible;
  protected readonly phone = this.preferences.phone;

  private anchorMonth: {year: number; month: number} | null = null;
  private availableMonths: AgendaMonth[] = [];
  private visibleMonths: AgendaMonthView[] = [];
  private eventDayDelays = new Map<string, string>();

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['events']) {
      this.availableMonths = agendaMonths(this.events);
      const firstMonth = this.availableMonths[0];
      this.anchorMonth = firstMonth ? {year: firstMonth.year, month: firstMonth.month} : null;
      this.updateVisibleMonths();
    }
  }

  protected get months(): AgendaMonthView[] {
    return this.visibleMonths;
  }

  protected get canGoPrevious(): boolean {
    const available = this.availableMonths;
    return available.length > 0 && this.anchorIndex(available) > 0;
  }

  protected get canGoNext(): boolean {
    const available = this.availableMonths;
    if (available.length === 0) {
      return false;
    }
    return this.anchorIndex(available) < Math.max(0, available.length - (this.phone() ? 1 : 3));
  }

  protected get selectedEvent(): VpdEventModel | null {
    return this.events.find(event => event.id === this.selectedEventId) ?? this.events[0] ?? null;
  }

  protected get selectedEvents(): VpdEventModel[] {
    return this.selectedEvent ? [this.selectedEvent] : [];
  }

  protected get currentMonthEvents(): VpdEventModel[] {
    const month = this.months[0];
    if (!month) {
      return [];
    }
    const unique = new Map<string, VpdEventModel>();
    for (const cell of month.cells) {
      for (const occurrence of cell.events) {
        unique.set(occurrence.event.id, occurrence.event);
      }
    }
    return [...unique.values()].sort((a, b) => a.dateStart.getTime() - b.dateStart.getTime());
  }

  protected previousMonths(): void {
    this.moveMonths(-1);
  }

  protected nextMonths(): void {
    this.moveMonths(1);
  }

  protected identity(event: VpdEventModel): EventIdentity {
    return eventIdentity(event);
  }

  protected monthStep(): number {
    return this.phone() ? 1 : 3;
  }

  protected lightDelay(date: string): string {
    return this.eventDayDelays.get(date) ?? '0s';
  }

  protected eventTypeLabel(event: VpdEventModel): string {
    if (this.identity(event) === 'halloween') {
      return 'Loto d’Halloween';
    }
    switch (event.eventType) {
      case VpdEventEnum.Bingo:
        return 'Loto';
      case VpdEventEnum.Books:
        return 'Bourse aux livres';
      default:
        return 'Rendez-vous';
    }
  }

  protected eventDayNumber(day: number | null): string {
    return String(day ?? '').padStart(2, '0');
  }

  protected dayAriaLabel(date: string, event: VpdEventModel): string {
    const label = new Intl.DateTimeFormat('fr-FR', {
      timeZone: 'Europe/Paris',
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    }).format(new Date(`${date}T12:00:00Z`));
    const place = event.city ? `, ${event.city}` : '';
    const opening = event.hourOpenDoors
      ? `, ouverture à ${new Intl.DateTimeFormat('fr-FR', {timeZone: 'UTC', hour: '2-digit', minute: '2-digit'}).format(new Date(event.hourOpenDoors))}`
      : '';
    return `${label} : ${this.eventTypeLabel(event)}, ${event.name}${place}${opening}`;
  }

  protected selectEvent(occurrence: CalendarEventOccurrence, date: string): void {
    this.eventSelected.emit({eventId: occurrence.event.id, date});
  }

  private moveMonths(direction: -1 | 1): void {
    const available = this.availableMonths;
    if (available.length === 0) {
      return;
    }
    const current = this.anchorIndex(available);
    const maxStart = Math.max(0, available.length - (this.phone() ? 1 : 3));
    const nextIndex = Math.min(maxStart, Math.max(0, current + direction * this.monthStep()));
    const target = available[nextIndex];
    this.anchorMonth = {year: target.year, month: target.month};
    this.updateVisibleMonths();
  }

  private updateVisibleMonths(): void {
    if (this.availableMonths.length === 0) {
      this.visibleMonths = [];
      this.eventDayDelays = new Map();
      return;
    }

    const anchor = this.anchorMonth ?? {
      year: this.availableMonths[0].year,
      month: this.availableMonths[0].month,
    };
    const anchorKey = this.key(anchor.year, anchor.month);
    const start = Math.max(0, this.availableMonths.findIndex(month => month.key === anchorKey));
    this.visibleMonths = this.availableMonths.slice(start, start + 3).map(month => ({
      ...month,
      cells: calendarMonth(month.year, month.month, this.events),
    }));

    const eventDays = [...new Set(this.visibleMonths
      .flatMap(month => month.cells)
      .filter(cell => cell.date !== null && cell.events.length > 0)
      .map(cell => cell.date!))];
    this.eventDayDelays = new Map(eventDays.map((date, index) => [date, `${index * 0.12}s`]));
  }

  private anchorIndex(available: AgendaMonth[]): number {
    if (!this.anchorMonth) {
      return 0;
    }
    const index = available.findIndex(month => month.key === this.key(this.anchorMonth!.year, this.anchorMonth!.month));
    return index < 0 ? 0 : index;
  }

  private key(year: number, month: number): string {
    return `${year}-${String(month + 1).padStart(2, '0')}`;
  }
}
