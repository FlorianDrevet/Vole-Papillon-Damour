import { Component, inject, input, output } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ProductModel } from '../../../../models/product.model';
import { CreateUpdateProductDialogComponent } from '../../../dialogs/create-update-product-dialog/create-update-product-dialog.component';
import { ConfirmationDialogComponent } from '../../../dialogs/confirmation-dialog/confirmation-dialog.component';
import { ProductFacadeService } from '../../../../facades/product.facade.service';

/**
 * Wrapper "smart" autour du composant `vpd-product-card` du design system.
 * Conserve le selector `app-product` historique.
 */
@Component({
  selector: 'app-product',
  templateUrl: './product.component.html',
  standalone: false,
})
export class ProductComponent {
  Product = input.required<ProductModel>();

  productUpdated = output<ProductModel>();
  productDeleted = output<string>();

  private readonly dialog = inject(MatDialog);
  private readonly _snackBar = inject(MatSnackBar);
  private readonly productFacade = inject(ProductFacadeService);

  openDialogUpdate(): void {
    const dialogRef = this.dialog.open(CreateUpdateProductDialogComponent, {
      data: this.Product(),
      maxWidth: '90vw',
      width: 'fit-content',
      height: 'fit-content',
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result !== null) {
        this.productUpdated.emit(result);
      }
    });
  }

  openDialogDeletion(): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: { title: 'Êtes-vous sûr de vouloir supprimer ce produit ?' },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.productFacade.deleteProduct$(this.Product().id).then(() => {
          this._snackBar.open('Produit supprimé avec succès', 'Fermer', {
            duration: 2000,
            horizontalPosition: 'end',
            verticalPosition: 'top',
          });
          this.productDeleted.emit(this.Product().id);
        }).catch(() => {
          this._snackBar.open('Erreur lors de la suppression de ce produit', 'Fermer', {
            duration: 2000,
            horizontalPosition: 'end',
            verticalPosition: 'top',
          });
        });
      }
    });
  }
}
