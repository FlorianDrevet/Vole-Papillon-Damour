import { Component, computed, effect, inject, input, OnInit, signal } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ProductFacadeService } from '../../facades/product.facade.service';
import { ProductSectionEnum } from '../../enums/productSection.enum';
import { ProductModel } from '../../models/product.model';
import { ProductCategoryEnum } from '../../enums/productCategory.enum';
import { CreateUpdateProductDialogComponent } from '../dialogs/create-update-product-dialog/create-update-product-dialog.component';
import { ConfirmationDialogComponent } from '../dialogs/confirmation-dialog/confirmation-dialog.component';

/**
 * Wrapper "smart" autour de `vpd-product-list`.
 * Conserve le selector `app-prices` et la responsabilité de fetch + filtrage + édition.
 */
@Component({
  selector: 'app-prices',
  templateUrl: './prices.component.html',
  standalone: false,
})
export class PricesComponent implements OnInit {
  private readonly productFacade = inject(ProductFacadeService);
  private readonly dialog = inject(MatDialog);
  private readonly _snackBar = inject(MatSnackBar);

  section = input(ProductSectionEnum.Bingo);
  category = input<ProductCategoryEnum | null>(null);
  newProduct = input<ProductModel | null>(null);

  allProducts = signal<ProductModel[]>([]);
  isLoading = signal(true);
  hasFailed = signal(false);

  filteredProducts = computed(() => {
    return this.allProducts()
      .filter((product) => product.productSection === this.section())
      .filter((product) =>
        product.productSection === ProductSectionEnum.Bar
          ? product.productCategory === this.category()
          : true,
      )
      .sort((a, b) => {
        if (a.available === b.available) {
          return a.name.localeCompare(b.name);
        }
        return a.available ? -1 : 1;
      });
  });

  constructor() {
    effect(
      () => {
        const product = this.newProduct();
        if (product !== null) {
          this.allProducts.update((products) => [...products, product]);
        }
      },
      { allowSignalWrites: true },
    );
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);
    this.hasFailed.set(false);

    this.productFacade
      .getAllProducts()
      .then((products) => {
        this.allProducts.set(products);
        this.isLoading.set(false);
      })
      .catch(() => {
        // La grille restait vide et muette quand l'appel échouait : impossible de
        // distinguer « aucun produit dans cette catégorie » d'une panne réseau.
        this.hasFailed.set(true);
        this.isLoading.set(false);
      });
  }

  onEditRequested(product: ProductModel): void {
    const dialogRef = this.dialog.open(CreateUpdateProductDialogComponent, {
      data: product,
      maxWidth: '90vw',
      width: 'fit-content',
      height: 'fit-content',
    });

    dialogRef.afterClosed().subscribe((result: ProductModel | null) => {
      if (result) {
        this.allProducts.update((all) => all.map((p) => (p.id === result.id ? result : p)));
      }
    });
  }

  onDeleteRequested(product: ProductModel): void {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      data: { title: 'Êtes-vous sûr de vouloir supprimer ce produit ?' },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.productFacade
          .deleteProduct$(product.id)
          .then(() => {
            this._snackBar.open('Produit supprimé avec succès', 'Fermer', {
              duration: 2000,
              horizontalPosition: 'end',
              verticalPosition: 'top',
            });
            this.allProducts.update((all) => all.filter((p) => p.id !== product.id));
          })
          .catch(() => {
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
