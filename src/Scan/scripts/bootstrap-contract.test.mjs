import {strict as assert} from 'node:assert';
import {readFile} from 'node:fs/promises';
import {test} from 'node:test';

const indexHtml = await readFile(new URL('../src/index.html', import.meta.url), 'utf8');
const viewportSources = await Promise.all([
  readFile(new URL('../src/index.html', import.meta.url), 'utf8'),
  readFile(new URL('../../Catalog/src/index.html', import.meta.url), 'utf8'),
  readFile(new URL('../../Website/src/index.html', import.meta.url), 'utf8'),
  readFile(new URL('../../BackOffice/src/index.html', import.meta.url), 'utf8'),
]);
const scanStyles = await readFile(new URL('../src/styles.scss', import.meta.url), 'utf8');
const loginStyles = await readFile(new URL('../src/app/auth/scan-login.component.scss', import.meta.url), 'utf8');
const appModule = await readFile(new URL('../src/app/app.module.ts', import.meta.url), 'utf8');
const environmentSources = await Promise.all([
  readFile(new URL('../src/environments/environment.ts', import.meta.url), 'utf8'),
  readFile(new URL('../src/environments/environment.development.ts', import.meta.url), 'utf8'),
]);

test('provides a host element for every bootstrapped root component', () => {
  assert.match(
    appModule,
    /bootstrap:\s*\[\s*AppComponent,\s*MsalRedirectComponent\s*\]/s,
  );
  assert.match(indexHtml, /<app-root\b[^>]*><\/app-root>/);
  assert.match(indexHtml, /<app-redirect\b[^>]*><\/app-redirect>/);
});

test('prevents mobile dezoom while preserving zoom-in on every front', () => {
  for (const viewportSource of viewportSources) {
    const viewportContent = viewportSource.match(/<meta name="viewport" content="([^"]+)"/)?.[1];

    assert.ok(viewportContent, 'the viewport metadata must be declared');
    assert.match(viewportContent, /minimum-scale=1/);
    assert.doesNotMatch(viewportContent, /maximum-scale=/);
    assert.doesNotMatch(viewportContent, /user-scalable=/);
  }
});

test('uses the full mobile width and keeps the desktop Scan frame', () => {
  assert.match(scanStyles, /\.app-screen\s*\{[\s\S]*?width:\s*100%;/);
  assert.match(
    scanStyles,
    /@media\s*\(min-width:\s*600px\)\s*\{[\s\S]*?\.app-screen\s*\{[\s\S]*?width:\s*min\(100%,\s*390px\)/,
  );
  assert.match(loginStyles, /\.login-welcome,\s*\n\.login-unauthorized\s*\{[\s\S]*?width:\s*100%;/);
  assert.match(
    loginStyles,
    /@media\s*\(min-width:\s*600px\)\s*\{[\s\S]*?\.login-welcome,\s*\n\s*\.login-unauthorized\s*\{[\s\S]*?width:\s*min\(100%,\s*390px\)/,
  );
});

test('initializes MSAL before the auth gate reads the account cache', () => {
  assert.match(
    appModule,
    /provideAppInitializer\(\(\)\s*=>\s*inject\(MsalService\)\.initialize\(\)\)/s,
  );
});

test('uses a tenant-scoped authority for the CIAM custom domain', () => {
  for (const environmentSource of environmentSources) {
    const tenantId = environmentSource.match(/tenantId:\s*'([^']+)'/)?.[1];
    const authority = environmentSource.match(/authority:\s*'([^']+)'/)?.[1];

    assert.ok(tenantId, 'the Entra tenant id must be configured');
    assert.ok(authority, 'the Entra authority must be configured');
    assert.equal(new URL(authority).pathname.replace(/\/$/, ''), `/${tenantId}`);
  }
});

test('uses the public scan host for production redirects', () => {
  const productionEnvironment = environmentSources[0];

  assert.match(
    productionEnvironment,
    /redirectUri:\s*'https:\/\/scan\.volepapillondamour\.fr'/,
  );
  assert.match(
    productionEnvironment,
    /postLogoutRedirectUri:\s*'https:\/\/scan\.volepapillondamour\.fr'/,
  );
});
