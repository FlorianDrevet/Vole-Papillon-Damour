import {ProductCategoryEnum} from "../enums/productCategory.enum";
import {ProductSectionEnum} from "../enums/productSection.enum";

export interface ProductModel {
  id: string,
  name: string,
  price: number,
  urlImage: string,
  productCategory: ProductCategoryEnum | null,
  productSection: ProductSectionEnum,
  index: number,
  available: boolean,
  visibleOnWebsite: boolean,
  promotions: PromotionModel[],
}

export interface PromotionModel {
  quantity: number,
  discountedPrice: number,
}
