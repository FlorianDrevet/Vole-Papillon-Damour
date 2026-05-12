import { Component } from '@angular/core';

@Component({
  selector: 'app-how-to-help',
  templateUrl: './how-to-help.component.html',
  styleUrl: './how-to-help.component.scss'
})
export class HowToHelpComponent {
  readonly listPictures: string[] = [
    "images/Association/don-livre.jpg",
    "images/Association/don-livre2.jpg",
    "images/Association/don-livre3.jpg",
    "images/Association/don-livre4.jpg",
    "images/Association/don-livre6.jpg",
    "images/Association/don-livre7.jpg",
    "images/Association/don-livre8.jpg",
    "images/Association/don-dvd.jpg",
  ]
}
