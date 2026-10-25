# 13 - Book Recommendations

## Production queue observation — 2026-10-25

- The Worker was processing `book-recommendations`, but 18 batch-completion traces between 10:42 and 10:55 UTC each reported `Started: 50, Ready: 0, Failed: 50`. EF logs show the insert into `dbo.BookNeighbors` failing. The failure cause is still unknown; do not treat recommendations as healthy merely because `Recommendations__Enabled=True`.
- ISBNdb catch-up only re-enqueues a recommendation job that already exists for a book. It does not seed the initial job for a book without one; the catalog recommendation backfill is separate.
## Public similar-book suggestions

- `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/Recommendations/RecommendationOptions.cs`
  supplies the defaults for the recommendation API: `SimilarMinScore = 0.50`,
  `SimilarMinCount = 1`, and `SimilarMaxCount = 20`. `Recommendations:Enabled` still gates
  recommendation reads and computation; check `NEXT.md` and the activation runbook before
  assuming a deployment is active.
- `GetSimilarBooksQueryHandler` reads the current neighbor generation, filters by score,
  canonical public visibility, and available or announced stock, then applies the minimum and
  maximum counts.
- Catalog renders the public list in
  `src/Catalog/src/app/features/book-detail/similar-books/`. One returned book is enough to
  show the section. Multiple items use a horizontal, snap-scrolling track; accessible previous
  and next buttons appear only while more content is available in that direction. The scroll
  uses the Catalog's global reduced-motion rule.
- The current behavior is specified in `docs/recommandations-livres/07-specification.md`.
  Component regressions live in `similar-books.component.spec.ts`; backend defaults and query
  behavior are covered by the Infrastructure and Application recommendation test projects.
- `Book.Summary` is ISBN-level bibliographic data and the public book detail reads it directly. `WorkGrouping` receives summaries keyed by ISBN and prefers that value, with a legacy-profile fallback. `RefreshBookSimilarityProfilesCommandHandler` copies a fetched legacy summary into an empty `Book.Summary` and clears summary fields before persisting refreshed `NoticeJson`; profile refresh remains gated by `Recommendations:Enabled`.
