import { isPlatformBrowser } from '@angular/common';
import { Directive, ElementRef, HostListener, inject, OnInit, PLATFORM_ID, Renderer2 } from '@angular/core';

@Directive({
  selector: 'img[appImageFadeIn]',
  standalone: false,
})
export class ImageFadeInDirective implements OnInit {
  private readonly element = inject(ElementRef<HTMLImageElement>);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly renderer = inject(Renderer2);

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    this.renderer.setAttribute(this.element.nativeElement, 'data-image-state', 'pending');
    if (this.element.nativeElement.complete) this.reveal();
  }

  @HostListener('load')
  onLoad(): void {
    this.reveal();
  }

  @HostListener('error')
  onError(): void {
    this.reveal();
  }

  private reveal(): void {
    this.renderer.setAttribute(this.element.nativeElement, 'data-image-state', 'visible');
  }
}
