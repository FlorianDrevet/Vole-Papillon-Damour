import { Component, input } from '@angular/core';
import { BackgroundColorEnum } from '../../../../../shared/enums/backgroundColor.enum';

@Component({
    selector: 'app-history-container',
    templateUrl: './history-container.component.html',
    styleUrl: './history-container.component.scss',
    standalone: false
})
export class HistoryContainerComponent {
  ContainerBackgroundContainer = input.required<BackgroundColorEnum>();
  Year = input.required<number>();
  Title = input.required<string>();
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
