import {Component, inject, input, output} from '@angular/core';
import {ProductModel} from "../../../../models/product.model";

@Component({
    selector: 'app-product',
    templateUrl: './product.component.html',
    styleUrl: './product.component.scss',
    standalone: false
})
export class ProductComponent {
  Product = input.required<ProductModel>();
}
