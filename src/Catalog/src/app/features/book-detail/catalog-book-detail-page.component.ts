import {DOCUMENT} from '@angular/common';
import {ChangeDetectionStrategy, Component, Inject, OnDestroy, OnInit} from '@angular/core';
import {Meta, Title} from '@angular/platform-browser';
import {ActivatedRoute} from '@angular/router';
import {Subject, catchError, of, switchMap, takeUntil} from 'rxjs';

import {environment} from '../../../environments/environment';
import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogBook} from '../../core/catalog.models';
import {publicBookPath} from '../../shared/catalog-url';

@Component({
  selector: 'app-catalog-book-detail-page',
  standalone: false,
  templateUrl: './catalog-book-detail-page.component.html',
  styleUrls: ['./catalog-book-detail-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogBookDetailPageComponent implements OnInit, OnDestroy {
  book: CatalogBook | null = null;
  loading = true;
  notFound = false;
  coverFailed = false;

  private readonly destroyed = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly api: CatalogApiService,
    private readonly title: Title,
    private readonly meta: Meta,
    @Inject(DOCUMENT) private readonly document: Document,
  ) {}

  ngOnInit(): void {
    this.route.paramMap
      .pipe(
        switchMap(params => {
          this.loading = true;
          this.notFound = false;
          this.book = null;
          this.coverFailed = false;
          return this.api.getBook(this.isbnFromSlug(params.get('slug') || ''))
            .pipe(catchError(() => of(null)));
        }),
        takeUntil(this.destroyed),
      )
      .subscribe(book => {
        this.book = book;
        this.notFound = book === null;
        this.loading = false;
        if (book) {
          this.setSeo(book);
        }
      });
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  bookPath(): string {
    return this.book ? publicBookPath(this.book) : '/';
  }

  formatDate(value: string): string {
    return new Intl.DateTimeFormat('fr-FR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      timeZone: 'Europe/Paris',
    }).format(new Date(value));
  }

  formatAvailabilityDate(value: string): string {
    return this.formatDate(value);
  }

  formatQuantity(quantity: number, singular: string, plural: string): string {
    return `${quantity} ${quantity === 1 ? singular : plural}`;
  }

  onCoverError(): void {
    this.coverFailed = true;
  }

  private isbnFromSlug(slug: string): string {
    const match = slug.match(/(\d{13})$/);
    return match?.[1] || slug;
  }

  private setSeo(book: CatalogBook): void {
    const bookTitle = book.title || 'Livre';
    const author = book.authors ? ` · ${book.authors}` : '';
    this.title.setTitle(`${bookTitle}${author} · Bourse aux livres`);
    this.meta.updateTag({
      name: 'description',
      content: `${bookTitle}${book.authors ? ` de ${book.authors}` : ''}. Disponibilité indicative à la bourse aux livres.`,
    });
    this.meta.updateTag({name: 'robots', content: 'index, follow'});

    const canonical = this.document.head.querySelector('link[rel="canonical"]') || this.document.createElement('link');
    canonical.setAttribute('rel', 'canonical');
    canonical.setAttribute('href', `${environment.publicUrl}${publicBookPath(book)}`);
    if (!canonical.parentNode) {
      this.document.head.appendChild(canonical);
    }

    const structuredData = this.document.head.querySelector('#catalog-book-jsonld') || this.document.createElement('script');
    structuredData.setAttribute('id', 'catalog-book-jsonld');
    structuredData.setAttribute('type', 'application/ld+json');
    structuredData.textContent = JSON.stringify({
      '@context': 'https://schema.org',
      '@type': 'Book',
      name: bookTitle,
      isbn: book.isbn13,
      ...(book.authors ? {author: { '@type': 'Person', name: book.authors }} : {}),
      ...(book.publisher ? {publisher: { '@type': 'Organization', name: book.publisher }} : {}),
      ...(book.publicationYear ? {datePublished: String(book.publicationYear)} : {}),
      ...(book.coverUrl ? {image: book.coverUrl} : {}),
      url: `${environment.publicUrl}${publicBookPath(book)}`,
    });
    if (!structuredData.parentNode) {
      this.document.head.appendChild(structuredData);
    }
  }
}
