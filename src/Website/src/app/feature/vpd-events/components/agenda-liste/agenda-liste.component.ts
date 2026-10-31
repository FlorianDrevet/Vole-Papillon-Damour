import {CommonModule} from '@angular/common';
import {Component, inject, Input} from '@angular/core';
import {RouterLink} from '@angular/router';
import {countdownLabel} from '../../../../shared/events/countdown';
import {eventIdentity, EventIdentity} from '../../../../shared/events/event-identity';
import {groupByStartMonth, AgendaMonth} from '../../../../shared/events/agenda';
import {VpdEventEnum} from '../../../../shared/enums/vpdEvent.enum';
import {VpdEventModel} from '../../../../shared/models/vpdEvent.model';
import {HalloweenPreferences} from '../../../../shared/components/mascots/halloween-preferences';
import {VpdBouleBingoComponent} from '../../../../shared/components/mascots/boule-bingo.component';
import {VpdChauveSourisComponent} from '../../../../shared/components/mascots/vpd-chauve-souris.component';
import {VpdCitrouilleComponent} from '../../../../shared/components/mascots/vpd-citrouille.component';
import {VpdPapillonComponent} from '../../../../shared/components/mascots/papillon.component';

@Component({
  selector: 'app-agenda-liste',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    VpdBouleBingoComponent,
    VpdChauveSourisComponent,
    VpdCitrouilleComponent,
    VpdPapillonComponent,
  ],
  templateUrl: './agenda-liste.component.html',
  styleUrl: './agenda-liste.component.scss',
})
export class AgendaListComponent {
  private readonly preferences = inject(HalloweenPreferences);

  @Input() events: VpdEventModel[] = [];
  @Input() showMonthHeader = true;
  @Input() selectedEventId: string | null = null;

  protected readonly calm = this.preferences.calm;
  protected readonly visible = this.preferences.visible;

  protected get months(): AgendaMonth[] {
    return groupByStartMonth(this.events);
  }

  protected identity(event: VpdEventModel): EventIdentity {
    return eventIdentity(event);
  }

  protected countdown(event: VpdEventModel): string | null {
    return countdownLabel(event, new Date());
  }

  protected typeLabel(event: VpdEventModel): string {
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

  protected meta(event: VpdEventModel): string {
    const parts = [event.city];
    if (event.hourOpenDoors) {
      const time = new Intl.DateTimeFormat('fr-FR', {
        timeZone: 'Europe/Paris',
        hour: '2-digit',
        minute: '2-digit',
      }).format(event.hourOpenDoors).replace(':', ' h ');
      parts.push(`ouverture à ${time}`);
    }
    return parts.filter(Boolean).join(' · ');
  }
}
