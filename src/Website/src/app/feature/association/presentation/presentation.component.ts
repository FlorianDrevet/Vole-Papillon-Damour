import { Component } from '@angular/core';

@Component({
    selector: 'app-presentation',
    templateUrl: './presentation.component.html',
    standalone: false
})
export class PresentationComponent {
  /**
   * Bureau élu en assemblée générale. La maquette 7a montre quatre portraits
   * nommés ; les noms réels n'ayant pas encore été communiqués, on conserve la
   * mise en page définitive avec des emplacements de substitution.
   */
  readonly board = [
    { name: 'Nom à compléter', role: 'Présidente · maman de Maxence' },
    { name: 'Nom à compléter', role: 'Trésorier · papa de Maxence' },
    { name: 'Nom à compléter', role: 'Secrétaire' },
    { name: 'Nom à compléter', role: 'Foire aux livres' },
  ];

  /** `meta` porte le poids du PDF une fois le document déposé (cf. maquette : « PDF · 180 Ko »). */
  readonly documents = [
    { label: 'Statuts de l’association', meta: 'Document à ajouter' },
    { label: 'Rapport d’activité 2024', meta: 'Document à ajouter' },
    { label: 'Récépissé de déclaration en préfecture', meta: 'Document à ajouter' },
  ];
}
