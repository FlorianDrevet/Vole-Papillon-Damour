import {Component, inject, OnInit, signal} from '@angular/core';
import {FormBuilder, FormGroup, Validators} from "@angular/forms";
import {FileUploadInterface} from "../../../interfaces/fileUpload.interface";
import {MatSnackBar} from "@angular/material/snack-bar";
import {MAT_DIALOG_DATA, MatDialogRef} from "@angular/material/dialog";
import {
  CreationUpdatePartieComponent
} from "../../../../feature/event-detail/components/bingo-event/components/dialogs/creation-partie/creation-update-partie.component";
import {ProductModel} from "../../../models/product.model";
import {ProductFacadeService} from "../../../facades/product.facade.service";
import {ProductSectionEnum} from "../../../enums/productSection.enum";
import {ImageUtils} from "../../../utils/image.utils";
import {ProductCategoryEnum} from "../../../enums/productCategory.enum";

@Component({
    selector: 'app-create-update-product-dialog',
    templateUrl: './create-update-product-dialog.component.html',
    styleUrl: './create-update-product-dialog.component.scss',
    standalone: false
})
export class CreateUpdateProductDialogComponent implements OnInit {
  newProductForm: FormGroup;
  isLoading = signal(false);
  updateProduct = signal<ProductModel | null>(null);
  principalImage = signal<FileUploadInterface>({fileName: '', fileContent: ''});

  section = signal<ProductSectionEnum | null>(null);
  category = signal<ProductCategoryEnum | null>(null);
  protected readonly document = document;
  protected readonly ImageUtils = ImageUtils;
  protected readonly ProductCategoryEnum = ProductCategoryEnum;
  protected readonly ProductSectionEnum = ProductSectionEnum;
  private readonly fb = inject(FormBuilder);
  private readonly productFacadeService = inject(ProductFacadeService);
  private readonly _snackBar = inject(MatSnackBar);
  private readonly _dialogRef = inject(MatDialogRef<CreationUpdatePartieComponent>)
  private readonly _data = inject<ProductModel | null>(MAT_DIALOG_DATA);

  constructor() {
    this.updateProduct.set(this._data);

    this.newProductForm = this.fb.group({
      name: ['', Validators.required],
      image: [null, Validators.required],
      price: [null, [Validators.required, Validators.pattern(/^\d+([.,]\d{1,2})?$/)]],
      section: [null, Validators.required],
      category: [null, null],
      available: [true, null],
    });

    if (this.updateProduct() !== null) {
      this.section.set(this.updateProduct()!.productSection);
      this.category.set(this.updateProduct()!.productCategory);

      this.newProductForm.get('name')?.setValue(this.updateProduct()!.name);
      this.newProductForm.get('price')?.setValue(this.updateProduct()!.price);
      this.newProductForm.get('section')?.setValue(this.updateProduct()!.productSection);
      this.newProductForm.get('category')?.setValue(this.updateProduct()!.productCategory);
      this.newProductForm.get('available')?.setValue(this.updateProduct()!.available);

      const fileName = this.updateProduct()!.urlImage.split('/').pop();
      this.principalImage.set({fileName: fileName!, fileContent: new URL(this.updateProduct()!.urlImage)});
      this.newProductForm.get('image')?.setValue(fileName);
    }
  }

  ngOnInit(): void {
    this.newProductForm.get('section')?.valueChanges!.subscribe((value) => {
      this.section.set(value);
      this._changeValidators(value);
    })

    this.newProductForm.get('category')?.valueChanges!.subscribe((value) => {
      this.category.set(value);
    })
  }

  onNoClick(): void {
    this._dialogRef.close(null);
  }

  onYesClick(): void {
    if (this.newProductForm.invalid) {
      this.newProductForm.markAllAsTouched();

      Object.keys(this.newProductForm.controls).forEach(key => {
        const controlErrors = this.newProductForm.get(key)!.errors;
        if (controlErrors) {
          console.log('Control Errors for:', key, controlErrors);
        }
      });
      return;
    }
    this.isLoading.set(true);
    if (this.updateProduct() === null) {
      this.productFacadeService.postCreateProduct$(this.createFormData()).then((result) => {
        this._snackBar.open("Le produit a bien été créé", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
        console.log(result)
        this._dialogRef.close(result);
      }).catch((error) => {
        this._snackBar.open("Erreur lors de la création du produit", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
      })
    } else {
      this.productFacadeService.putUpdateProduct$(this.updateProduct()!.id, this.createFormData()).then((result) => {
        this._snackBar.open("Le produit a bien été modifié", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
        this._dialogRef.close(result);
      }).catch((error) => {
        this._snackBar.open("Erreur lors de la modification de ce produit", "Fermer", {
          duration: 2000,
          horizontalPosition: "end",
          verticalPosition: "top"
        });
        this.isLoading.set(false);
      })
    }
  }

  private _changeValidators(value: ProductSectionEnum) {
    if (value === ProductSectionEnum.Bar) {
      this.newProductForm.get('category')?.setValidators([Validators.required]);
    } else {
      this.newProductForm.get('category')?.clearValidators();
    }
  }

  private createFormData() {
    const formData = new FormData();

    formData.append("Name", this.newProductForm.get('name')?.value);
    formData.append("Price", this.newProductForm.get('price')?.value);
    formData.append("ProductSection", this.newProductForm.get('section')?.value);
    formData.append("Available", this.newProductForm.get('available')?.value);

    if (this.section() === ProductSectionEnum.Bar) {
      formData.append("ProductCategory", this.newProductForm.get('category')?.value);
    }

    if (typeof this.principalImage().fileContent !== 'string') {
      formData.append("UrlImage", this.principalImage().fileContent as string);
    } else {
      formData.append("Image", ImageUtils.createBlobFromImage(this.principalImage().fileContent as string), this.principalImage().fileName);
    }

    return formData;
  }
}
