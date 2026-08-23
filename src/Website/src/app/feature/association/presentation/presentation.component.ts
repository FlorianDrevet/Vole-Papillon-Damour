import { Component } from '@angular/core';

@Component({
    selector: 'app-presentation',
    templateUrl: './presentation.component.html',
    standalone: false
})
export class PresentationComponent {
  readonly board = [
    { role: 'Présidente', note: 'à compléter' },
    { role: 'Trésorier', note: 'à compléter' },
    { role: 'Secrétaire', note: 'à compléter' },
    { role: 'Bénévole référent·e — foire aux livres', note: 'à compléter' },
  ];

  readonly documents = [
    'Statuts de l’association',
    'Rapport d’activité',
    'Récépissé de déclaration en préfecture',
  ];
}
