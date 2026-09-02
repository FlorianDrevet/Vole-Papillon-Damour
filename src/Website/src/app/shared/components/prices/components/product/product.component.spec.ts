import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DesignSystemModule } from '@vpd/ui';
import { ProductSectionEnum } from '../../../../enums/productSection.enum';
import { ProductModel } from '../../../../models/product.model';
import { ProductComponent } from './product.component';

describe('ProductComponent', () => {
  let fixture: ComponentFixture<ProductComponent>;

  const product: ProductModel = {
    id: 'book-1',
    name: 'Roman',
    price: 2.5,
    urlImage: 'https://cdn.example.test/roman.png',
    productCategory: null,
    productSection: ProductSectionEnum.Book,
    index: 1,
    available: true,
    visibleOnWebsite: true,
    promotions: [{ quantity: 6, discountedPrice: 12 }],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ProductComponent],
      imports: [DesignSystemModule],
    }).compileComponents();

    fixture = TestBed.createComponent(ProductComponent);
    fixture.componentRef.setInput('Product', product);
    fixture.detectChanges();
  });

  it('renders the API image, price, and promotion in the Website card', () => {
    expect(fixture.nativeElement.querySelector('.website-product-card')).not.toBeNull();

    const image = fixture.nativeElement.querySelector('img');
    const cardText = fixture.nativeElement.textContent;

    expect(image.getAttribute('src')).toBe(product.urlImage);
    expect(cardText).toContain('2.50€');
    expect(cardText).toContain('12.00€');
  });
});
