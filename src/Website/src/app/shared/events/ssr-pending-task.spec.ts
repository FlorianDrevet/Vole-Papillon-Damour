import {PendingTasks} from '@angular/core';
import {SSR_DATA_TIMEOUT_MS, trackSsrDataRequest} from './ssr-pending-task';

describe('trackSsrDataRequest', () => {
  it('registers server work and releases it only once', () => {
    const removePendingTask = jasmine.createSpy('removePendingTask');
    const pendingTasks = {
      add: jasmine.createSpy('add').and.returnValue(removePendingTask),
    } as unknown as PendingTasks;

    const finish = trackSsrDataRequest(pendingTasks, 'server');
    finish();
    finish();

    expect(pendingTasks.add).toHaveBeenCalledTimes(1);
    expect(removePendingTask).toHaveBeenCalledTimes(1);
  });

  it('does not register browser work as an SSR task', () => {
    const pendingTasks = {
      add: jasmine.createSpy('add'),
    } as unknown as PendingTasks;

    const finish = trackSsrDataRequest(pendingTasks, 'browser');
    finish();

    expect(pendingTasks.add).not.toHaveBeenCalled();
  });

  it('releases a stalled server render after the three-second limit', () => {
    jasmine.clock().install();
    const removePendingTask = jasmine.createSpy('removePendingTask');
    const pendingTasks = {
      add: jasmine.createSpy('add').and.returnValue(removePendingTask),
    } as unknown as PendingTasks;

    try {
      const finish = trackSsrDataRequest(pendingTasks, 'server');
      jasmine.clock().tick(SSR_DATA_TIMEOUT_MS);
      finish();

      expect(removePendingTask).toHaveBeenCalledTimes(1);
    } finally {
      jasmine.clock().uninstall();
    }
  });
});
