import {DOCUMENT} from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  Inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {Meta, Title} from '@angular/platform-browser';
import {ActivatedRoute} from '@angular/router';
import {Subject, catchError, firstValueFrom, of, switchMap, takeUntil} from 'rxjs';

import {environment} from '../../../environments/environment';
import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';
import {CatalogBook, CatalogWatchlistScope} from '../../core/catalog.models';
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
  readonly notificationScope = signal<CatalogWatchlistScope>('Edition');
  readonly notifyPending = signal(false);
  readonly notifyMessage = signal<string | null>(null);
  readonly notifyError = signal<string | null>(null);

  private readonly destroyed = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly api: CatalogApiService,
    private readonly title: Title,
    private readonly meta: Meta,
    private readonly auth: CatalogAuthService,
    private readonly memberApi: CatalogMemberApiService,
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
          this.notificationScope.set(book.workId ? 'Work' : 'Edition');
          this.setSeo(book);
        }
        this.changeDetector.markForCheck();
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

  async notify(item: CatalogBook): Promise<void> {
    this.notifyMessage.set(null);
    this.notifyError.set(null);

    if (!this.auth.isAuthenticated()) {
      try {
        await this.auth.login(publicBookPath(item));
      } catch {
        this.notifyError.set('La connexion n’a pas pu être démarrée. Réessayez.');
      }
      return;
    }

    const scope: CatalogWatchlistScope = item.workId
      ? this.notificationScope()
      : 'Edition';
    const request = scope === 'Work'
      ? {scope, workId: item.workId, isbn13: null}
      : {scope, workId: null, isbn13: item.isbn13};

    this.notifyPending.set(true);
    try {
      const token = await this.auth.getApiAccessToken();
      await firstValueFrom(this.memberApi.addWatchlistItem(token, request));
      this.notifyMessage.set('Le titre a été ajouté à votre liste.');
    } catch (error: unknown) {
      this.notifyError.set(this.describeNotificationError(error));
    } finally {
      this.notifyPending.set(false);
    }
  }

  private isbnFromSlug(slug: string): string {
    const match = slug.match(/(\d{13})$/);
    return match?.[1] || slug;
  }

  private describeNotificationError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 409) {
      return 'Ce titre est déjà présent dans votre liste.';
    }

    if (error instanceof HttpErrorResponse && error.status === 401) {
      return 'La session a expiré. Reconnectez-vous pour continuer.';
    }

    return 'Le titre n’a pas pu être ajouté. Réessayez dans un instant.';
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
