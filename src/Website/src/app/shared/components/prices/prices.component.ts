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
      .filter(product => product.productSection === this.section() && (this.category() === null || product.productCategory === this.category()))
      .filter(product => product.available)
      .filter(product => !product.name.includes('euro'))
      .filter(product => !product.name.includes('centime'))
      // Tri sur la disponibilité, puis par nom, puis par index
      .sort((a, b) => {
        return a.index - b.index; // Tri par index si le nom est identique
      });
  });

  ngOnInit(): void {
    this.productFacade.getAllProducts().then(products => {
      this.allProducts.set(products);
      console.log(products);
    });
  }
}
