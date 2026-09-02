import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';

import { ProductSectionEnum } from '../../../enums/productSection.enum';
import { ProductFacadeService } from '../../../facades/product.facade.service';
import { ProductModel } from '../../../models/product.model';
import { CreateUpdateProductDialogComponent } from './create-update-product-dialog.component';

describe('CreateUpdateProductDialogComponent', () => {
  let fixture: ComponentFixture<CreateUpdateProductDialogComponent>;
  let component: CreateUpdateProductDialogComponent;

  const product: ProductModel = {
    id: 'cash-only-1',
    name: '1 euro',
    price: 1,
    urlImage: 'https://cdn.example.test/cash-only.png',
    productCategory: null,
    productSection: ProductSectionEnum.Bingo,
    index: 1,
    available: true,
    visibleOnWebsite: false,
    promotions: [],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [CreateUpdateProductDialogComponent],
      imports: [ReactiveFormsModule],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: product },
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: MatSnackBar, useValue: { open: jasmine.createSpy('open') } },
        { provide: ProductFacadeService, useValue: jasmine.createSpyObj('ProductFacadeService', [
          'postCreateProduct$',
          'putUpdateProduct$',
        ]) },
      ],
    })
      .overrideComponent(CreateUpdateProductDialogComponent, { set: { template: '' } })
      .compileComponents();

    fixture = TestBed.createComponent(CreateUpdateProductDialogComponent);
    component = fixture.componentInstance;
  });

  it('preserves the public visibility choice and sends it in the form data', () => {
    expect(component.newProductForm.get('visibleOnWebsite')?.value).toBeFalse();

    const formData = (component as any).createFormData() as FormData;

    expect(formData.get('VisibleOnWebsite')).toBe('false');
  });
});
