import {Injectable} from '@angular/core';
import axios from 'axios'
import {environment} from "../../../environments/environment";
import {MethodEnum} from "../enums/method.enum";
import {Router} from "@angular/router";
import {AuthenticationService} from "./authentication.service";

@Injectable({
  providedIn: 'root'
})
export class AxiosService {

  constructor(private auth: AuthenticationService, private router: Router) {
    axios.defaults.baseURL = environment.api_url

    axios.interceptors.request.use(
      function (config) {
        if (config.url === '/auth/login') {
          return config;
        }
        const token = auth.getBearerToken();

        if (token) {
          config.headers['Authorization'] = token;
        } else {
          router.navigate(['login']).then(null);
          return Promise.reject('No token found');
        }

        return config;
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
          auth.logout();
          router.navigate(['login']).then(null);
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


