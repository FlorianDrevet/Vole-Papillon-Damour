import {Injectable} from '@angular/core';
import {EMPTY, Observable, catchError, defer, finalize, of, shareReplay, tap} from 'rxjs';

import {CatalogApiService} from './catalog-api.service';
import {CatalogSearchParams, CatalogSearchResponse} from './catalog.models';

interface CatalogSearchCacheEntry {
  response: CatalogSearchResponse;
  expiresAt: number;
}

@Injectable({providedIn: 'root'})
export class CatalogSearchCacheService {
  private readonly ttlMilliseconds = 60_000;
  private readonly maximumEntries = 20;
  private readonly cache = new Map<string, CatalogSearchCacheEntry>();
  private readonly inFlight = new Map<string, Observable<CatalogSearchResponse>>();

  constructor(private readonly api: CatalogApiService) {}

  search(params: CatalogSearchParams): Observable<CatalogSearchResponse> {
    const normalizedParams = this.normalize(params);
    const key = JSON.stringify(normalizedParams);
    const cached = this.getCached(key);
    if (cached) {
      return of(cached);
    }

    const pending = this.inFlight.get(key);
    if (pending) {
      return pending;
    }

    let request$: Observable<CatalogSearchResponse>;
    request$ = defer(() => this.api.search(normalizedParams)).pipe(
      tap(response => this.store(key, response)),
      finalize(() => {
        if (this.inFlight.get(key) === request$) {
          this.inFlight.delete(key);
        }
      }),
      shareReplay({bufferSize: 1, refCount: true}),
    );
    this.inFlight.set(key, request$);
    return request$;
  }

  prefetchNextPage(params: CatalogSearchParams, response: CatalogSearchResponse): void {
    if (!response.hasMore) {
      return;
    }

    this.search({...params, page: (params.page ?? 1) + 1})
      .pipe(catchError(() => EMPTY))
      .subscribe();
  }

  private normalize(params: CatalogSearchParams): CatalogSearchParams {
    return {
      query: this.normalizeText(params.query),
      genre: this.normalizeText(params.genre),
      availability: params.availability ?? 'all',
      rareOnly: params.rareOnly ?? false,
      includeExhausted: params.includeExhausted ?? false,
      includeTotalCount: params.includeTotalCount ?? false,
      sort: params.sort ?? 'relevance',
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 24,
    };
  }

  private normalizeText(value: string | undefined): string {
    return value?.trim().replace(/\s+/g, ' ') ?? '';
  }

  private getCached(key: string): CatalogSearchResponse | null {
    const entry = this.cache.get(key);
    if (!entry) {
      return null;
    }

    if (entry.expiresAt <= Date.now()) {
      this.cache.delete(key);
      return null;
    }

    this.cache.delete(key);
    this.cache.set(key, entry);
    return entry.response;
  }

  private store(key: string, response: CatalogSearchResponse): void {
    this.cache.delete(key);
    this.cache.set(key, {
      response,
      expiresAt: Date.now() + this.ttlMilliseconds,
    });

    while (this.cache.size > this.maximumEntries) {
      const oldestKey = this.cache.keys().next().value;
      if (oldestKey === undefined) {
        return;
      }
      this.cache.delete(oldestKey);
    }
  }
}
