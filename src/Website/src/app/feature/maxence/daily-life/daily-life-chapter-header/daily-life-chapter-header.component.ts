import { Component, input, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-daily-life-chapter-header',
  templateUrl: './daily-life-chapter-header.component.html',
  styleUrl: './daily-life-chapter-header.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false
})
export class DailyLifeChapterHeaderComponent {
  chapter = input.required<string>();
  title = input.required<string>();
  intro = input<string>('');
}
