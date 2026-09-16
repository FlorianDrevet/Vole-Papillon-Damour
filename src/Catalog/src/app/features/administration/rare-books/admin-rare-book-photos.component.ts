import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  signal,
} from '@angular/core';

import {CatalogAdminRareBookPhoto} from '../../../core/catalog.models';

export type RareBookPhotoAction =
  | {kind: 'add'; file: File; caption: string}
  | {kind: 'delete'; photoId: string}
  | {kind: 'reorder'; photoIds: string[]}
  | {kind: 'caption'; photoId: string; caption: string};

@Component({
  selector: 'app-admin-rare-book-photos',
  standalone: false,
  templateUrl: './admin-rare-book-photos.component.html',
  styleUrls: ['./admin-rare-book-photos.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminRareBookPhotosComponent {
  @Input() photos: CatalogAdminRareBookPhoto[] = [];
  @Input() disabled = false;
  @Output() action = new EventEmitter<RareBookPhotoAction>();

  readonly uploadError = signal<string | null>(null);
  readonly selectedPhotoId = signal<string | null>(null);
  uploadCaption = '';
  private draggedPhotoId: string | null = null;

  selectPhoto(photoId: string): void {
    this.selectedPhotoId.set(photoId);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      this.uploadError.set('Choisissez une photo JPEG, WebP ou PNG.');
      return;
    }
    if (file.size > 8 * 1024 * 1024) {
      this.uploadError.set('La photo ne peut pas dépasser 8 Mo.');
      return;
    }

    this.uploadError.set(null);
    this.action.emit({kind: 'add', file, caption: this.uploadCaption.trim()});
    this.uploadCaption = '';
  }

  deleteSelected(): void {
    const photoId = this.selectedPhotoId();
    if (!photoId || this.disabled) {
      return;
    }
    this.action.emit({kind: 'delete', photoId});
    this.selectedPhotoId.set(null);
  }

  moveSelected(delta: -1 | 1): void {
    const selectedPhotoId = this.selectedPhotoId();
    if (!selectedPhotoId || this.disabled) {
      return;
    }
    const ids = this.photos.slice().sort((a, b) => a.position - b.position).map(photo => photo.id);
    const index = ids.indexOf(selectedPhotoId);
    const nextIndex = index + delta;
    if (index < 0 || nextIndex < 0 || nextIndex >= ids.length) {
      return;
    }

    [ids[index], ids[nextIndex]] = [ids[nextIndex], ids[index]];
    this.action.emit({kind: 'reorder', photoIds: ids});
  }

  updateCaption(photoId: string, event: Event): void {
    if (this.disabled) {
      return;
    }
    const input = event.target as HTMLInputElement;
    this.action.emit({kind: 'caption', photoId, caption: input.value.trim()});
  }

  startDrag(photoId: string): void {
    this.draggedPhotoId = photoId;
    this.selectedPhotoId.set(photoId);
  }

  allowDrop(event: DragEvent): void {
    event.preventDefault();
  }

  dropPhoto(targetPhotoId: string, event: DragEvent): void {
    event.preventDefault();
    const draggedPhotoId = this.draggedPhotoId;
    this.draggedPhotoId = null;
    if (!draggedPhotoId || draggedPhotoId === targetPhotoId || this.disabled) {
      return;
    }

    const ids = this.photos.slice().sort((a, b) => a.position - b.position).map(photo => photo.id);
    const from = ids.indexOf(draggedPhotoId);
    const to = ids.indexOf(targetPhotoId);
    if (from < 0 || to < 0) {
      return;
    }

    ids.splice(from, 1);
    ids.splice(to, 0, draggedPhotoId);
    this.action.emit({kind: 'reorder', photoIds: ids});
  }

  totalBytes(): number {
    return this.photos.reduce((total, photo) => total + photo.sizeBytes, 0);
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} o`;
    }
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toLocaleString('fr-FR', {maximumFractionDigits: 1})} Ko`;
    }
    return `${(bytes / (1024 * 1024)).toLocaleString('fr-FR', {maximumFractionDigits: 1})} Mo`;
  }
}
