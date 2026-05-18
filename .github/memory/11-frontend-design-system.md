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
  - `vpd-image` (`vpd-image, app-vpd-image`) — superset of both apps (rounded, backgroundColor, rotation, orientation, highPriorityFetching, height, width)
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

Each app keeps thin "smart wrapper" components with the legacy `app-*` selectors and original input/output names (`ActualityModel`, `VpdEvent`, `Product`, `(actualityDeleted)`, `(actualityUpdated)`, etc.). These wrappers forward the model to the DS component, set `[editable]` to the right mode, and bind dialogs/facades — they hold no presentation logic.

- BackOffice wrappers: `editable=true`, delegate edit/delete to Material dialogs + facades.
- Website wrappers: `editable=false`, pass-through only (read-only public site).

### Duplicates removed

Both `src/BackOffice/src/app/shared/components/{title,under-section,vpd-button,vpd-image}/` and `src/Website/src/app/shared/components/{title,under-section,button,vpd-image}/`, plus the per-app `capitalize.pipe.ts` / `line-number-title.pipe.ts` / `price.pipe.ts` are gone — `@vpd/ui` is the single source. SharedModules now only declare app-specific components and pull in `DesignSystemModule`. Dialog components (`CreateUpdateActualityDialog`, `CreateUpdateProductDialog`) remain declared in `FeatureModule` where they already lived; `SharedModule` keeps only `ConfirmationDialogComponent` + `CreateUpdateEventDialogComponent` on the BackOffice side.