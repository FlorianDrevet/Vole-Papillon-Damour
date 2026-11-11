import { Component, input, output } from '@angular/core';
import { DsProductModel } from '../../models/ds-product.model';

/**
 * Grille de produits unifiée (purement présentation).
 *
 * Le tri/filtre/source des produits reste à la charge du parent (chaque app
 * a sa propre logique : Website filtre par disponibilité, BackOffice montre tout).
 */
@Component({
  selector: 'vpd-product-list',
  templateUrl: './vpd-product-list.component.html',
  styleUrl: './vpd-product-list.component.scss',
  standalone: false,
})
export class VpdProductListComponent {
  products = input.required<DsProductModel[]>();
  editable = input<boolean>(false);
  showPromotions = input<boolean>(true);
  reorderable = input<boolean>(false);
  reorderDisabled = input<boolean>(false);

  productEditRequested = output<DsProductModel>();
  productDeleteRequested = output<DsProductModel>();
  productOrderChangeRequested = output<string[]>();

  moveProduct(index: number, direction: -1 | 1): void {
    const nextIndex = index + direction;
    const products = this.products();
    if (
      !this.reorderable() ||
      this.reorderDisabled() ||
      index < 0 ||
      nextIndex < 0 ||
      nextIndex >= products.length
    ) {
      return;
    }

    const reorderedProducts = [...products];
    [reorderedProducts[index], reorderedProducts[nextIndex]] = [
      reorderedProducts[nextIndex],
      reorderedProducts[index],
    ];
    this.productOrderChangeRequested.emit(reorderedProducts.map((product) => product.id));
  }
}
