import { Component } from '@angular/core';

@Component({
    selector: 'app-carousel',
    templateUrl: './carousel.component.html',
    standalone: false
})
export class CarouselComponent {
  readonly partners = [
    { logo: 'images/zone-154.jpg', url: 'https://www.zone154.fr/', alt: 'Zone 154' },
    { logo: 'images/clogan.webp', url: 'https://www.clogane.fr/', alt: 'Clogane' },
    { logo: 'images/collectif-coeur.png', url: 'https://lcdc42.org/', alt: 'Collectif Cœur' },
  ];
}
