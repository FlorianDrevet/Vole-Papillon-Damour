import {HttpClient, HttpHeaders, HttpParams} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable} from 'rxjs';

import {environment} from '../../environments/environment';
import {
  CatalogAddedSelectionItem,
  CatalogAddedWatchlistItem,
  CatalogFavoriteAddedResponse,
  CatalogFavoriteMergeEntry,
  CatalogFavoriteMergeResult,
  CatalogMemberFavoritesResponse,
  CatalogAlertPreferencesResponse,
  CatalogMemberCard,
  CatalogMemberGenreFavorites,
  CatalogRecommendationsResponse,
  CatalogPurchasesResponse,
  CatalogSelectionMergeEntry,
  CatalogSelectionMergeResult,
  CatalogSelectionResponse,
  CatalogSelectionTargetRequest,
  CatalogNotFoundReportCreated,
  CatalogWatchlistItemRequest,
  CatalogWatchlistResponse,
  CatalogVolunteerStatisticsResponse,
} from './catalog.models';

@Injectable({providedIn: 'root'})
export class CatalogMemberApiService {
  private readonly apiUrl = environment.apiUrl.replace(/\/$/, '');

  constructor(private readonly http: HttpClient) {}

  getFavorites(accessToken: string): Observable<CatalogMemberFavoritesResponse> {
    return this.http.get<CatalogMemberFavoritesResponse>(
      `${this.apiUrl}/catalog/me/favorites`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  addFavorite(accessToken: string, isbn13: string): Observable<CatalogFavoriteAddedResponse> {
    return this.http.put<CatalogFavoriteAddedResponse>(
      `${this.apiUrl}/catalog/me/favorites/${encodeURIComponent(isbn13)}`,
      null,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  removeFavorite(accessToken: string, isbn13: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/catalog/me/favorites/${encodeURIComponent(isbn13)}`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  mergeFavorites(
    accessToken: string,
    entries: readonly CatalogFavoriteMergeEntry[],
  ): Observable<CatalogFavoriteMergeResult> {
    return this.http.post<CatalogFavoriteMergeResult>(
      `${this.apiUrl}/catalog/me/favorites/merge`,
      {entries},
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

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

  reportNotFound(
    accessToken: string,
    itemId: string,
    request: {comment: string | null},
  ): Observable<CatalogNotFoundReportCreated> {
    return this.http.post<CatalogNotFoundReportCreated>(
      `${this.apiUrl}/catalog/me/selection/${encodeURIComponent(itemId)}/not-found-report`,
      request,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  cancelNotFoundReport(accessToken: string, reportId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/catalog/me/not-found-reports/${encodeURIComponent(reportId)}`,
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

  getGenreFavorites(accessToken: string): Observable<CatalogMemberGenreFavorites> {
    return this.http.get<CatalogMemberGenreFavorites>(
      `${this.apiUrl}/catalog/me/genre-favorites`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  addGenreFavorite(accessToken: string, genre: string): Observable<CatalogMemberGenreFavorites> {
    return this.http.post<CatalogMemberGenreFavorites>(
      `${this.apiUrl}/catalog/me/genre-favorites`,
      {genre},
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  removeGenreFavorite(accessToken: string, genre: string): Observable<CatalogMemberGenreFavorites> {
    return this.http.delete<CatalogMemberGenreFavorites>(
      `${this.apiUrl}/catalog/me/genre-favorites`,
      {headers: this.authorizationHeaders(accessToken), body: {genre}},
    );
  }

  getVolunteerStatistics(accessToken: string): Observable<CatalogVolunteerStatisticsResponse> {
    return this.http.get<CatalogVolunteerStatisticsResponse>(
      `${this.apiUrl}/scan/me/statistics`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  getCard(accessToken: string): Observable<CatalogMemberCard> {
    return this.http.get<CatalogMemberCard>(
      `${this.apiUrl}/catalog/me/card`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  getPurchases(accessToken: string, cursor?: string): Observable<CatalogPurchasesResponse> {
    const query = cursor ? `?cursor=${encodeURIComponent(cursor)}` : '';
    return this.http.get<CatalogPurchasesResponse>(
      `${this.apiUrl}/catalog/me/purchases${query}`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  getRecommendations(
    accessToken: string,
    limit = 4,
    minimumHearts?: number,
  ): Observable<CatalogRecommendationsResponse> {
    let params = new HttpParams().set('limit', limit);
    if (minimumHearts !== undefined) {
      params = params.set('minimumHearts', minimumHearts);
    }
    return this.http.get<CatalogRecommendationsResponse>(
      `${this.apiUrl}/catalog/me/recommendations`,
      {
        headers: this.authorizationHeaders(accessToken),
        params,
      },
    );
  }

  getRecommendationPreference(accessToken: string): Observable<{
    enabled: boolean;
    minimumHearts?: number;
    fairDigestConsentedAt?: string | null;
  }> {
    return this.http.get<{
      enabled: boolean;
      minimumHearts?: number;
      fairDigestConsentedAt?: string | null;
    }>(
      `${this.apiUrl}/catalog/me/recommendations/preference`,
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  setRecommendationPreference(
    accessToken: string,
    enabled: boolean,
    minimumHearts?: number,
    fairDigestConsent?: boolean,
  ): Observable<void> {
    return this.http.put<void>(
      `${this.apiUrl}/catalog/me/recommendations/preference`,
      {
        enabled,
        ...(minimumHearts === undefined ? {} : {minimumHearts}),
        ...(fairDigestConsent === undefined ? {} : {fairDigestConsent}),
      },
      {headers: this.authorizationHeaders(accessToken)},
    );
  }

  rotateCard(accessToken: string): Observable<CatalogMemberCard> {
    return this.http.post<CatalogMemberCard>(
      `${this.apiUrl}/catalog/me/card/rotate`,
      {},
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

