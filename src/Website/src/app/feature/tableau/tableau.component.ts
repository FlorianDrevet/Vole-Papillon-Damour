import {Component, computed, effect, inject, OnInit, signal} from '@angular/core';
import {ActivatedRoute} from "@angular/router";
import {VpdEventModel} from "../../shared/models/vpdEvent.model";
import {VpdEventsFacadeService} from "../../shared/facades/vpd-events.facade.service";
import {PartieTypeEnum} from "../../shared/enums/partieType.enum";
import {SseClientService} from "../../shared/clients/sse-client.service";
import {NumberLineEnum} from "../../shared/enums/numberLine.enum";

@Component({
    selector: 'app-tableau',
    templateUrl: './tableau.component.html',
    styleUrl: './tableau.component.scss',
    standalone: false
})
export class TableauComponent implements OnInit {
  sseClient = inject(SseClientService);
  readonly loadingNumbers = Array.from({ length: 90 }, (_, index) => index);

  _indexPartie = computed(() => {
    if (this.assoEvent() === undefined) {
      return -1
    }
    console.log(this.assoEvent())
    console.log(this.assoEvent()?.parties)
    return this.assoEvent()!.parties.findIndex(partie => partie.index === this.assoEvent()?.currentPartieIndex)
  })

  assoEvent = computed(() => {
    return this.sseClient.eventAsso()
  })

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
  route = inject(ActivatedRoute);

  ngOnInit(): void {
    this.getAssoEvent()
  }

  getAssoEvent() {
    this.route.paramMap.subscribe(params => {
      if (params.get('id') !== null) {
        this.sseClient.init(params.get('id')!);
      }
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
  protected readonly PartieTypeEnum = PartieTypeEnum;
  protected readonly NumberLineEnum = NumberLineEnum;
}
