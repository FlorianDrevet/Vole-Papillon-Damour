import {provideHttpClient} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {BookMetadataService} from './book-metadata.service';
import {BookMetadata} from './book-metadata.model';

describe('BookMetadataService', () => {
  let service: BookMetadataService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        BookMetadataService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(BookMetadataService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets metadata for the normalized ISBN', () => {
    const metadata: BookMetadata = {
      isbn13: '9782070363735',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1946,
      coverUrl: 'https://covers.example.test/book.jpg',
      source: 'BnF',
      workId: null,
      retrievedAt: '2026-09-03T08:00:00Z',
    };
    let received: BookMetadata | undefined;

    service.getMetadata('9782070363735').subscribe(value => received = value);

    const request = http.expectOne(`${environment.apiUrl}/books/9782070363735/metadata`);
    expect(request.request.method).toBe('GET');
    request.flush(metadata);

    expect(received).toEqual(metadata);
  });
});
