import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import {
  afterNextRender,
  Component,
  ElementRef,
  inject,
  Injector,
  OnDestroy,
  PLATFORM_ID,
  Renderer2,
  signal,
  ViewChild,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { findPhotoAlbum, PHOTO_ALBUMS_ROUTE, PhotoAlbum } from '../photo-album-catalog';

@Component({
  selector: 'app-photo-album-page',
  templateUrl: './photo-album-page.component.html',
  styleUrl: './photo-album-page.component.scss',
  standalone: false,
})
export class PhotoAlbumPageComponent implements OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly document = inject(DOCUMENT);
  private readonly renderer = inject(Renderer2);
  private readonly injector = inject(Injector);

  @ViewChild('photoDialog') private photoDialog?: ElementRef<HTMLDialogElement>;

  readonly albumsRoute = PHOTO_ALBUMS_ROUTE;
  readonly album: PhotoAlbum | undefined = findPhotoAlbum(this.route.snapshot.paramMap.get('albumSlug'));
  readonly selectedPhotoIndex = signal<number | null>(null);

  ngOnDestroy(): void {
    if (this.photoDialog?.nativeElement.open) this.photoDialog.nativeElement.close();
    this.restoreScrollLock();
  }

  openPhoto(index: number, trigger: HTMLButtonElement): void {
    if (!this.album || !isPlatformBrowser(this.platformId) || !this.photoDialog) return;

    this.selectedPhotoIndex.set(index);
    this.photoDialog.nativeElement.showModal();
    this.renderer.addClass(this.document.body, 'no-scroll');
    this.previousTrigger = trigger;
    afterNextRender(() => {
      this.photoDialog?.nativeElement.querySelector<HTMLButtonElement>('[data-photo-close]')?.focus();
    }, { injector: this.injector });
  }

  closePhoto(): void {
    const dialog = this.photoDialog?.nativeElement;
    if (dialog?.open) dialog.close();

    this.selectedPhotoIndex.set(null);
    this.restorePageFocus();
  }

  onPhotoDialogClick(event: MouseEvent): void {
    if (event.target === this.photoDialog?.nativeElement) this.closePhoto();
  }

  onPhotoDialogKeydown(event: KeyboardEvent): void {
    switch (event.key) {
      case 'Escape':
        event.preventDefault();
        this.closePhoto();
        break;
      case 'ArrowLeft':
        event.preventDefault();
        this.navigatePhoto(-1);
        break;
      case 'ArrowRight':
        event.preventDefault();
        this.navigatePhoto(1);
        break;
    }
  }

  onPhotoDialogClosed(): void {
    this.selectedPhotoIndex.set(null);
    this.restorePageFocus();
  }

  previousPhoto(): void {
    this.navigatePhoto(-1);
  }

  nextPhoto(): void {
    this.navigatePhoto(1);
  }

  private previousTrigger?: HTMLButtonElement;

  private navigatePhoto(offset: -1 | 1): void {
    const currentIndex = this.selectedPhotoIndex();
    if (!this.album || currentIndex === null) return;

    const nextIndex = currentIndex + offset;
    if (nextIndex < 0 || nextIndex >= this.album.photos.length) return;

    this.selectedPhotoIndex.set(nextIndex);
  }

  private restorePageFocus(): void {
    this.restoreScrollLock();
    this.previousTrigger?.focus();
    this.previousTrigger = undefined;
  }

  private restoreScrollLock(): void {
    if (isPlatformBrowser(this.platformId)) this.renderer.removeClass(this.document.body, 'no-scroll');
  }
}
