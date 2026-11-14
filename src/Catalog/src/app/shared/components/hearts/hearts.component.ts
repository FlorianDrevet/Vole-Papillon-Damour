import {ChangeDetectionStrategy, Component, Input} from '@angular/core';

@Component({
  selector: 'app-hearts',
  standalone: false,
  templateUrl: './hearts.component.html',
  styleUrls: ['./hearts.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeartsComponent {
  @Input() value = 3;

  readonly positions = [1, 2, 3, 4, 5];

  label(): string {
    const count = Math.max(0, Math.min(5, Math.round(this.value)));
    return `${count} ${count === 1 ? 'cœur' : 'cœurs'} sur 5`;
  }

  isFilled(position: number): boolean {
    return position <= Math.max(0, Math.min(5, Math.round(this.value)));
  }
}
