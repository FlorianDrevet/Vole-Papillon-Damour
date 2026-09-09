# L1 — Séparer les états de synchronisation et dire la vérité

**Audit de référence :** [`../2026-09-09-audit-scanette-reprise-gestes.md`](../2026-09-09-audit-scanette-reprise-gestes.md)
**Anomalies visées :** `RPR-01`, `RPR-08`, `NAV-03`
**Effort estimé :** ≈ 2–3 j
**Prérequis :** L0 livré.

---

## Contexte

Le bandeau de la Scanette affiche « Catalogue synchronisé » puis « Synchronisation à reprendre » sans que rien n'ait échoué. Ce sont **la même variable**.

`syncStatus: 'idle' | 'syncing' | 'success' | 'error'` et `get syncLabel()` dans `scanner.component.ts` encodent simultanément cinq états sans rapport : fraîcheur du catalogue, état de la file d'envoi, clôture de session, arriéré de gestes mis de côté, arriéré de quarantaine. Un enum à quatre valeurs ne peut pas représenter cinq axes indépendants.

Pire, `createOutboxSummary()` dans `scan-sync.service.ts` renseigne `orphaned` et `quarantined` avec des **totaux cumulés** (`countOrphanedOutboxEntries()`, `countQuarantinedOutboxEntries()`), et `performSync()` transforme tout total non nul en `syncStatus = 'error'`. Conséquence : dès qu'un geste a été mis de côté une fois, **toute synchronisation ultérieure affiche une erreur, indéfiniment**, même quand tout a parfaitement réussi. Appuyer sur « Synchroniser » ne fera jamais repasser le bandeau au vert.

Un bénévole apprend en une bourse à ignorer un bandeau rouge permanent. Il n'ira donc pas le lire le jour où il signale un vrai problème.

---

## Périmètre

`src/Scan/` uniquement.

Fichiers concernés :

- `src/Scan/src/app/offline/scan-offline.model.ts` — accueillir `ScanStatusSnapshot`
- `src/Scan/src/app/offline/scan-sync.service.ts` — `OutboxSyncSummary`, `createOutboxSummary()`, `flushOutboxInternal()`
- `src/Scan/src/app/offline/scan-local-store.service.ts` — compteurs
- `src/Scan/src/app/scanner/scanner.component.ts` — `performSync()`, `syncStatus`, `syncLabel()`
- `src/Scan/src/app/scanner/scanner.component.html` — les quatre copies de `<aside class="sync-panel">`
- nouveau : `src/Scan/src/app/offline/scan-status.service.ts`
- nouveau : composant `scan-status-bar`

---

## Ce qu'il faut construire

### 1. `ScanStatusSnapshot` — quatre axes indépendants

Dans `scan-offline.model.ts` :

```ts
export interface ScanStatusSnapshot {
  catalog:  {state: 'fresh' | 'stale' | 'never'; syncedAt: string | null};
  outbox:   {state: 'idle' | 'sending' | 'retrying'; queued: number};
  decision: {pending: number};   // session courante uniquement
  setAside: {total: number};     // arriéré à régler, action requise
}
```

`syncedAt` est une date ISO 8601 UTC, comme tous les horodatages de l'application. `'stale'` se déclenche au-delà d'un seuil à définir en constante nommée — proposer 4 h et le documenter.

L'axe `decision` porte le compteur de la **session courante**. À ce stade, `getOutboxCounts()` n'est pas encore paramétré par session (c'est `RPR-10`, lot L3) : câbler l'axe sur le compteur existant et laisser un `TODO` référençant `RPR-10`. Ne pas anticiper L3 ici.

### 2. `ScanStatusService`

Nouveau service `providedIn: 'root'` exposant un `Signal<ScanStatusSnapshot>`. Il devient **la seule source** des états affichés. Aucun composant ne recalcule un libellé d'état par lui-même.

### 3. Deltas dans `OutboxSyncSummary`

```ts
export interface OutboxSyncSummary {
  sent: number;
  remaining: number;
  stoppedOnError: boolean;
  quarantined?: number;      // total — alimente l'axe setAside
  orphaned?: number;         // total — alimente l'axe setAside
  newlyQuarantined: number;  // delta de CETTE synchronisation
  newlyOrphaned: number;     // delta de CETTE synchronisation
}
```

Les compteurs locaux `quarantined` et `orphaned` de `flushOutboxInternal()` portent déjà les deltas : ils sont incrémentés à chaque mise de côté effective. Les remonter tels quels dans les nouveaux champs, et cesser de les écraser par `Math.max(summary.quarantined ?? 0, quarantined)`.

### 4. `performSync()` ne ment plus

- Un **total** non nul alimente l'axe `setAside` — un bandeau d'action, pas un état d'erreur.
- Un **delta** non nul déclenche un message ponctuel : « 2 livres viennent d'être mis de côté ».
- `syncStatus = 'error'` n'est plus positionné que sur un échec réel de la requête en cours.
- Supprimer `get syncLabel()` et l'enum à quatre valeurs.

### 5. `<scan-status-bar>` — un composant, une position

Le gabarit contient **quatre copies rigoureusement identiques** de `<aside class="sync-panel">` (écrans `tri`, `manual`, `cash`, `consultation`), à une position verticale différente selon l'écran (`NAV-03`, reprise de `SCAN-17`).

Extraire un composant unique consommant `ScanStatusService`, et l'utiliser partout. La position devient identique sur tous les écrans.

Le composant affiche les axes qui ont quelque chose à dire — un axe au repos ne prend pas de place — et n'affiche jamais deux axes dans le même élément de texte.

### 6. Recueil de textes

Appliquer le tableau « Recueil de textes » de l'audit. Trois règles :

1. Le mot « geste » ne sort jamais à l'écran — on dit « livre ».
2. Tout message d'action requise porte son bouton d'action, sur la même ligne.
3. Aucun message ne demande de « prévenir un responsable » sans dire pourquoi lui plutôt que le bénévole.

Centraliser la pluralisation, aujourd'hui faite à la main par des ternaires dans `scanner.component.ts`, et corriger l'accord fautif « 2 gestes sans décision **a** été **isolés** ».

---

## Ce qu'il ne faut PAS toucher

- ❌ **Ne pas modifier `ScanOutboxStatus`.** La renomination `Orphaned` → `NeedsDecision` / `NeedsReattach` et la migration IndexedDB appartiennent à L2.
- ❌ **Ne pas construire l'écran `/reprise`.** L1 rend l'arriéré *visible et honnête* ; L2 le rend *actionnable*. Le bandeau `setAside` peut afficher un bouton inactif ou masqué à ce stade — le documenter dans la PR.
- ❌ **Ne pas paramétrer `getOutboxCounts()` par session** — c'est `RPR-10`, lot L3.
- ❌ **Ne pas toucher à `decide()`, `mergeRemoteSession()`, `rebindSession()`, `isBlockingScanStatus()`** — lots L2 et L3.
- ❌ **Ne pas introduire le routeur Angular** ni toucher au champ `screen` — lot L4. `<scan-status-bar>` est un composant réutilisé dans le gabarit existant, pas une coquille de routage.
- ❌ **Ne pas supprimer les `window.confirm()`** — lot L4.

Si une anomalie d'un autre lot est repérée, la mentionner dans le corps de la PR — ne pas la corriger.

---

## Définition de terminé

- [ ] **Test central :** deux synchronisations réussies consécutives, avec des entrées `Orphaned` présentes en base, n'affichent **aucune erreur** à la seconde.
- [ ] Test : une synchronisation réussie avec arriéré affiche simultanément « Catalogue à jour » **et** le bandeau d'arriéré, sans contradiction.
- [ ] Test : un delta non nul produit un message ponctuel, distinct du bandeau d'arriéré persistant.
- [ ] Test : un échec réseau réel produit bien `syncStatus = 'error'`.
- [ ] `grep -c "sync-panel" src/Scan/src/app/scanner/scanner.component.html` renvoie `0`.
- [ ] `syncLabel` n'existe plus dans la base de code.
- [ ] Aucune chaîne visible à l'écran ne contient le mot « geste ».
- [ ] `npm test` passe dans `src/Scan/`.
- [ ] Vérification responsive de la barre d'état sur les quatre écrans concernés.

---

## Workflow

- `.github/skills/delivery-workflow/SKILL.md` — worktree neuf depuis `origin/main`, branche `refactor/scan-status-axes`, PR vers `main`.
- `.github/skills/tdd-workflow/SKILL.md` — les tests d'abord.
- `.github/skills/angular-patterns/SKILL.md` et `.github/skills/ui-ux-front-saas/SKILL.md` — la barre d'état est une modification visible.
- `graphify update .` après les modifications de code.
- Rendre l'URL de la PR. Ne pas fusionner.
