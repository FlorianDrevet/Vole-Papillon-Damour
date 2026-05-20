import {inject, Injectable} from '@angular/core';
import {AxiosService} from "../services/axios.service";
import {VpdEventModel} from "../models/vpdEvent.model";
import {MethodEnum} from "../enums/method.enum";
import {NumberLineEnum} from "../enums/numberLine.enum";

@Injectable({
  providedIn: 'root'
})
export class LotoFacadeService {

  axiosService = inject(AxiosService);

  public postCreatePartie$(eventId: string, createPartieForm: FormData): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.POST, `/asso-events/${eventId}/parties`, createPartieForm, {}, true);
  }

  public putUpdatePartie$(eventId: string, partieId: string, updatePartieForm: FormData): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.PUT, `/asso-events/${eventId}/parties/${partieId}`, updatePartieForm, {}, true);
  }

  public deletePartie$(eventId: string, partieId: string): Promise<boolean> {
    return this.axiosService.request$(MethodEnum.DELETE, `/asso-events/${eventId}/parties/${partieId}`, {});
  }

  public postCreateLot$(vpdEvent: VpdEventModel, partieId: string, numberLine: NumberLineEnum, createLotForm: FormData): Promise<VpdEventModel> {
    const linePartie = vpdEvent.parties.find(partie => partie.id === partieId)?.lineParties.find(linePartie => linePartie.numberLine === numberLine);

    if (!linePartie) {
      const form = new FormData();
      createLotForm.forEach((value, key) => {
        form.append(`Lots[0].${key}`, value);
      });

      form.append("NumberLine", numberLine.toString());
      form.append("Index", "0");

      return this._postCreateLinePartie$(vpdEvent.id, partieId, form);
    }

    return this.axiosService.request$(MethodEnum.POST, `/asso-events/${vpdEvent.id}/parties/${partieId}/partie-lines/${linePartie.id}/lots`, createLotForm, {}, true);
  }

  public putUpdateLot$(eventId: string, partieId: string, linePartieId: string, lotId: string, updateLotForm: FormData): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.PUT, `/asso-events/${eventId}/parties/${partieId}/partie-lines/${linePartieId}/lots/${lotId}`, updateLotForm, {}, true);
  }

  public deleteLot$(eventId: string, partieId: string, linePartieId: string, lotId: string): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.DELETE, `/asso-events/${eventId}/parties/${partieId}/partie-lines/${linePartieId}/lots/${lotId}`, {});
  }

  private _postCreateLinePartie$(eventId: string, partieId: string, createLinePartieForm: FormData): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.POST, `/asso-events/${eventId}/parties/${partieId}/partie-lines`, createLinePartieForm, {}, true);
  }

  public deleteRollBack$(assoEventId: string): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.DELETE, `/asso-events/${assoEventId}/numeros`, null);
  }

  public postWin$(assoEventId: string): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.POST, `/asso-events/${assoEventId}/win-partie`, null);
  }

  public postBingoWin$(assoEventId: string, won: boolean): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.PUT, `/asso-events/${assoEventId}/bingo-win`, {"hasBeenWon": won});
  }

  public postNumberToPartie$(assoEventId: string, number: number): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.POST, `/asso-events/${assoEventId}/numeros`, {'numero': number});
  }
}
