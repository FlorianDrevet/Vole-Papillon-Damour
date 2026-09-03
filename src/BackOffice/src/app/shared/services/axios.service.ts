import {inject, Injectable} from '@angular/core';
import axios from 'axios'
import {Router} from "@angular/router";
import {firstValueFrom} from 'rxjs';
import {MsalService} from '@azure/msal-angular';

import {environment} from "../../../environments/environment";
import {MethodEnum} from "../enums/method.enum";
import {LOGIN_ROUTE} from '../auth/msal-config';
import {ApiAccessTokenService} from './api-access-token.service';

@Injectable({
  providedIn: 'root'
})
export class AxiosService {
  private readonly apiAccessTokenService = inject(ApiAccessTokenService);
  private readonly msalService = inject(MsalService);
  private readonly router = inject(Router);

  constructor() {
    axios.defaults.baseURL = environment.api_url

    axios.interceptors.request.use(
      async config => {
        const token = await firstValueFrom(this.apiAccessTokenService.getApiAccessToken$())
          .catch(error => {
            // Renvoyer systématiquement vers l'écran de connexion faisait boucler
            // l'application dès qu'un compte restait en cache : la page de
            // connexion voyait ce compte, laissait entrer, l'appel repartait, et
            // ainsi de suite. On n'y renvoie donc que si plus aucun compte n'est
            // connu ; sinon la reprise de session est déjà lancée ailleurs
            // (ApiAccessTokenService) et l'erreur remonte à l'appelant.
            this.redirectToLoginIfSignedOut();
            throw error;
          });

        config.headers.Authorization = `Bearer ${token}`;
        return config;
      },
      error => Promise.reject(error),
    );

    axios.interceptors.response.use(
      response => response,
      error => {
        if (error.response?.status === 401) {
          void this.router.navigate([LOGIN_ROUTE], {queryParams: {raison: 'session'}});
        }
        return Promise.reject(error);
      },
    );
  }

  public async request$(method: MethodEnum, url: string, data: any, headers: object = {}, isFormFile: boolean = false): Promise<any> {
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
  }

  private redirectToLoginIfSignedOut(): void {
    if (this.msalService.instance.getAllAccounts().length === 0) {
      void this.router.navigate([LOGIN_ROUTE]);
    }
  }
}
