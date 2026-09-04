import {HttpClient, HttpParams} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable} from 'rxjs';

import {environment} from '../../environments/environment';
import {
  CatalogBook,
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

  getNextFair(): Observable<CatalogFair> {
    return this.http.get<CatalogFair>(`${this.apiUrl}/catalog/fairs/next`);
  }

  getWork(workId: string): Observable<CatalogWorkResponse> {
    return this.http.get<CatalogWorkResponse>(
      `${this.apiUrl}/catalog/works/${encodeURIComponent(workId)}`,
    );
  }
}
