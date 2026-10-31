import {CommonModule} from '@angular/common';
import {Component, Input} from '@angular/core';
import {RouterLink} from '@angular/router';
import {countdownLabel} from '../../../../shared/events/countdown';
import {VpdEventEnum} from '../../../../shared/enums/vpdEvent.enum';
import {VpdEventModel} from '../../../../shared/models/vpdEvent.model';

@Component({
  selector: 'app-next-events',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './next-events.component.html',
  styleUrl: './next-events.component.scss',
})
export class NextEventsComponent {
  @Input() events: VpdEventModel[] = [];

  protected countdown(event: VpdEventModel): string | null {
    return countdownLabel(event, new Date(), Number.POSITIVE_INFINITY);
  }

  protected typeLabel(event: VpdEventModel): string {
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
