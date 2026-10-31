# 11 - Frontend Design System

## Shared Website and BackOffice language

- Website and BackOffice use Angular Material, Tailwind, shared SCSS foundations, and association-specific handwritten typography. Brand colors include navy `#012f5f`, cyan `#1ACDF8`, orange `#F67700`, text blue `#37628A`, and pale blue `#E9F3FF`; extend theme tokens before adding raw component colors.
- Website shell primitives and semantic surface/ink/line tokens live in `src/Website/src/styles.scss`. Prefer editorial panels, restrained shadows, and association-specific copy over generic enterprise/tech styling.
- BackOffice full-width bands use `px-shell`; centered content uses fixed `px-6 lg:px-11` gutters inside a max-width shell so parent scaling does not consume width on ultra-wide displays.
- Keep controls keyboard-accessible, labeled, visibly focused, and touch-friendly (44 px target where practical). Respect `prefers-reduced-motion` and the shared `vpd.tableau.calm` setting for event/loto animation.

## Catalog V2

- Catalog has a separate editorial catalogue system, not the Website association-content shell. The canonical mockup contract is `docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md`.
- Core palette: paper `#f7fbfe`, navy ink `#041d30`/`#072b45`, slate text, blue/cyan, and orange availability accents. Typography is Newsreader for headings, Libre Franklin for body/control text, and IBM Plex Mono for metadata and IDs.
- The shell uses the association butterfly, 1280 px content maximum, 24 px mobile/44 px desktop gutters, a cyan-blue-orange rule, and a dedicated three-column footer. At 1040 px it changes to mobile navigation to prevent intermediate-width overflow.
- Use shared book-card variants for home, search/list, rare, work, and detail contexts. Keep availability, future announcements, and rare-book price truthful and distinct; no fabricated stock, reservation, role, or physical-inventory state.
- Public catalogue pages remain crawlable. Account/admin screens use the same paper/ink language but keep API-backed roles/permissions and empty/error states explicit. Do not display account IDs, reporter identities, or prices beyond their contract.
- Catalog mobile layouts collapse sidebars and controls, preserve safe areas, use internal scrolling only where designed, and keep menus/dialogs operable by keyboard and touch.

## Shared UI package

- `src/SharedUi/` is raw TypeScript source consumed via `@vpd/ui` path mappings, not a separately built Angular library. Every app maps `@vpd/ui` and `@vpd/ui/*` and includes shared source in app/spec compilation and Tailwind content scanning.
- Shared module components include title, section, button, image, actuality/event/product cards and product list, plus price/capitalize/line-title pipes. Keep app-side wrappers thin: BackOffice wrappers wire dialogs/facades; Website wrappers are read-only.
- Shared display components remain display-only by default. Editing/deleting is enabled explicitly through inputs and event outputs; do not put app-specific dialogs or facade logic in `SharedUi`.
- Each app runs `src/SharedUi/scripts/link-shared-ui.mjs` through its own prebuild/prestart hook. Resolve dependencies relative to the consuming app.

## Event surfaces

- One Website event-identity helper feeds calendar cells, list cards, detail views, banners, and home cards. Keep Paris date handling and server rendering safe.
- Halloween scenes use the existing themed characters and event's `liveTheme`; classic/unthemed events retain the classic view. Calm/reduced-motion preferences apply to banner and board animation. Character styles are global to avoid per-component Sass budget growth.
- The Website `anyComponentStyle` budget is 7 kB warning / 8 kB error. Put reusable shared-shell/character styling in the global stylesheet when that avoids duplicate component bundles.
- See `.github/memory/18-halloween-loto-board.md` for current board behavior and its validation.
