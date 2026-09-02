import { VpdEventEnum } from '../enums/vpdEvent.enum';

/** Photos éditoriales communes à la page agenda et aux fiches de rendez-vous. */
export const EVENT_EDITORIAL_PHOTOS: Record<VpdEventEnum, string[]> = {
  [VpdEventEnum.Bingo]: [
    'images/MaxencesHistory/2016/Loto2016.jpg',
    'images/MaxencesHistory/2013/LotoTableau.jpg',
    'images/MaxencesHistory/2013/LotoSalle.jpg',
  ],
  [VpdEventEnum.Books]: [
    'images/Association/don-livre.jpg',
    'images/MaxencesHistory/2014/BourseAuxLivresAvril.jpg',
    'images/Association/don-livre3.jpg',
  ],
  [VpdEventEnum.Other]: [],
  [VpdEventEnum.UNKNOWN]: [],
};
