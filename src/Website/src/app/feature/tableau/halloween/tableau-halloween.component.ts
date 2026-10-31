import {Component, Input} from '@angular/core';
import {RouterLink} from '@angular/router';
import {LotoTableauView} from '@vpd/ui';
import {LiveTableauConnection} from '../../../shared/clients/sse-client.service';
import {VpdEventModel} from '../../../shared/models/vpdEvent.model';

@Component({
  selector: 'app-tableau-halloween',
  standalone: true,
  templateUrl: './tableau-halloween.component.html',
  styleUrl: './tableau-halloween.component.scss',
  imports: [RouterLink],
})
export class TableauHalloweenComponent {
  @Input({required: true}) view!: LotoTableauView;
  @Input({required: true}) event!: VpdEventModel | null;
  @Input({required: true}) connection!: LiveTableauConnection;

  protected readonly tombTilts = [-2, 1.5, -1, 2, -1.5];
}
