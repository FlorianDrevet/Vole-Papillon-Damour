import { isPlatformBrowser } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ElementRef,
  inject,
  input,
  output,
  PLATFORM_ID,
  Renderer2,
  ViewChild,
} from '@angular/core';
import { DsActualityModel } from '../../models/ds-actuality.model';

/**
 * Carte d'actualité unifiée.
 *
 * - `editable=false` (défaut) : affichage seul (Website).
 * - `editable=true` : affiche les boutons edit/supprimer et émet
 *   `editRequested` / `deleteRequested`. Le parent ouvre les dialogues
 *   et appelle les facades.
 */
@Component({
  selector: 'vpd-actuality-card',
  templateUrl: './vpd-actuality-card.component.html',
  styleUrl: './vpd-actuality-card.component.scss',
  standalone: false,
})
export class VpdActualityCardComponent implements AfterViewInit {
  actuality = input.required<DsActualityModel>();
  editable = input<boolean>(false);

  editRequested = output<DsActualityModel>();
  deleteRequested = output<DsActualityModel>();

  private readonly platformId = inject(PLATFORM_ID);
  private readonly renderer = inject(Renderer2);

  @ViewChild('article') article!: ElementRef;

  ngAfterViewInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    const divElement = this.article.nativeElement;
    const divHeight = divElement.offsetHeight;
    const computedStyle = globalThis.getComputedStyle(divElement);
    const fontSize = Number.parseFloat(computedStyle.fontSize);
    const lineHeight = Number.parseFloat(computedStyle.lineHeight);

    const actualLineHeight = Number.isNaN(lineHeight) ? fontSize * 1.2 : lineHeight;
    const numberOfLines = Math.floor(divHeight / actualLineHeight);

    this.renderer.setStyle(divElement, '-webkit-line-clamp', numberOfLines.toString());
  }

  onEdit(): void {
    this.editRequested.emit(this.actuality());
  }

  onDelete(): void {
    this.deleteRequested.emit(this.actuality());
  }
}
