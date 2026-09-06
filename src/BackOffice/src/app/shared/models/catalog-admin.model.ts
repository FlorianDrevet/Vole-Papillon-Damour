export interface AdminPeriodMetrics {
  from: string;
  to: string;
  scannedCount: number;
  keptCount: number;
  rejectedCount: number;
  soldQuantity: number;
  soldTitles: number;
}

export interface AdminStockSummary {
  availableQuantity: number;
  availableTitles: number;
  announcedQuantity: number;
  announcedTitles: number;
}

export interface AdminFairSummary {
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
  currentPeriod: AdminPeriodMetrics;
  previousPeriod: AdminPeriodMetrics;
  stock: AdminStockSummary;
  lastFair: AdminFairSummary | null;
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

export interface AdminAnnouncement {
  id: string;
  isbn13: string;
  fairId: string | null;
  quantity: number;
  status: string;
  createdAt: string;
  releasedAt: string | null;
  scanSessionId: string;
}

export interface AdminBookMovement {
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

export interface AdminBook {
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
  announcements: AdminAnnouncement[];
  movements: AdminBookMovement[];
}

export interface AdminBookPage {
  generatedAt: string;
  books: AdminBook[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdminBookOperation {
  isbn13: string;
  quantityAvailable: number;
  quantityAnnounced: number;
  changed: boolean;
  movementId: string | null;
}

export interface AdminQuantityCorrection {
  isbn13: string;
  previousQuantityAvailable: number;
  quantityAvailable: number;
  delta: number;
  changed: boolean;
  movementId: string | null;
}

export interface AdminFair {
  id: string;
  name: string;
  dateStart: string;
  dateEnd: string | null;
  isCancelled: boolean;
  revenue: number | null;
}

export interface AdminFairPage {
  generatedAt: string;
  fairs: AdminFair[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdminFairStats {
  fair: AdminFair;
  soldQuantity: number;
  soldTitles: number;
  revenue: number | null;
  averageBasket: number | null;
  salesByGenre: {genre: string | null; quantity: number}[];
  topBooks: {isbn13: string; title: string | null; authors: string | null; genre: string | null; quantity: number}[];
  dailySales: {day: string; quantity: number}[];
  previousFairs: {fairId: string; name: string; dateStart: string; soldQuantity: number; revenue: number | null}[];
}

export interface AdminScanSession {
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
  movements: AdminBookMovement[];
}

export interface AdminScanSessionPage {
  generatedAt: string;
  sessions: AdminScanSession[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdminOperation {
  scanSessionId: string;
  affectedMovementCount: number;
  affectedAlertCount: number;
  changed: boolean;
}

export interface AdminAlert {
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

export interface AdminAlertPage {
  generatedAt: string;
  alerts: AdminAlert[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdminAlertOperation {
  messageId: string;
  status: string;
  changed: boolean;
}

export interface AdminMemberSummary {
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

export interface AdminMemberDetail {
  member: AdminMemberSummary;
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

export interface AdminMemberPage {
  generatedAt: string;
  members: AdminMemberSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdminMemberOperation {
  memberId: string;
  alertStatus: string;
  changed: boolean;
  deletionCompleted: boolean;
}

export interface AdminSettings {
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

export interface AdminBookFilters {
  search?: string;
  metadataStatus?: string;
  rare?: boolean;
  hidden?: boolean;
  undated?: boolean;
  page?: number;
  pageSize?: number;
}

export interface AdminSessionFilters {
  status?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}
