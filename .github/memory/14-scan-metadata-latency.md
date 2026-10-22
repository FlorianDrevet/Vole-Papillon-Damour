# 14 - Scan Metadata Latency

## Azure Monitor observation — 2026-10-22

- Refreshed read-only App Insights queries for the previous 24 hours showed GET /books/{isbn13}/metadata: 101 HTTP 200 responses (p50 1.10 s, p95 3.50 s, max 4.44 s), 9 HTTP 503s (p95 2.33 s), and 5 HTTP 404s (p95 1.63 s).
- SQL had 6,390 dependencies, p95 about 5.8 ms, and zero failures, so SQL does not explain the multi-second metadata response in this sample.
- Provider calls run sequentially. BnF SRU had 92 HTTP 200 calls (p95 341 ms) and 25 timeout-like results around 800 ms. Open Library had 117 HTTP 200 calls (p95 954 ms, max 1.04 s). Google Books had 103 HTTP 400 calls (p50 136 ms, p95 670 ms, max 773 ms).
- Google Books 400s and slow BnF/Open Library calls are concurrent observations; the sequential resolver can accumulate provider latency. The logs do not prove the 400 cause or a one-to-one relation between provider calls and API failures.
- The Google Books client uses the documented ISBN query shape, but the deployed 400 body and API key/configuration were not verified. Its cause remains unresolved; inspect sanitized provider error details and configuration without printing secrets.
- A direct request with an intentionally invalid test key returned `API_KEY_INVALID`, consistent with (but not proof of) an invalid or restricted deployed key. Azure references a Container Apps secret for the setting; the value was not read. The documented `q=isbn:<ISBN>` query form is valid and requests require a key. Check key validity, Books API enablement and key restrictions in Google Cloud Console.
- Treat these numbers as a dated 24-hour sample, not a stable baseline. Re-run the query before making future service-level claims.

## Relevant runtime path

- API query: GET /books/{isbn13}/metadata. Application delegates to the Infrastructure bibliographic resolver; the anonymous probe does not persist a Book.
- Resolver order: BnF SRU, then Open Library, then Google Books as a fallback. SQL p95 was low in the observation above.
- Provider client and query-shape source: src/Backend/Vole_Papillon_Damour.Infrastructure/Services/Bibliographic/GoogleBooksClient.cs.

## Scan triage follow-up — 2026-10-22

The Scan UI now renders the local keep/discard result before the bibliographic API call finishes. The pending title, author, edition, and cover use skeletons; a metadata 404 shows an error-toned icon and manual-entry guidance, while other failures say the online search is unavailable. Metadata is cached best-effort, with a `lookupVersion` guard against results from an older scan. `src/Scan` tests pass 304/304 and the production build succeeds. A 320/390/1440 px fixture using the compiled CSS showed no horizontal overflow; the real route remains behind MSAL and needs an authenticated volunteer smoke check. No Azure changes or deployment were made.
