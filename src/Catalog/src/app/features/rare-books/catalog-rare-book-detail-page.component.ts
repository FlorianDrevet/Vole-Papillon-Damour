import {DOCUMENT} from '@angular/common';
import {ChangeDetectionStrategy, ChangeDetectorRef, Component, Inject, OnDestroy, OnInit} from '@angular/core';
import {Meta, Title} from '@angular/platform-browser';
import {ActivatedRoute} from '@angular/router';
import {Subject, catchError, of, switchMap, takeUntil} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogRareBook} from '../../core/catalog.models';

const CATALOG_ORIGIN = 'https://livres.volepapillondamour.fr';

@Component({
  selector: 'app-catalog-rare-book-detail-page',
  standalone: false,
  templateUrl: './catalog-rare-book-detail-page.component.html',
  styleUrls: ['./catalog-rare-book-detail-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogRareBookDetailPageComponent implements OnInit, OnDestroy {
  book: CatalogRareBook | null = null;
  relatedBooks: CatalogRareBook[] = [];
  selectedPhoto = 0;
  galleryOpen = false;
  loading = true;
  notFound = false;

  private readonly destroyed = new Subject<void>();
  private canonicalElement: HTMLLinkElement | null = null;
  private structuredDataElement: HTMLScriptElement | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly api: CatalogApiService,
    private readonly title: Title,
    private readonly meta: Meta,
    private readonly changeDetector: ChangeDetectorRef,
    @Inject(DOCUMENT) private readonly document: Document,
  ) {}

  ngOnInit(): void {
    this.route.paramMap
      .pipe(
        switchMap(params => {
          this.loading = true;
          this.notFound = false;
          this.book = null;
          this.relatedBooks = [];
          this.selectedPhoto = 0;
          return this.api.getPublicRareBook(params.get('id') ?? '')
            .pipe(catchError(() => of(null)));
        }),
        takeUntil(this.destroyed),
      )
      .subscribe(detail => {
        this.loading = false;
        this.notFound = detail === null;
        if (detail) {
          this.book = detail.rareBook;
          this.relatedBooks = detail.relatedBooks;
          this.setSeo(detail.rareBook);
        }
        this.changeDetector.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
    this.canonicalElement?.remove();
    this.structuredDataElement?.remove();
  }

  photoUrl(book: CatalogRareBook, index = 0): string | null {
    return book.photos[index]?.blobUri ?? null;
  }

  conditionLabel(book: CatalogRareBook): string {
    return ({
      AsNew: 'Comme neuf',
      GoodWithFlaws: 'Bon état avec défauts',
      Worn: 'Usé',
      Damaged: 'Abîmé',
    } as Record<string, string>)[book.condition] ?? book.condition;
  }

  availabilityLabel(book: CatalogRareBook): string {
    return book.isSold ? 'Exemplaire déjà parti' : 'Disponible à la prochaine bourse';
  }

  selectPhoto(index: number): void {
    this.selectedPhoto = index;
  }

  openGallery(): void {
    this.galleryOpen = true;
  }

  closeGallery(): void {
    this.galleryOpen = false;
  }

  trackBook(_index: number, book: CatalogRareBook): string {
    return book.id;
  }

  private setSeo(book: CatalogRareBook): void {
    const description = book.publicDescription
      ?? `${book.title}${book.authorMention ? ` — ${book.authorMention}` : ''}. Livre rare proposé par Vole Papillon d’Amour.`;
    this.title.setTitle(`${book.title} — Livre rare | Vole Papillon d’Amour`);
    this.meta.updateTag({name: 'description', content: description.slice(0, 160)});
    this.meta.updateTag({property: 'og:title', content: `${book.title} — Livre rare`});
    this.meta.updateTag({property: 'og:description', content: description.slice(0, 160)});
    this.meta.updateTag({property: 'og:type', content: 'product'});

    const url = `${CATALOG_ORIGIN}/livres-rares/${encodeURIComponent(book.id)}`;
    this.canonicalElement?.remove();
    this.canonicalElement = this.document.createElement('link');
    this.canonicalElement.rel = 'canonical';
    this.canonicalElement.href = url;
    this.document.head.appendChild(this.canonicalElement);

    this.structuredDataElement?.remove();
    this.structuredDataElement = this.document.createElement('script');
    this.structuredDataElement.type = 'application/ld+json';
    this.structuredDataElement.text = JSON.stringify({
      '@context': 'https://schema.org',
      '@type': 'Book',
      name: book.title,
      author: book.authorMention ? {name: book.authorMention} : undefined,
      isbn: book.isbn13 ?? undefined,
      image: this.photoUrl(book) ?? undefined,
      offers: {
        '@type': 'Offer',
        price: book.price.toFixed(2),
        priceCurrency: 'EUR',
        availability: book.isSold
          ? 'https://schema.org/SoldOut'
          : 'https://schema.org/InStoreOnly',
        url,
      },
    });
    this.document.head.appendChild(this.structuredDataElement);
  }

}
