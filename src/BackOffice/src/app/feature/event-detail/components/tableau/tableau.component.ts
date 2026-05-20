import {Component, computed, inject, OnInit, signal} from '@angular/core';
import {ActivatedRoute} from "@angular/router";
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {LotoFacadeService} from "../../../../shared/facades/loto.facade.service";
import {VpdEventsFacadeService} from "../../../../shared/facades/vpd-events.facade.service";
import {environment} from "../../../../../environments/environment";
import {PartieTypeEnum} from "../../../../shared/enums/partieType.enum";
import {NumberLineEnum} from "../../../../shared/enums/numberLine.enum";

@Component({
    selector: 'app-tableau',
    templateUrl: './tableau.component.html',
    styleUrl: './tableau.component.scss',
    standalone: false
})
export class TableauComponent implements OnInit {
  _indexPartie = computed(() => {
    return this.assoEvent()!.parties.findIndex(partie => partie.index === this.assoEvent()?.currentPartieIndex)
  })

  assoEvent = signal<VpdEventModel | null>(null);

  currentPartie = computed(() => {
    return this.assoEvent()?.parties[this._indexPartie()]
  });

  currentLine = computed(() => {
    return this.currentPartie()?.lineParties.find(line => line.index === this.currentPartie()?.currentLineIndex)!
  });

  currentLotsStillWinnable = computed(() => {
      return this.currentLine()?.lots.filter(lot => lot.isWon === null)
    }
  );

  currentLots = computed(() => {
    return this.currentLine()?.lots
  });

  isFinished = signal(false);
  isPaused = signal(false);
  isLastNumero = signal(false);
  lastNumero = signal<number | null>(null);

  eventFacade = inject(VpdEventsFacadeService);
  lotoFacade = inject(LotoFacadeService);
  route = inject(ActivatedRoute);
  protected readonly PartieTypeEnum = PartieTypeEnum;

  ngOnInit(): void {
    this.getAssoEvent()
  }

  getAssoEvent() {
    this.route.paramMap.subscribe(params => {
      if (params.get('id') !== null) {
        this.eventFacade.getEventById$(params.get('id')!).then(response => {
          this.assignNewAssoEvent(response);
        })
      }
    })
  }

  public onNumberClicked(assoEvent: VpdEventModel) {
    this.assignNewAssoEvent(assoEvent)

    this.lastNumero.set(this.currentPartie()?.lastNumeros.slice(-1)[0] ?? null)
    this.isLastNumero.set(true);

    setTimeout(() => {
      this.isLastNumero.set(false);
    }, environment.time_numero_modal);
  }

  public assignNewAssoEvent(assoEvent: VpdEventModel) {
    this.assoEvent.set(assoEvent);

    if (assoEvent.currentPartieIndex >= (this.assoEvent()?.parties.length ?? 0)) {
      this.isFinished.set(true);
      return;
    }
  }

  public onRollBackClicked() {
    this.lotoFacade.deleteRollBack$(this.assoEvent()!.id).then(response => {
      this.assignNewAssoEvent(response);
    })
  }

  public isNeighboor(number: number) {
    if (this.currentPartie()!.partieType !== PartieTypeEnum.PLUSUNMOINSUN) {
      return false;
    }

    const lastNumero = this.currentPartie()?.lastNumeros.slice(-1)[0]

    const allNeigboors = []
    for (let i = this.currentPartie()!.liveNumeros.length - 1; i >= 0; i--) {
      if (this.currentPartie()!.liveNumeros[i] === lastNumero) {
        break;
      }
      allNeigboors.push(this.currentPartie()!.liveNumeros[i]);
    }
    console.log(allNeigboors)
    return allNeigboors.includes(number);
  }
  titleNbTimes = computed(() => {
    if (this.currentLotsStillWinnable().length === 2) {
      return "1ère fois"
    }
    if (this.currentLotsStillWinnable().length === 1) {
      return "2ème fois"
    }
    return ""
  })
  protected readonly NumberLineEnum = NumberLineEnum;

  onBingoWinClicked() {
    this.lotoFacade.postBingoWin$(this.assoEvent!()!.id, !this.assoEvent()!.bingoHasBeenWon).then(response => {
      this.assignNewAssoEvent(response);
    })
  }

  onWinClicked() {
    if (this.currentPartie()?.pauseAfter && (this.currentPartie()?.partieType === PartieTypeEnum.AMERICAINE || this.currentPartie()!.currentLineIndex >= 2)) {
      this.isPaused.set(true);
    }
    this.lotoFacade.postWin$(this.assoEvent()!.id).then(response => {
      this.assignNewAssoEvent(response);
    })
  }
}
