import {ChangeDetectionStrategy, Component, EventEmitter, Input, Output} from '@angular/core';

@Component({
  selector: 'app-minimum-hearts-control',
  standalone: false,
  templateUrl: './minimum-hearts-control.component.html',
  styleUrls: ['./minimum-hearts-control.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MinimumHeartsControlComponent {
  @Input() minimum = 3;
  @Output() readonly minimumChange = new EventEmitter<number>();

  readonly levels = [1, 2, 3, 4];

  select(level: number): void {
    this.minimumChange.emit(level);
  }
}
