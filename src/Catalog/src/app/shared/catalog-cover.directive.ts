import {Directive, EventEmitter, HostBinding, HostListener, Input, OnChanges, Output} from '@angular/core';

import {
  CatalogCoverSize,
  catalogCoverSrcset,
  catalogCoverUrl,
  catalogOriginalCoverUrl,
} from './catalog-cover-url';

@Directive({
  selector: 'img[catalogCover]',
  standalone: false,
})
export class CatalogCoverDirective implements OnChanges {
  @Input() catalogCover: string | null = null;
  /** Taille demandée pour `src` ; absente, la fiche livre garde 660x990. */
  @Input() catalogCoverSize: CatalogCoverSize | null = null;
  /** Tailles pour `srcset` (descripteurs `Nw`) ; absente, pas de `srcset`. */
  @Input() catalogCoverSrcset: readonly CatalogCoverSize[] | null = null;
  @Output() catalogCoverFailed = new EventEmitter<void>();

  @HostBinding('attr.src') src: string | null = null;
  @HostBinding('attr.srcset') srcset: string | null = null;
  @HostBinding('attr.hidden') hidden: '' | null = null;

  private fallbackApplied = false;

  ngOnChanges(): void {
    this.fallbackApplied = false;
    this.hidden = null;
    this.src = catalogCoverUrl(this.catalogCover, this.catalogCoverSize);
    this.srcset = catalogCoverSrcset(this.catalogCover, this.catalogCoverSrcset ?? []);
  }

  @HostListener('error')
  onImageError(): void {
    const originalUrl = catalogOriginalCoverUrl(this.catalogCover);

    if (!this.fallbackApplied && originalUrl && this.src !== originalUrl) {
      this.fallbackApplied = true;
      // Un srcset prime sur src : il faut le retirer pour que la source d'origine soit utilisée.
      this.srcset = null;
      this.src = originalUrl;
      return;
    }

    this.hidden = '';
    this.catalogCoverFailed.emit();
  }
}
