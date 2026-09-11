import {ChangeDetectionStrategy, Component, Input} from '@angular/core';

export type VpdLoaderVariant =
  | 'line'
  | 'flight'
  | 'squares'
  | 'skeleton'
  | 'ring'
  | 'traverse'
  | 'butterflies'
  | 'fill'
  | 'compact';

export type VpdLoaderSize = 'small' | 'medium' | 'large';
export type VpdLoaderLayout = 'grid' | 'list';
export type VpdLoaderSurface = 'light' | 'dark';

@Component({
  selector: 'vpd-loader',
  templateUrl: './vpd-loader.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: false,
})
export class VpdLoaderComponent {
  @Input() variant: VpdLoaderVariant = 'ring';
  @Input() label = 'Chargement';
  @Input() detail = '';
  @Input() title = '';
  @Input() logoSrc = 'images/papillon_without_back.png';
  @Input() count = 3;
  @Input() size: VpdLoaderSize = 'medium';
  @Input() layout: VpdLoaderLayout = 'grid';
  @Input() surface: VpdLoaderSurface = 'light';
  @Input() progress: number | null = null;

  readonly butterflies = [0, 1, 2];

  get skeletonItems(): number[] {
    const requestedCount = Number(this.count);
    const safeCount = Number.isFinite(requestedCount) ? Math.floor(requestedCount) : 1;
    return Array.from({length: Math.max(1, Math.min(safeCount, 12))}, (_, index) => index);
  }

  get progressPercent(): number {
    if (this.progress === null || !Number.isFinite(Number(this.progress))) {
      return 0;
    }

    return Math.max(0, Math.min(100, Number(this.progress)));
  }

  get progressClipPath(): string {
    return `inset(${100 - this.progressPercent}% 0 0 0)`;
  }

  get flightTitle(): string {
    return this.title || 'Tri des livres';
  }

  get flightDetail(): string {
    return this.detail || 'Préparation de la session';
  }
}
