import { Component, OnDestroy, OnInit, signal } from '@angular/core';

@Component({
    selector: 'app-carousel',
    templateUrl: './carousel.component.html',
    styleUrls: ['./carousel.component.scss'],
    standalone: false
})
export class CarouselComponent{
  partners = [
    {logo: 'images/zone-154.jpg', url: 'https://www.zone154.fr/' },
    {logo: 'images/clogan.webp', url: 'https://www.clogane.fr/' },
    {logo: 'images/collectif-coeur.png', url: 'https://lcdc42.org/' },
  ]
}
