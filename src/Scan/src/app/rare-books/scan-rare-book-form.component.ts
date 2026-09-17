import {Component, OnDestroy, OnInit} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {firstValueFrom} from 'rxjs';

import {BookMetadata} from '../scanner/book-metadata.model';
import {BookMetadataService} from '../scanner/book-metadata.service';
import {normalizeIsbn} from '../scanner/isbn.util';
import {
  ScanRareBook,
  ScanRareBookDraftInput,
  ScanRareBookPhotoQueueEntry,
} from '../offline/scan-offline.model';
import {ScanRareBookService} from './scan-rare-book.service';

export type ScanRareBookFormStep = 'choice' | 'candidate' | 'identify' | 'describe' | 'photos';

@Component({
  selector: 'app-scan-rare-book-form',
  templateUrl: './scan-rare-book-form.component.html',
  styleUrls: ['./scan-rare-book-form.component.scss'],
  standalone: false,
})
export class ScanRareBookFormComponent implements OnInit, OnDestroy {
  readonly visibleSteps: readonly ScanRareBookFormStep[] = ['identify', 'describe', 'photos'];
  readonly shelves = [
    'Éditions anciennes',
    'Illustrés',
    'Beaux-arts',
    'Régionalisme',
  ];
  readonly conditions = [
    {value: 'AsNew', label: 'Comme neuf'},
    {value: 'GoodWithFlaws', label: 'Bon état avec défauts'},
    {value: 'Worn', label: 'Usé'},
    {value: 'Damaged', label: 'Abîmé'},
  ];

  step: ScanRareBookFormStep = 'choice';
  isbn = '';
  form: ScanRareBookDraftInput = emptyDraft();
  candidate: BookMetadata | null = null;
  book: ScanRareBook | null = null;
  photos: ScanRareBookPhotoQueueEntry[] = [];
  pendingPhotoCount = 0;
  candidateLoading = false;
  saving = false;
  error: string | null = null;
  private readonly previewUrls = new Map<string, string>();

  constructor(
    private readonly rareBooks: ScanRareBookService,
    private readonly metadataService: BookMetadataService,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
  ) {}

  async ngOnInit(): Promise<void> {
    const clientId = this.route.snapshot.paramMap.get('id');
    if (!clientId) {
      return;
    }

    this.book = await this.rareBooks.get(clientId);
    if (!this.book) {
      this.error = 'Cette fiche rare n’est plus présente sur cet appareil.';
      return;
    }

    this.form = toDraftInput(this.book);
    this.step = 'identify';
    await this.refreshPhotos();
  }

  ngOnDestroy(): void {
    for (const url of this.previewUrls.values()) {
      URL.revokeObjectURL(url);
    }
  }

  startIsbn(): void {
    this.error = null;
    this.candidate = null;
    this.isbn = this.form.isbn13 ?? '';
    this.step = 'candidate';
  }

  startNoIsbn(): void {
    this.error = null;
    this.candidate = null;
    this.form = {...emptyDraft(), title: 'Livre à renseigner'};
    this.step = 'identify';
  }

  async lookupCandidate(): Promise<void> {
    const normalized = normalizeIsbn(this.isbn);
    if (!normalized) {
      this.error = 'Saisissez un ISBN-10 ou ISBN-13 valide.';
      return;
    }

    this.error = null;
    this.candidateLoading = true;
    try {
      this.candidate = await firstValueFrom(this.metadataService.getMetadata(normalized));
      this.isbn = normalized;
    } catch {
      this.error = 'Aucune notice bibliographique n’a pu être trouvée. Vous pouvez saisir la fiche manuellement.';
      this.candidate = null;
    } finally {
      this.candidateLoading = false;
    }
  }

  useCandidate(): void {
    if (!this.candidate) {
      return;
    }

    this.form = {
      ...this.form,
      isbn13: this.candidate.isbn13,
      title: this.candidate.title ?? 'Titre à vérifier',
      authorMention: this.candidate.authors,
      publisher: this.candidate.publisher,
      publicationYear: this.candidate.publicationYear,
    };
    this.step = 'identify';
    this.error = null;
  }

  useManualEntry(): void {
    this.form = {...this.form, isbn13: normalizeIsbn(this.isbn)};
    this.step = 'identify';
    this.error = null;
  }

  async saveAndContinue(): Promise<void> {
    if (!this.validateIdentification()) {
      return;
    }

    await this.saveDraft();
    if (this.book) {
      this.step = 'describe';
    }
  }

  async saveDescription(): Promise<void> {
    if (!this.validateDescription()) {
      return;
    }

    await this.saveDraft();
    if (this.book) {
      this.step = 'photos';
    }
  }

  async addPhoto(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }

    if (!this.book) {
      await this.saveDraft();
    }
    if (!this.book) {
      return;
    }

    try {
      const position = this.photos.length;
      await this.rareBooks.queuePhoto(this.book.clientId, file, null, position);
      await this.refreshPhotos();
      this.pendingPhotoCount = await this.rareBooks.pendingPhotoCount();
      this.error = null;
    } catch (error: unknown) {
      this.error = error instanceof Error
        ? error.message
        : 'La photo ne peut pas être conservée hors ligne.';
    }
  }

  async removePhoto(photo: ScanRareBookPhotoQueueEntry): Promise<void> {
    await this.rareBooks.deleteQueuedPhoto(photo.queueId);
    await this.refreshPhotos();
    this.pendingPhotoCount = await this.rareBooks.pendingPhotoCount();
  }

  photoPreview(photo: ScanRareBookPhotoQueueEntry): string | null {
    let url = this.previewUrls.get(photo.queueId);
    if (!url) {
      url = URL.createObjectURL(photo.blob);
      this.previewUrls.set(photo.queueId, url);
    }
    return url;
  }

  back(): void {
    if (this.step === 'candidate' || this.step === 'identify') {
      this.step = 'choice';
    } else if (this.step === 'describe') {
      this.step = 'identify';
    } else if (this.step === 'photos') {
      this.step = 'describe';
    }
  }

  close(): void {
    void this.router.navigate(['/livres-rares']);
  }

  stepLabel(step: ScanRareBookFormStep): string {
    return ({
      choice: 'Origine',
      candidate: 'Notice',
      identify: 'Identifier',
      describe: 'Décrire & prix',
      photos: 'Photos',
    } as Record<ScanRareBookFormStep, string>)[step];
  }

  private async saveDraft(): Promise<void> {
    this.saving = true;
    this.error = null;
    try {
      if (!this.book) {
        this.book = await this.rareBooks.createDraft(this.form);
      } else {
        this.book = await this.rareBooks.saveDraft(this.book.clientId, this.form);
      }
      this.pendingPhotoCount = await this.rareBooks.pendingPhotoCount();
    } catch (error: unknown) {
      this.error = error instanceof Error
        ? error.message
        : 'Le brouillon ne peut pas être conservé hors ligne.';
    } finally {
      this.saving = false;
    }
  }

  private async refreshPhotos(): Promise<void> {
    if (!this.book) {
      this.photos = [];
      return;
    }

    const allPhotos = await this.rareBooks.queuedPhotos(this.book.clientId);
    const activeIds = new Set(allPhotos.map(photo => photo.queueId));
    for (const [id, url] of this.previewUrls) {
      if (!activeIds.has(id)) {
        URL.revokeObjectURL(url);
        this.previewUrls.delete(id);
      }
    }
    this.photos = allPhotos;
  }

  private validateIdentification(): boolean {
    if (!this.form.title.trim()) {
      this.error = 'Un titre, même provisoire, est nécessaire.';
      return false;
    }
    if (!this.form.shelf) {
      this.error = 'Choisissez un rayon.';
      return false;
    }
    return true;
  }

  private validateDescription(): boolean {
    if (!Number.isFinite(this.form.price) || this.form.price < 0) {
      this.error = 'Le prix ferme doit être un nombre positif ou nul.';
      return false;
    }
    if (!this.form.condition) {
      this.error = 'Indiquez l’état du livre.';
      return false;
    }
    return true;
  }
}

function emptyDraft(): ScanRareBookDraftInput {
  return {
    title: '',
    authorMention: null,
    publisher: null,
    publicationYear: null,
    shelf: 'Éditions anciennes',
    price: 0,
    condition: 'AsNew',
    publicDescription: null,
    binding: null,
    dimensions: null,
    pageCount: null,
    shelfLocation: null,
    priceSetBy: null,
    isbn13: null,
  };
}

function toDraftInput(book: ScanRareBook): ScanRareBookDraftInput {
  return {
    isbn13: book.isbn13,
    title: book.title,
    authorMention: book.authorMention,
    publisher: book.publisher,
    publicationYear: book.publicationYear,
    shelf: book.shelf,
    price: book.price,
    condition: book.condition,
    publicDescription: book.publicDescription,
    binding: book.binding,
    dimensions: book.dimensions,
    pageCount: book.pageCount,
    shelfLocation: book.shelfLocation,
    priceSetBy: book.priceSetBy,
  };
}
