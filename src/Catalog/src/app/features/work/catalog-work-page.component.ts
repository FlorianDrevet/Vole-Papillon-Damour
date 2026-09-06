import {DOCUMENT} from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  Inject,
  OnDestroy,
  OnInit,
} from '@angular/core';
import {Meta, Title} from '@angular/platform-browser';
import {ActivatedRoute} from '@angular/router';
import {Subject, catchError, of, switchMap, takeUntil} from 'rxjs';

import {environment} from '../../../environments/environment';
import {CatalogApiService} from '../../core/catalog-api.service';
import {CatalogWorkResponse} from '../../core/catalog.models';

@Component({
  selector: 'app-catalog-work-page',
  standalone: false,
  templateUrl: './catalog-work-page.component.html',
  styleUrls: ['./catalog-work-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogWorkPageComponent implements OnInit, OnDestroy {
  work: CatalogWorkResponse | null = null;
  loading = true;
  notFound = false;

  private readonly destroyed = new Subject<void>();

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
          this.work = null;
          return this.api.getWork(params.get('workId') || '').pipe(catchError(() => of(null)));
        }),
        takeUntil(this.destroyed),
      )
      .subscribe(work => {
        this.work = work;
        this.notFound = work === null;
        this.loading = false;
        if (work) {
          this.setSeo(work);
        }
        this.changeDetector.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  trackBook(_index: number, isbn13: string): string {
    return isbn13;
  }

  private setSeo(work: CatalogWorkResponse): void {
    const title = work.title || 'Œuvre';
    this.title.setTitle(`${title} · Éditions disponibles · Bourse aux livres`);
    this.meta.updateTag({
      name: 'description',
      content: `${title}${work.authors ? ` de ${work.authors}` : ''}. Découvrez les éditions recensées par la bourse aux livres.`,
    });
    this.meta.updateTag({name: 'robots', content: 'index, follow'});
    const canonical = this.document.head.querySelector('link[rel="canonical"]') || this.document.createElement('link');
    canonical.setAttribute('rel', 'canonical');
    canonical.setAttribute('href', `${environment.publicUrl}/oeuvre/${encodeURIComponent(work.workId)}`);
    if (!canonical.parentNode) {
      this.document.head.appendChild(canonical);
    }
  }
}
