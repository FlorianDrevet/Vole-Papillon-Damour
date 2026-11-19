import {HttpErrorResponse} from '@angular/common/http';
import {ChangeDetectionStrategy, ChangeDetectorRef, Component} from '@angular/core';
import {Router} from '@angular/router';

import {ScanAuthService} from '../auth/scan-auth.service';
import {EAN13_MODULE_COUNT, Ean13BarRect, ean13BarRects, isEan13} from '../label-printer/ean13';
import {BookWithoutIsbnService} from './book-without-isbn.service';
import {
  MAX_PHOTOS,
  OrdinaryForm,
  RareForm,
  emptyOrdinaryForm,
  emptyRareForm,
  toOrdinaryPayload,
  toRarePayload,
  validateOrdinaryForm,
  validatePhoto,
  validateRareForm,
} from './book-without-isbn.rules';

export type BookWithoutIsbnKind = 'ordinary' | 'rare';

export interface CreatedBookLabel {
  code: string;
  kind: BookWithoutIsbnKind;
  price: string | null;
}

// E2 (formulaire) et E3 (fiche créée, impression proposée) de la spec. Création en ligne uniquement (RG-180).
@Component({
  selector: 'app-book-without-isbn-page',
  templateUrl: './book-without-isbn-page.component.html',
  styleUrls: ['./book-without-isbn-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  standalone: false,
})
export class BookWithoutIsbnPageComponent {
  readonly conditions = [
    {value: 'AsNew', label: 'Comme neuf'},
    {value: 'GoodWithFlaws', label: 'Bon état avec défauts'},
    {value: 'Worn', label: 'Usé'},
    {value: 'Damaged', label: 'Abîmé'},
  ];
  readonly maxPhotos = MAX_PHOTOS;
  readonly moduleCount = EAN13_MODULE_COUNT;

  kind: BookWithoutIsbnKind = 'ordinary';
  ordinary: OrdinaryForm = emptyOrdinaryForm();
  rare: RareForm = emptyRareForm();
  photos: File[] = [];
  photoError: string | null = null;
  busy = false;
  error: string | null = null;
  created: CreatedBookLabel | null = null;
  bars: Ean13BarRect[] = [];

  constructor(
    private readonly service: BookWithoutIsbnService,
    private readonly auth: ScanAuthService,
    private readonly router: Router,
    private readonly changeDetector: ChangeDetectorRef,
  ) {
    this.kind = this.canCreateOrdinary ? 'ordinary' : 'rare';
  }

  get canCreateOrdinary(): boolean {
    return this.auth.canSort;
  }

  get canCreateRare(): boolean {
    return this.auth.canManageRareBooks;
  }

  chooseKind(kind: BookWithoutIsbnKind): void {
    if ((kind === 'ordinary' && !this.canCreateOrdinary) || (kind === 'rare' && !this.canCreateRare)) {
      return;
    }

    this.kind = kind;
    this.error = null;
    this.changeDetector.markForCheck();
  }

  addPhotos(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    this.photoError = null;
    for (const file of files) {
      if (this.photos.length >= MAX_PHOTOS) {
        this.photoError = `Un livre accepte au plus ${MAX_PHOTOS} photos.`;
        break;
      }

      const problem = validatePhoto(file);
      if (problem !== null) {
        this.photoError = problem;
        continue;
      }

      this.photos = [...this.photos, file];
    }

    this.changeDetector.markForCheck();
  }

  removePhoto(index: number): void {
    this.photos = this.photos.filter((_, position) => position !== index);
    this.changeDetector.markForCheck();
  }

  async submit(): Promise<void> {
    if (this.busy || this.created !== null) {
      return;
    }

    if (!isOnline()) {
      this.error = 'Création impossible sans connexion : une fiche sans ISBN ne se crée pas hors ligne.';
      this.changeDetector.markForCheck();
      return;
    }

    const problem = this.kind === 'ordinary'
      ? validateOrdinaryForm(this.ordinary)
      : validateRareForm(this.rare);
    if (problem !== null) {
      this.error = problem;
      this.changeDetector.markForCheck();
      return;
    }

    this.busy = true;
    this.error = null;
    this.changeDetector.markForCheck();
    try {
      const created = this.kind === 'ordinary'
        ? await this.createOrdinary()
        : await this.createRare();
      this.created = created;
      this.bars = isEan13(created.code) ? ean13BarRects(created.code) : [];
    } catch (error) {
      this.error = messageFor(error);
    } finally {
      this.busy = false;
      this.changeDetector.markForCheck();
    }
  }

  printLabel(): void {
    if (this.created === null) {
      return;
    }

    const queryParams = this.created.price === null
      ? {code: this.created.code}
      : {code: this.created.code, price: this.created.price};
    void this.router.navigate(['/etiquette'], {queryParams});
  }

  later(): void {
    void this.router.navigateByUrl('/accueil');
  }

  createAnother(): void {
    this.created = null;
    this.bars = [];
    this.photos = [];
    this.photoError = null;
    this.error = null;
    this.ordinary = emptyOrdinaryForm();
    this.rare = emptyRareForm();
    this.changeDetector.markForCheck();
  }

  private async createOrdinary(): Promise<CreatedBookLabel> {
    const result = await this.service.createOrdinary(toOrdinaryPayload(this.ordinary));
    await this.uploadPhotos(file => this.service.addOrdinaryPhoto(result.code, file));
    return {code: result.code, kind: 'ordinary', price: null};
  }

  private async createRare(): Promise<CreatedBookLabel> {
    const form = this.rare;
    const result = await this.service.createRare(toRarePayload(form, newClientGestureId()));
    if (result.isbn13 === null) {
      throw new Error('La fiche rare a été créée sans code-barres.');
    }

    await this.uploadPhotos(file => this.service.addRarePhoto(result.id, file));
    const price = toRarePayload(form, '').price.toFixed(2);
    return {code: result.isbn13, kind: 'rare', price: `${price} €`};
  }

  // La fiche existe déjà : un échec de photo n'annule pas la création, il est signalé.
  private async uploadPhotos(upload: (file: File) => Promise<unknown>): Promise<void> {
    for (const file of this.photos) {
      try {
        await upload(file);
      } catch {
        this.photoError = 'La fiche est créée, mais une photo n’a pas pu être envoyée. Ajoutez-la depuis le site.';
        return;
      }
    }
  }
}

function isOnline(): boolean {
  return typeof navigator === 'undefined' || navigator.onLine !== false;
}

function newClientGestureId(): string {
  return globalThis.crypto.randomUUID();
}

function messageFor(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'Connexion impossible : aucune fiche n’est créée.';
    }

    const body = error.error as {error?: string; description?: string} | null;
    return body?.description ?? body?.error ?? `Impossible de créer la fiche (${error.status}).`;
  }

  return error instanceof Error ? error.message : 'Impossible de créer la fiche.';
}