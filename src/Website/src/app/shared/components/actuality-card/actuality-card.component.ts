import {isPlatformBrowser} from '@angular/common';
import {AfterViewInit, Component, ElementRef, inject, input, PLATFORM_ID, Renderer2, ViewChild} from '@angular/core';
import {ActualityModel} from "../../models/actuality.model";

@Component({
  selector: 'app-actuality-card',
  templateUrl: './actuality-card.component.html',
  styleUrl: './actuality-card.component.scss',
  standalone: false
})
export class ActualityCardComponent implements AfterViewInit{
  ActualityModel = input.required<ActualityModel>()

  private readonly platformId = inject(PLATFORM_ID);
  private readonly renderer = inject(Renderer2);

  @ViewChild('article') article!: ElementRef;

  ngAfterViewInit() {
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
}
