import { ProductCategoryEnum } from '../enums/product-category.enum';
import { ProductSectionEnum } from '../enums/product-section.enum';

export interface DsPromotionModel {
  quantity: number;
  discountedPrice: number;
}

export interface DsProductModel {
  id: string;
  name: string;
  price: number;
  urlImage: string;
  productCategory: ProductCategoryEnum | null;
  productSection: ProductSectionEnum;
  index: number;
  available: boolean;
  visibleOnWebsite: boolean;
  promotions: DsPromotionModel[];
}
