import {TestBed} from '@angular/core/testing';

import {ScanConfirmationService} from './scan-confirmation.service';

describe('ScanConfirmationService', () => {
  let service: ScanConfirmationService;

  beforeEach(() => {
    TestBed.configureTestingModule({providers: [ScanConfirmationService]});
    service = TestBed.inject(ScanConfirmationService);
  });

  it('resolves the pending confirmation from an explicit user action', async () => {
    const result = service.confirm({
      title: 'Quitter le tri ?',
      message: 'La session reste ouverte.',
      confirmLabel: 'Quitter',
      cancelLabel: 'Rester',
    });

    expect(service.request()).toEqual(jasmine.objectContaining({title: 'Quitter le tri ?'}));
    service.resolve(true);

    await expectAsync(result).toBeResolvedTo(true);
    expect(service.request()).toBeNull();
  });
});
