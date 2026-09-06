import {TestBed} from '@angular/core/testing';

import {CatalogAdminFacadeService} from './catalog-admin.facade.service';
import {MethodEnum} from '../enums/method.enum';
import {AxiosService} from '../services/axios.service';

describe('CatalogAdminFacadeService', () => {
  let service: CatalogAdminFacadeService;
  let axiosService: jasmine.SpyObj<AxiosService>;

  beforeEach(() => {
    axiosService = jasmine.createSpyObj<AxiosService>('AxiosService', ['request$']);
    axiosService.request$.and.resolveTo({});
    TestBed.configureTestingModule({
      providers: [
        CatalogAdminFacadeService,
        {provide: AxiosService, useValue: axiosService},
      ],
    });
    service = TestBed.inject(CatalogAdminFacadeService);
  });

  it('passes the typed book filters as query parameters', async () => {
    await service.getBooks({search: ' Camus ', rare: true, undated: false, page: 2, pageSize: 25});

    expect(axiosService.request$).toHaveBeenCalledWith(
      MethodEnum.GET,
      '/books/admin/books',
      {search: ' Camus ', rare: true, undated: false, page: 2, pageSize: 25},
    );
  });

  it('uses PATCH for quantity correction and keeps the ISBN in the route', async () => {
    await service.correctQuantity('9782070612758', {quantityAvailable: 4, note: 'Inventaire'});

    expect(axiosService.request$).toHaveBeenCalledWith(
      MethodEnum.PATCH,
      '/books/admin/books/9782070612758/quantity',
      {quantityAvailable: 4, note: 'Inventaire'},
    );
  });

  it('uses explicit query flags for rare and visibility actions', async () => {
    await service.setRare('9782070612758', true);
    await service.setVisibility('9782070612758', false);

    expect(axiosService.request$).toHaveBeenCalledWith(
      MethodEnum.POST,
      '/books/admin/books/9782070612758/rare?isRare=true',
      null,
    );
    expect(axiosService.request$).toHaveBeenCalledWith(
      MethodEnum.POST,
      '/books/admin/books/9782070612758/visibility?hidden=false',
      null,
    );
  });
});
