import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { ProductFacadeService } from '../../facades/product.facade.service';
import { ProductSectionEnum } from '../../enums/productSection.enum';
import { ProductModel } from '../../models/product.model';
import { ProductCategoryEnum } from '../../enums/productCategory.enum';

/**
 * Wrapper Website autour de `vpd-product-list`. Filtre les produits disponibles
 * et ceux autorisés sur le site public.
 */
@Component({
  selector: 'app-prices',
  templateUrl: './prices.component.html',
  styleUrl: './prices.component.scss',
  standalone: false,
})
export class PricesComponent implements OnInit {
  private readonly productFacade = inject(ProductFacadeService);

  section = input(ProductSectionEnum.Bingo);
  category = input<ProductCategoryEnum | null>(null);
  newProduct = input<ProductModel | null>(null);

  allProducts = signal<ProductModel[]>([]);
  readonly isLoading = signal(true);
  readonly loadingProducts = [0, 1, 2, 3];
  filteredProducts = computed(() => {
    return this.allProducts()
      .filter(
        (product) =>
          product.productSection === this.section() &&
          (this.category() === null || product.productCategory === this.category()),
      )
      .filter((product) => product.available)
      .filter((product) => product.visibleOnWebsite)
      .filter((product) => !product.name.includes('euro'))
      .filter((product) => !product.name.includes('centime'))
      .filter((product) => !/^(10|50)\s*c$/i.test(product.name.trim()))
      .sort((a, b) => a.index - b.index);
  });

  ngOnInit(): void {
    this.productFacade.getPublicProducts().then((products) => {
      this.allProducts.set(products ?? []);
    })
      .catch(() => undefined)
      .finally(() => this.isLoading.set(false));
  }
}
