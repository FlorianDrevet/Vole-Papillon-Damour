import {isPlatformServer} from '@angular/common';
import {PendingTasks} from '@angular/core';

export const SSR_DATA_TIMEOUT_MS = 3_000;

/** Keep a server render stable while route data is loading, with a bounded wait. */
export function trackSsrDataRequest(pendingTasks: PendingTasks, platformId: Object): () => void {
  if (!isPlatformServer(platformId)) {
    return () => undefined;
  }

  const removePendingTask = pendingTasks.add();
  let isPending = true;
  let timeout: ReturnType<typeof setTimeout>;
  const finish = (): void => {
    if (!isPending) {
      return;
    }
    isPending = false;
    clearTimeout(timeout);
    removePendingTask();
  };

  timeout = setTimeout(finish, SSR_DATA_TIMEOUT_MS);
  return finish;
}
