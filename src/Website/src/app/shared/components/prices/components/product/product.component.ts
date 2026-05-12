import {Component, inject, input, output} from '@angular/core';
import {ProductModel} from "../../../../models/product.model";

@Component({
  selector: 'app-product',
  templateUrl: './product.component.html',
  styleUrl: './product.component.scss'
})
export class ProductComponent {
  Product = input.required<ProductModel>();
}
