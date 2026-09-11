import {MyDate} from "../extensions/MyDate";

export type ActualityStatus = 'Draft' | 'Published';

export interface ActualityModel {
  id: string,
  title: string,
  urlPrincipalImage: string,
  images: string[],
  article: string,
  date: MyDate,
  facebookLink: string | null,
  instagramLink: string | null,
  status: ActualityStatus,
  titleNeedsReview: boolean,
  importedAt: string | null,
}
