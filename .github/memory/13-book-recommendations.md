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
