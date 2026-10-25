# 14 - Scan Metadata Latency

## Azure Monitor observation — 2026-10-25

- Read-only App Insights queries over seven days show the active API revision `vpd-api-ca-dev-fc--0000023` serving 100% of traffic. The `rg-vpd-dev` environment is production per the project memory, despite the resource names.
- `GET /books/{isbn13}/metadata`: 5,343 observed requests, p95 10.932 s, max 15.497 s, 1,293 failures. `GET /scan/manual-requests/candidate`: 5,322, p95 10.910 s, max 15.419 s, 989 failures.
- Slow request traces have only 2–42 ms of SQL dependency time. Metadata resolution spans reach p95 10.771 s. Open Library timeout spans on revision 23 are commonly 10 s, with a 14.686 s max; individual `openlibrary.org` dependencies hit the 5 s HttpClient timeout. BnF timeout spans are about 800 ms.
- Code inspection confirms that the default metadata path passed `includeBibliographicDetails=false` through the resolver but `OpenLibraryClient.FindAsync` still fetched `/isbn/{isbn}.json` and sometimes `/works/{id}.json` before `/search.json`. These sequential calls could each consume a separate 5 s timeout, matching the provider traces. SQL is not the cause.
- [PR #345](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/345) is open and unmerged on `fix/scan-metadata-provider-latency`. It gates edition/work detail reads on `includeBibliographicDetails`, retains edition lookup as a fallback when search cannot provide a result/WorkId, and keeps full enrichment behavior when details are requested. Manual scan candidate lookup skips provider resolution when there are no active work-scope requests and uses `ResolveWorkIdAsync` rather than resolving full metadata. Application 690/690 and Infrastructure 241/241 pass locally. No Azure changes or deployment.
- Catalog SSR `GET /` has only 38 observations (p95 14.794 s, max 17.891 s); its directly logged API calls account for roughly 2.7–3.2 s, so the remaining time is unexplained. Instrument SSR render and initial data loading before attributing a cause. The app has `minReplicas=1`; logs do not establish a cold-start cause.
- Worker `EnrichCatalogBatch` is a background batch of 50 and has p95 around 164–171 s, max 179.8 s. Treat it separately from interactive API latency; review batch size/concurrency/retry behavior after the API fix.
- Google Books calls return HTTP 400 in the current sample; the Worker previously received `API_KEY_INVALID` on 2026-10-23. Verify API enablement and key restrictions, then replace the Azure secret with a valid key. The secret value was not read or changed.

## Azure Monitor observation — 2026-10-22

- Refreshed read-only App Insights queries for the previous 24 hours showed GET /books/{isbn13}/metadata: 101 HTTP 200 responses (p50 1.10 s, p95 3.50 s, max 4.44 s), 9 HTTP 503s (p95 2.33 s), and 5 HTTP 404s (p95 1.63 s).
- SQL had 6,390 dependencies, p95 about 5.8 ms, and zero failures, so SQL does not explain the multi-second metadata response in this sample.
- Provider calls run sequentially. BnF SRU had 92 HTTP 200 calls (p95 341 ms) and 25 timeout-like results around 800 ms. Open Library had 117 HTTP 200 calls (p95 954 ms, max 1.04 s). Google Books had 103 HTTP 400 calls (p50 136 ms, p95 670 ms, max 773 ms).
- Google Books 400s and slow BnF/Open Library calls are concurrent observations; the sequential resolver can accumulate provider latency. The logs do not prove the 400 cause or a one-to-one relation between provider calls and API failures.
- The Google Books client uses the documented ISBN query shape, but the deployed 400 body and API key/configuration were not verified. Its cause remains unresolved; inspect sanitized provider error details and configuration without printing secrets.
- On 2026-10-23, a request from the Worker using its configured Google Books key returned `API_KEY_INVALID`; the key value was not displayed. The configured Azure secret therefore needs to be replaced with a valid key after checking Google Books API enablement and key restrictions. No secret rotation was performed.
- Treat these numbers as a dated 24-hour sample, not a stable baseline. Re-run the query before making future service-level claims.

## Worker enrichment provider state — 2026-10-23

- The Books runtime deployment completed successfully; Worker revision `vpd-worker-ca-dev-fc--0000022` is Healthy with one replica.
- Durable run `d6dbf40d63df48f8b313dd4ca13e4d20` resumed from completed batch 75. At 2026-10-23 14:05 UTC it was Running at batch 92, with 850 books processed, 58 failed, and 57 not found. The previous run had failed at batch 75 / 3,750 books on duplicate `PK_BookGenres`; the deployed comparer fix avoided that failure so far.
- After refreshing the ISBN queue, 104 records were marked failed. ISBN `9782070577026` had blank title, author, publisher, year, and genre fields. The publisher’s education site confirmed *Les vacances du Petit Nicolas*, René Goscinny and Sempé, Gallimard Jeunesse, 2007, genre Humour. Those fields were saved manually in the portal; the record is now `MANUAL` and the failed queue count fell to 103. [Publisher record](https://www.cercle-enseignement.com/Ouvrages/Gallimard-Jeunesse/Folio-Junior/Les-vacances-du-petit-Nicolas).
- Several historical failures still show the old generic metadata-validation message. Provider failures now expose `HttpRequestException`, while the invalid Google Books key and BnF timeouts are provider-wide configuration/latency problems, not per-ISBN corrections.
- PR #338 raises the Worker-only BnF timeout to 2,000 ms by default. It is open, not merged or deployed; the active run continues on the 800 ms configuration.

## Relevant runtime path

- API query: GET /books/{isbn13}/metadata. Application delegates to the Infrastructure bibliographic resolver; the anonymous probe does not persist a Book.
- Resolver order: BnF SRU, then Open Library, then Google Books as a fallback. SQL p95 was low in the observation above.
- Provider client and query-shape source: src/Backend/Vole_Papillon_Damour.Infrastructure/Services/Bibliographic/GoogleBooksClient.cs.

## Scan triage follow-up — 2026-10-22

The Scan UI now renders the local keep/discard result before the bibliographic API call finishes. The pending title, author, edition, and cover use skeletons; a metadata 404 shows an error-toned icon and manual-entry guidance, while other failures say the online search is unavailable. Metadata is cached best-effort, with a `lookupVersion` guard against results from an older scan. `src/Scan` tests pass 304/304 and the production build succeeds. A 320/390/1440 px fixture using the compiled CSS showed no horizontal overflow; the real route remains behind MSAL and needs an authenticated volunteer smoke check. No Azure changes or deployment were made.
