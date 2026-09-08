import {scanStoreNames} from './scan-offline.model';
import {ScanLocalStoreService} from './scan-local-store.service';

export async function clearScanCatalogForTest(service: ScanLocalStoreService): Promise<void> {
  await service.getCatalogBooks();
  const databasePromise = (service as unknown as {
    databasePromise: Promise<IDBDatabase> | null;
  }).databasePromise;
  if (!databasePromise) {
    throw new Error('The scan test database was not opened.');
  }

  const database = await databasePromise;
  await new Promise<void>((resolve, reject) => {
    const transaction = database.transaction(scanStoreNames.catalog, 'readwrite');
    transaction.objectStore(scanStoreNames.catalog).clear();
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error ?? new Error('Unable to clear the test catalog.'));
    transaction.onabort = () => reject(transaction.error ?? new Error('Unable to clear the test catalog.'));
  });
}
