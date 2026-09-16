export const CATALOG_ADMIN_SECTIONS = [
  'overview',
  'sessions',
  'dead-stock',
  'catalogue',
  'inventory',
  'statistics',
  'alerts',
  'members',
  'accounts',
  'settings',
] as const;

export type CatalogAdminSection = typeof CATALOG_ADMIN_SECTIONS[number];

export const CATALOG_ADMIN_STATISTICS_TABS = [
  'fairs',
  'evolution',
  'books',
  'volunteers',
] as const;

export type CatalogAdminStatisticsTab = typeof CATALOG_ADMIN_STATISTICS_TABS[number];

const CATALOG_ADMIN_SECTION_SET = new Set<string>(CATALOG_ADMIN_SECTIONS);
const CATALOG_ADMIN_STATISTICS_TAB_SET = new Set<string>(CATALOG_ADMIN_STATISTICS_TABS);

export function isCatalogAdminSection(value: string | null | undefined): value is CatalogAdminSection {
  return value !== null && value !== undefined && CATALOG_ADMIN_SECTION_SET.has(value);
}

export function catalogAdminSectionFromRoute(value: string | null | undefined): CatalogAdminSection {
  return isCatalogAdminSection(value) ? value : 'overview';
}

export function catalogAdminSectionPath(section: CatalogAdminSection): string {
  return `/administration/${section}`;
}

export function isCatalogAdminStatisticsTab(value: string | null | undefined): value is CatalogAdminStatisticsTab {
  return value !== null && value !== undefined && CATALOG_ADMIN_STATISTICS_TAB_SET.has(value);
}

export function catalogAdminStatisticsTabFromRoute(value: string | null | undefined): CatalogAdminStatisticsTab {
  return isCatalogAdminStatisticsTab(value) ? value : 'fairs';
}
