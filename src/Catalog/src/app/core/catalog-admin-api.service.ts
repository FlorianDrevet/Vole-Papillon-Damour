import {HttpClient, HttpHeaders, HttpParams} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable} from 'rxjs';

import {environment} from '../../environments/environment';
import {CatalogDeadStockResponse} from './catalog.models';

@Injectable({providedIn: 'root'})
export class CatalogAdminApiService {
  private readonly apiUrl = environment.apiUrl.replace(/\/$/, '');

  constructor(private readonly http: HttpClient) {}

  getDeadStock(
    accessToken: string,
    minAgeMonths: number,
    minQuantity: number,
  ): Observable<CatalogDeadStockResponse> {
    if (!accessToken.trim()) {
      throw new Error('An administrator access token is required.');
    }

    return this.http.get<CatalogDeadStockResponse>(
      `${this.apiUrl}/books/admin/dead-stock`,
      {
        headers: new HttpHeaders({Authorization: `Bearer ${accessToken}`}),
        params: new HttpParams()
          .set('minAgeMonths', minAgeMonths)
          .set('minQuantity', minQuantity),
      },
    );
  }
}
