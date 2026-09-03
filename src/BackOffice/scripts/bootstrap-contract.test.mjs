import {strict as assert} from 'node:assert';
import {readFile} from 'node:fs/promises';
import {test} from 'node:test';

const indexHtml = await readFile(new URL('../src/index.html', import.meta.url), 'utf8');
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

test('uses a tenant-scoped authority for the CIAM custom domain', () => {
  for (const environmentSource of environmentSources) {
    const tenantId = environmentSource.match(/tenantId:\s*"([^"]+)"/)?.[1];
    const authority = environmentSource.match(/authority:\s*"([^"]+)"/)?.[1];

    assert.ok(tenantId, 'the Entra tenant id must be configured');
    assert.ok(authority, 'the Entra authority must be configured');
    assert.equal(new URL(authority).pathname.replace(/\/$/, ''), `/${tenantId}`);
  }
});
