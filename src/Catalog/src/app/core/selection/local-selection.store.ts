import {Inject, Injectable, PLATFORM_ID} from '@angular/core';
import {isPlatformBrowser} from '@angular/common';
import {LocalSelectionEntry, SelectionRef, selectionKey} from './selection-merge';

const STORAGE_KEY = 'vpd.catalog.selection.v1';

@Injectable({providedIn: 'root'})
export class LocalSelectionStore {
  readonly available: boolean;

  constructor(@Inject(PLATFORM_ID) platformId: object) {
    this.available = isPlatformBrowser(platformId) && LocalSelectionStore.probe();
  }

  list(): LocalSelectionEntry[] {
    if (!this.available) {
      return [];
    }

    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return [];
      }

      const parsed: unknown = JSON.parse(raw);
      if (!isStoredSelection(parsed) || !parsed.entries.every(isLocalSelectionEntry)) {
        return [];
      }

      return parsed.entries;
    } catch {
      return [];
    }
  }

  has(ref: SelectionRef): boolean {
    const key = selectionKey(ref);
    return this.list().some(entry => selectionKey(entry.ref) === key);
  }

  add(entry: LocalSelectionEntry): void {
    if (!this.available || this.has(entry.ref)) {
      return;
    }

    this.write([entry, ...this.list()]);
  }

  remove(ref: SelectionRef): void {
    if (!this.available) {
      return;
    }

    const key = selectionKey(ref);
    this.write(this.list().filter(entry => selectionKey(entry.ref) !== key));
  }

  clear(): void {
    if (!this.available) {
      return;
    }

    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage refused: nothing left to clear on this device.
    }
  }

  private write(entries: LocalSelectionEntry[]): void {
    if (!this.available) {
      return;
    }

    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({version: 1, entries}));
    } catch {
      // Anonymous selection is best-effort when storage is full or unavailable.
    }
  }

  private static probe(): boolean {
    try {
      localStorage.getItem(STORAGE_KEY);
      return true;
    } catch {
      return false;
    }
  }
}

function isStoredSelection(value: unknown): value is {version: 1; entries: unknown[]} {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const stored = value as {version?: unknown; entries?: unknown};
  return stored.version === 1 && Array.isArray(stored.entries);
}

function isLocalSelectionEntry(value: unknown): value is LocalSelectionEntry {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const entry = value as Partial<LocalSelectionEntry>;
  if (typeof entry.title !== 'string' || typeof entry.addedAt !== 'string' ||
      Number.isNaN(Date.parse(entry.addedAt)) || typeof entry.ref !== 'object' || entry.ref === null) {
    return false;
  }

  const ref = entry.ref as Partial<SelectionRef>;
  return ref.kind === 'edition'
    ? typeof ref.isbn13 === 'string' && ref.isbn13.length > 0
    : ref.kind === 'rare' && typeof ref.rareBookId === 'string' && ref.rareBookId.length > 0;
}

