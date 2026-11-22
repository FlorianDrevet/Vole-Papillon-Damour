import {provideHttpClient, withXhr} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';

import {environment} from '../../environments/environment';
import {BookPhoto} from './book-photo.model';
import {BookPhotoService} from './book-photo.service';

describe('BookPhotoService', () => {
  let service: BookPhotoService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        BookPhotoService,
        provideHttpClient(withXhr()),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(BookPhotoService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads photos for the scanned ISBN', () => {
    const photos: BookPhoto[] = [{
      id: 'photo-1',
      url: 'https://images.example.test/book.jpg',
      position: 0,
      contentType: 'image/jpeg',
    }];
    let received: BookPhoto[] | undefined;

    service.getPhotos('9782070363735').subscribe(value => received = value);

    const request = http.expectOne(`${environment.apiUrl}/books/9782070363735/photos`);
    expect(request.request.method).toBe('GET');
    request.flush(photos);

    expect(received).toEqual(photos);
  });

  it('uploads a photo as multipart form data for the scanned ISBN', () => {
    const file = new File(['book photo'], 'book.jpg', {type: 'image/jpeg'});
    const photo: BookPhoto = {
      id: 'photo-1',
      url: 'https://images.example.test/book.jpg',
      position: 0,
      contentType: 'image/jpeg',
    };
    let received: BookPhoto | undefined;

    service.uploadPhoto('9782070363735', file).subscribe(value => received = value);

    const request = http.expectOne(`${environment.apiUrl}/books/9782070363735/photos`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body instanceof FormData).toBeTrue();
    const uploadedFile = (request.request.body as FormData).get('File') as File;
    expect(uploadedFile.name).toBe(file.name);
    expect(uploadedFile.type).toBe(file.type);
    expect(uploadedFile.size).toBe(file.size);
    request.flush(photo);

    expect(received).toEqual(photo);
  });
});
