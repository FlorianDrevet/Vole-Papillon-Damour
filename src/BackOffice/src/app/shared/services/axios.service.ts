import {Injectable} from '@angular/core';
import axios from 'axios'
import {environment} from "../../../environments/environment";
import {MethodEnum} from "../enums/method.enum";
import {Router} from "@angular/router";
import {firstValueFrom} from 'rxjs';

import {ApiAccessTokenService} from './api-access-token.service';

@Injectable({
  providedIn: 'root'
})
export class AxiosService {

  constructor(private readonly apiAccessTokenService: ApiAccessTokenService, private readonly router: Router) {
    axios.defaults.baseURL = environment.api_url

    axios.interceptors.request.use(
      async function (config) {
        try {
          const token = await firstValueFrom(apiAccessTokenService.getApiAccessToken$());
          config.headers.Authorization = `Bearer ${token}`;
          return config;
        } catch (error) {
          await router.navigate(['/login']);
          return Promise.reject(error);
        }
      },
      function (error) {
        return Promise.reject(error);
      }
    );

    axios.interceptors.response.use(
      function (response) {
        return response;
      },
      function (error) {
        if (error.response && error.response.status === 401) {
          router.navigate(['/login']).then(null);
        }
        return Promise.reject(error);
      }
    );
  }

  public async request$(method: MethodEnum, url: string, data: any, headers: object = {}, isFormFile: boolean = false): Promise<any> {
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


