import {Directive, EventEmitter, HostBinding, HostListener, Input, OnChanges, Output} from '@angular/core';

import {catalogCoverUrl, catalogOriginalCoverUrl} from './catalog-cover-url';

@Directive({
  selector: 'img[catalogCover]',
  standalone: false,
})
export class CatalogCoverDirective implements OnChanges {
  @Input() catalogCover: string | null = null;
  @Output() catalogCoverFailed = new EventEmitter<void>();

  @HostBinding('attr.src') src: string | null = null;
  @HostBinding('attr.hidden') hidden: '' | null = null;

  private fallbackApplied = false;

  ngOnChanges(): void {
    this.fallbackApplied = false;
    this.hidden = null;
    this.src = catalogCoverUrl(this.catalogCover);
  }

  @HostListener('error')
  onImageError(): void {
    const originalUrl = catalogOriginalCoverUrl(this.catalogCover);

    if (!this.fallbackApplied && originalUrl && this.src !== originalUrl) {
      this.fallbackApplied = true;
      this.src = originalUrl;
      return;
    }

    this.hidden = '';
    this.catalogCoverFailed.emit();
  }
}
