import {HttpClient, HttpParams} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable, timeout} from 'rxjs';

import {environment} from '../../environments/environment';
import {
  ScanBookResponse,
  ScanCatalogDeltaResponse,
  ScanSaleResponse,
  ScanSessionResponse,
  ScanVolunteerStatisticsResponse,
} from './scan-offline.model';
import {ScanRareBook} from './scan-offline.model';

const RARE_BOOKS_ADMIN_REQUEST_TIMEOUT_MS = 30_000;

export interface OpenScanSessionRequest {
  mode: 'AvailableNow' | 'NextFair';
  targetAssoEventsId: string | null;
  clientSessionId: string;
  startedAt: string;
}
export interface ScanBookRequest {
  isbn: string;
  kept: boolean;
  occurredAt: string;
  clientGestureId: string;
}

export interface RegisterSaleRequest {
  isbn: string;
  quantity: number;
  occurredAt: string;
  clientGestureId: string;
  checkoutPassageId?: string | null;
}

export interface AssociateCheckoutPassageRequest {
  credential: string;
  occurredAt: string;
}

export interface ScanPassageAssociationResponse {
  checkoutPassageId: string;
  status: 'Associated' | 'Accepted' | 'Unresolved';
  displayLabel: string | null;
  alreadyProcessed: boolean;
}

export interface ResolveMemberCardResponse {
  displayLabel: string;
}

export interface MarkRareBookSoldRequest {
  occurredAt: string;
  scanSessionId: string | null;
  assoEventsId: string | null;
  checkoutPassageId?: string | null;
}

export interface CloseScanSessionRequest {
  closeReason: 'Manual' | 'Inactivity' | 'Disconnect' | 'TokenExpired';
}

export type ScanRareBookCreateRequest = Omit<ScanRareBook, 'clientId' | 'serverId' | 'clientGestureId' | 'status' |
  'isSold' | 'thumbnail' | 'updatedAt' | 'rowVersion' | 'syncStatus' | 'lastError'> & {
  clientGestureId: string;
};

export interface ScanRareBookUpdateRequest extends Omit<ScanRareBookCreateRequest, 'clientGestureId'> {
  rowVersion: string;
}

export interface ScanRareBookPhotoResponse {
  id: string;
  blobUri: string;
  blobName: string;
  caption: string | null;
  position: number;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
  uploadedBy: string;
}

export interface ScanRareBookResponse {
  id: string;
  slug: string;
  isbn13: string | null;
  title: string;
  authorMention: string | null;
  publisher: string | null;
  publicationYear: number | null;
  shelf: string;
  price: number;
  condition: string;
  publicDescription: string | null;
  binding: string | null;
  dimensions: string | null;
  pageCount: number | null;
  shelfLocation: string | null;
  status: 'Draft' | 'Published';
  isSold: boolean;
  soldAt: string | null;
  soldAtFairId: string | null;
  soldInSessionId: string | null;
  priceSetBy: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
  rowVersion: string;
  photos: ScanRareBookPhotoResponse[];
}

export interface ScanRareBookPageResponse {
  generatedAt: string;
  books: ScanRareBookResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({providedIn: 'root'})
export class ScanApiService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private readonly http: HttpClient) {}

  getCatalogDelta(since: string | null): Observable<ScanCatalogDeltaResponse> {
    let params = new HttpParams();
    if (since) {
      params = params.set('since', since);
    }

    return this.http.get<ScanCatalogDeltaResponse>(
      `${this.baseUrl}/scan/catalog/delta`,
      {params},
    );
  }

  getVolunteerStatistics(): Observable<ScanVolunteerStatisticsResponse> {
    return this.http.get<ScanVolunteerStatisticsResponse>(
      `${this.baseUrl}/scan/me/statistics`,
    );
  }

  openSession(request: OpenScanSessionRequest): Observable<ScanSessionResponse> {
    return this.http.post<ScanSessionResponse>(
      `${this.baseUrl}/scan/sessions`,
      request,
    );
  }

  scanBook(sessionId: string, request: ScanBookRequest): Observable<ScanBookResponse> {
    return this.http.post<ScanBookResponse>(
      `${this.baseUrl}/scan/sessions/${encodeURIComponent(sessionId)}/scans`,
      request,
    );
  }

  registerSale(request: RegisterSaleRequest): Observable<ScanSaleResponse> {
    return this.http.post<ScanSaleResponse>(
      `${this.baseUrl}/scan/sales`,
      request,
    );
  }

  associatePassage(
    checkoutPassageId: string,
    request: AssociateCheckoutPassageRequest,
  ): Observable<ScanPassageAssociationResponse> {
    return this.http.put<ScanPassageAssociationResponse>(
      `${this.baseUrl}/scan/passages/${encodeURIComponent(checkoutPassageId)}/member`,
      request,
    );
  }

  resolveMemberCard(credential: string): Observable<ResolveMemberCardResponse> {
    return this.http.post<ResolveMemberCardResponse>(
      `${this.baseUrl}/scan/member-cards/resolve`,
      {credential},
    );
  }

  markRareBookSold(
    id: string,
    request: MarkRareBookSoldRequest,
  ): Observable<ScanRareBookResponse> {
    return this.http.post<ScanRareBookResponse>(
      `${this.baseUrl}/rare-books/cash/${encodeURIComponent(id)}/sold`,
      request,
    );
  }

  restoreRareBookAvailability(id: string): Observable<ScanRareBookResponse> {
    return this.http.post<ScanRareBookResponse>(
      `${this.baseUrl}/rare-books/cash/${encodeURIComponent(id)}/restore`,
      null,
    );
  }

  closeSession(sessionId: string, request: CloseScanSessionRequest): Observable<ScanSessionResponse> {
    return this.http.post<ScanSessionResponse>(
      `${this.baseUrl}/scan/sessions/${encodeURIComponent(sessionId)}/close`,
      request,
    );
  }

  createRareBook(request: ScanRareBookCreateRequest): Observable<ScanRareBookResponse> {
    return this.http.post<ScanRareBookResponse>(
      `${this.baseUrl}/rare-books/admin`,
      request,
    );
  }

  updateRareBook(
    id: string,
    request: ScanRareBookUpdateRequest,
  ): Observable<ScanRareBookResponse> {
    return this.http.put<ScanRareBookResponse>(
      `${this.baseUrl}/rare-books/admin/${encodeURIComponent(id)}`,
      request,
    );
  }

  getRareBooksAdmin(): Observable<ScanRareBookPageResponse> {
    return this.http.get<ScanRareBookPageResponse>(
      `${this.baseUrl}/rare-books/admin`,
      {params: new HttpParams().set('availability', 'all').set('page', 1).set('pageSize', 200)},
    ).pipe(timeout({first: RARE_BOOKS_ADMIN_REQUEST_TIMEOUT_MS}));
  }

  addRareBookPhoto(
    id: string,
    file: Blob,
    caption: string | null,
    fileName = 'photo.jpg',
  ): Observable<ScanRareBookResponse> {
    const formData = new FormData();
    formData.append('file', file, fileName);
    if (caption !== null) {
      formData.append('caption', caption);
    }
    return this.http.post<ScanRareBookResponse>(
      `${this.baseUrl}/rare-books/admin/${encodeURIComponent(id)}/photos`,
      formData,
    );
  }
}
