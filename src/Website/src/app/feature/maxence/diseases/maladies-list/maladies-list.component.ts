import { Component } from '@angular/core';

interface DiseaseCard {
  code: string;
  category: string;
  name: string;
  description: string;
  route: string;
}

@Component({
  selector: 'app-maladies-list',
  templateUrl: './maladies-list.component.html',
  standalone: false,
})
export class MaladiesListComponent {
  readonly diseases: DiseaseCard[] = [
    { code: '01', category: 'Intestin', name: 'Maladie de Hirschsprung', description: 'Absence de cellules nerveuses sur une partie de l’intestin : le transit ne se fait pas seul.', route: '/maxence/maladies/hirschsprung' },
    { code: '02', category: 'Digestif', name: 'P.O.I.C.', description: 'Pseudo-obstruction intestinale chronique : l’intestin se comporte comme s’il était bouché.', route: '/maxence/maladies/poic' },
    { code: '03', category: 'Peau et dents', name: 'Dysplasie ectodermique', description: 'Peau, cheveux, dents et glandes sudoripares se développent mal ; la chaleur devient dangereuse.', route: '/maxence/maladies/dysplasie-ectodermique' },
    { code: '04', category: 'Nerfs', name: 'Neuropathie', description: 'Atteinte des nerfs périphériques : douleurs, faiblesse musculaire, sensibilité modifiée.', route: '/maxence/maladies/neuropathie' },
    { code: '05', category: 'Os', name: 'Ostéoporose', description: 'Des os fragiles qui se fracturent pour un choc minime, et un besoin permanent de prudence.', route: '/maxence/maladies/osteoporose' },
    { code: '06', category: 'Thyroïde', name: 'Hyperthyroïdie', description: 'Une thyroïde qui s’emballe : cœur rapide, fatigue, perte de poids, traitement à vie.', route: '/maxence/maladies/hyperthyroidie' },
    { code: '07', category: 'Cœur', name: 'Wolff-Parkinson-White', description: 'Un circuit électrique en trop dans le cœur, qui provoque des accès de tachycardie.', route: '/maxence/maladies/wolff-parkinson-white' },
  ];
}
