# 04 - Frontend And Client Surfaces

## Angular Applications

The Angular web apps are Angular 21 projects with Angular Material and Tailwind in the toolchain.

- `src/BackOffice/` - admin UI, with MSAL Angular (`@azure/msal-angular` 5.3.1,
  `@azure/msal-browser` 5.20.0) and `@dhutaryan/ngx-mat-timepicker`
- `src/Website/` - public UI for the association website, now built with Angular SSR and hydration support
- `src/Catalog/` - separate public books catalog, built with Angular SSR and hydration support
- `src/Scan/` - Angular 21 Scanette PWA for ISBN capture, offline triage, consultation, cash
  sales, IndexedDB persistence, and volunteer authentication/synchronization

### Actuality review workflow

`BackOffice/src/app/feature/actualities` requests `GET /actuality/all?includeDrafts=true`
and shows a draft-count banner, an optional draft-only filter, source links, title-review
and dormant-after-30-days badges. The existing edit dialog can save a draft or save then
call `POST /actuality/{id}/publish`; `Website` renders the public actuality article with
`white-space: pre-line`. The public API still hides drafts, so the Website model does
not need an import-specific UI.

As of 2026-09-10, Website `/association/photos` separates the image catalog into 23
yearly Maxence albums (2004–2026) and three event albums. Cards now navigate to the
SSR-rendered `/association/photos/:albumSlug` page, which exposes a masonry gallery and
keyboard-accessible links back to the catalog; the previous image-selection dialog has
been removed. The 2005 source folders are merged into one 94-photo album, and the
celebrity and Maxence 20th-birthday folders are included as 15- and 88-photo event
albums. Existing `/maxence/histoire` chapters link to their matching year album only
where a chapter exists (2004–2016). The hero still includes a three-image mosaic, the
history banner remains after the video catalog, and the mixed video grid uses
stretch-to-row cards with a flexible copy block and `object-contain`. The Website asset
addition contains 715 source images; local validation passes 72 ChromeHeadless tests and
the SSR/production build, with no deployment made.

The public catalog is intentionally separate from the association Website. It uses typed
`CatalogApiService`/models and the `/catalog/*` API reads for search, book details, works,
the next books fair, and the dynamic sitemap; the home calendar also consumes the existing
public `/asso-events` schedule and keeps only future Books events. Its public routes are `/`, `/recherche`,
`/catalogue`, `/livres/:slug`, `/oeuvre/:workId`, `/donnees-personnelles`, and the legal,
privacy, cookie and accessibility pages. The UI keeps
available quantities separate from future announcements, leaves exhausted books visible,
and gates Microsoft Clarity, Google Analytics 4 and the Google Maps embed behind explicit consent choices. The `/compte` member route uses a dynamic,
SSR-safe MSAL Browser loader, reads/removes watchlist items through bearer-protected API
calls, exposes alert suspension/reactivation and the durable account-deletion request.
`/desinscription` is a client-only authenticated opt-out route. The `/administration`
route now consumes the typed admin APIs for overview, catalogue, sessions, dead stock,
fairs, alerts, members and settings; the BackOffice administration screen also exposes a
typed **Comptes et rôles** tab for creating Entra accounts and assigning `Tri`, `Caisse`
or `Administration` roles. The API remains the authorization boundary and the current
administrator cannot remove their own `Administration` role.
As of 2026-09-11, Catalog `/administration` keeps the public catalogue header and uses the
AdminSidebar visual language for its dashboard, scan sessions, catalogue, dead-stock,
inventory, fair statistics, accounts and settings workspaces. The navigation groups the
site-member and volunteer surfaces under `Réservé à l'administration` → `Comptes & rôles`;
the workspace keeps the existing `/accounts/admin` contract for typed listing, account
creation and role updates and shows a contextual notice when the Entra directory cannot be
read. The inventory workspace keeps physical history and carton/rayon actions explicitly
empty or disabled because no corresponding endpoint or domain model exists; the UI does not
invent those values. Opening a scan session now upserts the local member projection with
the display-name claim; session reads show that first/last name only and never expose the
stored volunteer email as a substitute. Historic sessions with neither a local user row nor
stored contact data show a neutral missing-name label and require reopening or a separate
backfill. The dashboard period chips are typed and reload the overview with UTC bounds for
30 days, three calendar months, or twelve months; the scan-session navigation badge uses the
same pending-alert/non-cancelled predicate as the `Encore corrigeables` view.

Administration action feedback is rendered as one fixed, dismissible toast above the content
flow, with separate accessible success/error tones and live-region semantics. The page keeps
only the latest feedback state so a stale success cannot remain visible alongside a validation
error; loading and contextual empty/error notices remain in the normal page flow.

As of 2026-09-12, Catalog `/administration` groups the former fair-statistics sidebar item
under one `Statistiques` entry with two tabs: `Statistiques par bourse` and
`Statistiques des bénévoles`. The second tab consumes the typed
`/books/admin/volunteers/stats` aggregation and renders contribution, concentration,
calibration, waiting-stock, monthly heatmap, renewal and genre views with period/fair
filters. The quality-data mockup subtab and its 4C screen are intentionally not implemented.
The UI keeps roles activity-derived (Tri/Caisse) and does not pretend to mirror Entra roles.

The public search sort control keeps a native accessible `<select>` for keyboard and screen
reader behavior while wrapping it in Catalog design-system spacing, border, pill, chevron,
hover, and focus styles. The administration Settings navigation icon uses a balanced custom
SVG so it remains legible at the responsive sidebar sizes.
The external bibliographic result block is kept separate from local results. DEV ACS email
delivery is enabled after domain verification; a real authorized-recipient test remains open.

As of 2026-09-12, Catalog `/donnees-personnelles` explains the account data categories,
Entra/ACS recipients, six GDPR rights, the secure e-mail request path, the one-month
response target and CNIL escalation. It is linked from the Catalog footer and from the
signed-out account card before login/registration; the public route remains indexable while
`/compte`, `/administration` and `/desinscription` stay private. The account deletion copy
now reflects the durable local cleanup plus the asynchronous Entra identity deletion. Exact
retention durations and the automatic three-year inactivity purge still require association
validation; the page deliberately does not promise an unimplemented purge.

As of 2026-09-11, the Catalog home places an API-driven `Par genres` browser and an
account-following callout after the rare-books section; the former `Votre sélection` block
is removed. Recent books use the shared `BookCardComponent` `home` variant for the compact
mockup treatment (cover/status/content/footer). The existing `list` variant remains the
search-result layout with publisher/year/genre metadata and availability rows, while rare,
work and detail cards keep the default/grid contract. The existing cover placeholder remains
the fallback whenever a book has no image, and the home never invents genre counts that the API
does not provide.

The Catalog follow-up polish keeps the genre section in that position and turns recent-home
availability into compact colored pill tags. The browser shell initializes cached MSAL state
on arrival, keeps connected member/admin account controls at the same width, and the search
page exposes direct typed `Work`/`Edition` follow buttons: references that identify one
common work get a single section-level all-editions action above the list, while each precise
edition remains independently followable and empty reference results expose no follow action.
The old scope-selection modal is removed; a pending follow survives the sign-in redirect and
submits directly after the cached session is restored. The administrator header uses the
clickable `Administration` tag as the sole header entry to `/administration`; the mobile drawer
does not duplicate the former `Espace administrateur` link.

As of 2026-09-11, the shared `VpdLoaderComponent` brings the loader catalogue from the design
artifact to Website, Catalog, and Scan. Its variants map to the nine supplied screens: `line`
(1a), `flight` (1b), `squares` (1c), `skeleton` (1d), `ring` (1e), `traverse` (2a),
`butterflies` (2b), `fill` (2c), and `compact` (2d). The shared Sass is imported globally by
the three consuming apps so the loader styles do not inflate Angular component-style budgets;
the component exposes status/progress semantics and stops motion under `prefers-reduced-motion`.
Website uses the line loader for route transitions and skeletons for actuality/event reads.
Catalog uses traverse for navigation, squares for search, skeletons for catalogue grids/lists,
butterflies for bibliographic lookups, and compact/ring states for detail, account, and follow
actions. Scan uses `flight`/1b only while the root auth state is `checking`, which makes it the
first screen during startup before the login surface appears; lookup waits use butterflies.
The determinate `fill`/2c variant is available for a future flow that exposes a real percentage,
but no current client invents one. Local validation passes 75 Website, 165 Catalog, and 175 Scan
ChromeHeadless tests plus the three production builds. PR [#147](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/147)
is open for review; no deployment was made.

After the 2026-09-08 rollout, Catalog analytics use dedicated public build variables:
`CATALOG_GOOGLE_ANALYTICS_MEASUREMENT_ID` for GA4 `G-GBHC67EGGF` and `CLARITY_PROJECT_ID`
for Clarity `yerabb7gnt`; the Website variable `GOOGLE_ANALYTICS_MEASUREMENT_ID` remains
`G-D67DMFCTDG`. Live smoke confirmed that both scripts stay absent until consent and that
the footer can reopen the choice for withdrawal. Search Console now contains the Catalog
sitemap; initial crawl and analytics data remain delayed external checks.

As of 2026-09-06, the Catalog P2/P3 integration is implemented in the V2 visual shell:
the search page calls local and external reference endpoints independently; reference
items can be followed as an edition or work after Entra login; the account page manages
watchlist items and alert preference; `/desinscription` confirms an authenticated alert
opt-out; and `/administration` exposes every currently available admin workspace with
typed filters, details, corrections, confirmations and truthful empty/error states. The
Catalog client keeps private data client-rendered and marks `/compte`, `/administration`
and `/desinscription` `noindex, nofollow`. It deliberately does not add role-editing or
physical-carton controls without a contract: role assignment in the Catalog volunteer
workspace uses the existing `/accounts/admin` contract, while physical carton/rayon
controls remain explanatory rather than fake.

As of 2026-09-12, the Catalog administration Inventory workspace is fiche-first: it loads
all admin book fiches without dead-stock or metadata work-queue filters, supports search by
ISBN/title/author, and keeps external reference results separate from local inventory until
an administrator selects one. An ISBN lookup normalizes ISBN-10/ISBN-13 before calling the
typed reference API; adding a selected reference sends its bibliographic metadata, initial
available quantity and required note through `POST /books/admin/books`. Each local row exposes
confirmed `+`/`−` corrections through the typed quantity endpoint, while announced quantity
remains visibly separate and redirected fiches are read-only for stock actions. The view does
not invent carton/rayon counts or a bulk physical-count workflow.

The Catalog auth service reads the `roles` claim from the API access token after silent
acquisition, exposes an `isAdministrator` signal for navigation affordances, and accepts
the API's `Administration`/legacy `Admin` role names. If silent acquisition requires an
interaction, it starts an MSAL redirect back to the current private route and the account
and administration pages render a renewal message instead of their generic API failure.
The backend remains the authorization boundary; a client-side admin link never grants
access by itself.
The root Catalog shell detects a pending OAuth `code`/`error` response and initializes the
lazy MSAL service so the saved `/compte` start page can be restored; ordinary anonymous
shell bootstraps remain lazy.

The signed-out Catalog `/compte` state uses the V2 editorial shell: a two-column member
introduction, concrete watchlist/alert benefits, provider-neutral login copy, and separate
`Se connecter`/`Créer un compte` actions. `CatalogAuthService.register()` starts the
External ID account-creation prompt with a `/compte` return URL; the corresponding
`infra/entra/Configure-EntraUserFlow.ps1` flow is attached only to the catalog application.
The route remains private and noindex while the public catalogue stays browseable without
authentication.

The authenticated Catalog `/compte` surface keeps the member watchlist as the default
tabbed view. `Ma liste de recherche` renders typed watchlist items with cover fallback,
edition scope, available/future/pending availability and alert dates; `Préférences et
compte` contains alert suspension, the authenticated identity, administration handoff and
account deletion. The header account trigger uses the full cached MSAL display name and a
filled navy treatment when connected, while anonymous visitors keep an outlined trigger
with a person icon. The Catalog account component owns the tab state locally and continues
to use the existing bearer-protected member API without changing its contracts.

As of 2026-09-09, `/prochaines-dates` keeps the next Books event in a prominent card and
renders the complete future Books schedule below it. The Catalog client reads the public
`/asso-events` collection, filters typed `Books` events and keeps the API's chronological
ordering as the source for both views. The card reuses the Website's calendar, clock and
location assets, embeds the same Google Maps URL only after a dedicated Maps consent, and
keeps both an opt-in placeholder and an external Maps link when that consent is absent.

As of 2026-09-07, the Catalog public entry point is the **Accueil** tab at `/`. It combines
the editorial hero, search and genre shortcuts with recent books, rare books, featured
genres, and a compact next-fair teaser that shows only the date and opening hours. The
`Les prochaines dates` tab now routes to `/prochaines-dates`, which renders the next
Books event's full details: date stamp, schedule, address, calendar link, map/location card,
and the complete list of later events; it no longer includes the home search or catalogue
sections.
The hero's standalone butterfly was replaced by a CSS book composition. Fixed Catalog copy
uses **bourse aux livres** rather than the standalone term. The latest local check passes
85 ChromeHeadless tests and the production build; the known initial bundle budget warning
remains, and live data was not changed.

The Catalog genre navigation keeps five curated source values in
`src/app/core/catalog-genres.ts`. The home hero and search filter merge those fallback
options with any additional genres returned by `GET /catalog/search`; the home cards link to
`/recherche?genre=...`, so the existing query-param subscription immediately loads the
filtered catalogue and preserves a selected genre even while the response is loading.

The public Catalog does not need a client contract change for bibliographic genre enrichment:
its existing `genres` projection remains API-driven, so newly persisted provider genres will
appear in the home/search dropdown and navigation after the Worker backfill. Curated fallback
values remain available while the catalog contains no matching provider genres.

The 2026-09-08 Catalog Lot 6 pass keeps public search filtering, ordering and pagination in
the EF query, removes the empty-result full-table fallback, and scopes announcement/fair
lookups to the returned page. Unknown browser routes now render a real 404 with
`noindex,nofollow`, and the SSR adapter returns HTTP 404 for them. Book canonical/JSON-LD
nodes are owned and removed with the detail component. Catalog HTTP now uses
`provideHttpClient(withFetch())` with hydration, while MSAL is no longer a global bootstrap
initializer and is loaded only when an authenticated feature needs it. The home fair read
uses the typed `/asso-events` collection and filters its future `Books` events; the client
does not rely on the single-event `/asso-events/next-books` projection.
Validation passes with 110 Catalog and 122 Scan ChromeHeadless tests, 333 backend solution
tests, and the Catalog production SSR build; the existing initial bundle budget warning
remains. Local SSR smoke confirms an unknown route returns 404/noindex and a legal route
returns 200; no deployment was made.

## Planned Books Scan client decisions

As of 2026-09-05, the P1-5 Scan foundation is implemented in `src/Scan` and deployed to
the DEV ACA: local verdicts, IndexedDB session/catalog/outbox persistence, MSAL `Tri`
authentication, and sequential gesture replay are present. P1-2 selected the existing
Jasmine/Karma/ChromeHeadless toolchain: browser integration tests use real IndexedDB and
a fake transport simulates delays, failures, mid-flight disconnects, and duplicate
responses. The local outbox states are `Pending`, `Kept`, `Rejected`, `CancelledLocal`,
`Quarantined`, and `Orphaned`; only final decisions reach the API, while a transmitted
cancellation becomes a new inverse gesture. Transient replay failures use exponential
backoff; repeated 4xx validation failures are quarantined after the same five-attempt
budget as the backend alert outbox.

The Scanette redesign shown in `docs/bourse-aux-livres/maquettes/scanette/` is implemented
and deployed in the same PWA: home and session-mode selection, distinct verdict surfaces,
session summary, cash register, consultation, manual ISBN keypad, and offline variants.
Consultation uses the local catalog without creating an outbox gesture; the cash screen
persists each sale in IndexedDB, decrements local stock optimistically, and replays it to
`POST /scan/sales` with an idempotent `ClientGestureId`. The root auth gate shows a dedicated
login surface until an Entra account with `Tri` or `Caisse` is available. Since 2026-09-09,
the signed-out screen follows the Scan connection mockup's 1a full-marine layout with the
official butterfly asset, while the authenticated-but-unauthorized state uses the 1c
missing-role card and keeps `Changer de compte`/`Réessayer la connexion` wired to the
existing MSAL actions. A previously
authorized cached account enters a visible degraded mode when silent renewal fails:
local triage, consultation, cash capture, camera and IndexedDB outbox remain usable, but
synchronization waits for a fresh token; an explicit logout or server 401/403 clears the
local authorization marker. The tri scan view starts the ZXing camera automatically,
keeps manual/photo fallback, and no longer renders the former top toast stack. Both Scan environments use the tenant-scoped CIAM
authority; the login request carries an explicit root return page and surfaces redirect
failures inline. The MSAL interceptor protects `/scan/*` with the API bearer token; the
wildcard is required for nested delta/session endpoints. CI validation covers 79
ChromeHeadless tests, four bootstrap tests, the production build, and deployment workflow
`33924618301`; the public HTTP smoke is green, while the interactive Tri retest still
requires a signed-in browser/device.

The 2026-09-04 Scan follow-up keeps the live camera open on the cash and consultation
surfaces: it starts on entry and restarts after each decoded book, while the cash list is
kept below the compact camera panel. Each cash item now has its own removal action. The
local workflow marks the same ISBN as `Déjà scanné à l’instant` when it reappears within
five seconds in one session (`RG-04`), and the verdict card is intentionally smaller so
the catalog facts remain visible. Ending a session synchronizes and closes its remote
session before clearing the active IndexedDB snapshot; a failed close keeps the local
gestures rather than silently losing them. Validation for this follow-up passes with 74
ChromeHeadless tests, the bootstrap contract, and the production build; it is included in
the deployed Scan image. The subsequent nested-endpoint authentication regression is
covered by the 79-test CI run described above.

The September 2026 volunteer statistics slice adds a private `/statistiques` route to the
Scan PWA and a `Ma contribution` tab to the authenticated Catalog account page when the
API token contains `Tri` or `Caisse`. Both clients consume the typed `GET /scan/me/statistics`
response. Scan stores one account-guarded snapshot in its existing IndexedDB `session` store
and renders it when the API is unavailable; clearing account state removes that snapshot.
The Catalog renders the same scan/cash data online, with twelve-month bars, time-slot
heatmap, impact, genres, fair sales, recent sessions and explicit estimated-value labels.
The Scan view supports a Tri/Caisse role switch when both roles are present. Connected
account smoke and post-deploy responsive checks remain required.

The 2026-09-12 Scan follow-up presents the authenticated home secondary actions as two
paired cards: a blue chart icon opens `/statistiques`, and an orange account-switch icon
starts the existing logout flow. The statistics route now owns a fixed viewport-height
surface and a touch-enabled `.statistics-scroll` container, so its long private report
scrolls internally while the global scan screens keep their intentional overflow lock.

The 2026-09-07 camera feedback follow-up keeps the same live stream for all three scan
destinations and renders a `Scan détecté` progress surface while the local/catalog and
bibliographic lookups are pending. The active camera preview is keyboard- and touch-
accessible; its focus action applies `single-shot`/`continuous` video-track constraints
when the browser exposes them and falls back quietly to the device autofocus otherwise.
The component exposes the focus state to assistive technology and preserves the existing
permission/session reuse behavior.

The 2026-09-07 Scan Lot 1 hardening keeps close requests as durable session records so a
new local triage session can open before an older close reaches the API. Pending gestures
from the older session are marked `Orphaned` instead of being silently kept under the new
mode; close processing groups decided gestures by their original session and ignores
quarantined/orphaned entries when deciding whether a close may complete. Local catalog
lookup failures are shown on cash and consultation, and a cash line is never created from
an unknown `isRare: false` fallback.

The 2026-09-08 Scan Lot 2 hardening subscribes to the scanner login observable, keeps the
IndexedDB connection lazy and reopenable after `versionchange`/blocked opens, and exposes
typed storage errors for the closed-by-another-instance versus unavailable-storage cases.
The scan surface distinguishes a session awaiting closure from a failed local write; cash
navigation confirms before discarding an unfinished sale, validated pending sales expose a
local cancellation action, and session termination is confirmed. The manual ISBN keypad
accepts the ISBN-10 `X` check character and describes its 10-or-13-character contract.

The 2026-09-08 Scan Lot 3 quality pass separates pending decisions from gestures awaiting
transmission on triage, cash, and consultation, exposes the synchronization action on each
operating surface, and removes the listed dead entry points. The scan delta now carries the
next Books fair date/schedule; the local session copy uses the synchronized `alertDelayMinutes`
for its correction window, while the admin verdict defaults are aligned to duplicate `5` and
demand-sales `1`. Photo decoding now creates candidates lazily and stops after the first
success, and camera permission/README copy is browser-neutral. Quarantined cash sales are
excluded from replay and local test fixtures clear only their catalog projection without
reintroducing a production purge method.

The 2026-09-08 Scan Lot 4 accessibility pass gives labeled camera, keypad, and cash-list
containers explicit semantics, removes the competing label from the live manual ISBN value,
and keeps a real `h1` target for each operating screen in both the empty and triage-verdict
states. The USB scanner focus behavior from S-22 remains deliberately unmodified until it
has been confirmed with the association's physical scanner.

The 2026-09-09 Scanette reprise lots 0-5 add the diagnostic export and recovery surfaces,
explicit IndexedDB migration/status handling, stale-session integrity checks, and durable
account-switch/session-close decisions. The Scan shell now routes home, triage, cash,
consultation, recovery, diagnostics, settings, and confirmation screens without the former
in-component navigation/status chrome. Every local set-aside gesture emits the low-cardinality
`scan_gesture_set_aside` telemetry event without account identifiers. BackOffice lists only
`InProgress` sessions older than 24 hours and can force-close them through an
`Administration`-protected action with the configured delayed-alert timing explained before
confirmation. After reconciling this delivery with the alert/camera layout from `main`, local
validation passes 165 Scan tests, 5 BackOffice bootstrap tests, 349 backend solution tests, and
both Angular production builds; responsive/device retests remain manual.

The 2026-09-11 Scan synchronization UX pass removes the authenticated shell's global return
header and legacy synchronization panel, leaving the multicolor rule as the first page chrome.
Automatic synchronization still runs when local mode becomes ready, after account authorization,
on reconnection, and on the existing periodic retry. Successful syncs use the transient
`Synchronisation réussie` toast. Offline, failed, and pending-action states render as one compact
`scan-status-bar` below the active mode/session header; `(hors connexion)` and `(action à faire)`
are explicit tags, and the strip opens a status modal with typed actions for retry, session
account-switch recovery, triage, and set-aside recovery. The old expanded alert rail is gone from
the user-facing layout. Local validation passes 170 Scan ChromeHeadless tests and the Scan
production build; PR [#127](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/127) is open,
connected production smoke remains pending, and no deployment was made.

The 2026-09-12 Scan storage-capability follow-up keeps the browser-persistence warning out of
the `home` and `session-mode` choice screens. On operating screens, its modal copy distinguishes local
browser retention from server synchronization, offers a user-gesture retry through
`ScanWorkflowService.requestPersistentStorage()`, and removes the sync retry action when no
outbox or synchronization error needs it. The repeated primary alert is omitted from the modal
details; an accepted persistence request closes the modal and shows the existing success toast.

The 2026-09-08 Catalog Lot 5 public-surface pass removes the hard-coded featured genre
taxonomy: the home cards, hero selector, and navigation menu now use only genres returned
by the public catalog API, and the home section stays hidden when that list is empty. The
home availability count queries the available projection, the fair location uses a
consent-gated Google Maps frame with a static link fallback, the footer year is computed at
render time, and the personal Facebook profile is no longer exposed. The existing direct
publisher and hosting information in the legal page was verified and left unchanged.

## App Structure

Both Angular apps follow the same high-level split:

- `core/` for shell, layouts, login, and cross-app wiring
- `feature/` for routed screens and business-facing UI
- `shared/` for interfaces, guards, services, and shared components

Verified feature roots:

- `BackOffice`: `actualities`, `actuality-detail`, `caisse`, `catalog-administration`, `dashboard-vpd`, `event-detail`, `vpd-events`
- `Website`: `actuality-detail`, `actuality-page`, `association`, `contact`, `event-detail`, `home`, `legal`, `maxence`, `tableau`, `vpd-all-events`, `vpd-events`

The 2026-09-06 camera-permission follow-up keeps the authorized media stream alive between
books: detection pauses while the verdict is shown, then `Garder`/`Écarter` resumes the same
stream without another `getUserMedia()` request. It passes 90 Scan ChromeHeadless tests and
the production build; the current main change still needs a Scan deployment and iPhone retest.

## Frontend Conventions

- Preserve the admin/public split, typed models/contracts, and each app's established HTTP
  access pattern. Both Angular applications use `provideZonelessChangeDetection()`.
- Catalog pages use manual RxJS subscriptions and call `ChangeDetectorRef.markForCheck()`
  after async loads; the external-reference follow action also does so in `finally` after
  its awaited watchlist request.
- Scan uses the `@vpd/ui` path-mapped SharedUi source, typed `HttpClient` services, ZXing
  `TRY_HARDER` for EAN-13/EAN-8 and ISBN QR codes, keyboard/photo/manual fallbacks, and
  explicit view notification after async callbacks. Cover URLs come from the provider chain,
  with an ISBN-based Open Library retry and a shared placeholder as defensive fallbacks.
- Scan auth reads API access-token roles: `Tri` can triage, `Caisse` can sell, and both can
  consult. MSAL initialization, `<app-redirect>`, tenant-scoped authority, and an explicit
  `redirectStartPage` keep refresh, role, and redirect failures visible and recoverable.
- `src/SharedUi/scripts/link-shared-ui.mjs` is invoked by each app's `prebuild`/`prestart`
  hooks and resolves dependencies from the calling application's working directory.
- Legacy event values are UTC wall-clock components: BackOffice converts picker values through
  `MyDate`, while Website renders UTC and uses `hourOpenDoors` for Books event start times.

## Website Rendering Modes

- `src/Website/` uses Angular SSR with `provideClientHydration(withEventReplay())` and
  render-mode mapping in `src/app/app.routes.server.ts`.
- Static association, Maxence, and legal pages are prerendered; content-driven pages are
  server-rendered; the live `evenement/:id/tableau` route stays client-rendered for `EventSource`.

## Data Access And Live Updates

- BackOffice/Website use Axios; Scan uses typed `HttpClient`, derives its LAN API host on port
  `5257` in development, and uses the deployed API URL in production. BackOffice protects
  routes with `MsalGuard` and adds its silent bearer through Axios, not `MsalInterceptor`.
- Website SSE is browser-only, replaces the prior source, ignores malformed payloads, and
  reconnects with bounded backoff. Website prices use `/product/public`/`visibleOnWebsite`,
  while the cashier and BackOffice retain the full `/product` projection.

## MAUI Client

- `src/MauiCashApp/` is Android-only .NET MAUI 10 (`net10.0-android`) with MVVM Toolkit,
  Refit, SQLite, embedded config, and MSAL.NET 4.88.0 silent-first auth using the Android
  redirect `msal427c90de-bf59-4b01-af63-dc0799248496://auth` and an `AuthHandler`.
- `IVpdApi.GetProductsAsync()` calls `GET /product`; the cash surface intentionally keeps the
  full projection, including products hidden from Website.
