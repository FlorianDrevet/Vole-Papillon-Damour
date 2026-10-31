import {inject, Injectable} from '@angular/core';
import {Router} from '@angular/router';
import {firstValueFrom} from 'rxjs';
import axios, {type AxiosError, type InternalAxiosRequestConfig} from 'axios';

import {environment} from '../../../environments/environment';
import {MethodEnum} from '../enums/method.enum';
import {LOGIN_ROUTE} from '../auth/msal-config';
import {AuthSessionService} from '../auth/auth-session.service';
import {ApiAccessTokenService} from './api-access-token.service';

@Injectable({
  providedIn: 'root'
})
export class AxiosService {
  private readonly apiAccessTokenService = inject(ApiAccessTokenService);
  private readonly authSession = inject(AuthSessionService);
  private readonly router = inject(Router);

  constructor() {
    axios.defaults.baseURL = environment.api_url;

    axios.interceptors.request.use(
      async config => {
        if ((config as AuthRetriedRequestConfig).__vpdAuthRetried) {
          return config;
        }

        const token = await firstValueFrom(this.apiAccessTokenService.getApiAccessToken$())
          .catch(error => {
            // Renvoyer systématiquement vers l'écran de connexion faisait boucler
            // l'application dès qu'un compte restait en cache : la page de
            // connexion voyait ce compte, laissait entrer, l'appel repartait, et
            // ainsi de suite. On n'y renvoie donc que si plus aucun compte n'est
            // connu ; sinon la reprise de session est déjà lancée ailleurs
            // (ApiAccessTokenService) et l'erreur remonte à l'appelant.
            this.redirectIfAccountUnavailable();
            throw error;
          });

        config.headers.Authorization = `Bearer ${token}`;
        return config;
      },
      error => Promise.reject(error),
    );

    axios.interceptors.response.use(
      response => response,
      async (error: AxiosError) => {
        if (error.response?.status !== 401) {
          return Promise.reject(error);
        }

        const originalRequest = error.config as AuthRetriedRequestConfig | undefined;
        if (!originalRequest || originalRequest.__vpdAuthRetried) {
          void this.router.navigate([LOGIN_ROUTE], {queryParams: {raison: 'session'}});
          return Promise.reject(error);
        }

        originalRequest.__vpdAuthRetried = true;
        let token: string;
        try {
          token = await firstValueFrom(this.apiAccessTokenService.getApiAccessToken$(true));
        } catch (refreshError) {
          this.redirectIfAccountUnavailable();
          return Promise.reject(refreshError);
        }

        originalRequest.headers.Authorization = `Bearer ${token}`;
        return axios(originalRequest);
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

  private redirectIfAccountUnavailable(): void {
    if (this.authSession.isAuthenticated()) {
      return;
    }

    void this.router.navigate([LOGIN_ROUTE], {
      ...(this.authSession.requiresAccountSelection() ? {queryParams: {raison: 'compte'}} : {}),
    });
  }
}

interface AuthRetriedRequestConfig extends InternalAxiosRequestConfig {
  __vpdAuthRetried?: boolean;
}
