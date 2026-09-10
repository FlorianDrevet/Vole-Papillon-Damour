import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { findPhotoAlbum, PHOTO_ALBUMS_ROUTE, PhotoAlbum } from '../photo-album-catalog';

@Component({
  selector: 'app-photo-album-page',
  templateUrl: './photo-album-page.component.html',
  standalone: false,
})
export class PhotoAlbumPageComponent {
  private readonly route = inject(ActivatedRoute);

  readonly albumsRoute = PHOTO_ALBUMS_ROUTE;
  readonly album: PhotoAlbum | undefined = findPhotoAlbum(this.route.snapshot.paramMap.get('albumSlug'));
}
