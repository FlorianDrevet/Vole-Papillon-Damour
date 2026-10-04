import assert from 'node:assert/strict';
import {createServer, request} from 'node:http';
import {after, before, test} from 'node:test';
import {reqHandler} from '../dist/vole_papillon_damour_website/server/server.mjs';

let server;
let origin;

before(async () => {
  server = createServer(reqHandler);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  origin = `http://127.0.0.1:${server.address().port}`;
});

after(() => new Promise(resolve => server.close(resolve)));

for (const path of [
  '/accueil',
  '/actualite/3e85f3a6-1f26-48c1-8371-106d1e179624?utm_source=google',
  '/evenement/69e14da9-ccc3-43ce-859f-3ce838bd655c',
  '/robots.txt',
  '/sitemap.xml',
]) {
  test(`www permanently redirects ${path} without losing the path or query`, async () => {
    const response = await requestPage(origin + path, {
      headers: {host: 'www.volepapillondamour.fr'},
    });
    assert.equal(response.status, 301);
    assert.equal(response.location, 'https://volepapillondamour.fr' + path);
  });
}

test('Azure forwarded www host redirects before prerendered files are served', async () => {
  const response = await requestPage(origin + '/association/presentation', {
    headers: {
      host: 'internal.azurecontainerapps.io',
      'x-forwarded-host': 'www.volepapillondamour.fr',
      'x-forwarded-proto': 'https',
      'x-forwarded-path': '/association/presentation',
    },
  });
  assert.equal(response.status, 301);
  assert.equal(response.location, 'https://volepapillondamour.fr/association/presentation');
});

for (const host of ['volepapillondamour.fr', 'www.volepapillondamour.fr']) {
  test(`${host} root redirects permanently to the canonical home in one hop`, async () => {
    const response = await requestPage(origin + '/?utm_source=google', {
      headers: {host},
    });
    assert.equal(response.status, 301);
    assert.equal(response.location, 'https://volepapillondamour.fr/accueil?utm_source=google');
  });
}

for (const host of ['volepapillondamour.fr', 'localhost']) {
  test(`${host} serves the sitemap without a redirect loop`, async () => {
    const response = await requestPage(origin + '/sitemap.xml', {headers: {host}});
    assert.equal(response.status, 200);
    assert.equal(response.location, null);
    assert.match(response.body, /https:\/\/volepapillondamour\.fr\/accueil/);
  });
}

// The album is SSR; the other pages are prerendered. These routes need no API.
for (const path of ['/association/presentation', '/maxence/histoire', '/accessibilite', '/association/photos/maxence-2004']) {
  test(`${path} contains exactly one canonical in the original HTML head`, async () => {
    const response = await requestPage(origin + path + '?utm_source=google', {
      headers: {host: 'volepapillondamour.fr', 'x-forwarded-proto': 'https'},
    });
    assert.equal(response.status, 200);
    const html = response.body;
    const head = html.match(/<head>[\s\S]*?<\/head>/)?.[0] ?? '';
    const canonicals = head.match(/<link\b[^>]*rel="canonical"[^>]*>/g) ?? [];
    assert.equal(canonicals.length, 1);
    assert.ok(canonicals[0].includes(`href="https://volepapillondamour.fr${path}"`));
  });
}

// node:http preserves a custom Host header; Node's fetch can replace it.
function requestPage(url, options) {
  return new Promise((resolve, reject) => {
    const req = request(url, options, response => {
      let body = '';
      response.setEncoding('utf8');
      response.on('data', chunk => body += chunk);
      response.on('end', () => resolve({
        status: response.statusCode,
        location: response.headers.location ?? null,
        body,
      }));
      response.on('error', reject);
    });
    req.on('error', reject);
    req.setTimeout(15000, () => req.destroy(new Error('SEO HTTP request timed out')));
    req.end();
  });
}
