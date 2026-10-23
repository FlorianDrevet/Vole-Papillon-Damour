# Genre taxonomy normalization

## Status

Planned; start the data inventory after the active full-catalog enrichment has finished so the source snapshot includes its final results. At the last recorded check on 2026-10-23 14:18 UTC, Durable run `d6dbf40d63df48f8b313dd4ca13e4d20` was still running at batch 97. Do not treat that checkpoint as final.

## Goal and scope

- Inventory every distinct stored genre and subject, with usage counts and the ISBNs/books that use each value.
- Define a canonical set of real literary genres/categories. It can exceed the 16 catalog search facets; those facets are not the limit of the taxonomy.
- Separate book genres from places, events, themes, and other subjects that are not genres (for example, “Pompeii”). Assign a genre to a book that has only subjects when the evidence supports a meaningful classification; retain ambiguous cases for review instead of guessing.
- Map spelling, punctuation, accent, singular/plural, and synonym variants to canonical values. Preserve multiple valid genres for a book.
- Prepare an idempotent migration script with a dry-run report, before/after counts, duplicate-link handling, and a list of unmapped values. No production-data migration has been prepared or run yet.

## Planned sequence

1. Confirm the enrichment run is complete, then take a read-only database snapshot of all genre and subject values and their associated books.
2. Produce the complete raw inventory and frequency counts. Do not infer the taxonomy from only the most common values or only the existing 16 filters.
3. Propose canonical names and an explicit source-value-to-category mapping. Include rationale and confidence for subject-only classifications, and retain an unmapped/review list.
4. Build an idempotent migration that applies only reviewed mappings, deduplicates equivalent genre links without deleting books, and supports preview/rollback verification.
5. Validate the proposed category counts and catalog filters against the migrated result before any production application. Azure resources named `rg-vpd-dev` are treated as production for this user.

## Recurring report follow-up

- The user will provide the desired report format before implementation.
- Compare a weekly GitHub Actions schedule with a local agent and use the simpler maintainable option. Neither has been implemented.
- The report should list new or changed books since the last completed normalization, with ISBN, title, raw genre/subject values, and proposed canonical mapping.
- Track the last normalization time with a UTC watermark (candidate name: `GenreNormalizationWatermarkUtc`) so routine reports do not repeat the full catalogue. Keep the initial full inventory separate from later incremental reports.

## Existing behavior boundary

Merged PR #337 added `BookGenre.NamesAreEquivalent` for deterministic comparison/deduplication during enrichment and added a resume endpoint for the catalog worker. It prevents equivalent spellings from colliding for the same book, but it does not rename the stored taxonomy, classify subjects, or migrate existing genre links.

## Completion criteria

- Every raw value has a documented canonical mapping or an explicit unmapped/review disposition.
- The migration preview accounts for all affected books and genre links, including duplicate links and subject-only books.
- The recurring report format and execution mechanism are agreed, and the watermark prevents reprocessing the full catalogue after initial normalization.
