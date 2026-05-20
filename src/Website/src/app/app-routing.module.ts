import {NgModule} from '@angular/core';
import {RouterModule, Routes} from '@angular/router';
import {HomeComponent} from "./feature/home/home.component";
import {HistoryComponent} from "./feature/maxence/history/history.component";
import {ActualityPageComponent} from "./feature/actuality-page/actuality-page.component";
import {ActualityDetailComponent} from "./feature/actuality-detail/actuality-detail.component";
import {PresentationComponent} from "./feature/association/presentation/presentation.component";
import {HowToHelpComponent} from "./feature/association/how-to-help/how-to-help.component";
import {NewspapersComponent} from "./feature/association/newspapers/newspapers.component";
import {PicturesComponent} from "./feature/association/pictures/pictures.component";
import {VpdEventsPageComponent} from "./feature/vpd-events/vpd-events-page.component";
import {EventDetailComponent} from "./feature/event-detail/event-detail.component";
import {GastrostomyComponent} from "./feature/maxence/diseases/gastrostomy/gastrostomy.component";
import {HirschsprungComponent} from "./feature/maxence/diseases/hirschsprung/hirschsprung.component";
import {
  WolffParkinsonWhiteComponent
} from "./feature/maxence/diseases/wolff-parkinson-white/wolff-parkinson-white.component";
import {
  DyslasieEctodermiqueComponent
} from "./feature/maxence/diseases/dyslasie-ectodermique/dyslasie-ectodermique.component";
import {NeuropathieComponent} from "./feature/maxence/diseases/neuropathie/neuropathie.component";
import {LosteoporoseComponent} from "./feature/maxence/diseases/losteoporose/losteoporose.component";
import {PoicComponent} from "./feature/maxence/diseases/poic/poic.component";
import {HyperthyroidieComponent} from "./feature/maxence/diseases/hyperthyroidie/hyperthyroidie.component";
import {DailyCareComponent} from "./feature/maxence/daily-life/daily-care/daily-care.component";
import {HospitalCareComponent} from "./feature/maxence/daily-life/hospital-care/hospital-care.component";
import {SchoolComponent} from "./feature/maxence/daily-life/school/school.component";
import {MisfortuneComponent} from "./feature/maxence/daily-life/misfortune/misfortune.component";
import {OrgansTransplantComponent} from "./feature/maxence/daily-life/organs-transplant/organs-transplant.component";
import {TableauComponent} from "./feature/tableau/tableau.component";
import {VpdAllEventsComponent} from "./feature/vpd-all-events/vpd-all-events.component";
import {LegalPageComponent} from "./feature/legal/legal-page.component";
import {LEGAL_PAGE_PATHS} from "./feature/legal/legal-page-paths";

const routes: Routes = [
  {
    path: 'accueil',
    component: HomeComponent
  },
  {
    path: 'association',
    children: [
      {
        path: '',
        redirectTo: 'presentation',
        pathMatch: 'full'
      },
      {
        path: 'presentation',
        component: PresentationComponent,
        pathMatch: 'full'
      },
      {
        path: 'comment-aider',
        component: HowToHelpComponent,
        pathMatch: 'full'
      },
      {
        path: 'revue-de-presses',
        component: NewspapersComponent,
        pathMatch: 'full'
      },
      {
        path: 'photos',
        component: PicturesComponent,
        pathMatch: 'full'
      }
    ]
  },
  {
    path: 'toute-l-actualite',
    component: ActualityPageComponent
  },
  {
    path: 'actualite/:id',
    component: ActualityDetailComponent
  },
  {
    path: 'evenement',
    component: VpdEventsPageComponent
  },
  {
    path: 'evenement/all',
    component: VpdAllEventsComponent
  },
  {
    path: 'evenement/:id',
    component: EventDetailComponent
  },
  {
    path: 'evenement/:id/tableau',
    component: TableauComponent
  },
  {
    path: 'maxence',
    children: [
      {
        path: '',
        redirectTo: 'histoire',
        pathMatch: 'full'
      },
      {
        path: 'histoire',
        component: HistoryComponent,
        pathMatch: 'full'
      },
      {
        path: 'maladies',
        children: [
          {
            path: '',
            redirectTo: 'hirschsprung',
            pathMatch: 'full'
          },
          {
            path: 'gastrostomie',
            component: GastrostomyComponent,
            pathMatch: 'full'
          },
          {
            path: 'hirschsprung',
            component: HirschsprungComponent,
            pathMatch: 'full'
          },
          {
            path: 'wolff-parkinson-white',
            component: WolffParkinsonWhiteComponent,
            pathMatch: 'full'
          },
          {
            path: 'dysplasie-ectodermique',
            component: DyslasieEctodermiqueComponent,
            pathMatch: 'full'
          },
          {
            path: 'neuropathie',
            component: NeuropathieComponent,
            pathMatch: 'full'
          },
          {
            path: 'ostéoporose',
            component: LosteoporoseComponent,
            pathMatch: 'full'
          },
          {
            path: 'poic',
            component: PoicComponent,
            pathMatch: 'full'
          },
          {
            path: 'hyperthyroidie',
            component: HyperthyroidieComponent,
            pathMatch: 'full'
          },
        ]
      },
      {
        path: 'vie-quotidienne',
        children: [
          {
            path: '',
            redirectTo: 'soins-quotidiens',
            pathMatch: 'full'
          },
          {
            path: 'soins-quotidiens',
            component: DailyCareComponent,
            pathMatch: 'full'
          },
          {
            path: 'soins-hospitaliers',
            component: HospitalCareComponent,
            pathMatch: 'full'
          },
          {
            path: 'ecole',
            component: SchoolComponent,
            pathMatch: 'full'
          },
          {
            path: 'malchance',
            component: MisfortuneComponent,
            pathMatch: 'full'
          },
          {
            path: 'greffe',
            component: OrgansTransplantComponent,
            pathMatch: 'full'
          },
        ]
      }
    ]
  },
  {
    path: LEGAL_PAGE_PATHS.mentionsLegales,
    component: LegalPageComponent,
    title: "Mentions légales | Vole Papillon d'Amour",
    data: {
      legalPagePath: LEGAL_PAGE_PATHS.mentionsLegales,
      legalFooterLabel: 'Mentions légales'
    }
  },
  {
    path: LEGAL_PAGE_PATHS.politiqueConfidentialite,
    component: LegalPageComponent,
    title: "Politique de confidentialité | Vole Papillon d'Amour",
    data: {
      legalPagePath: LEGAL_PAGE_PATHS.politiqueConfidentialite,
      legalFooterLabel: 'Confidentialité'
    }
  },
  {
    path: LEGAL_PAGE_PATHS.politiqueCookies,
    component: LegalPageComponent,
    title: "Politique de cookies | Vole Papillon d'Amour",
    data: {
      legalPagePath: LEGAL_PAGE_PATHS.politiqueCookies,
      legalFooterLabel: 'Cookies'
    }
  },
  {
    path: LEGAL_PAGE_PATHS.accessibilite,
    component: LegalPageComponent,
    title: "Accessibilité | Vole Papillon d'Amour",
    data: {
      legalPagePath: LEGAL_PAGE_PATHS.accessibilite,
      legalFooterLabel: 'Accessibilité',
      legalFooterStatus: 'non conforme'
    }
  },

  { path: '**', redirectTo: '/accueil', pathMatch: 'full' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
