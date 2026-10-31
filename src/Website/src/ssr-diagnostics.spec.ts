import {
  createSsrRequestDiagnosticsMiddleware,
  type SsrMemorySnapshot,
} from './ssr-diagnostics';

type ResponseEvent = 'finish' | 'close';

class FakeResponse {
  statusCode = 200;
  private readonly listeners = new Map<ResponseEvent, () => void>();

  once(event: ResponseEvent, listener: () => void): this {
    this.listeners.set(event, listener);
    return this;
  }

  emit(event: ResponseEvent): void {
    this.listeners.get(event)?.();
  }
}

describe('createSsrRequestDiagnosticsMiddleware', () => {
  it('Given an SSR response finishes, logs its route, duration, and memory change', () => {
    const records: Array<Record<string, unknown>> = [];
    const response = new FakeResponse();
    let now = 100;
    let memory: SsrMemorySnapshot = {
      rss: 100,
      heapUsed: 50,
      heapTotal: 80,
      external: 10,
      arrayBuffers: 5,
    };
    const next = jasmine.createSpy('next');

    const middleware = createSsrRequestDiagnosticsMiddleware({
      log: record => records.push(record),
      memoryUsage: () => memory,
      now: () => now,
      progressIntervalMs: 0,
    });

    middleware({ method: 'GET', originalUrl: '/evenement/123?secret=private' }, response, next);

    expect(next).toHaveBeenCalled();
    expect(records[0]).toEqual(jasmine.objectContaining({
      event: 'website.ssr.request.started',
      level: 'Information',
      method: 'GET',
      path: '/evenement/123',
      activeSsrRequests: 1,
      memory: jasmine.objectContaining({ rssBytes: 100, heapUsedBytes: 50 }),
    }));

    now = 150;
    memory = { ...memory, rss: 180, heapUsed: 85 };
    response.emit('finish');
    response.emit('close');

    expect(records.length).toBe(2);
    expect(records[1]).toEqual(jasmine.objectContaining({
      event: 'website.ssr.request.completed',
      outcome: 'finished',
      statusCode: 200,
      durationMs: 50,
      activeSsrRequests: 0,
      rssDeltaBytes: 80,
      heapUsedDeltaBytes: 35,
    }));
    expect(JSON.stringify(records)).not.toContain('secret');
  });

  it('Given a client disconnects, records the incomplete response and releases the active count', () => {
    const records: Array<Record<string, unknown>> = [];
    const response = new FakeResponse();
    const middleware = createSsrRequestDiagnosticsMiddleware({
      log: record => records.push(record),
      progressIntervalMs: 0,
    });

    middleware({ method: 'GET', originalUrl: '/accueil' }, response, () => undefined);
    response.emit('close');

    expect(records[1]).toEqual(jasmine.objectContaining({
      event: 'website.ssr.request.completed',
      level: 'Warning',
      outcome: 'closed',
      activeSsrRequests: 0,
    }));
  });

  it('Given an SSR request remains open, emits progress snapshots until it completes', () => {
    jasmine.clock().install();
    try {
      const records: Array<Record<string, unknown>> = [];
      const response = new FakeResponse();
      const middleware = createSsrRequestDiagnosticsMiddleware({
        log: record => records.push(record),
        progressIntervalMs: 1000,
      });

      middleware({ method: 'GET', originalUrl: '/evenement/123' }, response, () => undefined);
      jasmine.clock().tick(1000);

      expect(records[1]).toEqual(jasmine.objectContaining({
        event: 'website.ssr.request.in_progress',
        level: 'Warning',
        path: '/evenement/123',
        elapsedMs: jasmine.any(Number),
        activeSsrRequests: 1,
      }));

      response.emit('finish');
      jasmine.clock().tick(1000);

      expect(records.filter(record => record['event'] === 'website.ssr.request.in_progress').length).toBe(1);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('Given a request reaches the progress threshold, writes warning records to console.warn', () => {
    jasmine.clock().install();
    try {
      const response = new FakeResponse();
      const info = spyOn(console, 'info');
      const warn = spyOn(console, 'warn');
      const middleware = createSsrRequestDiagnosticsMiddleware({ progressIntervalMs: 1000 });

      middleware({ method: 'GET', originalUrl: '/evenement/123' }, response, () => undefined);
      expect(info.calls.mostRecent().args[0]).toContain('"level":"Information"');

      jasmine.clock().tick(1000);
      expect(warn.calls.mostRecent().args[0]).toContain('"event":"website.ssr.request.in_progress"');
      expect(warn.calls.mostRecent().args[0]).toContain('"level":"Warning"');

      response.emit('finish');
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
