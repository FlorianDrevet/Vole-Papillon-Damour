import {HttpBackend} from '@angular/common/http';
import {ApplicationInitStatus, signal} from '@angular/core';
import {TestBed} from '@angular/core/testing';
import type {AccountInfo} from '@azure/msal-browser';

import {AppModule} from './app.module';
import {CatalogAuthService} from './core/catalog-auth.service';

describe('AppModule', () => {
  function configureModule() {
    const initialize = jasmine.createSpy('initialize').and.resolveTo();
    const auth = {
      account: signal<AccountInfo | null>(null),
      isAuthenticated: signal(false),
      isAdministrator: signal(false),
      initialize,
    };

    TestBed.configureTestingModule({
      imports: [AppModule],
      providers: [{provide: CatalogAuthService, useValue: auth}],
    });

    return {auth, initialize};
  }

  it('uses FetchBackend so SSR HTTP responses can be transferred to hydration', () => {
    configureModule();

    const backend = TestBed.inject(HttpBackend);

    expect(backend.constructor.name).toContain('FetchBackend');
  });

  it('does not initialize MSAL while bootstrapping the anonymous shell', async () => {
    const {initialize} = configureModule();

    await TestBed.inject(ApplicationInitStatus).donePromise;

    expect(initialize).not.toHaveBeenCalled();
  });
});
