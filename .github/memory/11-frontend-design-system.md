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

## Angular Material Theme Details

- `BackOffice` uses a light theme based on the Material blue palette and also themes `ngx-mat-timepicker`.
- `BackOffice` sets both `brand-family` and `plain-family` to `CaveatBrush`.
- `Website` uses a light theme with Material azure/blue palettes.

## Global Layout Conventions

- `html` and `body` are forced to full height with a light background.
- `body` uses a flex column layout and the `font-caveatbrush` utility.
- `Website` also defines a global `.no-scroll` helper class.

## UX Structure Notes

- Both apps keep a `core/feature/shared` organization.
- `BackOffice` routes concentrate on admin workflows: login, actualities, events, tableau, and cash register.
- `Website` routes concentrate on public presentation, events, and the `Maxence` informational content tree.

## Guardrails For Future UI Work

- Preserve the custom handwritten/association visual identity rather than default enterprise styling.
- Extend existing Tailwind tokens and Material themes before introducing raw hex colors in components.
- Recheck desktop/mobile behavior on navigation and event-table screens when changing layout or theme code.