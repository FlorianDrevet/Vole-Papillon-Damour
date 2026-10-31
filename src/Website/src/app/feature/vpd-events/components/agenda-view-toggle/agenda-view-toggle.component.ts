import {CommonModule} from '@angular/common';
import {Component, EventEmitter, Input, Output} from '@angular/core';

export type AgendaView = 'calendar' | 'list';

@Component({
  selector: 'app-agenda-view-toggle',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './agenda-view-toggle.component.html',
  styleUrl: './agenda-view-toggle.component.scss',
})
export class AgendaViewToggleComponent {
  @Input() active: AgendaView = 'calendar';
  @Output() activeChange = new EventEmitter<AgendaView>();
}
