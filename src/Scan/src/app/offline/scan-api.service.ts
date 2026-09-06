import {HttpClient, HttpParams} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable} from 'rxjs';

import {environment} from '../../environments/environment';
import {
  ScanBookResponse,
  ScanCatalogDeltaResponse,
  ScanSaleResponse,
  ScanSessionResponse,
} from './scan-offline.model';

export interface OpenScanSessionRequest {
  mode: 'AvailableNow' | 'NextFair';
  targetAssoEventsId: string | null;
  clientSessionId: string;
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
}

export interface CloseScanSessionRequest {
  closeReason: 'Manual' | 'Inactivity' | 'Disconnect' | 'TokenExpired';
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

  closeSession(sessionId: string, request: CloseScanSessionRequest): Observable<ScanSessionResponse> {
    return this.http.post<ScanSessionResponse>(
      `${this.baseUrl}/scan/sessions/${encodeURIComponent(sessionId)}/close`,
      request,
    );
  }
}
