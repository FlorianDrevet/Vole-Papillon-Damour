import type { ApplicationInsights } from '@microsoft/applicationinsights-web';

import { ApplicationInsightsErrorHandler } from './application-insights-error-handler';

describe('ApplicationInsightsErrorHandler', () => {
  beforeEach(() => spyOn(console, 'error'));

  it('forwards an Angular error to Application Insights', () => {
    const client = jasmine.createSpyObj<ApplicationInsights>('ApplicationInsights', ['trackException']);
    const error = new Error('template failed');

    new ApplicationInsightsErrorHandler(() => client).handleError(error);

    expect(client.trackException).toHaveBeenCalledOnceWith({ exception: error });
    expect(console.error).toHaveBeenCalled();
  });

  it('wraps a non-Error value so the exception keeps a message', () => {
    const client = jasmine.createSpyObj<ApplicationInsights>('ApplicationInsights', ['trackException']);

    new ApplicationInsightsErrorHandler(() => client).handleError('unexpected value');

    const tracked = client.trackException.calls.mostRecent().args[0];
    expect(tracked.exception?.message).toBe('unexpected value');
  });

  it('only logs when telemetry is disabled or during server rendering', () => {
    expect(() => new ApplicationInsightsErrorHandler(() => null).handleError(new Error('boom'))).not.toThrow();
    expect(console.error).toHaveBeenCalled();
  });
});
