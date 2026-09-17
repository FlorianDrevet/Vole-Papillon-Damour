import {DOCUMENT} from '@angular/common';
import {HttpErrorResponse} from '@angular/common/http';
import {ChangeDetectionStrategy, ChangeDetectorRef, Component, Inject, OnDestroy, OnInit} from '@angular/core';
import {Meta, Title} from '@angular/platform-browser';
import {ActivatedRoute} from '@angular/router';
import {Subject, catchError, firstValueFrom, of, switchMap, takeUntil} from 'rxjs';

import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogAuthService} from '../../core/catalog-auth.service';
import {CatalogMemberApiService} from '../../core/catalog-member-api.service';
import {CatalogRareBook, CatalogWatchlistScope} from '../../core/catalog.models';

const CATALOG_ORIGIN = 'https://livres.volepapillondamour.fr';
const CONTACT_EMAIL = 'volepapillondamour@sfr.fr';

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
  followPending = false;
  followMessage: string | null = null;
  followError: string | null = null;
  authPromptOpen = false;

  private readonly destroyed = new Subject<void>();
  private pendingAuthFollow: CatalogRareBook | null = null;
  private canonicalElement: HTMLLinkElement | null = null;
  private structuredDataElement: HTMLScriptElement | null = null;

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
          this.relatedBooks = [];
          this.selectedPhoto = 0;
          return this.api.getPublicRareBook(params.get('slug') ?? '')
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

  async followRareBook(book: CatalogRareBook): Promise<void> {
    this.followMessage = null;
    this.followError = null;

    if (!this.auth.isAuthenticated()) {
      this.pendingAuthFollow = book;
      this.authPromptOpen = true;
      return;
    }

    this.followPending = true;
    try {
      const token = await this.auth.getApiAccessToken();
      await firstValueFrom(this.memberApi.addWatchlistItem(token, {
        scope: 'RareBook' satisfies CatalogWatchlistScope,
        workId: null,
        isbn13: null,
        rareBookId: book.id,
        title: book.title,
        authors: book.authorMention,
        publisher: book.publisher,
        publicationYear: book.publicationYear,
        coverUrl: null,
      }));
      this.followMessage = 'Cet exemplaire est maintenant suivi.';
    } catch (error: unknown) {
      this.followError = this.describeFollowError(error);
    } finally {
      this.followPending = false;
      this.changeDetector.markForCheck();
    }
  }

  closeAuthPrompt(): void {
    this.authPromptOpen = false;
    this.pendingAuthFollow = null;
  }

  async confirmAuthPrompt(mode: 'login' | 'register'): Promise<void> {
    const book = this.pendingAuthFollow;
    this.authPromptOpen = false;
    this.pendingAuthFollow = null;
    if (!book) {
      return;
    }

    const returnPath = ['/livres-rares', encodeURIComponent(book.slug)].join('/');
    try {
      if (mode === 'register') {
        await this.auth.register(returnPath);
      } else {
        await this.auth.login(returnPath);
      }
    } catch {
      this.followError = 'La connexion n’a pas pu être démarrée. Réessayez.';
      this.changeDetector.markForCheck();
    }
  }

  trackBook(_index: number, book: CatalogRareBook): string {
    return book.id;
  }

  questionHref(book: CatalogRareBook): string {
    return `mailto:${CONTACT_EMAIL}?subject=${encodeURIComponent(`Question — ${book.title}`)}`;
  }

  private setSeo(book: CatalogRareBook): void {
    const description = book.publicDescription
      ?? `${book.title}${book.authorMention ? ` — ${book.authorMention}` : ''}. Livre rare proposé par Vole Papillon d’Amour.`;
    this.title.setTitle(`${book.title} — Livre rare | Vole Papillon d’Amour`);
    this.meta.updateTag({name: 'description', content: description.slice(0, 160)});
    this.meta.updateTag({property: 'og:title', content: `${book.title} — Livre rare`});
    this.meta.updateTag({property: 'og:description', content: description.slice(0, 160)});
    this.meta.updateTag({property: 'og:type', content: 'product'});

    const url = `${CATALOG_ORIGIN}/livres-rares/${encodeURIComponent(book.slug)}`;
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

  private describeFollowError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 409) {
      return 'Cet exemplaire est déjà présent dans votre liste.';
    }

    if (error instanceof HttpErrorResponse && error.status === 401) {
      return 'La session a expiré. Reconnectez-vous pour continuer.';
    }

    return 'Cet exemplaire n’a pas pu être ajouté. Réessayez dans un instant.';
  }
}
