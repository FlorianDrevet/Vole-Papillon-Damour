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

  productEditRequested = output<DsProductModel>();
  productDeleteRequested = output<DsProductModel>();
}
