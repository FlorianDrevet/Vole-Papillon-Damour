import {HttpClient, HttpHeaders} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable} from 'rxjs';

import {environment} from '../../environments/environment';
import {
  CatalogAddedWatchlistItem,
  CatalogAlertPreferencesResponse,
  CatalogAddedSelectionItem,
  CatalogSelectionMergeEntry,
  CatalogSelectionMergeResult,
  CatalogSelectionResponse,
  CatalogSelectionStatus,
  CatalogSelectionTargetRequest,
  CatalogWatchlistItemRequest,
  CatalogWatchlistResponse,
  CatalogVolunteerStatisticsResponse,
} from './catalog.models';

@Injectable({providedIn: 'root'})
export class CatalogMemberApiService {
  private readonly apiUrl = environment.apiUrl.replace(/\/$/, '');

  constructor(private readonly http: HttpClient) {}

  getSelection(accessToken: string): Observable<CatalogSelectionResponse> {
    return this.http.get<CatalogSelectionResponse>(
      `${this.apiUrl}/catalog/me/selection`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  addSelectionItem(
    accessToken: string,
    request: CatalogSelectionTargetRequest,
  ): Observable<CatalogAddedSelectionItem> {
    return this.http.post<CatalogAddedSelectionItem>(
      `${this.apiUrl}/catalog/me/selection`,
      request,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  removeSelectionItem(accessToken: string, itemId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/catalog/me/selection/${encodeURIComponent(itemId)}`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  setSelectionStatus(
    accessToken: string,
    itemId: string,
    status: CatalogSelectionStatus,
  ): Observable<void> {
    return this.http.patch<void>(
      `${this.apiUrl}/catalog/me/selection/${encodeURIComponent(itemId)}`,
      {status},
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  mergeSelection(
    accessToken: string,
    entries: readonly CatalogSelectionMergeEntry[],
  ): Observable<CatalogSelectionMergeResult> {
    return this.http.post<CatalogSelectionMergeResult>(
      `${this.apiUrl}/catalog/me/selection/merge`,
      {entries},
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  getWatchlist(accessToken: string): Observable<CatalogWatchlistResponse> {
    return this.http.get<CatalogWatchlistResponse>(
      `${this.apiUrl}/catalog/me/watchlist`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  getVolunteerStatistics(accessToken: string): Observable<CatalogVolunteerStatisticsResponse> {
    return this.http.get<CatalogVolunteerStatisticsResponse>(
      `${this.apiUrl}/scan/me/statistics`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  addWatchlistItem(
    accessToken: string,
    request: CatalogWatchlistItemRequest,
  ): Observable<CatalogAddedWatchlistItem> {
    return this.http.post<CatalogAddedWatchlistItem>(
      `${this.apiUrl}/catalog/me/watchlist`,
      request,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  removeWatchlistItem(accessToken: string, itemId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/catalog/me/watchlist/${encodeURIComponent(itemId)}`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  setAlertStatus(
    accessToken: string,
    enabled: boolean,
  ): Observable<CatalogAlertPreferencesResponse> {
    return this.http.patch<CatalogAlertPreferencesResponse>(
      `${this.apiUrl}/catalog/me/alerts`,
      {enabled},
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  deleteAccount(accessToken: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/catalog/me`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  private authorizationHeaders(accessToken: string): HttpHeaders {
    if (!accessToken.trim()) {
      throw new Error('A member access token is required.');
    }

    return new HttpHeaders({Authorization: `Bearer ${accessToken}`});
  }
}

