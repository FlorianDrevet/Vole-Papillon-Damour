import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DesignSystemModule } from '@vpd/ui';
import { ProductSectionEnum } from '../../enums/productSection.enum';
import { ProductFacadeService } from '../../facades/product.facade.service';
import { ProductModel } from '../../models/product.model';
import { PricesComponent } from './prices.component';
import { ProductComponent } from './components/product/product.component';

describe('PricesComponent', () => {
  let fixture: ComponentFixture<PricesComponent>;
  let component: PricesComponent;
  let productFacade: jasmine.SpyObj<ProductFacadeService>;

  const publicProduct: ProductModel = {
    id: 'public-1',
    name: 'Tarif public',
    price: 2,
    urlImage: 'https://cdn.example.test/public.png',
    productCategory: null,
    productSection: ProductSectionEnum.Bingo,
    index: 1,
    available: true,
    visibleOnWebsite: true,
    promotions: [],
  };

  const cashOnlyProduct: ProductModel = {
    ...publicProduct,
    id: 'cash-only-1',
    name: '1 euro',
    visibleOnWebsite: false,
    index: 2,
  };

  const coinProduct: ProductModel = {
    ...publicProduct,
    id: 'coin-50c',
    name: '50c',
    index: 3,
  };

  beforeEach(async () => {
    productFacade = jasmine.createSpyObj<ProductFacadeService>('ProductFacadeService', ['getPublicProducts']);
    productFacade.getPublicProducts.and.returnValue(Promise.resolve([publicProduct, cashOnlyProduct, coinProduct]));

    await TestBed.configureTestingModule({
      declarations: [PricesComponent, ProductComponent],
      imports: [DesignSystemModule],
      providers: [{ provide: ProductFacadeService, useValue: productFacade }],
    }).compileComponents();

    fixture = TestBed.createComponent(PricesComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('section', ProductSectionEnum.Bingo);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('loads public products and excludes cash-only products', () => {
    expect(productFacade.getPublicProducts).toHaveBeenCalled();
    expect(component.filteredProducts()).toEqual([publicProduct]);
  });

  it('excludes coin denominations from the public price cards', () => {
    expect(component.filteredProducts().some(product => product.name === '50c')).toBeFalse();
  });
});
