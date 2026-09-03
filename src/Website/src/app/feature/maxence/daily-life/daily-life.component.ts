import { Component } from '@angular/core';

interface DailyLifeSection {
  id: string;
  number: string;
  kicker: string;
  title: string;
  summary: string;
  route: string;
  cta: string;
}

@Component({
  selector: 'app-daily-life',
  templateUrl: './daily-life.component.html',
  styleUrl: './daily-life.component.scss',
  standalone: false
})
export class DailyLifeComponent {
  readonly sections: readonly DailyLifeSection[] = [
    {
      id: 'soins-quotidiens',
      number: '01',
      kicker: 'À la maison',
      title: 'Les soins quotidiens',
      summary: "Alimentation, branchements, gastrostomie, stomie et soins de l'œil : les gestes qui rythment les journées de Maxence.",
      route: '/maxence/vie-quotidienne/soins-quotidiens',
      cta: 'Lire le récit des soins',
    },
    {
      id: 'soins-hospitaliers',
      number: '02',
      kicker: "À l'hôpital",
      title: 'Les soins hospitaliers',
      summary: "Consultations, hospitalisations, examens et douleur : un parcours médical raconté de l'enfance à l'âge adulte.",
      route: '/maxence/vie-quotidienne/soins-hospitaliers',
      cta: 'Découvrir son parcours',
    },
    {
      id: 'ecole',
      number: '03',
      kicker: "À l'école",
      title: "L'école",
      summary: "De la maternelle au lycée professionnel, une scolarité construite avec les absences, les adaptations et l'envie d'apprendre.",
      route: '/maxence/vie-quotidienne/ecole',
      cta: 'Lire le récit de sa scolarité',
    },
    {
      id: 'greffe',
      number: '04',
      kicker: 'Un espoir',
      title: 'La greffe',
      summary: "Le projet de greffe viscérale, l'attente, les examens et ce que sa maladie a finalement rendu impossible.",
      route: '/maxence/vie-quotidienne/greffe',
      cta: 'Comprendre ce parcours',
    },
  ];
}
