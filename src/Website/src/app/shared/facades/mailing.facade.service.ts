import { Injectable } from '@angular/core';
import {AxiosService} from "../services/axios.service";
import {ActualityModel} from "../models/actuality.model";
import {MethodEnum} from "../enums/method.enum";

@Injectable({
  providedIn: 'root'
})
export class MailingFacadeService {

  constructor(private axiosService: AxiosService) { }

  public postAddEmail(email: string): Promise<boolean> {
    return this.axiosService.request(MethodEnum.POST, `/mailing-list`, { email });
  }
}
