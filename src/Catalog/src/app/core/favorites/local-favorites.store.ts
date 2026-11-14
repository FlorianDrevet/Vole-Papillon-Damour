import {isPlatformBrowser} from '@angular/common';
import {Inject, Injectable, PLATFORM_ID} from '@angular/core';

export interface LocalFavoriteEntry {
  isbn13: string;
  title: string;
  authors: string | null;
  coverUrl: string | null;
  addedAt: string;
  editionCount?: number;
  quantityAvailable?: number;
  quantityAnnounced?: number;
}

const STORAGE_KEY = 'vpd.catalog.favorites.v1';
const MAX_FAVORITES = 50;
const DEFAULT_MINIMUM_HEARTS = 3;

@Injectable({providedIn: 'root'})
export class LocalFavoritesStore {
  available: boolean;

  constructor(@Inject(PLATFORM_ID) platformId: object) {
    this.available = isPlatformBrowser(platformId) && LocalFavoritesStore.probe();
  }

  list(): LocalFavoriteEntry[] {
    if (!this.available) {
      return [];
    }

    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return [];
      }

      const parsed: unknown = JSON.parse(raw);
      if (!isStoredFavorites(parsed) || !parsed.entries.every(isLocalFavoriteEntry)) {
        return [];
      }

      return parsed.entries
        .map(entry => ({...entry}))
        .sort((left, right) => Date.parse(right.addedAt) - Date.parse(left.addedAt));
    } catch {
      this.available = false;
      return [];
    }
  }

  has(isbn13: string): boolean {
    return this.list().some(entry => entry.isbn13 === isbn13);
  }

  add(entry: LocalFavoriteEntry): boolean {
    if (!this.available || !isLocalFavoriteEntry(entry)) {
      return false;
    }

    const entries = this.list();
    if (entries.length >= MAX_FAVORITES || entries.some(item => item.isbn13 === entry.isbn13)) {
      return false;
    }

    return this.write([...entries, entry]);
  }

  remove(isbn13: string): void {
    if (!this.available) {
      return;
    }

    this.write(this.list().filter(entry => entry.isbn13 !== isbn13));
  }

  clear(): void {
    if (!this.available) {
      return;
    }

    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      this.available = false;
    }
  }

  minimumHearts(): number {
    const value = this.readSetting('minimumHearts');
    const parsed = Number(value);
    return Number.isInteger(parsed) && parsed >= 1 && parsed <= 4
      ? parsed
      : DEFAULT_MINIMUM_HEARTS;
  }

  setMinimumHearts(value: number): void {
    const threshold = Number.isFinite(value)
      ? Math.min(4, Math.max(1, Math.round(value)))
      : DEFAULT_MINIMUM_HEARTS;
    this.writeSetting('minimumHearts', String(threshold));
  }

  bannerCollapsed(): boolean {
    return this.readSetting('bannerCollapsed') === 'true';
  }

  setBannerCollapsed(value: boolean): void {
    this.writeSetting('bannerCollapsed', String(value));
  }

  homeEntryDismissed(): boolean {
    return this.readSetting('homeEntryDismissed') === 'true';
  }

  setHomeEntryDismissed(value: boolean): void {
    this.writeSetting('homeEntryDismissed', String(value));
  }

  private write(entries: LocalFavoriteEntry[]): boolean {
    if (!this.available) {
      return false;
    }

    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({version: 1, entries}));
      return true;
    } catch {
      this.available = false;
      return false;
    }
  }

  private readSetting(name: 'minimumHearts' | 'bannerCollapsed' | 'homeEntryDismissed'): string | null {
    if (!this.available) {
      return null;
    }

    try {
      return localStorage.getItem(`${STORAGE_KEY}.${name}`);
    } catch {
      this.available = false;
      return null;
    }
  }

  private writeSetting(
    name: 'minimumHearts' | 'bannerCollapsed' | 'homeEntryDismissed',
    value: string,
  ): void {
    if (!this.available) {
      return;
    }

    try {
      localStorage.setItem(`${STORAGE_KEY}.${name}`, value);
    } catch {
      this.available = false;
    }
  }

  private static probe(): boolean {
    try {
      const key = `${STORAGE_KEY}.probe`;
      localStorage.setItem(key, '1');
      localStorage.removeItem(key);
      return true;
    } catch {
      return false;
    }
  }
}

function isStoredFavorites(value: unknown): value is {version: 1; entries: unknown[]} {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const stored = value as {version?: unknown; entries?: unknown};
  return stored.version === 1 && Array.isArray(stored.entries);
}

function isLocalFavoriteEntry(value: unknown): value is LocalFavoriteEntry {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const entry = value as Partial<LocalFavoriteEntry>;
  return typeof entry.isbn13 === 'string' && /^\d{13}$/.test(entry.isbn13)
    && typeof entry.title === 'string' && entry.title.trim().length > 0
    && (entry.authors === null || typeof entry.authors === 'string')
    && (entry.coverUrl === null || typeof entry.coverUrl === 'string')
    && typeof entry.addedAt === 'string' && !Number.isNaN(Date.parse(entry.addedAt));
}
