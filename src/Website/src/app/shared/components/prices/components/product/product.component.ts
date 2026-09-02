import { Component, input } from '@angular/core';
import { ProductModel } from '../../../../models/product.model';

/**
 * Wrapper Website autour de `vpd-product-card`. Mode lecture seule + promotions visibles.
 */
@Component({
  selector: 'app-product',
  templateUrl: './product.component.html',
  styleUrl: './product.component.scss',
  standalone: false,
})
export class ProductComponent {
  Product = input.required<ProductModel>();
}
