import {Component, computed, inject, OnInit, PendingTasks, PLATFORM_ID, signal} from '@angular/core';
import {isPlatformBrowser} from '@angular/common';
import {VpdEventsFacadeService} from '../../shared/facades/vpd-events.facade.service';
import {VpdEventModel} from '../../shared/models/vpdEvent.model';
import {VpdEventEnum} from '../../shared/enums/vpdEvent.enum';
import {parisDay} from '../../shared/events/countdown';
import {EVENT_EDITORIAL_PHOTOS} from '../../shared/data/event-editorial-content';
import {trackSsrDataRequest} from '../../shared/events/ssr-pending-task';
import {AgendaFilter} from './components/agenda-filters/agenda-filters.component';
import {AgendaView} from './components/agenda-view-toggle/agenda-view-toggle.component';

const AGENDA_VIEW_STORAGE_KEY = 'vpd.agenda.vue';

@Component({
  selector: 'app-vpd-events',
  templateUrl: './vpd-events-page.component.html',
  standalone: false,
})
export class VpdEventsPageComponent implements OnInit {
  private readonly eventFacade = inject(VpdEventsFacadeService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly pendingTasks = inject(PendingTasks);

  /** Photos et points clés des deux rendez-vous permanents, indépendants des dates de l'API. */
  protected readonly lotoPhotos = EVENT_EDITORIAL_PHOTOS[VpdEventEnum.Bingo];
  protected readonly lotoHighlights = [
    'Des dizaines de lots offerts par nos partenaires.',
    'Une après-midi en famille, ouverte à tous.',
    'Les bénéfices servent aux besoins de Maxence et au soutien d’autres jeunes.',
  ];
  protected readonly booksPhotos = EVENT_EDITORIAL_PHOTOS[VpdEventEnum.Books];
  protected readonly booksHighlights = [
    'Des milliers d\'ouvrages triés par catégories : romans, jeunesse, BD, régionalisme.',
    'Petits prix, du livre de poche au beau livre.',
    'Vos livres sont acceptés toute l\'année au dépôt de Saint-Just-Saint-Rambert.',
  ];

  private readonly events = signal<VpdEventModel[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly upcomingEvents = computed(() => {
    const today = parisDay(new Date());
    return this.events()
      .filter(event => {
        const start = parisDay(event.dateStart);
        if (start >= today) {
          return true;
        }
        return event.eventType === VpdEventEnum.Books
          && event.dateEnd !== null
          && parisDay(event.dateEnd) >= today;
      })
      .sort((a, b) => a.dateStart.getTime() - b.dateStart.getTime());
  });
  protected readonly featuredEvent = computed<VpdEventModel | null>(() => this.upcomingEvents()[0] ?? null);
  protected readonly nextEvents = computed(() => this.upcomingEvents().slice(1, 4));
  protected readonly agendaFilter = signal<AgendaFilter>('all');
  protected readonly agendaView = signal<AgendaView>('calendar');
  protected readonly selectedEventId = signal<string | null>(null);
  protected readonly selectedDate = signal<string | null>(null);
  protected readonly filteredEvents = computed(() => this.upcomingEvents().filter(event => {
    switch (this.agendaFilter()) {
      case 'bingo':
        return event.eventType === VpdEventEnum.Bingo;
      case 'books':
        return event.eventType === VpdEventEnum.Books;
      case 'other':
        return event.eventType !== VpdEventEnum.Bingo && event.eventType !== VpdEventEnum.Books;
      default:
        return true;
    }
  }));
  protected readonly selectedEvent = computed(() => this.filteredEvents().find(event => event.id === this.selectedEventId())
    ?? this.filteredEvents()[0]
    ?? null);
  protected readonly selectedCalendarDate = computed(() => {
    const event = this.selectedEvent();
    if (!event) {
      return null;
    }
    const date = this.selectedDate();
    const start = parisDay(event.dateStart);
    const end = parisDay(event.dateEnd ?? event.dateStart);
    return date && date >= start && date <= end ? date : start;
  });

  ngOnInit(): void {
    this.restoreAgendaView();
    const finishSsrTask = trackSsrDataRequest(this.pendingTasks, this.platformId);
    this.eventFacade.getAllEvents$()
      .then(events => {
        this.events.set((events ?? []).map(event => this.toVpdEvent(event)));
      })
      .catch(() => undefined)
      .finally(() => {
        this.isLoading.set(false);
        finishSsrTask();
      });
  }

  protected setAgendaView(view: AgendaView): void {
    this.agendaView.set(view);
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    try {
      localStorage.setItem(AGENDA_VIEW_STORAGE_KEY, view === 'list' ? 'liste' : 'calendrier');
    } catch {
      // The current view remains usable if browser storage is unavailable.
    }
  }

  protected selectCalendarEvent(selection: {eventId: string; date: string}): void {
    this.selectedEventId.set(selection.eventId);
    this.selectedDate.set(selection.date);
  }

  private restoreAgendaView(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    try {
      this.agendaView.set(localStorage.getItem(AGENDA_VIEW_STORAGE_KEY) === 'liste' ? 'list' : 'calendar');
    } catch {
      // Calendar is the default when browser storage is unavailable.
    }
  }

  /** L'API renvoie des dates et un type d'évènement sérialisés en chaînes. */
  private toVpdEvent(event: VpdEventModel): VpdEventModel {
    const serialized = event as unknown as Record<string, unknown>;
    return {
      ...event,
      dateStart: new Date(serialized['dateStart'] as string),
      dateEnd: serialized['dateEnd'] ? new Date(serialized['dateEnd'] as string) : null,
      hourOpenDoors: serialized['hourOpenDoors'] ? new Date(serialized['hourOpenDoors'] as string) : null,
      hourCloseDoors: serialized['hourCloseDoors'] ? new Date(serialized['hourCloseDoors'] as string) : null,
      eventType: VpdEventEnum[event.eventType as unknown as keyof typeof VpdEventEnum],
    };
  }
}
