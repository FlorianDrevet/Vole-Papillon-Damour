/**
 * Date contract accepté par le design system.
 * Compatible avec Date, MyDate (BackOffice) et string ISO.
 * Le pipe `date` natif d'Angular accepte ces trois formes.
 */
export type DsDateLike = Date | string | number;

export interface DsActualityModel {
  id: string;
  title: string;
  urlPrincipalImage: string;
  images: string[];
  article: string;
  date: DsDateLike;
  facebookLink: string | null;
  instagramLink: string | null;
}
