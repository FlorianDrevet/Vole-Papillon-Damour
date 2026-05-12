import {Component, inject, signal} from '@angular/core';
import {ProductSectionEnum} from "../../shared/enums/productSection.enum";
import {MatDialog} from "@angular/material/dialog";
import {
  CreateUpdateProductDialogComponent
} from "../../shared/components/dialogs/create-update-product-dialog/create-update-product-dialog.component";
import {ProductModel} from "../../shared/models/product.model";
import {ProductCategoryEnum} from "../../shared/enums/productCategory.enum";

@Component({
  selector: 'app-caisse',
  templateUrl: './caisse.component.html',
  styleUrl: './caisse.component.scss'
})
export class CaisseComponent {
  filter = signal(ProductSectionEnum.Bar);
  filterBar = signal(ProductCategoryEnum.Salt);
  eventUpdated = signal<ProductModel | null>(null);

  private readonly dialog = inject(MatDialog);

  protected readonly ProductSectionEnum = ProductSectionEnum;

  changeFilter(section: ProductSectionEnum): void {
    this.filter.set(section);
  }
  protected readonly ProductCategoryEnum = ProductCategoryEnum;

  changeFilterBar(category: ProductCategoryEnum): void {
    this.filterBar.set(category);
  }

  downloadCaisseApp() {
    const link = document.createElement('a');
    link.href = 'apks/app-debug.apk';
    link.download = 'caisse-app.apk';
    link.click();
  }

  openDialogCreation() {
    const dialogRef = this.dialog.open(CreateUpdateProductDialogComponent, {
      data: null,
      "maxWidth": "90vw",
      "width": "fit-content",
      "height": "fit-content",
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result !== null) {
        this.eventUpdated.set(result);
      }
    });
  }
}
