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
  coverSource?: string | null;
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

export interface CatalogAlertPreferencesResponse {
  alertStatus: 'Active' | 'Suspended' | 'Blocked' | 'None' | string;
  bounceCount: number;
  changed: boolean;
}

export interface CatalogBookReference {
  isbn13: string | null;
  workId: string | null;
  title: string | null;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  coverUrl: string | null;
  source: string;
}

export interface CatalogReferenceSearchResponse {
  generatedAt: string;
  query: string;
  items: CatalogBookReference[];
  page: number;
  pageSize: number;
}

export interface CatalogAdminPeriodMetrics {
  from: string;
  to: string;
  scannedCount: number;
  keptCount: number;
  rejectedCount: number;
  soldQuantity: number;
  soldTitles: number;
}

export interface CatalogAdminStockSummary {
  availableQuantity: number;
  availableTitles: number;
  announcedQuantity: number;
  announcedTitles: number;
}

export interface CatalogAdminFairSummary {
  id: string;
  name: string;
  dateStart: string;
  dateEnd: string | null;
  soldQuantity: number;
  soldTitles: number;
  revenue: number | null;
}

export interface CatalogAdminOverview {
  generatedAt: string;
  currentPeriod: CatalogAdminPeriodMetrics;
  previousPeriod: CatalogAdminPeriodMetrics;
  stock: CatalogAdminStockSummary;
  lastFair: CatalogAdminFairSummary | null;
  deadStockCount: number;
  rareQueueCount: number;
  metadataMissingCount: number;
  undatedAnnouncementCount: number;
  inventoryDriftTitleCount: number;
  inventoryDriftQuantity: number;
  pendingAlerts: {
    pendingCount: number;
    oldestDueAt: string | null;
    nextDueAt: string | null;
  };
}

export interface CatalogAdminAnnouncement {
  id: string;
  isbn13: string;
  fairId: string | null;
  quantity: number;
  status: string;
  createdAt: string;
  releasedAt: string | null;
  scanSessionId: string;
}

export interface CatalogAdminBookMovement {
  id: string;
  isbn13: string;
  type: string;
  quantity: number;
  occurredAt: string;
  receivedAt: string;
  clockSuspect: boolean;
  scanSessionId: string | null;
  volunteerId: string | null;
  fairId: string | null;
  note: string | null;
  clientGestureId: string | null;
  reversalOfMovementId: string | null;
}

export interface CatalogAdminBook {
  isbn13: string;
  workId: string | null;
  title: string | null;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  physicalFormat: string | null;
  language: string | null;
  genre: string | null;
  metadataStatus: string;
  metadataSource: string | null;
  manuallyEditedFields: string | null;
  quantityAvailable: number;
  quantityAnnounced: number;
  salesCount: number;
  rejectionCount: number;
  isRare: boolean;
  isHidden: boolean;
  redirectedToIsbn13: string | null;
  coverUrl: string | null;
  firstSeenAt: string;
  lastAvailableAt: string | null;
  updatedAt: string;
  announcements: CatalogAdminAnnouncement[];
  movements: CatalogAdminBookMovement[];
}

export interface CatalogAdminBookPage {
  generatedAt: string;
  books: CatalogAdminBook[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CatalogAdminOperation {
  isbn13?: string;
  quantityAvailable?: number;
  quantityAnnounced?: number;
  changed: boolean;
  movementId?: string | null;
  scanSessionId?: string;
  affectedMovementCount?: number;
  affectedAlertCount?: number;
}

export interface CatalogAdminAlertOperation {
  messageId: string;
  status: string;
  changed: boolean;
}

export interface CatalogAdminQuantityCorrection {
  isbn13: string;
  previousQuantityAvailable: number;
  quantityAvailable: number;
  delta: number;
  changed: boolean;
  movementId: string | null;
}

export interface CatalogAdminFair {
  id: string;
  name: string;
  dateStart: string;
  dateEnd: string | null;
  isCancelled: boolean;
  revenue: number | null;
}

export interface CatalogAdminFairPage {
  generatedAt: string;
  fairs: CatalogAdminFair[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CatalogAdminFairStats {
  fair: CatalogAdminFair;
  soldQuantity: number;
  soldTitles: number;
  revenue: number | null;
  averageBasket: number | null;
  salesByGenre: {genre: string | null; quantity: number}[];
  topBooks: {isbn13: string; title: string | null; authors: string | null; genre: string | null; quantity: number}[];
  dailySales: {day: string; quantity: number}[];
  previousFairs: {fairId: string; name: string; dateStart: string; soldQuantity: number; revenue: number | null}[];
}

export interface CatalogAdminScanSession {
  id: string;
  volunteerId: string;
  volunteerName: string | null;
  mode: string;
  fairId: string | null;
  fairName: string | null;
  startedAt: string;
  lastScanAt: string;
  lastSyncAt: string;
  endedAt: string | null;
  closeReason: string | null;
  status: string;
  scannedCount: number;
  keptCount: number;
  rejectedCount: number;
  alertCount: number;
  pendingAlertCount: number;
  nextAlertDueAt: string | null;
  movements: CatalogAdminBookMovement[];
}

export interface CatalogAdminScanSessionPage {
  generatedAt: string;
  sessions: CatalogAdminScanSession[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CatalogAdminAlert {
  id: string;
  scanSessionId: string | null;
  memberId: string | null;
  status: string;
  itemCount: number;
  attempts: number;
  createdAt: string;
  dueAt: string;
  sentAt: string | null;
  lastError: string | null;
}

export interface CatalogAdminAlertPage {
  generatedAt: string;
  alerts: CatalogAdminAlert[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CatalogAdminMemberSummary {
  id: string;
  externalId: string | null;
  email: string | null;
  displayName: string | null;
  createdAt: string;
  lastSeenAt: string;
  anonymizedAt: string | null;
  alertStatus: string;
  bounceCount: number;
  watchlistItemCount: number;
  alertHistoryCount: number;
}

export interface CatalogAdminMemberDetail {
  member: CatalogAdminMemberSummary;
  watchlist: {
    id: string;
    scope: string;
    workId: string | null;
    isbn13: string | null;
    title: string | null;
    authors: string | null;
    quantityAvailable: number;
    quantityAnnounced: number;
    addedAt: string;
    lastAlertAt: string | null;
  }[];
  alerts: {id: string; isbn13: string; title: string | null; sentAt: string; outboxMessageId: string | null}[];
}

export interface CatalogAdminMemberOperation {
  memberId: string;
  alertStatus: string;
  changed: boolean;
  deletionCompleted: boolean;
}

export interface CatalogAdminMemberPage {
  generatedAt: string;
  members: CatalogAdminMemberSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CatalogAdminSettings {
  duplicateThreshold: number;
  demandSalesThreshold: number;
  deadStockMinAgeDays: number;
  deadStockMinQuantity: number;
  watchlistMaxItems: number;
  alertCooldownDays: number;
  sessionIdleTimeoutMinutes: number;
  alertDelayMinutes: number;
  updatedAt: string;
  updatedBy: string;
}

export interface CatalogAdminBookFilters {
  search?: string;
  metadataStatus?: string;
  rare?: boolean;
  hidden?: boolean;
  undated?: boolean;
  page?: number;
  pageSize?: number;
}

export interface CatalogAdminSessionFilters {
  status?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export interface CatalogAdminAlertFilters {
  status?: string;
  scanSessionId?: string;
  memberId?: string;
  page?: number;
  pageSize?: number;
}

export interface CatalogAdminMemberFilters {
  search?: string;
  alertStatus?: string;
  page?: number;
  pageSize?: number;
}
