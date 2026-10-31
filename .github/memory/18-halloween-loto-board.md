# Halloween Loto Board

## Contract and routing

- The feature spec, plan, and design handoff live under `docs/features/halloween-loto-board/`.
- `TodayEventResult.LiveTheme` is populated only for a Bingo event; the API response exposes the optional string `liveTheme`. Website `TodayEventModel` accepts `Classic`, `Halloween`, or `null`.
- `TodayBannerComponent` selects the Halloween variant only when `today.liveTheme === 'Halloween'`. Classic/absent themes keep the existing banner template. The backoffice projection is not changed.
- The Halloween board is under `src/Website/src/app/feature/tableau/halloween/`; its root-level `HalloweenPreferences` is shared with `src/Website/src/app/core/layouts/today-banner/`.

## Motion and rollout

- `HalloweenPreferences` persists the calm choice at `vpd.tableau.calm` and combines it with `prefers-reduced-motion`. The banner entrance is claimed once by the root `TodayBannerHalloweenMotionService` per page load; no session storage is used.
- E6 bats use responsive viewport-relative paths on phones so both cross the 390 px banner; desktop retains the source-maquette distances. The board pauses its animations when `document.visibilityState` is hidden.
- Character animation rules live in `halloween-characters.scss`, imported globally with host-tag selectors. This preserves the source character styles without adding Angular component-style budget warnings.
- The API reads the `Event.LiveTheme` column added by `AddEventLiveTheme`. Apply that migration before deploying the API; no deployed migration or Azure change was made for this branch.
- Visual QA assets are in `docs/features/halloween-loto-board/screenshots/`.

## Verified validation (2026-10-31)

- The implementation is under review in [PR #364](https://github.com/FlorianDrevet/Vole-Papillon-Damour/pull/364); it is open and has not been merged.
- Backend solution tests: 1,061 passed. Website ChromeHeadless tests: 148 passed; Website production build passes with existing initial-bundle/style/CommonJS warnings.
- BackOffice production build passes and direct Angular tests pass 60/60. Its `npm test` wrapper still stops on two existing bootstrap-contract regex assertions before Angular tests run.
- At CPU throttling ×4, the Bingo animation produced 159 sampled frame gaps; the maximum was 50 ms, with zero gaps over 50 ms and zero Long Tasks. Headless checks also confirmed hidden-page pause, reduced-motion/calm suppression, and no second ghost after same-page route navigation.
- Website initial output is 1.73 MB raw / 412.65 KB estimated transfer, about 100 KB raw above the measured `origin/main` baseline and within RG-37's 150 KB allowance. The build has no new character-style warnings; remaining initial-bundle, classic-tableau/product-style, and CommonJS warnings are pre-existing.
- `graphify update .` refreshed AST extraction; HTML visualization is blocked by Graphify's 9,278-node limit. Q1 about simplifying the classic theme remains open.
