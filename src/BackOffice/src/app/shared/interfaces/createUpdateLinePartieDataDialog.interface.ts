import {
  VpdEventModel,
  VpdEventPartieLineModel,
  VpdEventPartieLotModel,
  VpdEventPartieModel
} from "../models/vpdEvent.model";
import {NumberLineEnum} from "../enums/numberLine.enum";

export interface CreateUpdateLinePartieDataDialogInterface {
  lot: VpdEventPartieLotModel | null,
  linePartie: VpdEventPartieLineModel | null,
  partie: VpdEventPartieModel,
  event: VpdEventModel,
  numberLine: NumberLineEnum
}
