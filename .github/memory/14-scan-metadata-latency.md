# 14 - Scan Metadata Latency

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
