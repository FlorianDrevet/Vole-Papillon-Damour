# ISBNdb-first book notices

## Runtime flow

- `docs/features/isbndb-migration/01-fonctionnel.md` defines rules `RG-100`–`RG-129`; the plan is `02-plan.md`.
- The Infrastructure ISBNdb client uses a one-request-per-second process-wide limiter, groups at most 100 ISBNs, retries 429 responses at most three times, and records quota headers through the shared quota guard. It never calls `/stats` or persists `image_original`.
- ISBNdb notice mapping lives in Application. ISBN lookups are coalesced by ISBN so concurrent scans, batches and enrichment can share a request. BnF, Open Library and Google Books remain the temporary fallback; manually completed fields are preserved.
- ISBNdb sourced books are eligible for recommendations from stored notice data. Fallback books wait until a definitive notice is available. During catch-up, processed found/absent books now get a job if missing and are enqueued after the page commits, provided recommendations are enabled; untouched books still require the separate initial backfill. A page hit by `DbUpdateConcurrencyException` is retried up to twice after pending writes are detached and books reloaded; cached ISBNdb responses are reused, so a retry does not spend quota again. If retries are exhausted, the run is finalized `Unavailable`, the failed page remains eligible for a later run, and the original exception is rethrown. Counts advance only after commit; finalization failures are logged without replacing the original exception.
- The Worker runs catch-up on `Isbndb__CatchUpSchedule`; the infrastructure default is `0 30 2 * * *` UTC with `Isbndb__RequestsPerSecond=1`.
- API routes expose quota/status, absent books, completion and recheck operations. Catalog uses a cached, rate-limited public edition search beneath the last external-results page.

## Scan matching

- The offline Scan catalog stores `workKey`, publisher, publication year and other ISBNs. IndexedDB schema version 7 clears the cached catalog and its sync marker once so the new fields can be resynced.
- Same non-null `workId` or an ISBN listed in either edition's `otherIsbns` is a strong edition link and contributes sibling stock to the duplicate threshold. Matching `workKey` alone is a weak, informational link and never changes the verdict.
- The result card lists in-stock sibling editions. Late metadata can reveal the list after a scan without replacing a verdict the volunteer has already seen.

## Configuration and rollout

- `infra/main.bicep` accepts secure optional parameter `isbndbApiKey`; the Key Vault secret is `isbndb-api-key`, and the API/Worker receive `Isbndb__ApiKey` only when configured.
- `infra/parameters/main.dev.bicepparam` reads `ISBNDB_API_KEY`; `.github/workflows/infra-deploy.yml` maps the GitHub secret. Never put the key in tracked files, logs or browser code.
- Deploy Infra with the key before Books runtime migrations, then Catalog and Scan. The first catch-up examines existing books and may consume one credit per found book over several nights. No deployment or remote migration is part of local validation.

## Validation state (2026-10-24)

- Backend solution build passes. Domain (235), Infrastructure (238), Application (688) and Worker (8) tests pass; API retains the same 32 failures recorded at task 0.
- Scan ChromeHeadless: 313/313 pass. Catalog: 575/578 pass; the three failures are existing AccountCard assertions. Catalog and Scan production builds pass with Angular budget warnings.
- `az bicep build --file infra/main.bicep` passes. `graphify update .` extracts the AST but cannot render the 9,068-node graph because it exceeds the HTML visualization size cap.
- Catalog administration E1/E2/E3 were checked at 390 and 1440 px. The E5 browser visual check remains unverified because CUA browser-tab creation timed out. Live ISBNdb smoke tests remain pending: no local environment or user-secrets key was configured; no credits were used.
