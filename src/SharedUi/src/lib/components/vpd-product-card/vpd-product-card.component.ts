import { Component, input, output } from '@angular/core';
import { DsProductModel } from '../../models/ds-product.model';

/**
 * Carte produit unifiée.
 *
 * - `editable=false` (défaut) : affichage seul, affiche promotions (Website).
 * - `editable=true` : affiche les boutons edit/supprimer (BackOffice).
 *
 * `showPromotions` (défaut `true`) permet de masquer les promotions en mode caisse.
 */
@Component({
  selector: 'vpd-product-card',
  templateUrl: './vpd-product-card.component.html',
  standalone: false,
})
export class VpdProductCardComponent {
  product = input.required<DsProductModel>();
  editable = input<boolean>(false);
  showPromotions = input<boolean>(true);

  editRequested = output<DsProductModel>();
  deleteRequested = output<DsProductModel>();

  onEdit(): void {
    this.editRequested.emit(this.product());
  }

  onDelete(): void {
    this.deleteRequested.emit(this.product());
  }
}
