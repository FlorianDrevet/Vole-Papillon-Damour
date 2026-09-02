import {inject, Injectable} from '@angular/core';
import {AxiosService} from "../services/axios.service";
import {MethodEnum} from "../enums/method.enum";
import {ProductModel} from "../models/product.model";

@Injectable({
  providedIn: 'root'
})
export class ProductFacadeService {

  axiosService = inject(AxiosService);

  public getAllProducts(): Promise<ProductModel[]> {
    return this.axiosService.request(MethodEnum.GET, `/product`, null);
  }

  public getPublicProducts(): Promise<ProductModel[]> {
    return this.axiosService.request(MethodEnum.GET, `/product/public`, null);
  }

  public postCreateProduct$(createProductForm: FormData): Promise<ProductModel> {
    return this.axiosService.request(MethodEnum.POST, `/product`, createProductForm, {}, true);
  }

  public putUpdateProduct$(productId: string, updateProductForm: FormData): Promise<ProductModel> {
    return this.axiosService.request(MethodEnum.PUT, `/product/${productId}`, updateProductForm, {}, true);
  }

  public deleteProduct$(productId: string): Promise<void> {
    return this.axiosService.request(MethodEnum.DELETE, `/product/${productId}`, null);
  }
}
