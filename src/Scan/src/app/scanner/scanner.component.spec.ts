import {FormsModule} from '@angular/forms';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {of, Subject} from 'rxjs';

import {DesignSystemModule} from '@vpd/ui';
import {BookMetadata} from './book-metadata.model';
import {BookMetadataService} from './book-metadata.service';
import {CameraScannerService} from './camera-scanner.service';
import {ScannerComponent} from './scanner.component';

describe('ScannerComponent', () => {
  let fixture: ComponentFixture<ScannerComponent>;
  let component: ScannerComponent;
  let metadataService: jasmine.SpyObj<BookMetadataService>;
  let cameraService: jasmine.SpyObj<CameraScannerService>;

  beforeEach(async () => {
    metadataService = jasmine.createSpyObj<BookMetadataService>('BookMetadataService', ['getMetadata']);
    cameraService = jasmine.createSpyObj<CameraScannerService>('CameraScannerService', ['start']);

    await TestBed.configureTestingModule({
      declarations: [ScannerComponent],
      imports: [FormsModule, DesignSystemModule],
      providers: [
        {provide: BookMetadataService, useValue: metadataService},
        {provide: CameraScannerService, useValue: cameraService},
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ScannerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('normalizes a valid ISBN before requesting metadata', async () => {
    const metadata = createMetadata();
    metadataService.getMetadata.and.returnValue(of(metadata));
    component.isbnInput = '0-306-40615-2';

    await component.submit();

    expect(metadataService.getMetadata).toHaveBeenCalledOnceWith('9780306406157');
    expect(component.metadata).toEqual(metadata);
    expect(component.errorMessage).toBeNull();
  });

  it('rejects an invalid ISBN without making a request', async () => {
    component.isbnInput = '4006381333931';

    await component.submit();

    expect(metadataService.getMetadata).not.toHaveBeenCalled();
    expect(component.metadata).toBeNull();
    expect(component.errorMessage).toContain('ISBN');
  });

  it('ignores metadata that belongs to an older scan', async () => {
    const firstResponse = new Subject<BookMetadata>();
    const firstMetadata = createMetadata('Premier livre');
    const secondMetadata = createMetadata('Livre courant');
    metadataService.getMetadata.and.returnValues(firstResponse.asObservable(), of(secondMetadata));

    const firstLookup = component.lookup('9780306406157');
    await component.lookup('9782070363735');
    firstResponse.next(firstMetadata);
    firstResponse.complete();
    await firstLookup;

    expect(component.metadata).toEqual(secondMetadata);
  });

  function createMetadata(title = 'Le Petit Prince'): BookMetadata {
    return {
      isbn13: '9782070363735',
      title,
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1946,
      coverUrl: null,
      source: 'BnF',
      workId: null,
      retrievedAt: '2026-09-03T08:00:00Z',
    };
  }
});
