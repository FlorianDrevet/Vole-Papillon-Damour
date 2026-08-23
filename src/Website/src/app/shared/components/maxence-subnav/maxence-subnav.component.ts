import { Component, signal } from '@angular/core';
import { Router } from '@angular/router';

interface MaxenceSubnavItem {
  url: string;
  label: string;
  matchPrefix: string;
}

const MAXENCE_SUBNAV_ITEMS: MaxenceSubnavItem[] = [
  { url: '/maxence/histoire', label: 'Son histoire', matchPrefix: '/maxence/histoire' },
  { url: '/maxence/maladies', label: 'Ses maladies', matchPrefix: '/maxence/maladies' },
  { url: '/maxence/vie-quotidienne/soins-quotidiens', label: 'Son quotidien, ses combats', matchPrefix: '/maxence/vie-quotidienne' },
];

/** Barre d'onglets affichée en haut de chaque page de la rubrique Maxence. */
@Component({
  selector: 'app-maxence-subnav',
  templateUrl: './maxence-subnav.component.html',
  standalone: false,
})
export class MaxenceSubnavComponent {
  readonly items = MAXENCE_SUBNAV_ITEMS;
  readonly currentUrl = signal('');

  constructor(private readonly router: Router) {
    this.currentUrl.set(this.router.url);
    this.router.events.subscribe(() => this.currentUrl.set(this.router.url));
  }

  isActive(item: MaxenceSubnavItem): boolean {
    return this.currentUrl().startsWith(item.matchPrefix);
  }
}
