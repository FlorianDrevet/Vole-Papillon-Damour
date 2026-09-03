import {Component, inject, signal} from '@angular/core';
import {MatDialog} from "@angular/material/dialog";

import {ProductSectionEnum} from "../../shared/enums/productSection.enum";
import {ProductCategoryEnum} from "../../shared/enums/productCategory.enum";
import {ProductModel} from "../../shared/models/product.model";
import {
  CreateUpdateProductDialogComponent
} from "../../shared/components/dialogs/create-update-product-dialog/create-update-product-dialog.component";

@Component({
    selector: 'app-caisse',
    templateUrl: './caisse.component.html',
    standalone: false
})
export class CaisseComponent {
  private readonly dialog = inject(MatDialog);

  protected readonly ProductSectionEnum = ProductSectionEnum;

  protected readonly filter = signal(ProductSectionEnum.Bar);
  protected readonly filterBar = signal(ProductCategoryEnum.Salt);
  protected readonly createdProduct = signal<ProductModel | null>(null);

  protected readonly sections: ReadonlyArray<{value: ProductSectionEnum; label: string}> = [
    {value: ProductSectionEnum.Bar, label: 'Buvette'},
    {value: ProductSectionEnum.Bingo, label: 'Loto'},
    {value: ProductSectionEnum.Book, label: 'Livres'},
  ];

  protected readonly barCategories: ReadonlyArray<{value: ProductCategoryEnum; label: string}> = [
    {value: ProductCategoryEnum.Salt, label: 'Salé'},
    {value: ProductCategoryEnum.Sugar, label: 'Sucré'},
    {value: ProductCategoryEnum.ColdDrink, label: 'Boisson froide'},
    {value: ProductCategoryEnum.HotDrink, label: 'Boisson chaude'},
  ];

  protected changeFilter(section: ProductSectionEnum): void {
    this.filter.set(section);
  }

  protected changeFilterBar(category: ProductCategoryEnum): void {
    this.filterBar.set(category);
  }

  protected downloadCaisseApp(): void {
    const link = document.createElement('a');
    link.href = 'apks/app-debug.apk';
    link.download = 'caisse-app.apk';
    link.click();
  }

  protected openDialogCreation(): void {
    const dialogRef = this.dialog.open(CreateUpdateProductDialogComponent, {
      data: null,
      maxWidth: '90vw',
      width: 'fit-content',
      height: 'fit-content',
    });

    dialogRef.afterClosed().subscribe((result?: ProductModel | null) => {
      // Fermer le dialogue sans valider renvoie `undefined` : `!== null` laissait
      // passer une entrée vide, qui venait s'ajouter à la grille des produits.
      if (result) {
        this.createdProduct.set(result);
      }
    });
  }
}
