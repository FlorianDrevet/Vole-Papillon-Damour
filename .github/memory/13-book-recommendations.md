# 13 - Book Recommendations

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

## Runtime observation (2026-10-25)

- The production API and Worker (in resource group `rg-vpd-dev`) both reported `Recommendations__Enabled=True`, but the authenticated BackOffice recommendations tab showed zero jobs. The initial recommendation catch-up is a separate manual BackOffice action (`Rattraper le catalogue`); enabling the feature does not seed jobs by itself.
- ISBNdb catch-up only force-requests/re-enqueues a recommendation job when a job already exists for that ISBN. It does not create the initial job for an existing book with no job.
- A newly closed scan session creates recommendation jobs which the Worker consumes from `book-recommendations`. Public per-book similar suggestions and personalized `Pour vous` suggestions are separate flows; the latter needs a signed-in member's purchases and explicit preferences.
- The manual catalog backfill was not run during the 2026-10-25 investigation. A public detail page checked during that investigation had no similar-books block; its ISBNdb source was not established, so this alone does not prove behavior for an ISBNdb-sourced book.
