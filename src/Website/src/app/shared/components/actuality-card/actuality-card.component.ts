import {AfterViewInit, Component, ElementRef, inject, input, Renderer2, ViewChild} from '@angular/core';
import {ActualityModel} from "../../models/actuality.model";

@Component({
    selector: 'app-actuality-card',
    templateUrl: './actuality-card.component.html',
    styleUrl: './actuality-card.component.scss',
    standalone: false
})
export class ActualityCardComponent implements AfterViewInit{
  ActualityModel = input.required<ActualityModel>()

  private readonly renderer = inject(Renderer2);

  @ViewChild('article') article!: ElementRef;

  ngAfterViewInit() {
    const divElement = this.article.nativeElement;
    const divHeight = divElement.offsetHeight;
    const fontSize = parseFloat(window.getComputedStyle(divElement).fontSize);
    const lineHeight = parseFloat(window.getComputedStyle(divElement).lineHeight);

    const actualLineHeight = isNaN(lineHeight) ? fontSize * 1.2 : lineHeight;

    const numberOfLines = Math.floor(divHeight / actualLineHeight);

    this.renderer.setStyle(divElement, '-webkit-line-clamp', numberOfLines.toString());
  }
}
