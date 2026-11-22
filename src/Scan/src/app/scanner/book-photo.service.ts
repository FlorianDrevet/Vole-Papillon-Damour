import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable} from 'rxjs';

import {environment} from '../../environments/environment';
import {BookPhoto} from './book-photo.model';

@Injectable({providedIn: 'root'})
export class BookPhotoService {
  constructor(private readonly http: HttpClient) {}

  getPhotos(isbn13: string): Observable<BookPhoto[]> {
    return this.http.get<BookPhoto[]>(this.photosUrl(isbn13));
  }

  uploadPhoto(isbn13: string, file: File): Observable<BookPhoto> {
    const formData = new FormData();
    formData.append('File', file, file.name);
    return this.http.post<BookPhoto>(this.photosUrl(isbn13), formData);
  }

  private photosUrl(isbn13: string): string {
    return `${environment.apiUrl}/books/${encodeURIComponent(isbn13)}/photos`;
  }
}
