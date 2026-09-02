import {Component, inject, OnInit, signal} from '@angular/core';
import {Router} from "@angular/router";
import {NavigationItemInterface} from "../../../shared/interfaces/navigationItem.interface";

@Component({
    selector: 'app-navigation',
    templateUrl: './navigation.component.html',
    styleUrl: './navigation.component.scss',
    standalone: false
})
export class NavigationComponent implements OnInit {
  router = inject(Router)

  url = signal<string>('');
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
      this.url.set(this.router.url);
    });
  }
}
