import {Component, inject, OnInit, signal} from '@angular/core';
import {Router} from "@angular/router";
import {NavigationItemInterface} from "../../../shared/interfaces/navigationItem.interface";

@Component({
  selector: 'app-navigation',
  templateUrl: './navigation.component.html',
  styleUrl: './navigation.component.scss'
})
export class NavigationComponent implements OnInit {
  router = inject(Router)

  url = signal<string>('');
  subNavigation = signal<NavigationItemInterface | null>(null);
  baseUrl = signal<string>('')
  readonly navigationUrls: NavigationItemInterface[] = [
    {
      url: "/actualites",
      title: "L'actualité",
      subNav: []
    },
    {
      url: "/evenements",
      title: "Les événements",
      subNav: []
    },
    {
      url: "/caisse",
      title: "La Caisse",
      subNav: []
    },
  ];

  ngOnInit(): void {
    this.router.events.subscribe(() => {
      this._setSubNavigation();
      this.url.set(this.router.url);
    });
  }

  _setSubNavigation() {
    for (let nav of this.navigationUrls) {
      if (this.router.url.includes(nav.url)) {
        this.subNavigation.set(nav);
        this.baseUrl.set(nav.url);
        break;
      }
    }
  }

  OnNavigationClick(item: NavigationItemInterface) {
    this.router.navigateByUrl(item.url);
    this.baseUrl.set(item.url);
    this.subNavigation.set(item);
  }

  OnSubNavigationClick(url: string) {
    this.router.navigateByUrl(this.baseUrl() + url);
  }
}
