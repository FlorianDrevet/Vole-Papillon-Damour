import { Component } from '@angular/core';

@Component({
    selector: 'app-pictures',
    templateUrl: './pictures.component.html',
    standalone: false
})
export class PicturesComponent {
  readonly donationPhotos: string[] = [
    'images/Association/don-livre.jpg',
    'images/Association/don-livre2.jpg',
    'images/Association/don-livre3.jpg',
    'images/Association/don-livre4.jpg',
    'images/Association/don-livre5.jpg',
    'images/Association/don-livre6.jpg',
    'images/Association/don-livre7.jpg',
    'images/Association/don-livre8.jpg',
    'images/Association/don-dvd.jpg',
  ];

  readonly upcomingAlbums = ['Loto solidaire', 'Foire aux livres', 'Remises de matériel'];
}
