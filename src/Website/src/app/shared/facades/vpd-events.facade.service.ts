import {Injectable} from '@angular/core';
import {AxiosService} from "../services/axios.service";
import {MethodEnum} from "../enums/method.enum";
import {VpdEventModel} from "../models/vpdEvent.model";
import {TodayEventModel} from "../models/today-event.model";

@Injectable({
  providedIn: 'root'
})
export class VpdEventsFacadeService {

  constructor(private axiosService: AxiosService) { }

  public getLatestEventBingo(): Promise<VpdEventModel> {
    return this.axiosService.request(MethodEnum.GET, `/asso-events/next-bingo`, null);
  }

  public getLatestEventBooks(): Promise<VpdEventModel> {
    return this.axiosService.request(MethodEnum.GET, `/asso-events/next-books`, null);
  }

  public getLatestEventOthers(): Promise<VpdEventModel[]> {
    return this.axiosService.request(MethodEnum.GET, `/asso-events/next-other-event`, null);
  }

  public getEventById(id: string): Promise<VpdEventModel> {
    return this.axiosService.request(MethodEnum.GET, `/asso-events/${id}`, null);
  }

  public getEventById$(id: string): Promise<VpdEventModel> {
    return this.axiosService.request(MethodEnum.GET, `/asso-events/${id}`, null);
  }

  /** L'événement du jour (loto en priorité), ou `null` (204) s'il n'y en a pas. */
  public async getTodayEvent(): Promise<TodayEventModel | null> {
    const today: TodayEventModel | '' | null = await this.axiosService.request(MethodEnum.GET, `/asso-events/today`, null);
    return today ? today : null;
  }

  public getAllEvents$(): Promise<VpdEventModel[]> {
    return this.axiosService.request(MethodEnum.GET, `/asso-events`, null);
  }
}
