import {HttpClient, HttpParams} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable, map} from 'rxjs';

import {environment} from '../../environments/environment';
import {
  CatalogBook,
  CatalogPublicEventResponse,
  CatalogReferenceSearchResponse,
  CatalogFair,
  CatalogSearchParams,
  CatalogSearchResponse,
  CatalogWorkResponse,
} from './catalog.models';

@Injectable({providedIn: 'root'})
export class CatalogApiService {
  private readonly apiUrl = environment.apiUrl.replace(/\/$/, '');

  constructor(private readonly http: HttpClient) {}

  search(params: CatalogSearchParams = {}): Observable<CatalogSearchResponse> {
    let httpParams = new HttpParams();
    if (params.query?.trim()) {
      httpParams = httpParams.set('q', params.query.trim());
    }
    if (params.genre?.trim()) {
      httpParams = httpParams.set('genre', params.genre.trim());
    }
    if (params.availability && params.availability !== 'all') {
      httpParams = httpParams.set('availability', params.availability);
    }
    if (params.rareOnly) {
      httpParams = httpParams.set('rare', 'true');
    }
    if (params.sort && params.sort !== 'relevance') {
      httpParams = httpParams.set('sort', params.sort);
    }
    if (params.page !== undefined) {
      httpParams = httpParams.set('page', params.page);
    }
    if (params.pageSize !== undefined) {
      httpParams = httpParams.set('pageSize', params.pageSize);
    }

    return this.http.get<CatalogSearchResponse>(`${this.apiUrl}/catalog/search`, {
      params: httpParams,
    });
  }

  getBook(isbn13: string): Observable<CatalogBook> {
    return this.http.get<CatalogBook>(
      `${this.apiUrl}/catalog/books/${encodeURIComponent(isbn13)}`,
    );
  }

  getUpcomingFairs(): Observable<CatalogFair[]> {
    return this.http.get<CatalogPublicEventResponse>(`${this.apiUrl}/asso-events/next-books`).pipe(
      map(event => [this.mapPublicEventToFair(event)]),
    );
  }

  private mapPublicEventToFair(event: CatalogPublicEventResponse): CatalogFair {
    return {
      id: event.id,
      name: event.name,
      dateStart: event.dateStart,
      dateEnd: event.dateEnd,
      openAt: event.hourOpenDoors
        ? this.combineDateAndWallClock(event.dateStart, event.hourOpenDoors)
        : event.dateStart,
      closeAt: event.hourCloseDoors
        ? this.combineDateAndWallClock(event.dateEnd ?? event.dateStart, event.hourCloseDoors)
        : event.dateEnd,
      roadNumber: event.roadNumber,
      city: event.city,
      cityCode: event.cityCode,
      road: event.road,
    };
  }

  /** The event API stores opening/closing values as UTC wall-clock components. */
  private combineDateAndWallClock(dateValue: string, wallClockValue: string): string {
    const date = new Date(dateValue);
    const wallClock = new Date(wallClockValue);
    return new Date(Date.UTC(
      date.getUTCFullYear(),
      date.getUTCMonth(),
      date.getUTCDate(),
      wallClock.getUTCHours(),
      wallClock.getUTCMinutes(),
      wallClock.getUTCSeconds(),
      wallClock.getUTCMilliseconds(),
    )).toISOString();
  }

  getWork(workId: string): Observable<CatalogWorkResponse> {
    return this.http.get<CatalogWorkResponse>(
      `${this.apiUrl}/catalog/works/${encodeURIComponent(workId)}`,
    );
  }

  searchReferences(
    query: string,
    page = 1,
    pageSize = 20,
  ): Observable<CatalogReferenceSearchResponse> {
    const httpParams = new HttpParams()
      .set('q', query.trim())
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<CatalogReferenceSearchResponse>(
      `${this.apiUrl}/catalog/reference/search`,
      {params: httpParams},
    );
  }
}
