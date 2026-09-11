# 11 - Frontend Design System

## Shared UI Stack

- Both Angular apps use Angular Material plus Tailwind.
- Both import `scss/main` from `styles.scss` and define a custom Material theme.
- Both apps use custom font families through Tailwind and global body styling.

## Theme Tokens

Verified Tailwind theme tokens include:

- fonts: `CaveatBrush`, `DancingScript-Regular`
- colors: `primary-color` `#012f5f`, `secondary-color` `#1ACDF8`, `tertiary-color` `#F67700`, `text-color` `#37628A`, `background-color` `#E9F3FF`, `white`, `gold`, `success`
- `BackOffice` also defines `gray`
- `Website` now also maps semantic RGB-backed tokens through Tailwind/CSS variables for `surface`, `surface-soft`, `surface-strong`, `ink`, `ink-soft`, and `line`, so component templates can stay on semantic colors instead of raw hex values.

## Angular Material Theme Details

- `BackOffice` uses a light theme based on the Material blue palette and also themes `ngx-mat-timepicker`.
- `BackOffice` sets both `brand-family` and `plain-family` to `CaveatBrush`.
- `Website` uses a light theme with Material azure/blue palettes.

## Global Layout Conventions

- `html` and `body` are forced to full height with a light background.
- `body` uses a flex column layout and the `font-caveatbrush` utility.
- `Website` also defines a global `.no-scroll` helper class.
- `Website` centralizes shell-heavy styling in `src/styles.scss` with reusable primitives such as `.vpd-glass-card`, `.vpd-pill-badge`, and `.vpd-button`, which keeps shared navigation/home polish out of Angular component style budgets.
- The Website Sass entrypoint now uses `@use "scss/main"` and `src/scss/_main.scss` re-exports its partials via `@forward`, which removes the Dart Sass `@import` deprecation warnings without changing the rendered CSS.

## UX Structure Notes

- Both apps keep a `core/feature/shared` organization.
- `BackOffice` routes concentrate on admin workflows: login, actualities, events, tableau, and cash register.
- `Website` routes concentrate on public presentation, events, and the `Maxence` informational content tree.

## Guardrails For Future UI Work

- Preserve the custom handwritten/association visual identity rather than default enterprise styling.
- Extend existing Tailwind tokens and Material themes before introducing raw hex colors in components.
- Recheck desktop/mobile behavior on navigation and event-table screens when changing layout or theme code.
- For public-shell refinements, prefer evolving the semantic tokens and shared shell primitives in `src/styles.scss` before adding one-off component-level styling.
- On the Website public shell, prefer solid editorial panels, restrained shadows, and association-specific copy over heavy glassmorphism, floating glows, or generic startup-style hero effects.

## Public Catalog Visual Language

`src/Catalog/` has a dedicated catalog shell matching
`docs/bourse-aux-livres/maquettes/catalogue/`: pale blue surfaces, deep navy ink,
blue/orange availability accents, Newsreader display text, Libre Franklin body text, and
IBM Plex Mono metadata labels. It intentionally does not copy the Website's analytics or
association-content shell. The shared `BookCardComponent` is the visual primitive for
recent, rare, search, work, and detail entry points; available quantities and future
announcements are rendered as separate lines.

### Catalog administration shell

The Catalog `/administration` route follows the administration maquettes rather than the
public header/footer shell. Its canonical desktop frame is a 244px deep-navy
`AdminSidebar` (`#072b45`) beside a paper workspace with 40px top / 44px horizontal
content padding; the sidebar groups are Pilotage, Travail, Comptes and Réglages, with a
cyan active rule and translucent active background. Dashboard and workspace headings use
Newsreader, metadata uses IBM Plex Mono, and body controls use Libre Franklin. The shell
collapses the sidebar and content gutters at tablet/mobile breakpoints while preserving
the same information architecture. All maquette workspaces are represented, including
the correction dialog and account role editor; physical inventory history/actions remain
truthful empty or disabled when the API has no corresponding domain contract.

### Catalogue V2 — convention canonique

The detailed local reference is [the Catalogue V2 convention](../../docs/bourse-aux-livres/maquettes/catalogue/V2-CONVENTION.md).

- The current reference is the V2 catalogue mockup reviewed in September 2026. Future
  Catalog work should extend this visual language by default, including the public pages,
  account surface, and administration shell.
- The shell uses the real `papillon_without_back.png` mark, a 1280px maximum content width,
  24px mobile / 44px desktop gutters, the Website-like 302px dropdown panel, a thin
  cyan-blue-orange brand rule, and a dedicated three-column Catalog footer. The footer
  exposes catalogue navigation, the external association handoff, and local legal links;
  it does not repeat the Website's Maxence or association-content sections. Navigation dropdowns
  must be keyboard/focus usable and hidden when neither hovered nor focused.
- The current header keeps the account trigger outside the desktop navigation flex, centers
  the 42px controls, gives the external association link the same 42px height and orange
  hover/focus underline, and uses a person/login icon in both auth states. Anonymous users
  get an outlined `Mon compte` trigger; a connected user gets a filled navy trigger with
  the full display name from MSAL. It switches to the mobile layout at `1040px` to avoid
  intermediate-width overflow.
- Typography stays `Newsreader` for editorial headings, `Libre Franklin` for body and
  controls, and `IBM Plex Mono` for labels, metadata, dates, and technical identifiers.
  The canonical Catalog palette is paper `#f7fbfe`, paper-soft `#e9f4fb`, ink `#041d30` /
  `#072b45`, slate `#33536e` / `#4e6c84` / `#6d8ba2`, blue `#0c6ea6` / `#1497d6`, cyan
  `#7fd8f5`, orange `#f0801c` / `#f9a93c` / `#dc6412`, and pale lines `#d9e9f4` /
  `#e2eef7`.
- The External ID hosted authentication page uses the same Catalog palette through
  `infra/entra/vpd-catalog-authentication.css`. It targets Microsoft's documented `.ext-*`
  branding selectors, keeps the page French, and uses system fallbacks for Newsreader and
  Libre Franklin because tenant branding CSS cannot load the Catalog's local font assets.
- The home hero is an editorial question on a solid deep-navy panel, with the official
  association butterfly used as the desktop visual mark and a restrained mobile placement.
  Its search owns the genre selector and the API-backed title count; the next-fair block
  may expose a generated `.ics` link. The home
  `#prochaines-dates` calendar keeps the next Books event prominent with a dark date card
  and a light map/location card inspired by the Website event detail, then uses editorial
  date rows for the remaining future fairs.
- The home genre section uses five compact editorial cards, with the existing paper/ink
  palette and brand rule. Cards route to the filtered search page; the search and hero
  selectors use the same curated fallback plus any API-provided genre values.
- Public catalogue, search, work, detail, account, legal, and existing administration
  views share the same spacing, cards, pills, border language, restrained motion, and
  responsive collapse. Availability and future announcements remain truthful and separate;
  no price, stock, role, or external-reference data may be invented in the UI.
- The account page's authenticated watchlist follows the September 2026 mockup: a large
  editorial heading, two local tabs, one bordered card per followed title, and a separate
  preferences/account panel. Cards use the existing `CatalogWatchlistItem` contract for
  cover URL, edition publisher/year, quantity availability, next fair date, added date and
  last alert; they must not invent a reservation or price state.
- The Catalog client now consumes the typed P2/P3 administration API for overview,
  catalogue metadata/stock, dead stock, scan sessions, fairs/revenue, alert queues,
  members and settings. It also consumes the external bibliographic search separately
  from local catalogue results and lets authenticated members follow editions/works and
  suspend alerts. Keep the navigation/data contracts truthful: role assignment remains
  Entra-owned and physical cartons/capacity are not API concepts, so those controls stay
  explanatory rather than fake.

## Shared Design System Library (`@vpd/ui`)

- Location: [src/SharedUi/](src/SharedUi/) (raw TS source, not an Angular library project). Consumed via TypeScript path mapping `@vpd/ui` declared in each app's `tsconfig.json`.
- Module: `DesignSystemModule` from `@vpd/ui` is imported and re-exported by each app's `SharedModule`.
- Wired into each app via:
  - `tsconfig.json` `paths`: `@vpd/ui` + `@vpd/ui/*`, plus explicit `@angular/*`, `rxjs`, `rxjs/*`, `tslib` mappings so the bundler resolves bare imports from outside the app folder.
  - `tsconfig.app.json` / `tsconfig.spec.json` `include`: adds `../SharedUi/src/**/*.ts`.
  - `tailwind.config.js` `content`: adds `../SharedUi/src/**/*.{html,ts}`.
- Components (selectors keep `app-*` legacy aliases to avoid template churn):
  - `vpd-title` (`vpd-title, app-title`)
  - `vpd-under-section` (`vpd-under-section, app-under-section`)
  - `vpd-button` (`vpd-button, app-button, app-vpd-button`)
  - `vpd-image` (`vpd-image, app-vpd-image`) - superset of both apps (rounded, backgroundColor, rotation, orientation, highPriorityFetching, height, width)
  - `vpd-actuality-card`, `vpd-event-card`, `vpd-product-card`, `vpd-product-list`
- Pipes (kept legacy names so templates still write `| price`, `| capitalize`, `| lineNumberTitle`): `VpdPricePipe`, `VpdCapitalizePipe`, `VpdLineNumberTitlePipe`.
- Enums use string values (`OneLine`, `Bingo`, etc.) so they line up with API payloads. `DsVpdEventModel.eventType` is `VpdEventEnum | string | number` to remain compatible with Website's local numeric `VpdEventEnum` (which the Website itself converts at runtime).

### Display vs Edit pattern

DS card components are display-only by default. To enable edit/delete actions, pass `[editable]="true"` and listen to `(editRequested)` / `(deleteRequested)`:

```html
<vpd-actuality-card [actuality]="model()"
                    [editable]="true"
                    (editRequested)="openUpdateDialog()"
                    (deleteRequested)="openDeleteDialog()"></vpd-actuality-card>
```

`vpd-product-list` exposes the same outputs as `productEditRequested` / `productDeleteRequested` for each product in the grid. `[showPromotions]` toggles the promotion ribbon (Website default: `true` via wrapper; BackOffice prices: `false`).

### App-side wrappers

Each app keeps thin "smart wrapper" components with the legacy `app-*` selectors and original input/output names (`ActualityModel`, `VpdEvent`, `Product`, `(actualityDeleted)`, `(actualityUpdated)`, etc.). These wrappers forward the model to the DS component, set `[editable]` to the right mode, and bind dialogs/facades - they hold no presentation logic.

- BackOffice wrappers: `editable=true`, delegate edit/delete to Material dialogs + facades.
- Website wrappers: `editable=false`, pass-through only (read-only public site).

### Duplicates removed

Both `src/BackOffice/src/app/shared/components/{title,under-section,vpd-button,vpd-image}/` and `src/Website/src/app/shared/components/{title,under-section,button,vpd-image}/`, plus the per-app `capitalize.pipe.ts` / `line-number-title.pipe.ts` / `price.pipe.ts` are gone - `@vpd/ui` is the single source. SharedModules now only declare app-specific components and pull in `DesignSystemModule`. Dialog components (`CreateUpdateActualityDialog`, `CreateUpdateProductDialog`) remain declared in `FeatureModule` where they already lived; `SharedModule` keeps only `ConfirmationDialogComponent` + `CreateUpdateEventDialogComponent` on the BackOffice side.
