export interface BookMetadata {
  isbn13: string;
  title: string | null;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  physicalFormat?: string | null;
  coverUrl: string | null;
  coverSource?: string | null;
  source: string;
  workId: string | null;
  workKey?: string | null;
  retrievedAt: string;
}
