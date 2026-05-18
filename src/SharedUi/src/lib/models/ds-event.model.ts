import { DsDateLike } from './ds-actuality.model';
import { VpdEventEnum } from '../enums/vpd-event.enum';

export interface DsVpdEventModel {
  eventType: VpdEventEnum | string | number;
  id: string;
  name: string;
  description: string;
  dateStart: DsDateLike;
  dateEnd: DsDateLike | null;
  hourOpenDoors: DsDateLike | null;
  hourCloseDoors: DsDateLike | null;
  urlImageMap: string | null;
  urlRegistration: string | null;
  urlImage: string;
  city: string;
  road: string;
  cityCode: string;
  roadNumber: string;
  bingoHasBeenWon: boolean;
  bingoNumeros: number[];
}
