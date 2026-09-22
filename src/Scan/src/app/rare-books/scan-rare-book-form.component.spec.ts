import {CommonModule} from '@angular/common';
import {ComponentFixture, TestBed} from '@angular/core/testing';
import {FormsModule} from '@angular/forms';
import {ActivatedRoute, Router} from '@angular/router';
import {of} from 'rxjs';

import {BookMetadataService} from '../scanner/book-metadata.service';
import {ScanStatusBarComponent} from '../offline/scan-status-bar.component';
import {ScanRareBookService} from './scan-rare-book.service';
import {ScanRareBookFormComponent} from './scan-rare-book-form.component';

describe('ScanRareBookFormComponent', () => {
  let fixture: ComponentFixture<ScanRareBookFormComponent>;
  let component: ScanRareBookFormComponent;
  let rareBooks: jasmine.SpyObj<ScanRareBookService>;
  let metadata: jasmine.SpyObj<BookMetadataService>;

  beforeEach(() => {
    rareBooks = jasmine.createSpyObj<ScanRareBookService>('ScanRareBookService', [
      'get', 'createDraft', 'saveDraft', 'queuePhoto', 'pendingPhotoCount',
    ]);
    metadata = jasmine.createSpyObj<BookMetadataService>('BookMetadataService', ['getMetadata']);
    rareBooks.pendingPhotoCount.and.resolveTo(0);
    metadata.getMetadata.and.returnValue(of({
      isbn13: '9782070363735',
      title: 'Le Petit Prince',
      authors: 'Antoine de Saint-Exupéry',
      publisher: 'Gallimard',
      publicationYear: 1946,
      coverUrl: null,
      source: 'BNF',
      workId: null,
      retrievedAt: '2026-09-17T10:00:00.000Z',
    }));

    TestBed.configureTestingModule({
      declarations: [ScanRareBookFormComponent, ScanStatusBarComponent],
      imports: [CommonModule, FormsModule],
      providers: [
        {provide: ScanRareBookService, useValue: rareBooks},
        {provide: BookMetadataService, useValue: metadata},
        {provide: Router, useValue: jasmine.createSpyObj<Router>('Router', ['navigate'])},
        {provide: ActivatedRoute, useValue: {snapshot: {paramMap: {get: () => null}}}},
      ],
    });
    fixture = TestBed.createComponent(ScanRareBookFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('looks up an ISBN candidate and uses it to prefill a draft without publishing it', async () => {
    component.startIsbn();
    component.isbn = '9782070363735';

    await component.lookupCandidate();
    component.useCandidate();

    expect(metadata.getMetadata).toHaveBeenCalledWith('9782070363735');
    expect(component.form.title).toBe('Le Petit Prince');
    expect(component.form.authorMention).toBe('Antoine de Saint-Exupéry');
    expect(component.form.isbn13).toBe('9782070363735');
    expect(component.step).toBe('identify');
    expect(rareBooks.createDraft).not.toHaveBeenCalled();
  });

  it('creates a Draft before a photo is queued and keeps the price display-only', async () => {
    component.startNoIsbn();
    const draft = {
      clientId: 'client-1',
      serverId: null,
      clientGestureId: 'client-1',
      isbn13: null,
      title: 'Les Fables',
      authorMention: null,
      publisher: null,
      publicationYear: null,
      shelf: 'Éditions anciennes',
      price: 60,
      condition: 'GoodWithFlaws',
      publicDescription: null,
      binding: null,
      dimensions: null,
      pageCount: null,
      shelfLocation: null,
      priceSetBy: null,
      status: 'Draft' as const,
      isSold: false,
      thumbnail: null,
      updatedAt: '2026-09-17T10:00:00.000Z',
      rowVersion: null,
      syncStatus: 'PendingCreate' as const,
      lastError: null,
    };
    rareBooks.createDraft.and.resolveTo(draft);

    await component.saveAndContinue();

    expect(rareBooks.createDraft).toHaveBeenCalledWith(jasmine.objectContaining({
      title: 'Livre à renseigner',
      price: 0,
    }));
    expect(component.book?.status).toBe('Draft');
    expect(component.step).toBe('describe');
  });

  it('presents the new-book choice with the two mockup paths', () => {
    expect(fixture.nativeElement.querySelector('.rare-flow-header')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Ce livre a-t-il un code-barres');

    const choices = fixture.nativeElement.querySelectorAll('.rare-choice');
    expect(choices.length).toBe(2);
    expect(choices[0].textContent).toContain('Scanner l’ISBN');
    expect(choices[1].textContent).toContain('Ce livre n’a pas d’ISBN');
  });
});
