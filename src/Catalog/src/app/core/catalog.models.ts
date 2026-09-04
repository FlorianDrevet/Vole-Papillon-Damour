export type CatalogAvailability = 'all' | 'available' | 'next';
export type CatalogSort = 'relevance' | 'recent';

export interface CatalogBook {
  isbn13: string;
  title: string | null;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  physicalFormat: string | null;
  language: string | null;
  genre: string | null;
  workId: string | null;
  coverUrl: string | null;
  quantityAvailable: number;
  quantityAnnounced: number;
  nextFairAt: string | null;
  lastAvailableAt: string | null;
  firstSeenAt: string;
  updatedAt: string;
  isRare: boolean;
}

export interface CatalogSearchResponse {
  generatedAt: string;
  books: CatalogBook[];
  totalCount: number;
  page: number;
  pageSize: number;
  genres: string[];
}

export interface CatalogSearchParams {
  query?: string;
  genre?: string;
  availability?: CatalogAvailability;
  rareOnly?: boolean;
  sort?: CatalogSort;
  page?: number;
  pageSize?: number;
}

export interface CatalogFair {
  id: string;
  name: string;
  dateStart: string;
  dateEnd: string | null;
  openAt: string;
  closeAt: string | null;
  roadNumber: number | null;
  city: string;
  cityCode: number;
  road: string;
}

export interface CatalogWorkResponse {
  workId: string;
  title: string | null;
  authors: string | null;
  editions: CatalogBook[];
}

export interface CatalogDeadStockBook {
  isbn13: string;
  title: string | null;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  genre: string | null;
  quantityAvailable: number;
  firstAvailableAt: string;
}

export interface CatalogDeadStockResponse {
  generatedAt: string;
  minAgeMonths: number;
  minQuantity: number;
  books: CatalogDeadStockBook[];
}

export type CatalogWatchlistScope = 'Work' | 'Edition';

export interface CatalogWatchlistItemRequest {
  scope: CatalogWatchlistScope;
  workId: string | null;
  isbn13: string | null;
}

export interface CatalogAddedWatchlistItem {
  id: string;
  scope: CatalogWatchlistScope;
  workId: string | null;
  isbn13: string | null;
  addedAt: string;
}

export interface CatalogWatchlistItem {
  id: string;
  scope: CatalogWatchlistScope;
  workId: string | null;
  isbn13: string | null;
  book: CatalogBook | null;
  addedAt: string;
  lastAlertAt: string | null;
}

export interface CatalogWatchlistResponse {
  generatedAt: string;
  alertStatus: 'Active' | 'Suspended' | 'Blocked' | string;
  bounceCount: number;
  items: CatalogWatchlistItem[];
}
