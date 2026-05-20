import {Component, effect, OnInit, Renderer2, signal} from '@angular/core';
import {NavigationItemInterface} from "../../../shared/interfaces/navigationItem.interface";
import { Router } from '@angular/router';

@Component({
    selector: 'app-navigation',
    templateUrl: './navigation.component.html',
    styleUrl: './navigation.component.scss',
    standalone: false
})
export class NavigationComponent implements OnInit{
  Router!: Router
  subNavigation = signal<NavigationItemInterface | null>(null);
  baseUrl = signal<string>('')
  url = signal<string>('');

  constructor(private router: Router) {
    this.Router = router;
  }

  ngOnInit(): void {
    this.router.events.subscribe(() => {
      this._setSubNavigation();
      this.url.set(this.router.url);
    });
  }

  _setSubNavigation() {
    for (let nav of this.navigationUrls){
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

  readonly navigationUrls: NavigationItemInterface[] = [
    {
      url: "/accueil",
      title: "Accueil",
      subNav: []
    },
    {
      url: "/association",
      title: "L'association",
      subNav: [{
        url: "/presentation",
        title: "Qui sommes-nous ?",
        subNav: []
      },
        {
          url: "/comment-aider",
          title: "Comment nous aider ?",
          subNav: []
        },
        {
          url: "/revue-de-presses",
          title: "La presse en parle",
          subNav: []
        },
        {
          url: "/photos",
          title: "Galerie photos",
          subNav: []
        },
      ]
    },
    {
      url: "/maxence",
      title: "Découvrir Maxence",
      subNav: [
        {
          url: '/histoire',
          title: 'Son histoire',
          subNav: []
        },
        {
          url: '/maladies',
          title: 'Ses maladies',
          subNav: [
            {
              url: '/hirschsprung',
              title: 'Hirschsprung',
              subNav: []
            },
            {
              url: '/poic',
              title: 'Le syndrome P.O.I.C.',
              subNav: []
            },
            {
              url: '/wolff-parkinson-white',
              title: 'Wolff Parkinson White',
              subNav: []
            },
            {
              url: '/dysplasie-ectodermique',
              title: 'La dysplasie ectodermique',
              subNav: []
            },
            {
              url: '/neuropathie',
              title: 'Neurophatie',
              subNav: []
            },
            {
              url: '/ostéoporose',
              title: 'L\'ostéoporose',
              subNav: []
            },
            {
              url: '/hyperthyroidie',
              title: 'L\'hyperthyroïdie',
              subNav: []
            },
            {
              url: '/gastrostomie',
              title: 'La gastrostomie',
              subNav: []
            },
          ]
        },
        {
          url: '/vie-quotidienne',
          title: 'Son quotidien, ses combats',
          subNav: [
            {
              url: '/soins-quotidiens',
              title: 'Les soins quotidiens',
              subNav: []
            },
            {
              url: '/soins-hospitaliers',
              title: 'Les soins hospitaliers',
              subNav: []
            },
            {
              url: '/ecole',
              title: 'L\'école',
              subNav: []
            },
            {
              url: '/malchance',
              title: 'La malchance',
              subNav: []
            },
            {
              url: '/greffe',
              title: 'La greffe, un espoir éphémère',
              subNav: []
            },
          ]
        }
      ]
    },
    {
      url: "/evenement",
      title: "Nos évènements",
      subNav: []
    },
    {
      url: "/toute-l-actualite",
      title: "Actualité",
      subNav: []
    },
    {
      url: "/contact",
      title: "Nous Contacter",
      subNav: []
    }
  ];
}
