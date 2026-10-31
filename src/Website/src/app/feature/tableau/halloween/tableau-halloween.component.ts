import {Component, inject, Input} from '@angular/core';
import {RouterLink} from '@angular/router';
import {LotoTableauView} from '@vpd/ui';
import {LiveTableauConnection} from '../../../shared/clients/sse-client.service';
import {VpdEventModel} from '../../../shared/models/vpdEvent.model';
import {HalloweenPreferences} from './halloween-preferences';
import {VpdChaudronComponent} from './characters/vpd-chaudron.component';
import {VpdCitrouilleComponent} from './characters/vpd-citrouille.component';
import {VpdFantomeComponent} from './characters/vpd-fantome.component';
import {VpdChauveSourisComponent} from './characters/vpd-chauve-souris.component';

@Component({
  selector: 'app-tableau-halloween',
  standalone: true,
  templateUrl: './tableau-halloween.component.html',
  styleUrl: './tableau-halloween.component.scss',
  imports: [RouterLink, VpdCitrouilleComponent, VpdChauveSourisComponent, VpdFantomeComponent, VpdChaudronComponent],
})
export class TableauHalloweenComponent {
  protected readonly preferences = inject(HalloweenPreferences);
  @Input({required: true}) view!: LotoTableauView;
  @Input({required: true}) event!: VpdEventModel | null;
  @Input({required: true}) connection!: LiveTableauConnection;

  protected readonly tombTilts = [-2, 1.5, -1, 2, -1.5];
}
