import {HttpClient, HttpErrorResponse, HttpHeaders, provideHttpClient, withInterceptors} from '@angular/common/http';
import {HttpTestingController, provideHttpClientTesting} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';
import {firstValueFrom} from 'rxjs';

import {CatalogAuthService} from './catalog-auth.service';
import {catalogAuthInterceptor} from './catalog-auth.interceptor';

describe('catalogAuthInterceptor', () => {
  let http: HttpTestingController;
  let auth: jasmine.SpyObj<CatalogAuthService>;

  beforeEach(() => {
    auth = jasmine.createSpyObj<CatalogAuthService>(
      'CatalogAuthService',
      ['refreshApiAccessToken', 'markSessionRequiresReauthentication'],
    );
    auth.refreshApiAccessToken.and.resolveTo('fresh-token');

    TestBed.configureTestingModule({
      providers: [
        {provide: CatalogAuthService, useValue: auth},
        provideHttpClient(withInterceptors([catalogAuthInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('retries a protected request once with a silently refreshed token after a 401', async () => {
    const responsePromise = firstValueFrom(
      TestBed.inject(HttpClient).get<{ok: boolean}>('/member', {
        headers: new HttpHeaders({Authorization: 'Bearer stale-token'}),
      }),
    );

    const firstRequest = http.expectOne('/member');
    expect(firstRequest.request.headers.get('Authorization')).toBe('Bearer stale-token');
    firstRequest.flush(null, {status: 401, statusText: 'Unauthorized'});
    await Promise.resolve();

    const retryRequest = http.expectOne('/member');
    expect(retryRequest.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    retryRequest.flush({ok: true});

    await expectAsync(responsePromise).toBeResolvedTo({ok: true});
    expect(auth.refreshApiAccessToken).toHaveBeenCalledTimes(1);
  });

  it('does not retry a public request after a 401', async () => {
    const responsePromise = firstValueFrom(
      TestBed.inject(HttpClient).get('/public'),
    ).catch(error => error as HttpErrorResponse);

    const request = http.expectOne('/public');
    request.flush(null, {status: 401, statusText: 'Unauthorized'});

    const error = await responsePromise as HttpErrorResponse;
    expect(error.status).toBe(401);
    expect(auth.refreshApiAccessToken).not.toHaveBeenCalled();
    expect(auth.markSessionRequiresReauthentication).not.toHaveBeenCalled();
  });

  it('marks the session when the single retry is also unauthorized', async () => {
    const responsePromise = firstValueFrom(
      TestBed.inject(HttpClient).get('/member', {
        headers: new HttpHeaders({Authorization: 'Bearer stale-token'}),
      }),
    ).catch(error => error as HttpErrorResponse);

    http.expectOne('/member').flush(null, {status: 401, statusText: 'Unauthorized'});
    await Promise.resolve();
    http.expectOne('/member').flush(null, {status: 401, statusText: 'Unauthorized'});

    const error = await responsePromise as HttpErrorResponse;
    expect(error.status).toBe(401);
    expect(auth.refreshApiAccessToken).toHaveBeenCalledTimes(1);
    expect(auth.markSessionRequiresReauthentication).toHaveBeenCalledTimes(1);
  });
});
