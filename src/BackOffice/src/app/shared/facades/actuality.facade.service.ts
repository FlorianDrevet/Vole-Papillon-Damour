import {Injectable} from '@angular/core';
import {AxiosService} from "../services/axios.service";
import {ActualityModel} from "../models/actuality.model";
import {MethodEnum} from "../enums/method.enum";

@Injectable({
  providedIn: 'root'
})
export class ActualityFacadeService {

  constructor(private axiosService: AxiosService) {
  }

  public getActualityById$(id: string): Promise<ActualityModel> {
    return this.axiosService.request$(MethodEnum.GET, `/actuality/${id}`, null);
  }

  public postNewActuality$(actuality: FormData): Promise<ActualityModel> {
    return this.axiosService.request$(MethodEnum.POST, `/actuality`, actuality, {}, true);
  }

  public putUpdateActuality$(id: string, actuality: FormData): Promise<ActualityModel> {
    console.log(actuality)
    return this.axiosService.request$(MethodEnum.PUT, `/actuality/${id}`, actuality, {}, true);
  }

  public deleteActualityById$(id: string): Promise<boolean> {
    return this.axiosService.request$(MethodEnum.DELETE, `/actuality/${id}`, null);
  }
}
