export type CatalogAvailability = 'all' | 'available' | 'next';
export type CatalogSort = 'relevance' | 'recent' | 'price-asc' | 'price-desc';

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
  rareBookSlug?: string | null;
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
  includeExhausted?: boolean;
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

/** Public event shape returned by the upcoming association-event collection. */
export interface CatalogPublicEventResponse {
  id: string;
  name: string;
  eventType: string;
  dateStart: string;
  dateEnd: string | null;
  hourOpenDoors: string | null;
  hourCloseDoors: string | null;
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

export type CatalogSelectionStatus = 'ToTake' | 'Purchased';
export type CatalogSelectionAvailability = 'Available' | 'Announced' | 'OutOfStock' | 'RareSold' | 'Unavailable';
export type CatalogSelectionKind = 'edition' | 'rare';
export type CatalogNotFoundReportStatus = 'Open' | 'Found' | 'Withdrawn' | 'Dismissed' | 'Lapsed';
export type CatalogNotFoundLocation = 'Fair' | 'Premises';
export type CatalogNotFoundWithdrawalReason = 'NotFoundOnShelf' | 'Damaged' | 'Other';

export interface CatalogNotFoundReportSummary {
  id: string;
  status: CatalogNotFoundReportStatus;
  reportedAt: string;
  closedAt: string | null;
}

export interface CatalogNotFoundReportCreated {
  reportId: string;
  reportedAt: string;
  alreadyOpen: boolean;
}

export interface CatalogNotFoundReportComment {
  text: string;
  location: CatalogNotFoundLocation | null;
  reportedAt: string;
}

export type CatalogNotFoundTargetKind = 'edition' | 'rare';
export type CatalogNotFoundQueueSort = 'most-reported' | 'oldest' | 'newest' | 'genre';

export interface CatalogNotFoundQueueParams {
  kind: 'all' | CatalogNotFoundTargetKind;
  sort: CatalogNotFoundQueueSort;
  page: number;
  pageSize: number;
}

export interface CatalogNotFoundQueueTarget {
  kind: CatalogNotFoundTargetKind;
  isbn13: string | null;
  rareBookId: string | null;
  title: string;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  coverUrl: string | null;
  genre: string | null;
  quantityAvailable: number;
  reportCount: number;
  memberCount: number;
  firstReportedAt: string;
  lastReportedAt: string;
  overdue: boolean;
  comments: CatalogNotFoundReportComment[];
}

export interface CatalogNotFoundQueue {
  generatedAt: string;
  openTargetCount: number;
  openReportCount: number;
  overdueTargetCount: number;
  page: number;
  pageSize: number;
  totalCount: number;
  items: CatalogNotFoundQueueTarget[];
}

export interface CatalogClosedNotFoundReport {
  closedAt: string;
  kind: CatalogNotFoundTargetKind;
  isbn13: string | null;
  rareBookId: string | null;
  title: string;
  outcome: 'Found' | 'Withdrawn' | 'Dismissed' | 'Lapsed' | string;
  reportCount: number;
  withdrawnQuantity: number | null;
  closedByName: string | null;
  note: string | null;
}

export interface CatalogClosedNotFoundReports {
  generatedAt: string;
  page: number;
  pageSize: number;
  totalCount: number;
  items: CatalogClosedNotFoundReport[];
}

export type CatalogNotFoundSummaryTarget =
  | {isbn13: string; rareBookId?: never}
  | {rareBookId: string; isbn13?: never};

export interface CatalogNotFoundSummary {
  openTargetCount: number;
  overdueTargetCount: number;
  openReportCount: number;
  firstReportedAt: string | null;
  latestComment: CatalogNotFoundReportComment | null;
}

export interface CatalogNotFoundClosure {
  closedReportCount: number;
  withdrawnQuantity: number | null;
  quantityAvailable: number | null;
  movementId: string | null;
}

export type CatalogNotFoundCloseAction = 'found' | 'withdrawal' | 'dismissal';
export type CatalogNotFoundCloseRequest =
  | {note?: string}
  | {quantityFound: number; reason: CatalogNotFoundWithdrawalReason; note: string}
  | {reason: CatalogNotFoundWithdrawalReason; note: string};

export interface CatalogMemberCard {
  qrPayload: string;
  recoveryCode: string;
  displayLabel: string;
  issuedAt: string;
}

export interface CatalogSelectionTargetRequest {
  isbn13?: string;
  rareBookId?: string;
}

export interface CatalogSelectionMergeEntry {
  isbn13?: string;
  rareBookId?: string;
  addedAt: string;
}

export interface CatalogAddedSelectionItem {
  id: string;
  alreadyPresent: boolean;
}

export interface CatalogSelectionItem {
  id: string;
  kind: CatalogSelectionKind;
  isbn13: string | null;
  rareBookId: string | null;
  rareBookSlug: string | null;
  title: string;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  physicalFormat: string | null;
  coverUrl: string | null;
  availability: CatalogSelectionAvailability;
  availabilityCheckedAt: string;
  status: CatalogSelectionStatus;
  notFoundReport: CatalogNotFoundReportSummary | null;
  addedAt: string;
  purchasedAt: string | null;
}

export interface CatalogSelectionResponse {
  generatedAt: string;
  nextFair: {id: string; startsAt: string} | null;
  items: CatalogSelectionItem[];
}

export interface CatalogSelectionMergeResult {
  added: number;
  alreadyPresent: number;
  rejected: string[];
}

export type CatalogPurchaseLineState = 'Associated' | 'Cancelled';

export interface CatalogPurchaseLine {
  id: string;
  kind: 'edition' | 'rare';
  isbn13: string | null;
  rareBookId: string | null;
  title: string;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  physicalFormat: string | null;
  quantity: number;
  state: CatalogPurchaseLineState;
  currentCoverUrl: string | null;
}

export interface CatalogPurchasePassage {
  id: string;
  reference: string;
  occurredAt: string;
  fairId: string | null;
  fairLabel: string | null;
  activeBookCount: number;
  lines: CatalogPurchaseLine[];
}

export interface CatalogPurchasesResponse {
  passages: CatalogPurchasePassage[];
  nextCursor: string | null;
}

export type CatalogWatchlistScope = 'Work' | 'Edition' | 'RareBook';

export interface CatalogWatchlistItemRequest {
  scope: CatalogWatchlistScope;
  workId: string | null;
  isbn13: string | null;
  title?: string | null;
  authors?: string | null;
  publisher?: string | null;
  publicationYear?: number | null;
  coverUrl?: string | null;
  rareBookId?: string | null;
}

export interface CatalogAddedWatchlistItem {
  id: string;
  scope: CatalogWatchlistScope;
  workId: string | null;
  isbn13: string | null;
  rareBookId?: string | null;
  addedAt: string;
}

export interface CatalogWatchlistItem {
  id: string;
  scope: CatalogWatchlistScope;
  workId: string | null;
  isbn13: string | null;
  rareBookId?: string | null;
  title?: string | null;
  authors?: string | null;
  publisher?: string | null;
  publicationYear?: number | null;
  coverUrl: string | null;
  book: CatalogBook | null;
  rareBook?: CatalogRareBook | null;
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

export interface CatalogVolunteerStatisticsResponse {
  generatedAt: string;
  memberSince: string | null;
  scan: CatalogVolunteerScanStatistics;
  cash: CatalogVolunteerCashStatistics;
}

export interface CatalogVolunteerScanStatistics {
  scannedCount: number;
  keptCount: number;
  rejectedCount: number;
  sessionCount: number;
  durationMinutes: number;
  firstSessionAt: string | null;
  medianTeamKeepRatePercent: number | null;
  monthly: CatalogVolunteerMonthlyStatistics[];
  timeSlots: CatalogVolunteerTimeSlotStatistics[];
  impact: CatalogVolunteerImpactStatistics;
  topGenres: CatalogVolunteerGenreStatistics[];
  recentSessions: CatalogVolunteerSessionStatistics[];
}

export interface CatalogVolunteerCashStatistics {
  grossSoldQuantity: number;
  soldQuantity: number;
  saleMovementCount: number;
  voidedSaleQuantity: number;
  fairCount: number;
  estimatedDurationMinutes: number;
  estimatedCadencePerHour: number | null;
  medianTeamCadencePerHour: number | null;
  fairBreakdown: CatalogVolunteerFairStatistics[];
  peak: CatalogVolunteerPeakStatistics;
  topBooks: CatalogVolunteerBookStatistics[];
  topGenres: CatalogVolunteerGenreStatistics[];
  estimatedTriAndCashOverlap: number;
  estimatedRevenueShare: number | null;
  revenueShare: CatalogVolunteerRevenueShare | null;
  durationIsEstimated: boolean;
  cadenceIsEstimated: boolean;
  revenueShareIsEstimated: boolean;
  triAndCashOverlapIsEstimated: boolean;
}

export interface CatalogVolunteerMonthlyStatistics {
  periodStart: string;
  kept: number;
  rejected: number;
}

export interface CatalogVolunteerTimeSlotStatistics {
  dayOfWeek: number;
  slot: 'morning' | 'afternoon' | 'evening' | string;
  count: number;
}

export interface CatalogVolunteerImpactStatistics {
  foundReaderCount: number;
  newTitleCount: number;
  rareCount: number;
  alertItemCount: number;
  foundReaderIsEstimated: boolean;
}

export interface CatalogVolunteerGenreStatistics {
  name: string;
  quantity: number;
}

export interface CatalogVolunteerSessionStatistics {
  id: string;
  startedAt: string;
  durationMinutes: number;
  scannedCount: number;
  keptRatePercent: number;
  mode: string;
}

export interface CatalogVolunteerFairStatistics {
  id: string;
  name: string;
  dateStart: string;
  netSoldQuantity: number;
}

export interface CatalogVolunteerPeakStatistics {
  fairDate: string | null;
  localHour: number | null;
  quantity: number;
}

export interface CatalogVolunteerBookStatistics {
  isbn13: string;
  title: string;
  quantity: number;
}

export interface CatalogVolunteerRevenueShare {
  fairId: string;
  fairName: string;
  fairDate: string;
  fairRevenue: number;
  volunteerSoldQuantity: number;
  fairSoldQuantity: number;
  estimatedShare: number;
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

export interface CatalogAdminAddBookRequest {
  isbn13: string;
  quantityAvailable: number;
  note: string;
  title?: string | null;
  authors?: string | null;
  publisher?: string | null;
  publicationYear?: number | null;
  physicalFormat?: string | null;
  language?: string | null;
  genre?: string | null;
  coverUrl?: string | null;
  workId?: string | null;
  fields?: string[] | null;
}

export interface CatalogAdminQuantityCorrectionRequest {
  quantityAvailable: number;
  note: string;
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

export interface CatalogAdminVolunteerStatistics {
  generatedAt: string;
  from: string | null;
  to: string | null;
  fairId: string | null;
  team: CatalogAdminVolunteerTeamSummary;
  volunteers: CatalogAdminVolunteerContribution[];
  monthlyActivity: CatalogAdminVolunteerMonthlyActivity[];
  renewal: CatalogAdminVolunteerRenewal;
  dominantGenres: CatalogAdminVolunteerGenre[];
}

export interface CatalogAdminVolunteerTeamSummary {
  activeVolunteerCount: number;
  scannedCount: number;
  keptCount: number;
  rejectedCount: number;
  keptRatePercent: number | null;
  soldQuantity: number;
  soldOfKeptRatePercent: number | null;
  sessionCount: number;
  scanDurationMinutes: number;
  cashDurationMinutes: number;
  totalDurationMinutes: number;
  averageSessionsPerVolunteer: number | null;
}

export interface CatalogAdminVolunteerContribution {
  volunteerId: string;
  displayName: string;
  roles: string[];
  sessionCount: number;
  scanDurationMinutes: number;
  cashDurationMinutes: number;
  totalDurationMinutes: number;
  scannedCount: number;
  keptCount: number;
  keptRatePercent: number | null;
  soldQuantity: number;
  waitingQuantity: number;
  waitingOverYearQuantity: number;
  flowRatePercent: number | null;
  firstActivityAt: string | null;
  lastActivityAt: string | null;
  dominantGenre: string | null;
}

export interface CatalogAdminVolunteerMonthlyActivity {
  volunteerId: string;
  displayName: string;
  months: CatalogAdminVolunteerMonth[];
}

export interface CatalogAdminVolunteerMonth {
  periodStart: string;
  sessionCount: number;
}

export interface CatalogAdminVolunteerRenewal {
  newCount: number;
  regularCount: number;
  withdrawingCount: number;
  windowDays: number;
}

export interface CatalogAdminVolunteerGenre {
  name: string;
  quantity: number;
}

export interface CatalogAdminFairsEvolution {
  generatedAt: string;
  from: string | null;
  to: string | null;
  fairCount: number;
  totalSoldQuantity: number;
  totalRevenue: number | null;
  averageBasket: number | null;
  growthSinceFirstPercent: number | null;
  seasons: {season: string; averageSoldQuantity: number | null; fairCount: number}[];
  fairs: CatalogAdminFairEvolutionEntry[];
}

export interface CatalogAdminFairEvolutionEntry {
  fairId: string;
  name: string;
  dateStart: string;
  dateEnd: string | null;
  soldQuantity: number;
  revenue: number | null;
  averageBasket: number | null;
  variationPercent: number | null;
  daysOpen: number | null;
}

export interface CatalogAdminCatalogueFlowStats {
  generatedAt: string;
  from: string | null;
  to: string | null;
  funnel: CatalogAdminCatalogueFunnel;
  flowRateByGenre: {genre: string; keptQuantity: number; soldQuantity: number; flowRatePercent: number | null}[];
  timeToSellDistribution: {bucket: string; quantity: number; sharePercent: number | null}[];
}

export interface CatalogAdminCatalogueFunnel {
  scannedCount: number;
  keptCount: number;
  keptRatePercent: number | null;
  soldCount: number;
  soldRatePercent: number | null;
  dormantCount: number;
  dormantOverYearCount: number;
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
  sentAlertCount: number;
  cancelledAlertCount: number;
  failedAlertCount: number;
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
    rareBookId: string | null;
    title: string | null;
    authors: string | null;
    quantityAvailable: number;
    quantityAnnounced: number;
    addedAt: string;
    lastAlertAt: string | null;
  }[];
  alerts: {
    id: string;
    isbn13: string | null;
    rareBookId: string | null;
    title: string | null;
    sentAt: string;
    outboxMessageId: string | null;
  }[];
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
  notFoundReportDailyLimit: number;
  updatedAt: string;
  updatedBy: string;
}

export type CatalogRareBookCondition = 'AsNew' | 'GoodWithFlaws' | 'Worn' | 'Damaged';

export interface CatalogRareBookPhoto {
  id: string;
  blobUri: string;
  blobName: string;
  caption: string | null;
  position: number;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
}

export interface CatalogRareBook {
  id: string;
  slug: string;
  isbn13: string | null;
  title: string;
  authorMention: string | null;
  publisher: string | null;
  publicationYear: number | null;
  price: number;
  condition: CatalogRareBookCondition;
  publicDescription: string | null;
  status: 'Draft' | 'Published';
  isSold: boolean;
  soldAt: string | null;
  photos: CatalogRareBookPhoto[];
}

export interface CatalogRareBookPage {
  generatedAt: string;
  books: CatalogRareBook[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CatalogRareBookDetail {
  rareBook: CatalogRareBook;
  relatedBooks: CatalogRareBook[];
}

export interface CatalogRareBookFilters {
  search?: string;
  includeSold?: boolean;
  sort?: 'recent' | 'price-asc' | 'price-desc';
  page?: number;
  pageSize?: number;
}

export type CatalogAdminRareBookStatus = 'Draft' | 'Published';
export type CatalogAdminRareBookAvailability = 'all' | 'available' | 'sold';
export type CatalogAdminRareBookCondition = 'AsNew' | 'GoodWithFlaws' | 'Worn' | 'Damaged';

export interface CatalogAdminRareBookPhoto {
  id: string;
  blobUri: string;
  blobName: string;
  caption: string | null;
  position: number;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
  uploadedBy: string;
}

export interface CatalogAdminRareBook {
  id: string;
  slug: string;
  isbn13: string | null;
  title: string;
  authorMention: string | null;
  publisher: string | null;
  publicationYear: number | null;
  price: number;
  condition: CatalogAdminRareBookCondition;
  publicDescription: string | null;
  status: CatalogAdminRareBookStatus;
  isSold: boolean;
  soldAt: string | null;
  soldAtFairId: string | null;
  soldInSessionId: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string;
  updatedBy: string;
  rowVersion: string;
  photos: CatalogAdminRareBookPhoto[];
}

export interface CatalogAdminRareBookPage {
  generatedAt: string;
  books: CatalogAdminRareBook[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CatalogAdminRareBookPublishResult {
  rareBook: CatalogAdminRareBook;
  changed: boolean;
  warnings: string[];
}

export interface CatalogAdminRareBookRequest {
  title: string;
  authorMention: string | null;
  publisher: string | null;
  publicationYear: number | null;
  price: number;
  condition: CatalogAdminRareBookCondition;
  publicDescription: string | null;
  isbn13: string | null;
}

export interface CatalogAdminUpdateRareBookRequest extends CatalogAdminRareBookRequest {
  rowVersion: string;
}

export interface CatalogAdminRareBookFilters {
  search?: string;
  status?: CatalogAdminRareBookStatus;
  availability?: CatalogAdminRareBookAvailability;
  hasIsbn?: boolean;
  minPrice?: number;
  maxPrice?: number;
  withoutPhoto?: boolean;
  page?: number;
  pageSize?: number;
}

export type CatalogAdminAccountRole = 'Tri' | 'Caisse' | 'Administration' | 'LivresRares';

export interface CatalogAdminAccount {
  externalId: string;
  email: string | null;
  displayName: string | null;
  accountEnabled: boolean;
  createdAt: string | null;
  roles: CatalogAdminAccountRole[];
}

export interface CatalogAdminAccountPage {
  generatedAt: string;
  accounts: CatalogAdminAccount[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CatalogAdminAccountFilters {
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CatalogAdminCreateAccountRequest {
  email: string;
  firstName: string;
  lastName: string;
  temporaryPassword: string;
  roles: CatalogAdminAccountRole[];
}

export interface CatalogAdminCheckoutPassageLookup {
  id: string;
  occurredAt: string;
  lineCount: number;
  displayLabel: string | null;
}

export interface CatalogAdminCatalogueFilters {
  search?: string;
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

