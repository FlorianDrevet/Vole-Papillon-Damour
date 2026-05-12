import {VpdEventEnum} from "../enums/vpdEvent.enum";
import {NumberLineEnum} from "../enums/numberLine.enum";
import {PartieTypeEnum} from "../enums/partieType.enum";
import {MyDate} from "../extensions/MyDate";

export interface VpdEventModel {
  eventType: VpdEventEnum,
  id: string,
  name: string,
  description: string,
  dateStart: MyDate,
  dateEnd: MyDate | null,
  hourOpenDoors: MyDate | null,
  hourCloseDoors: MyDate | null,
  urlImageMap: string | null,
  urlRegistration: string | null,
  urlImage: string,
  city: string,
  road: string,
  cityCode: string,
  roadNumber: string,
  parties: VpdEventPartieModel[], // TODO rendre nullable
  currentPartieIndex: number,
  bingoHasBeenWon: boolean
  bingoNumeros: number[]
}

export interface VpdEventPartieModel {
  id: string,
  name: string,
  partieType: PartieTypeEnum,
  index: number,
  pauseAfter: boolean,
  addedBingoNumber: number | null,
  lastNumeros: number[],
  liveNumeros: number[],
  lineParties: VpdEventPartieLineModel[],
  currentLineIndex: number
}

export interface VpdEventPartieLineModel {
  id: string,
  lots: VpdEventPartieLotModel[],
  numberLine: NumberLineEnum,
  index: number
}

export interface VpdEventPartieLotModel {
  id: string,
  name: string,
  urlImage: string,
  index: number,
  isWon: boolean | null
}
