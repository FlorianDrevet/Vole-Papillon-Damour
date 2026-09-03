export interface BookMetadata {
  isbn13: string;
  title: string | null;
  authors: string | null;
  publisher: string | null;
  publicationYear: number | null;
  coverUrl: string | null;
  source: string;
  workId: string | null;
  retrievedAt: string;
}
