import {Component, computed, effect, inject, input, OnInit, signal} from '@angular/core';
import {ProductFacadeService} from "../../facades/product.facade.service";
import {ProductSectionEnum} from "../../enums/productSection.enum";
import {ProductModel} from "../../models/product.model";
import {ProductCategoryEnum} from "../../enums/productCategory.enum";

@Component({
  selector: 'app-prices',
  templateUrl: './prices.component.html',
  styleUrl: './prices.component.scss'
})
export class PricesComponent implements OnInit {
  productFacade = inject(ProductFacadeService);

  section = input(ProductSectionEnum.Bingo);
  category = input<ProductCategoryEnum | null>(null);
  newProduct = input<ProductModel | null>(null);

  allProducts = signal<ProductModel[]>([]);
  filteredProducts = computed(() => {
    return this.allProducts()
      .filter(product => product.productSection === this.section())
      .filter(product => product.productSection === ProductSectionEnum.Bar ? product.productCategory === this.category() : true)
      .sort((a, b) => {
        if (a.available === b.available) {
          return a.name.localeCompare(b.name);
        }
        return a.available ? -1 : 1;
      });
  })

  constructor() {
    effect(() => {
      console.log("NEW", this.newProduct());
      if (this.newProduct() !== null) {
        this.allProducts.update(products => [...products, this.newProduct()!]);
      }
    }, {allowSignalWrites: true});
  }

  ngOnInit(): void {
    this.productFacade.getAllProducts().then(products => {
      this.allProducts.set(products);
      console.log(products);
    });
  }

  updateProduct($event: ProductModel) {
    this.allProducts.set(this.allProducts().map(product => {
      if (product.id === $event.id) {
        return $event;
      }
      return product;
    }));
  }

  deleteProduct($event: string) {
    this.allProducts.set(this.allProducts().filter(product => product.id !== $event));
  }
}
