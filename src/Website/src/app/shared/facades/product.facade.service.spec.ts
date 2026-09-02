import { TestBed } from '@angular/core/testing';

import { MethodEnum } from '../enums/method.enum';
import { AxiosService } from '../services/axios.service';
import { ProductFacadeService } from './product.facade.service';

describe('ProductFacadeService', () => {
  let facade: ProductFacadeService;
  let axiosService: jasmine.SpyObj<AxiosService>;

  beforeEach(() => {
    axiosService = jasmine.createSpyObj<AxiosService>('AxiosService', ['request']);
    axiosService.request.and.returnValue(Promise.resolve([]));

    TestBed.configureTestingModule({
      providers: [
        ProductFacadeService,
        { provide: AxiosService, useValue: axiosService },
      ],
    });

    facade = TestBed.inject(ProductFacadeService);
  });

  it('requests the public product projection', async () => {
    await facade.getPublicProducts();

    expect(axiosService.request).toHaveBeenCalledWith(MethodEnum.GET, '/product/public', null);
  });
});
