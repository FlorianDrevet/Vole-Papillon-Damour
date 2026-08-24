import { Component } from '@angular/core';

@Component({
    selector: 'app-presentation',
    templateUrl: './presentation.component.html',
    standalone: false
})
export class PresentationComponent {
  /**
   * Bureau élu en assemblée générale. Seule la présidente est une donnée publique
   * (annuaire des associations de la mairie de Saint-Just-Saint-Rambert) ; les trois
   * autres postes restent en substitution tant que les noms ne sont pas communiqués.
   */
  readonly board = [
    { name: 'Corinne Drevet', role: 'Présidente · maman de Maxence' },
    { name: 'Nom à compléter', role: 'Trésorier · papa de Maxence' },
    { name: 'Nom à compléter', role: 'Secrétaire' },
    { name: 'Nom à compléter', role: 'Foire aux livres' },
  ];

  /**
   * `meta` porte le poids du PDF une fois le document déposé (cf. maquette : « PDF · 180 Ko »).
   * Les statuts et le récépissé ne sont pas publiés en ligne : il faut les obtenir auprès de
   * l'association ou de la sous-préfecture de Montbrison (déclaration du 15 février 2010,
   * RNA W421002487) avant de pouvoir les mettre en téléchargement ici.
   */
  readonly documents = [
    { label: 'Statuts de l’association', meta: 'Document à ajouter' },
    { label: 'Rapport d’activité 2024', meta: 'Document à ajouter' },
    { label: 'Récépissé de déclaration en préfecture', meta: 'Document à ajouter' },
  ];
}
