import { Injectable } from '@angular/core';
import axios from 'axios'
import {environment} from "../../../environments/environment";
import {MethodEnum} from "../enums/method.enum";

@Injectable({
  providedIn: 'root'
})
export class AxiosService {

  constructor() {
    axios.defaults.baseURL = environment.api_url
  }

  public async request(method: MethodEnum, url: string, data: any, headers: object = {}, isFormFile: boolean = false): Promise<any> {
    try {
      if (isFormFile) {
        headers = {...headers, "Content-Type": "multipart/form-data"};
      }
      else {
        headers = {...headers, "Content-Type": "application/json"};
      }

      const response = await axios({
        method,
        url,
        data,
        headers: headers,
        params: method === MethodEnum.GET ? data : {}
      });

      return response.data;
    } catch (error) {
      throw error;
    }
  }
}


