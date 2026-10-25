# ISBNdb-first book notices

## Runtime flow

- `docs/features/isbndb-migration/01-fonctionnel.md` defines rules `RG-100`–`RG-129`; the plan is `02-plan.md`.
- The Infrastructure ISBNdb client uses a one-request-per-second process-wide limiter, groups at most 100 ISBNs, retries 429 responses at most three times, and records quota headers through the shared quota guard. It never calls `/stats` or persists `image_original`.
- ISBNdb notice mapping lives in Application. ISBN lookups are coalesced by ISBN so concurrent scans, batches and enrichment can share a request. BnF, Open Library and Google Books remain the temporary fallback; manually completed fields are preserved.
- ISBNdb sourced books are eligible for recommendations from stored notice data. Fallback books wait until a definitive notice is available.
- The Worker runs catch-up on `Isbndb__CatchUpSchedule`; the infrastructure default is `0 30 2 * * *` UTC with `Isbndb__RequestsPerSecond=1`.
- API routes expose quota/status, absent books, completion and recheck operations. Catalog uses a cached, rate-limited public edition search beneath the last external-results page.

## Production catch-up follow-up — 2026-10-25

- The Worker restart produced healthy revision `vpd-worker-ca-dev-fc--0000033`. The temporary timer `0 40 10 * * *` was loaded, but no `IsbndbCatchUp` invocation was recorded at its 10:40 UTC occurrence. The timer is restored to `0 30 2 * * *` UTC.
- The Function-key-protected manual POST route was invoked at 10:50 UTC and returned HTTP 500 after about two minutes. App Insights records `DbUpdateConcurrencyException` during `FinishRunAsync` after the handler expected one updated row and SQL reported zero. Do not infer the exact books written from this exception; candidate metadata may have partially persisted. The dashboard continued to show the 03:30 Paris run as in progress.
- The quota panel still showed 3,400/5,000 from a response read at 03:59 Paris; it is not a current balance. Verify quota and run state from a fresh provider/header/status read before another attempt.
- The manual endpoint awaits the full catch-up command and only returns after completion; it does not return a background job identifier. It requires a Function key. Never record that key in repository memory or logs.
- Current network state is external HTTPS ingress on target port 8080 (`allowInsecure=false`) and unrestricted outbound access (ACA environment `publicNetworkAccess=Enabled`, no VNet configuration). The public root returned 200. The ingress/port settings are live Azure state and have not been reconciled with Bicep.
- Follow-up before another production run: identify the original concurrency failure and make finalization resilient; verify the current candidate count/quota; then start one run and confirm its final status and counters.
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
