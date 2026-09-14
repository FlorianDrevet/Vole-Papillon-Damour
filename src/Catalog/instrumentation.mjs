// Server-side telemetry for the Catalog SSR host, loaded with `node --import`.
//
// It measures what the Node process does — incoming page requests, the fetch calls
// that render them against the API, exceptions and logs — and exports it to the
// Catalog Application Insights. Nothing here is sent to or runs in visitors'
// browsers, so it adds no tracker to the public pages (ENF-14).

// The ESM loader hooks must be registered before Express or Angular import `http`.
import '@azure/monitor-opentelemetry/loader';

const connectionString = process.env.APPLICATIONINSIGHTS_CONNECTION_STRING;

// The SDK's own calls (Azure metadata probe, dynamic configuration, ingestion) would
// otherwise be traced as dependencies of the Catalog and billed as such.
const telemetryHosts = ['169.254.169.254', 'monitor.azure.com', 'applicationinsights.azure.com'];

function isTelemetryHost(host) {
  const hostname = String(host ?? '').split(':')[0];
  return telemetryHosts.some(candidate => hostname === candidate || hostname.endsWith(`.${candidate}`));
}

if (connectionString) {
  const {useAzureMonitor} = await import('@azure/monitor-opentelemetry');
  const {registerInstrumentations} = await import('@opentelemetry/instrumentation');
  const {UndiciInstrumentation} = await import('@opentelemetry/instrumentation-undici');

  useAzureMonitor({
    azureMonitorExporterOptions: {connectionString},
    // Keep every trace (11-observabilite.md §5). tracesPerSecond must be 0 for
    // samplingRatio to apply; otherwise the distro rate-limits.
    samplingRatio: 1,
    tracesPerSecond: 0,
    instrumentationOptions: {
      http: {
        enabled: true,
        ignoreOutgoingRequestHook: request => isTelemetryHost(request.hostname ?? request.host),
        requestHook: (span, request) => {
          // Outgoing ClientRequest objects expose getHeader; incoming requests do not.
          if (typeof request.getHeader === 'function') {
            return;
          }
          // Express is bundled, so no route instrumentation runs: without a route the
          // request is exported as a bare "GET" and every page collapses into one row.
          const path = String(request.url ?? '/').split('?')[0];
          span.setAttribute('http.route', path);
          span.updateName(`${request.method} ${path}`);
        },
      },
      azureSdk: {enabled: false},
      mongoDb: {enabled: false},
      mySql: {enabled: false},
      postgreSql: {enabled: false},
      redis: {enabled: false},
      redis4: {enabled: false},
    },
  });

  // Angular SSR and the sitemap proxy call the API through Node's fetch (undici),
  // which the distro does not instrument. Without this the API time spent inside a
  // slow page render is invisible and the trace to vpd-api is broken.
  registerInstrumentations({
    instrumentations: [
      new UndiciInstrumentation({
        ignoreRequestHook: request => isTelemetryHost(URL.canParse(request.origin) ? new URL(request.origin).host : ''),
      }),
    ],
  });
}
