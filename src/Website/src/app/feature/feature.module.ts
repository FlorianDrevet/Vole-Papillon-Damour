import {NgModule} from '@angular/core';
import {CommonModule, NgOptimizedImage} from '@angular/common';
import {HomeComponent} from './home/home.component';
import {SharedModule} from "../shared/shared.module";
import {HistoryComponent} from './maxence/history/history.component';
import {RouterLink} from "@angular/router";
import {MaxenceIntroComponent} from "./home/components/maxence-intro/maxence-intro.component";
import {ActualityComponent} from './home/components/actuality/actuality.component';
import {VpdEventSections} from './home/components/vpd-events/vpd-event-sections';
import {EventCardComponent} from "../shared/components/event-card/event-card.component";
import {TimeLineComponent} from './maxence/history/components/time-line/time-line.component';
import {HistoryContainerComponent} from './maxence/history/components/history-container/history-container.component';
import {Year2004Component} from './maxence/history/timeline/year-2004/year-2004.component';
import {Year2005Component} from './maxence/history/timeline/year-2005/year-2005.component';
import {Year2006Component} from './maxence/history/timeline/year-2006/year-2006.component';
import {Year2007Component} from './maxence/history/timeline/year-2007/year-2007.component';
import {Year2008Component} from './maxence/history/timeline/year-2008/year-2008.component';
import {Year2009Component} from './maxence/history/timeline/year-2009/year-2009.component';
import {Year2010Component} from './maxence/history/timeline/year-2010/year-2010.component';
import {Year2011Component} from './maxence/history/timeline/year-2011/year-2011.component';
import {Year2012Component} from './maxence/history/timeline/year-2012/year-2012.component';
import {Year2013Component} from './maxence/history/timeline/year-2013/year-2013.component';
import {Year2014Component} from './maxence/history/timeline/year-2014/year-2014.component';
import {Year2015Component} from './maxence/history/timeline/year-2015/year-2015.component';
import {Year2016Component} from './maxence/history/timeline/year-2016/year-2016.component';
import {ActualityPageComponent} from './actuality-page/actuality-page.component';
import {ActualityByMonthComponent} from './actuality-page/components/actuality-by-month/actuality-by-month.component';
import {ActualityDetailComponent} from './actuality-detail/actuality-detail.component';
import {PresentationComponent} from './association/presentation/presentation.component';
import {HowToHelpComponent} from './association/how-to-help/how-to-help.component';
import {NewspapersComponent} from './association/newspapers/newspapers.component';
import {PicturesComponent} from './association/pictures/pictures.component';
import {SpecialEventComponent} from './vpd-events/components/special-event/special-event.component';
import {VpdEventsPageComponent} from "./vpd-events/vpd-events-page.component";
import {EventDetailComponent} from './event-detail/event-detail.component';
import {BingoEventComponent} from './event-detail/components/bingo-event/bingo-event.component';
import {BooksEventComponent} from './event-detail/components/books-event/books-event.component';
import {OtherEventComponent} from './event-detail/components/other-event/other-event.component';
import {SectionInfosEventComponent} from './event-detail/components/section-infos-event/section-infos-event.component';
import { GeneralInfosComponent } from './event-detail/components/general-infos/general-infos.component';
import { GastrostomyComponent } from './maxence/diseases/gastrostomy/gastrostomy.component';
import { HirschsprungComponent } from './maxence/diseases/hirschsprung/hirschsprung.component';
import { WolffParkinsonWhiteComponent } from './maxence/diseases/wolff-parkinson-white/wolff-parkinson-white.component';
import { DyslasieEctodermiqueComponent } from './maxence/diseases/dyslasie-ectodermique/dyslasie-ectodermique.component';
import { NeuropathieComponent } from './maxence/diseases/neuropathie/neuropathie.component';
import { LosteoporoseComponent } from './maxence/diseases/losteoporose/losteoporose.component';
import { PoicComponent } from './maxence/diseases/poic/poic.component';
import { HyperthyroidieComponent } from './maxence/diseases/hyperthyroidie/hyperthyroidie.component';
import { DailyCareComponent } from './maxence/daily-life/daily-care/daily-care.component';
import { HospitalCareComponent } from './maxence/daily-life/hospital-care/hospital-care.component';
import { SchoolComponent } from './maxence/daily-life/school/school.component';
import { OrgansTransplantComponent } from './maxence/daily-life/organs-transplant/organs-transplant.component';
import { ContactComponent } from './contact/contact.component';
import {CoreModule} from "../core/core.module";
import { PartieCardComponent } from './event-detail/components/bingo-event/components/partie-card/partie-card.component';
import { LotCardComponent } from './event-detail/components/bingo-event/components/lot-card/lot-card.component';
import {TableauComponent} from "./tableau/tableau.component";
import {ModalComponent} from "./tableau/components/modal-tableau/modal.component";
import {TagComponent} from "./tableau/components/tag/tag.component";
import {NumberComponent} from "./tableau/components/number/number.component";
import {VpdAllEventsComponent} from "./vpd-all-events/vpd-all-events.component";
import {MaladiesListComponent} from "./maxence/diseases/maladies-list/maladies-list.component";
import {ActionsComponent} from "./actions/actions.component";


@NgModule({
  declarations: [
    VpdEventsPageComponent,
    HomeComponent,
    HistoryComponent,
    MaxenceIntroComponent,
    ActualityComponent,
    VpdEventSections,
    EventCardComponent,
    TimeLineComponent,
    HistoryContainerComponent,
    Year2004Component,
    Year2005Component,
    Year2006Component,
    Year2007Component,
    Year2008Component,
    Year2009Component,
    Year2010Component,
    Year2011Component,
    Year2012Component,
    Year2013Component,
    Year2014Component,
    Year2015Component,
    Year2016Component,
    ActualityPageComponent,
    ActualityByMonthComponent,
    ActualityDetailComponent,
    PresentationComponent,
    HowToHelpComponent,
    NewspapersComponent,
    PicturesComponent,
    SpecialEventComponent,
    EventDetailComponent,
    BingoEventComponent,
    BooksEventComponent,
    OtherEventComponent,
    SectionInfosEventComponent,
    GeneralInfosComponent,
    GastrostomyComponent,
    HirschsprungComponent,
    WolffParkinsonWhiteComponent,
    DyslasieEctodermiqueComponent,
    NeuropathieComponent,
    LosteoporoseComponent,
    PoicComponent,
    HyperthyroidieComponent,
    DailyCareComponent,
    HospitalCareComponent,
    SchoolComponent,
    OrgansTransplantComponent,
    ContactComponent,
    MaladiesListComponent,
    ActionsComponent,
    PartieCardComponent,
    LotCardComponent,
    TableauComponent,
    ModalComponent,
    TagComponent,
    NumberComponent,
    VpdAllEventsComponent,
  ],
  imports: [
    CommonModule,
    NgOptimizedImage,
    SharedModule,
    RouterLink,
    CoreModule
  ]
})
export class FeatureModule { }
