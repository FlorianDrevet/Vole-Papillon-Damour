import { Component } from '@angular/core';

@Component({
    selector: 'app-how-to-help',
    templateUrl: './how-to-help.component.html',
    standalone: false
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
  ];

  readonly accepted = [
    'Romans, policiers, bandes dessinées, mangas',
    'Albums jeunesse',
    'Beaux livres, cuisine, jardinage, régionalisme',
    'Vinyles, CD, DVD et jeux de société complets',
    'Manuels scolaires récents',
  ];

  readonly refused = [
    'Encyclopédies et dictionnaires anciens',
    'Livres humides, moisis ou abîmés',
    'Revues et magazines',
    'VHS, cassettes, CD-ROM',
    'Livres annotés, surlignés ou incomplets',
  ];

  readonly faq = [
    { q: 'Puis-je vous faire un chèque quand même ?', a: 'Contactez-nous directement : nous vous conseillerons selon votre situation.' },
    { q: 'Délivrez-vous un reçu fiscal ?', a: 'À nous demander par écrit - cela dépend du type de soutien.' },
    { q: 'Reprenez-vous des jouets ou des vêtements ?', a: 'Occasionnellement, sur demande - écrivez-nous avant de vous déplacer.' },
    { q: 'Puis-je demander une aide pour mon enfant ?', a: 'Oui, voir la page « Nos actions » pour la marche à suivre.' },
  ];
}
