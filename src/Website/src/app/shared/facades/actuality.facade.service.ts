import { Injectable } from '@angular/core';
import {AxiosService} from "../services/axios.service";
import {ActualityModel} from "../models/actuality.model";
import {MethodEnum} from "../enums/method.enum";

@Injectable({
  providedIn: 'root'
})
export class ActualityFacadeService {

  constructor(private axiosService: AxiosService) { }

  public getActualityById(id: string): Promise<ActualityModel> {
    return this.axiosService.request(MethodEnum.GET, `/actuality/${id}`, null);
  }
}
