import {AxiosAdapter, AxiosError, AxiosHeaders, AxiosResponse} from 'axios';
import axios from 'axios';
import {MsalService} from '@azure/msal-angular';
import {AccountInfo} from '@azure/msal-browser';
import {Router} from '@angular/router';
import {TestBed} from '@angular/core/testing';
import {of} from 'rxjs';

import {LOGIN_ROUTE} from '../auth/msal-config';
import {MethodEnum} from '../enums/method.enum';
import {ApiAccessTokenService} from './api-access-token.service';
import {AxiosService} from './axios.service';

describe('AxiosService', () => {
  const account = {
    homeAccountId: 'home-id',
    environment: 'volepapillondamour.ciamlogin.com',
    tenantId: 'tenant-id',
    localAccountId: 'local-id',
    username: 'volunteer@example.test',
  } as AccountInfo;

  let service: AxiosService;
  let apiAccessTokenService: jasmine.SpyObj<ApiAccessTokenService>;
  let router: jasmine.SpyObj<Router>;
  let previousAdapter: typeof axios.defaults.adapter;
  let previousBaseUrl: string | undefined;

  beforeEach(() => {
    axios.interceptors.request.clear();
    axios.interceptors.response.clear();
    previousAdapter = axios.defaults.adapter;
    previousBaseUrl = axios.defaults.baseURL;

    apiAccessTokenService = jasmine.createSpyObj<ApiAccessTokenService>(
      'ApiAccessTokenService',
      ['getApiAccessToken$'],
    );
    apiAccessTokenService.getApiAccessToken$.and.returnValues(
      of('cached-token'),
      of('refreshed-token'),
    );
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    router.navigate.and.resolveTo(true);

    const msalInstance = {
      getActiveAccount: jasmine.createSpy('getActiveAccount').and.returnValue(account),
      getAllAccounts: jasmine.createSpy('getAllAccounts').and.returnValue([account]),
    };

    TestBed.configureTestingModule({
      providers: [
        AxiosService,
        {provide: ApiAccessTokenService, useValue: apiAccessTokenService},
        {provide: MsalService, useValue: {instance: msalInstance}},
        {provide: Router, useValue: router},
      ],
    });
    service = TestBed.inject(AxiosService);
  });

  afterEach(() => {
    axios.interceptors.request.clear();
    axios.interceptors.response.clear();
    axios.defaults.adapter = previousAdapter;
    axios.defaults.baseURL = previousBaseUrl;
    TestBed.resetTestingModule();
  });

  it('forces a token refresh and retries a request once after an API 401', async () => {
    let attempts = 0;
    const authorizationHeaders: string[] = [];
    axios.defaults.adapter = adapter(config => {
      authorizationHeaders.push(String(config.headers.get('Authorization')));
      attempts++;

      if (attempts === 1) {
        return createResponse(config, 401, null);
      }

      return createResponse(config, 200, {value: 'ok'});
    });

    const result = await service.request$(MethodEnum.GET, '/auth-test', null);

    expect(result).toEqual({value: 'ok'});
    expect(attempts).toBe(2);
    expect(authorizationHeaders).toEqual(['Bearer cached-token', 'Bearer refreshed-token']);
    expect(apiAccessTokenService.getApiAccessToken$.calls.argsFor(0)).toEqual([]);
    expect(apiAccessTokenService.getApiAccessToken$.calls.argsFor(1)).toEqual([true]);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('does not retry indefinitely when the refreshed token also receives a 401', async () => {
    let attempts = 0;
    axios.defaults.adapter = adapter(config => {
      attempts++;
      return createResponse(config, 401, null);
    });

    await expectAsync(service.request$(MethodEnum.GET, '/auth-test', null)).toBeRejected();

    expect(attempts).toBe(2);
    expect(apiAccessTokenService.getApiAccessToken$).toHaveBeenCalledTimes(2);
    expect(router.navigate).toHaveBeenCalledOnceWith(
      [LOGIN_ROUTE],
      {queryParams: {raison: 'session'}},
    );
  });

  it('does not refresh the token after an API 403', async () => {
    axios.defaults.adapter = adapter(config => createResponse(config, 403, null));

    await expectAsync(service.request$(MethodEnum.GET, '/auth-test', null)).toBeRejected();

    expect(apiAccessTokenService.getApiAccessToken$).toHaveBeenCalledOnceWith();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  function adapter(run: (config: Parameters<AxiosAdapter>[0]) => Promise<AxiosResponse>): AxiosAdapter {
    return config => run(config);
  }

  function createResponse(
    config: Parameters<AxiosAdapter>[0],
    status: number,
    data: unknown,
  ): Promise<AxiosResponse> {
    const response: AxiosResponse = {
      data,
      status,
      statusText: status === 200 ? 'OK' : status === 401 ? 'Unauthorized' : 'Forbidden',
      headers: new AxiosHeaders(),
      config,
    };

    if (status < 400) {
      return Promise.resolve(response);
    }

    return Promise.reject(new AxiosError(
      `Request failed with status code ${status}`,
      AxiosError.ERR_BAD_REQUEST,
      config,
      undefined,
      response,
    ));
  }
});
