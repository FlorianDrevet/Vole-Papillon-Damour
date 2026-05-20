import {VpdEventModel, VpdEventPartieModel} from "../models/vpdEvent.model";

export interface CreateUpdatePartieDataDialogInterface {
  partie: VpdEventPartieModel | null,
  event: VpdEventModel,
}
