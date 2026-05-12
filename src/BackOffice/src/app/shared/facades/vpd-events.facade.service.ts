import {Injectable} from '@angular/core';
import {AxiosService} from "../services/axios.service";
import {MethodEnum} from "../enums/method.enum";
import {VpdEventModel} from "../models/vpdEvent.model";

@Injectable({
  providedIn: 'root'
})
export class VpdEventsFacadeService {

  constructor(private axiosService: AxiosService) {
  }

  public getLatestEventBingo$(): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.GET, `/asso-events/next-bingo`, null);
  }

  public getLatestEventBooks$(): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.GET, `/asso-events/next-books`, null);
  }

  public getLatestEventOthers$(): Promise<VpdEventModel[]> {
    return this.axiosService.request$(MethodEnum.GET, `/asso-events/next-other-event`, null);
  }

  public getAllEvents$(): Promise<VpdEventModel[]> {
    return this.axiosService.request$(MethodEnum.GET, `/asso-events`, null);
  }

  public getEventById$(id: string): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.GET, `/asso-events/${id}`, null);
  }

  public postNewEvent$(event: FormData): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.POST, `/asso-events`, event, {}, true);
  }

  public putUpdateEvent$(id: string,  event: FormData): Promise<VpdEventModel> {
    return this.axiosService.request$(MethodEnum.PUT, `/asso-events/${id}`, event, {}, true);
  }

  public deleteEventById$(id: string): Promise<boolean> {
    return this.axiosService.request$(MethodEnum.DELETE, `/asso-events/${id}`, null);
  }

}
