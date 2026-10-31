import {CommonModule} from '@angular/common';
import {Component, EventEmitter, Input, Output} from '@angular/core';
import {VpdEventEnum} from '../../../../shared/enums/vpdEvent.enum';
import {VpdEventModel} from '../../../../shared/models/vpdEvent.model';

export type AgendaFilter = 'all' | 'bingo' | 'books' | 'other';

interface AgendaFilterOption {
  value: AgendaFilter;
  label: string;
}

@Component({
  selector: 'app-agenda-filters',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './agenda-filters.component.html',
  styleUrl: './agenda-filters.component.scss',
})
export class AgendaFiltersComponent {
  @Input() events: VpdEventModel[] = [];
  @Input() active: AgendaFilter = 'all';
  @Output() activeChange = new EventEmitter<AgendaFilter>();

  protected get filters(): AgendaFilterOption[] {
    if (this.events.length === 0) {
      return [];
    }

    const options: AgendaFilterOption[] = [{value: 'all', label: 'Tout'}];
    if (this.events.some(event => event.eventType === VpdEventEnum.Bingo)) {
      options.push({value: 'bingo', label: 'Lotos'});
    }
    if (this.events.some(event => event.eventType === VpdEventEnum.Books)) {
      options.push({value: 'books', label: 'Bourses aux livres'});
    }
    if (this.events.some(event => event.eventType !== VpdEventEnum.Bingo && event.eventType !== VpdEventEnum.Books)) {
      options.push({value: 'other', label: 'Autres'});
    }
    return options;
  }
}
