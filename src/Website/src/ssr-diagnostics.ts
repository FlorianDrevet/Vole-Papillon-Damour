export interface SsrMemorySnapshot {
  rss: number;
  heapUsed: number;
  heapTotal: number;
  external: number;
  arrayBuffers: number;
}

export interface SsrDiagnosticsOptions {
  log?: (record: Record<string, unknown>) => void;
  memoryUsage?: () => SsrMemorySnapshot;
  now?: () => number;
  progressIntervalMs?: number;
}

interface SsrRequest {
  method: string;
  originalUrl: string;
}

interface SsrResponse {
  statusCode: number;
  once(event: 'finish' | 'close', listener: () => void): unknown;
}

const EMPTY_MEMORY: SsrMemorySnapshot = {
  rss: 0,
  heapUsed: 0,
  heapTotal: 0,
  external: 0,
  arrayBuffers: 0,
};

function currentProcessMemory(): SsrMemorySnapshot {
  const runtime = (globalThis as typeof globalThis & {
    process?: { memoryUsage?: () => SsrMemorySnapshot };
  }).process;

  return runtime?.memoryUsage?.() ?? EMPTY_MEMORY;
}

function memoryRecord(memory: SsrMemorySnapshot): Record<string, number> {
  return {
    rssBytes: memory.rss,
    heapUsedBytes: memory.heapUsed,
    heapTotalBytes: memory.heapTotal,
    externalBytes: memory.external,
    arrayBuffersBytes: memory.arrayBuffers,
  };
}

export function createSsrRequestDiagnosticsMiddleware(
  options: SsrDiagnosticsOptions = {},
): (request: SsrRequest, response: SsrResponse, next: () => void) => void {
  let activeSsrRequests = 0;
  let requestSequence = 0;
  const log = options.log ?? (record => {
    const message = JSON.stringify(record);
    if (record['level'] === 'Warning') {
      console.warn(message);
      return;
    }
    console.info(message);
  });
  const memoryUsage = options.memoryUsage ?? currentProcessMemory;
  const now = options.now ?? Date.now;
  const progressIntervalMs = options.progressIntervalMs ?? 10_000;

  const snapshot = (): SsrMemorySnapshot => {
    try {
      return memoryUsage();
    } catch {
      return EMPTY_MEMORY;
    }
  };

  const write = (record: Record<string, unknown>): void => {
    try {
      log(record);
    } catch {
      // Diagnostics must never prevent a page from being served.
    }
  };

  return (request, response, next) => {
    const startedAt = now();
    const startedMemory = snapshot();
    const requestId = `ssr-${++requestSequence}`;
    const path = request.originalUrl.split('?', 1)[0];
    activeSsrRequests += 1;

    write({
      event: 'website.ssr.request.started',
      level: 'Information',
      requestId,
      method: request.method,
      path,
      activeSsrRequests,
      memory: memoryRecord(startedMemory),
    });

    let completed = false;
    const progressTimer = progressIntervalMs > 0
      ? setInterval(() => {
        write({
          event: 'website.ssr.request.in_progress',
          level: 'Warning',
          requestId,
          method: request.method,
          path,
          elapsedMs: Math.max(0, Math.round(now() - startedAt)),
          activeSsrRequests,
          memory: memoryRecord(snapshot()),
        });
      }, progressIntervalMs)
      : undefined;

    // Node timers support unref(); browser test runners use numeric timer IDs.
    (progressTimer as unknown as { unref?: () => void } | undefined)?.unref?.();

    const complete = (outcome: 'finished' | 'closed'): void => {
      if (completed) return;
      completed = true;
      if (progressTimer !== undefined) clearInterval(progressTimer);
      activeSsrRequests = Math.max(0, activeSsrRequests - 1);

      const completedMemory = snapshot();
      write({
        event: 'website.ssr.request.completed',
        level: outcome === 'closed' ? 'Warning' : 'Information',
        requestId,
        method: request.method,
        path,
        outcome,
        statusCode: response.statusCode,
        durationMs: Math.max(0, Math.round(now() - startedAt)),
        activeSsrRequests,
        memory: memoryRecord(completedMemory),
        rssDeltaBytes: completedMemory.rss - startedMemory.rss,
        heapUsedDeltaBytes: completedMemory.heapUsed - startedMemory.heapUsed,
        externalDeltaBytes: completedMemory.external - startedMemory.external,
        arrayBuffersDeltaBytes: completedMemory.arrayBuffers - startedMemory.arrayBuffers,
      });
    };

    response.once('finish', () => complete('finished'));
    response.once('close', () => complete('closed'));
    next();
  };
}
