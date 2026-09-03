import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable} from 'rxjs';

import {environment} from '../../environments/environment';
import {BookMetadata} from './book-metadata.model';

@Injectable({providedIn: 'root'})
export class BookMetadataService {
  constructor(private readonly http: HttpClient) {}

  getMetadata(isbn13: string): Observable<BookMetadata> {
    return this.http.get<BookMetadata>(
      `${environment.apiUrl}/books/${encodeURIComponent(isbn13)}/metadata`,
    );
  }
}
