import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';
import { WEBSITE_ORIGIN } from './website-origin';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const canonicalHostname = new URL(WEBSITE_ORIGIN).hostname;
const angularApp = new AngularNodeAppEngine({
  // Azure Container Apps terminates TLS at its ingress and forwards over plain
  // HTTP, so the original scheme, host and path only survive in the
  // X-Forwarded-* headers. Without trusting them the engine cannot rebuild the
  // request URL and falls back to the client-side shell for every route.
  trustProxyHeaders: true,
});

/**
 * Example Express Rest API endpoints can be defined here.
 * Uncomment and define endpoints as necessary.
 *
 * Example:
 * ```ts
 * app.get('/api/{*splat}', (req, res) => {
 *   // Handle API request
 * });
 * ```
 */

/**
 * Azure Container Apps forwards the original path in `X-Forwarded-Path`, a header
 * `@angular/ssr` does not know about - it only understands `X-Forwarded-Prefix`.
 * Left in place, the engine stops resolving the route and answers every request
 * with the client-side shell, so neither server-rendered nor prerendered pages
 * are ever served. Dropping it restores normal routing.
 */
app.use((req, _res, next) => {
  delete req.headers['x-forwarded-path'];
  next();
});

// Use the same trusted Azure ingress host as Angular SSR. Keep local development
// and Container Apps health probes on their own hostnames.
app.use((req, res, next) => {
  const hostname = (req.get('x-forwarded-host') ?? req.get('host') ?? '')
    .split(',')[0].trim().toLowerCase().replace(/:\d+$/, '');
  const isPublicHost = hostname === canonicalHostname || hostname === `www.${canonicalHostname}`;
  if ((req.method === 'GET' || req.method === 'HEAD') && isPublicHost &&
      (hostname !== canonicalHostname || req.path === '/')) {
    const target = req.path === '/' ? `/accueil${req.originalUrl.slice(1)}` : req.originalUrl;
    res.redirect(301, WEBSITE_ORIGIN + target);
    return;
  }
  next();
});

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) =>
      response ? writeResponseToNodeResponse(response, res) : next(),
    )
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
