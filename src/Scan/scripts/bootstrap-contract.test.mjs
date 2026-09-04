import {strict as assert} from 'node:assert';
import {readFile} from 'node:fs/promises';
import {test} from 'node:test';

const indexHtml = await readFile(new URL('../src/index.html', import.meta.url), 'utf8');
const appModule = await readFile(new URL('../src/app/app.module.ts', import.meta.url), 'utf8');

test('provides a host element for every bootstrapped root component', () => {
  assert.match(
    appModule,
    /bootstrap:\s*\[\s*AppComponent,\s*MsalRedirectComponent\s*\]/s,
  );
  assert.match(indexHtml, /<app-root\b[^>]*><\/app-root>/);
  assert.match(indexHtml, /<app-redirect\b[^>]*><\/app-redirect>/);
});

test('initializes MSAL before the auth gate reads the account cache', () => {
  assert.match(
    appModule,
    /provideAppInitializer\(\(\)\s*=>\s*inject\(MsalService\)\.initialize\(\)\)/s,
  );
});
