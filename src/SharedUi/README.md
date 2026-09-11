# SharedUi - Design System

Composants UI partagés entre `Website`, `Catalog`, `Scan` et `BackOffice`.

## Pattern d'utilisation

Tous les composants "métier" (actualité, événement, produit) supportent un mode lecture seule (par défaut) et un mode édition activable via l'input `[editable]`. Les actions d'édition/suppression sont remontées au parent via les outputs `(editRequested)` et `(deleteRequested)` ; le parent (BackOffice) ouvre les dialogues et appelle les facades.

## Composants exposés

- `vpd-title`
- `vpd-button`
- `vpd-image`
- `vpd-actuality-card`
- `vpd-event-card`
- `vpd-product-card`
- `vpd-product-list`
- `vpd-loader`

`vpd-loader` reprend le vocabulaire des maquettes avec les variantes `line` (1a),
`flight` (1b), `squares` (1c), `skeleton` (1d), `ring` (1e), `traverse` (2a),
`butterflies` (2b), `fill` (2c) et `compact` (2d). Les applications qui l'utilisent
importent aussi ses styles globaux depuis `src/lib/components/vpd-loader/` dans leur
`styles.scss`.

## Pipes

- `vpdPrice`
- `vpdCapitalize`
- `vpdLineNumberTitle`

## Import

Les deux apps Angular exposent un alias `@vpd/ui` via `tsconfig.json` -> `paths`. Importer :

```ts
import { DesignSystemModule } from '@vpd/ui';
```

Puis ajouter `DesignSystemModule` aux `imports` et `exports` du `SharedModule` de l'app.
