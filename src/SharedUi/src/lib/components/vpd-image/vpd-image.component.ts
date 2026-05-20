import { Component, input } from '@angular/core';
import { BackgroundColorEnum } from '../../enums/background-color.enum';
import { ImageOrientationEnum } from '../../enums/image-orientation.enum';
import { RotationEnum } from '../../enums/rotation.enum';

/**
 * Image illustrative VPD.
 * - `rounded`        : applique `rounded-3xl`
 * - `backgroundColor`: ajoute un padding et une couleur de fond
 * - `rotation`       : applique une rotation ±3deg
 * - `orientation`    : exposé pour évolution future (mise en page)
 * - `highPriorityFetching` : ajoute `fetchpriority="high"` (LCP, hero images)
 */
@Component({
  selector: 'vpd-image, app-vpd-image',
  templateUrl: './vpd-image.component.html',
  styleUrl: './vpd-image.component.scss',
  standalone: false,
})
export class VpdImageComponent {
  src = input.required<string>();
  rounded = input(false);
  backgroundColor = input<BackgroundColorEnum | null>(null);
  rotation = input<RotationEnum | null>(null);
  orientation = input<ImageOrientationEnum | null>(null);
  highPriorityFetching = input<boolean>(false);
  height = input<number | null>(null);
  width = input<number | null>(null);

  protected readonly BackgroundColorEnum = BackgroundColorEnum;
  protected readonly RotationEnum = RotationEnum;
}
