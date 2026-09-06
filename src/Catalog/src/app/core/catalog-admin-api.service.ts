import {HttpClient, HttpHeaders, HttpParams} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable} from 'rxjs';

import {environment} from '../../environments/environment';
import {
  CatalogAdminAlertFilters,
  CatalogAdminAlertPage,
  CatalogAdminAlertOperation,
  CatalogAdminBook,
  CatalogAdminBookFilters,
  CatalogAdminBookPage,
  CatalogAdminFairPage,
  CatalogAdminFairStats,
  CatalogAdminMemberDetail,
  CatalogAdminMemberFilters,
  CatalogAdminMemberOperation,
  CatalogAdminMemberPage,
  CatalogAdminOperation,
  CatalogAdminOverview,
  CatalogAdminQuantityCorrection,
  CatalogAdminScanSession,
  CatalogAdminScanSessionPage,
  CatalogAdminSessionFilters,
  CatalogAdminSettings,
  CatalogDeadStockResponse,
} from './catalog.models';

type QueryValue = string | number | boolean | null | undefined;

@Injectable({providedIn: 'root'})
export class CatalogAdminApiService {
  private readonly apiUrl = environment.apiUrl.replace(/\/$/, '');

  constructor(private readonly http: HttpClient) {}

  getOverview(accessToken: string, from?: string, to?: string): Observable<CatalogAdminOverview> {
    return this.http.get<CatalogAdminOverview>(
      `${this.apiUrl}/books/admin/overview`,
      this.options(accessToken, this.params({from, to})),
    );
  }

  getBooks(accessToken: string, filters: CatalogAdminBookFilters = {}): Observable<CatalogAdminBookPage> {
    return this.http.get<CatalogAdminBookPage>(
      `${this.apiUrl}/books/admin/books`,
      this.options(accessToken, this.params(filters)),
    );
  }

  getBook(accessToken: string, isbn13: string): Observable<CatalogAdminBook> {
    return this.http.get<CatalogAdminBook>(
      `${this.apiUrl}/books/admin/books/${encodeURIComponent(isbn13)}`,
      this.options(accessToken),
    );
  }

  addBook(accessToken: string, request: object): Observable<CatalogAdminOperation> {
    return this.http.post<CatalogAdminOperation>(
      `${this.apiUrl}/books/admin/books`,
      request,
      this.options(accessToken),
    );
  }

  updateMetadata(accessToken: string, isbn13: string, request: object): Observable<object> {
    return this.http.patch<object>(
      `${this.apiUrl}/books/admin/books/${encodeURIComponent(isbn13)}/metadata`,
      request,
      this.options(accessToken),
    );
  }

  correctQuantity(accessToken: string, isbn13: string, request: object): Observable<CatalogAdminQuantityCorrection> {
    return this.http.patch<CatalogAdminQuantityCorrection>(
      `${this.apiUrl}/books/admin/books/${encodeURIComponent(isbn13)}/quantity`,
      request,
      this.options(accessToken),
    );
  }

  withdraw(accessToken: string, isbn13: string, request: object): Observable<CatalogAdminOperation> {
    return this.http.post<CatalogAdminOperation>(
      `${this.apiUrl}/books/admin/books/${encodeURIComponent(isbn13)}/withdrawals`,
      request,
      this.options(accessToken),
    );
  }

  correctAnnouncement(accessToken: string, announcementId: string, request: object): Observable<CatalogAdminOperation> {
    return this.http.patch<CatalogAdminOperation>(
      `${this.apiUrl}/books/admin/announcements/${encodeURIComponent(announcementId)}/quantity`,
      request,
      this.options(accessToken),
    );
  }

  setRare(accessToken: string, isbn13: string, isRare: boolean): Observable<object> {
    return this.http.post<object>(
      `${this.apiUrl}/books/admin/books/${encodeURIComponent(isbn13)}/rare`,
      null,
      this.options(accessToken, this.params({isRare})),
    );
  }

  setVisibility(accessToken: string, isbn13: string, hidden: boolean): Observable<object> {
    return this.http.post<object>(
      `${this.apiUrl}/books/admin/books/${encodeURIComponent(isbn13)}/visibility`,
      null,
      this.options(accessToken, this.params({hidden})),
    );
  }

  merge(accessToken: string, sourceIsbn13: string, request: object): Observable<CatalogAdminOperation> {
    return this.http.post<CatalogAdminOperation>(
      `${this.apiUrl}/books/admin/books/${encodeURIComponent(sourceIsbn13)}/merge`,
      request,
      this.options(accessToken),
    );
  }

  deleteBook(accessToken: string, isbn13: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/books/admin/books/${encodeURIComponent(isbn13)}`,
      this.options(accessToken),
    );
  }

  getFairs(accessToken: string, includeCancelled = false, page = 1, pageSize = 50): Observable<CatalogAdminFairPage> {
    return this.http.get<CatalogAdminFairPage>(
      `${this.apiUrl}/books/admin/fairs`,
      this.options(accessToken, this.params({includeCancelled, page, pageSize})),
    );
  }

  getFairStats(accessToken: string, fairId: string): Observable<CatalogAdminFairStats> {
    return this.http.get<CatalogAdminFairStats>(
      `${this.apiUrl}/books/admin/fairs/${encodeURIComponent(fairId)}/stats`,
      this.options(accessToken),
    );
  }

  setFairRevenue(accessToken: string, fairId: string, revenue: number | null): Observable<object> {
    return this.http.put<object>(
      `${this.apiUrl}/books/admin/fairs/${encodeURIComponent(fairId)}/revenue`,
      {revenue},
      this.options(accessToken),
    );
  }

  getSessions(accessToken: string, filters: CatalogAdminSessionFilters = {}): Observable<CatalogAdminScanSessionPage> {
    return this.http.get<CatalogAdminScanSessionPage>(
      `${this.apiUrl}/books/admin/sessions`,
      this.options(accessToken, this.params(filters)),
    );
  }

  getSession(accessToken: string, sessionId: string): Observable<CatalogAdminScanSession> {
    return this.http.get<CatalogAdminScanSession>(
      `${this.apiUrl}/books/admin/sessions/${encodeURIComponent(sessionId)}`,
      this.options(accessToken),
    );
  }

  removeMovement(accessToken: string, sessionId: string, movementId: string): Observable<CatalogAdminOperation> {
    return this.http.post<CatalogAdminOperation>(
      `${this.apiUrl}/books/admin/sessions/${encodeURIComponent(sessionId)}/movements/${encodeURIComponent(movementId)}/remove`,
      null,
      this.options(accessToken),
    );
  }

  reassignSession(accessToken: string, sessionId: string, request: object): Observable<CatalogAdminOperation> {
    return this.http.post<CatalogAdminOperation>(
      `${this.apiUrl}/books/admin/sessions/${encodeURIComponent(sessionId)}/reassign`,
      request,
      this.options(accessToken),
    );
  }

  cancelSession(accessToken: string, sessionId: string): Observable<CatalogAdminOperation> {
    return this.http.post<CatalogAdminOperation>(
      `${this.apiUrl}/books/admin/sessions/${encodeURIComponent(sessionId)}/cancel`,
      null,
      this.options(accessToken),
    );
  }

  cancelSessionAlerts(accessToken: string, sessionId: string): Observable<CatalogAdminOperation> {
    return this.http.post<CatalogAdminOperation>(
      `${this.apiUrl}/books/admin/sessions/${encodeURIComponent(sessionId)}/alerts/cancel`,
      null,
      this.options(accessToken),
    );
  }

  forceSessionAlerts(accessToken: string, sessionId: string): Observable<CatalogAdminOperation> {
    return this.http.post<CatalogAdminOperation>(
      `${this.apiUrl}/books/admin/sessions/${encodeURIComponent(sessionId)}/alerts/force`,
      null,
      this.options(accessToken),
    );
  }

  getAlerts(accessToken: string, filters: CatalogAdminAlertFilters = {}): Observable<CatalogAdminAlertPage> {
    return this.http.get<CatalogAdminAlertPage>(
      `${this.apiUrl}/books/admin/alerts`,
      this.options(accessToken, this.params(filters)),
    );
  }

  cancelAlert(accessToken: string, messageId: string): Observable<CatalogAdminAlertOperation> {
    return this.http.post<CatalogAdminAlertOperation>(
      `${this.apiUrl}/books/admin/alerts/${encodeURIComponent(messageId)}/cancel`,
      null,
      this.options(accessToken),
    );
  }

  forceAlert(accessToken: string, messageId: string): Observable<CatalogAdminAlertOperation> {
    return this.http.post<CatalogAdminAlertOperation>(
      `${this.apiUrl}/books/admin/alerts/${encodeURIComponent(messageId)}/force`,
      null,
      this.options(accessToken),
    );
  }

  getMembers(accessToken: string, filters: CatalogAdminMemberFilters = {}): Observable<CatalogAdminMemberPage> {
    return this.http.get<CatalogAdminMemberPage>(
      `${this.apiUrl}/books/admin/members`,
      this.options(accessToken, this.params(filters)),
    );
  }

  getMember(accessToken: string, memberId: string): Observable<CatalogAdminMemberDetail> {
    return this.http.get<CatalogAdminMemberDetail>(
      `${this.apiUrl}/books/admin/members/${encodeURIComponent(memberId)}`,
      this.options(accessToken),
    );
  }

  setAlertStatus(accessToken: string, memberId: string, enabled: boolean): Observable<CatalogAdminMemberOperation> {
    const action = enabled ? 'unblock' : 'block';
    return this.http.post<CatalogAdminMemberOperation>(
      `${this.apiUrl}/books/admin/members/${encodeURIComponent(memberId)}/${action}`,
      null,
      this.options(accessToken),
    );
  }

  deleteMember(accessToken: string, memberId: string): Observable<CatalogAdminMemberOperation> {
    return this.http.delete<CatalogAdminMemberOperation>(
      `${this.apiUrl}/books/admin/members/${encodeURIComponent(memberId)}`,
      this.options(accessToken),
    );
  }

  getSettings(accessToken: string): Observable<CatalogAdminSettings> {
    return this.http.get<CatalogAdminSettings>(
      `${this.apiUrl}/books/admin/settings`,
      this.options(accessToken),
    );
  }

  updateSettings(accessToken: string, request: object): Observable<CatalogAdminSettings> {
    return this.http.put<CatalogAdminSettings>(
      `${this.apiUrl}/books/admin/settings`,
      request,
      this.options(accessToken),
    );
  }

  getDeadStock(accessToken: string, minAgeMonths: number, minQuantity: number): Observable<CatalogDeadStockResponse> {
    return this.http.get<CatalogDeadStockResponse>(
      `${this.apiUrl}/books/admin/dead-stock`,
      this.options(accessToken, this.params({minAgeMonths, minQuantity})),
    );
  }

  private options(accessToken: string, params?: HttpParams): {headers: HttpHeaders; params?: HttpParams} {
    if (!accessToken.trim()) {
      throw new Error('An administrator access token is required.');
    }

    return {
      headers: new HttpHeaders({Authorization: `Bearer ${accessToken}`}),
      ...(params ? {params} : {}),
    };
  }

  private params(values: object): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(values as Record<string, QueryValue>)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return params;
  }
}
