import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import {join} from 'node:path';

import {environment} from './environments/environment';

const browserDistFolder = join(import.meta.dirname, '../browser');
const app = express();
const angularApp = new AngularNodeAppEngine({trustProxyHeaders: true});

app.use((req, _res, next) => {
  delete req.headers['x-forwarded-path'];
  next();
});

app.use((req, res, next) => {
  const routePath = req.path.replace(/\/+$/, '') || '/';
  if (routePath === '/administration' || routePath === '/compte' || routePath === '/desinscription') {
    res.setHeader('X-Robots-Tag', 'noindex, nofollow');
  }
  next();
});

app.get('/sitemap.xml', async (_req, res, next) => {
  try {
    const response = await fetch(`${environment.apiUrl}/catalog/sitemap.xml`);
    if (!response.ok) {
      res.status(response.status).type('text/plain').send('Sitemap unavailable');
      return;
    }

    res.type('application/xml').send(await response.text());
  } catch {
    res.status(503).type('text/plain').send('Sitemap unavailable');
  }
});

app.use(express.static(browserDistFolder, {
  maxAge: '1y',
  index: false,
  redirect: false,
}));

app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then(response => response ? writeResponseToNodeResponse(response, res) : next())
    .catch(next);
});

if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, error => {
    if (error) {
      throw error;
    }
    console.log(`Catalog SSR listening on http://localhost:${port}`);
  });
}

export const reqHandler = createNodeRequestHandler(app);
