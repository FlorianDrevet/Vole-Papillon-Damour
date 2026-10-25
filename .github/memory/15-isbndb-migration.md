# ISBNdb-first book notices

## Runtime flow

- `docs/features/isbndb-migration/01-fonctionnel.md` defines rules `RG-100`–`RG-129`; the plan is `02-plan.md`.
- The Infrastructure ISBNdb client uses a one-request-per-second process-wide limiter, groups at most 100 ISBNs, retries 429 responses at most three times, and records quota headers through the shared quota guard. It never calls `/stats` or persists `image_original`.
- ISBNdb notice mapping lives in Application. ISBN lookups are coalesced by ISBN so concurrent scans, batches and enrichment can share a request. BnF, Open Library and Google Books remain the temporary fallback; manually completed fields are preserved.
- ISBNdb sourced books are eligible for recommendations from stored notice data. Fallback books wait until a definitive notice is available.
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

## Production catch-up observation (2026-10-25)

- The scheduled run started at 2026-10-25 02:30 UTC / 03:30 Paris. At 09:46 UTC / 10:46 Paris, Catalog still showed it as in progress, with 12,636 books pending (12,627 in stock) and 61 books absent from ISBNdb.
- The ISBNdb response last observed at 02:59 UTC / 03:59 Paris reported 3,400 of 5,000 credits remaining. Catalog attributed 1,554 credits to the catch-up, a 46-credit difference from the 1,600 inferred from the provider quota. Do not infer the reason without provider/invocation logs.
- Worker revision `vpd-worker-ca-dev-fc--0000029` (`vpd-worker:8f55d46`) in production resource group `rg-vpd-dev` was Healthy with one active replica; `minReplicas=0`, `maxReplicas=1`, schedule `0 30 2 * * *`, request rate 1/s, default bulk size 100, and no `functionTimeout` setting in `host.json`.
- App Insights had no Worker invocation telemetry for the run window, and Container Apps historical console logs were unavailable because the environment has no Log Analytics destination configured. CPU fell to low activity after the first roughly 30 minutes. This is consistent with an early interruption, but a timeout is only a hypothesis; the run's final stop reason is unknown. Container Apps documents a 30-minute default, with a caveat that the default depends on the trigger when `minReplicas=0`. The run's zero summary counters are written at finalization, so they do not establish that zero books were processed.
- Production API and Worker recommendation flags were both `True`, while BackOffice displayed zero recommendation jobs. The feature flag does not run the manual initial catalog backfill. ISBNdb catch-up only re-requests recommendation jobs that already exist. No Azure data plane was changed or directly read, and no recommendation backfill was started.
