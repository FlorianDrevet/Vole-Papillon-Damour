import {strict as assert} from 'node:assert';
import {readFile} from 'node:fs/promises';
import {test} from 'node:test';

const indexHtml = await readFile(new URL('../src/index.html', import.meta.url), 'utf8');
const appModule = await readFile(new URL('../src/app/app.module.ts', import.meta.url), 'utf8');
const routingModule = await readFile(new URL('../src/app/app-routing.module.ts', import.meta.url), 'utf8');
const tsconfig = await readFile(new URL('../tsconfig.json', import.meta.url), 'utf8');
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

test('resolves framework packages through node, not through tsconfig paths', () => {
  // Rediriger `@angular/*` (ou `rxjs`, `tslib`) vers un chemin de fichier sort ces
  // paquets de l'assemblage commun du serveur de développement, alors que leurs
  // sous-chemins continuent d'être résolus normalement : Angular se retrouve avec
  // deux exemplaires de `@angular/platform-browser`, ne trouve plus le fournisseur
  // `DomRendererFactory2`, et `ng serve` n'affiche qu'une page blanche (NG0201).
  const paths = tsconfig.slice(tsconfig.indexOf('"paths"'));
  for (const alias of ['@angular/*', 'rxjs', 'rxjs/*', 'tslib']) {
    assert.ok(
      !paths.includes(`"${alias}"`),
      `${alias} must not be redirected through tsconfig paths`,
    );
  }
});

test('leaves the Entra redirect landing route unguarded', () => {
  // Entra ramène toujours sur la racine (`redirectUri`). Si cette route est
  // protégée par MsalGuard, le guard traite la redirection en même temps que
  // MsalRedirectComponent et la navigation reste suspendue : l'application
  // affiche un cadre vide dont on ne peut plus sortir.
  for (const environmentSource of environmentSources) {
    const redirectUri = environmentSource.match(/redirectUri:\s*"([^"]+)"/)?.[1];
    assert.ok(redirectUri, 'the Entra redirect URI must be configured');
    assert.equal(
      new URL(redirectUri).pathname,
      '/',
      'the redirect URI must stay on the root route handled by AuthLandingComponent',
    );
  }

  const rootRouteStart = routingModule.indexOf("path: ''");
  const rootRouteEnd = routingModule.indexOf("path: 'login'");
  assert.ok(rootRouteStart > -1 && rootRouteEnd > rootRouteStart, 'a root route must be declared');

  const rootRoute = routingModule.slice(rootRouteStart, rootRouteEnd);
  assert.ok(
    !rootRoute.includes('canActivate'),
    'the root route must not be guarded',
  );
});
