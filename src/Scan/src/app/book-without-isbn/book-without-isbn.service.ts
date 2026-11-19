import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {firstValueFrom} from 'rxjs';

import {environment} from '../../environments/environment';

export interface OrdinaryBookPayload {
  title: string;
  quantityAvailable: number;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  physicalFormat: string | null;
  language: string | null;
  genre: string | null;
  summary: string | null;
  edition: string | null;
  pages: number | null;
}

export interface CreatedOrdinaryBook {
  code: string;
  quantityAvailable: number;
}

export interface RareBookPayload {
  title: string;
  authorMention: string | null;
  publisher: string | null;
  publicationYear: number | null;
  price: number;
  condition: string;
  publicDescription: string | null;
  isbn13: null;
  clientGestureId: string;
}

export interface CreatedRareBook {
  id: string;
  isbn13: string | null;
}

// Création en ligne uniquement (RG-180) : aucun appel n'est mis en file hors connexion.
@Injectable({providedIn: 'root'})
export class BookWithoutIsbnService {
  constructor(private readonly http: HttpClient) {}

  createOrdinary(payload: OrdinaryBookPayload): Promise<CreatedOrdinaryBook> {
    return firstValueFrom(this.http.post<CreatedOrdinaryBook>(
      `${environment.apiUrl}/books/without-isbn`,
      payload,
    ));
  }

  addOrdinaryPhoto(code: string, file: File): Promise<unknown> {
    return firstValueFrom(this.http.post(
      `${environment.apiUrl}/books/${encodeURIComponent(code)}/photos`,
      photoForm(file),
    ));
  }

  createRare(payload: RareBookPayload): Promise<CreatedRareBook> {
    return firstValueFrom(this.http.post<CreatedRareBook>(
      `${environment.apiUrl}/rare-books/admin`,
      payload,
    ));
  }

  addRarePhoto(id: string, file: File): Promise<unknown> {
    return firstValueFrom(this.http.post(
      `${environment.apiUrl}/rare-books/admin/${encodeURIComponent(id)}/photos`,
      photoForm(file),
    ));
  }
}

function photoForm(file: File): FormData {
  const form = new FormData();
  form.append('File', file, file.name);
  return form;
}