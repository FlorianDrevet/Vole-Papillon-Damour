# L4 — Navigation unifiée : une coquille, un dos, une barre

**Audit de référence :** [`../2026-09-09-audit-scanette-reprise-gestes.md`](../2026-09-09-audit-scanette-reprise-gestes.md)
**Anomalies visées :** `NAV-01`, `NAV-02`, `NAV-04`, `NAV-05`, `NAV-06`, `NAV-09`, `NAV-10`
**Effort estimé :** ≈ 4–5 j
**Prérequis :** L1 livré — `<scan-status-bar>` doit exister. Indépendant de L2 et L3 : peut démarrer en parallèle.

---

## Contexte

`ScannerComponent` compte **1 348 lignes de TypeScript et 637 lignes de gabarit** pour sept écrans pilotés par un simple champ `screen: ScanScreen`. Il n'y a pas de routeur : donc pas d'URL, pas d'historique, pas de retour navigateur, pas de reprise après rechargement. Sur iOS, le geste de retour système quitte l'application.

Chaque écran a réinventé sa barre, son dos et ses conteneurs d'erreur :

- **`NAV-01`** — `cash` et `consultation` portent un `dock-home-link` ; `tri` et `session-mode` n'ont **aucun retour**. La seule sortie du tri est « Terminer », qui publie les livres et arme le délai d'alerte. C'est la reprise de `SCAN-14` (audit du 8 septembre), non corrigé.
- **`NAV-02`** — cinq idiomes de navigation coexistent : cartes plein écran, lien de dock « Revenir au scan », lien tertiaire « Accueil », pilule pleine largeur, et rien du tout.
- **`NAV-05`** — deux `window.confirm()` natifs, sur les décisions les plus lourdes de l'application.
- **`NAV-06`** — cinq conteneurs d'erreur distincts, sans échelle de gravité.
- **`NAV-09`** — la caméra démarre en `force` sur caisse et consultation, sous condition sur le tri ; `toggleCamera()` n'existe pas sur le tri.
- **`NAV-10`** — la barre de session perd son bouton « Terminer » sur la saisie manuelle.

Le bénévole apprend à ne pas faire confiance à la position des commandes. C'est le pire résultat possible pour une application utilisée debout, en bourse, avec un carton de livres dans les bras.

---

## Périmètre

`src/Scan/` uniquement.

- `src/Scan/src/app/app.module.ts` — déclaration des routes
- `src/Scan/src/app/scanner/scanner.component.ts` et `.html` — à découper
- nouveau : `ScanShellComponent`, un composant par route, `ScanMessage`
- `src/Scan/src/app/scanner/camera-scanner.service.ts` — unification des règles caméra

---

## Ce qu'il faut construire

### 1. Le routeur — `NAV-04`

```
/                       ScanShellComponent — en-tête, barre d'état, dos, feuilles
├── /accueil            Trier · Caisse · Consulter
├── /reprise            gestes à reprendre (écran livré par L2)
├── /tri/mode           choix du mode
├── /tri                scan et décision
├── /tri/fin            résumé de session
├── /caisse
└── /consulter
```

`ScanScreen` et le champ `screen` disparaissent, ainsi que `destinationForScreen()` et `isScanDestinationActive()` dans leur forme actuelle.

**Gardes `canActivate` :**

- `/tri` et `/tri/mode` — rôle Entra `Tri`
- `/caisse` — rôle Entra `Caisse`
- `/tri/fin` — une session terminée existe, sinon redirection vers `/accueil`

Si L2 est déjà livré, ajouter une garde qui redirige vers `/reprise` lorsque l'arriéré l'exige. Si L2 ne l'est pas encore, déclarer la route `/reprise` et laisser la garde en `TODO` documenté dans la PR — ne pas construire l'écran ici.

### 2. Découpage de `ScannerComponent`

`ScanShellComponent` porte l'en-tête, le dos, `<scan-status-bar>` (livré par L1) et l'hôte des feuilles modales. Un composant par route porte son écran et rien d'autre.

**Aucun composant de `src/Scan/src/app/` ne doit dépasser 400 lignes** à la fin du lot.

### 3. Un seul dos, toujours au même endroit — `NAV-01`, `NAV-02`

En haut à gauche de l'en-tête, sur **tous** les écrans sauf `/accueil`.

**Le dock du bas ne porte plus jamais de navigation** — uniquement l'action principale de l'écran. Supprimer `dock-home-link`, « Revenir au scan », et la pilule « Revenir à l'accueil » de la fin de session : le dos les remplace tous.

Depuis `/tri` avec `scannedCount > 0`, le dos demande confirmation — mais ne clôture jamais la session. Quitter le tri et terminer une session sont deux actions distinctes.

### 4. Feuilles modales, pas remplacement d'écran

La **saisie manuelle** et les **confirmations** deviennent des feuilles modales sur la route courante. Le contexte et le flux caméra sont préservés : plus de `stopCamera()` / `startCamera()` à chaque aller-retour, ce qui résout une partie de `NAV-09` et supprime la redemande de permission sur certains navigateurs.

`manualReturnScreen` disparaît : une feuille revient là d'où elle vient, par construction.

### 5. Suppression des `window.confirm()` — `NAV-05`

Les deux appels de `scanner.component.ts` (`endSession()` et `returnHome()`) sont remplacés par le composant de confirmation. Aucun `window.confirm` ni `window.alert` ne doit subsister dans `src/Scan/`.

### 6. `ScanMessage` — trois niveaux, pas cinq conteneurs — `NAV-06`

| Niveau | Comportement |
|---|---|
| **info** | discret, se dissipe, n'appelle aucune action |
| **action requise** | persistant, porte son bouton d'action sur la même ligne |
| **blocage** | barre l'écran, explique la sortie |

Remplace `syncError`, `storageError`, `logoutError`, `cashMessage`, `sessionCloseError` et leurs cinq classes CSS ad hoc (`scan-feedback`, `cash-storage-error`, `consultation-storage-error`, `message-error`, `logout-feedback`).

### 7. Caméra unifiée — `NAV-09`

Mêmes règles de démarrage, d'arrêt et de reprise sur `/tri`, `/caisse` et `/consulter`. Bouton marche/arrêt présent partout, y compris sur le tri.

### 8. Barre de session identique partout — `NAV-10`

Même composant, même jeu d'actions, sur tous les écrans qui l'affichent.

---

## Ce qu'il ne faut PAS toucher

- ❌ **Ne pas construire l'écran `/reprise`** — lot L2. On déclare la route, on ne remplit pas l'écran.
- ❌ **Ne pas modifier `ScanOutboxStatus`, la migration IndexedDB, ni la logique de session** (`decide()`, `mergeRemoteSession()`, compteurs, `isBlockingScanStatus()`) — lots L2 et L3.
- ❌ **Ne pas modifier `ScanStatusService` ni `<scan-status-bar>`** — livrés par L1. On les consomme depuis la coquille.
- ❌ **Ne pas modifier le backend.**
- ❌ **Ne pas changer le verrouillage du mode de session** (`NAV-08`) : c'est un choix produit, hors périmètre de ce lot. Le signaler en question ouverte dans la PR.
- ❌ **Ne pas retoucher les textes** du recueil — livrés par L1.

Ce lot est un refactor de structure. **Aucun changement de comportement métier** ne doit y être introduit. Si une anomalie d'un autre lot est repérée, la mentionner dans le corps de la PR.

---

## Définition de terminé

- [ ] Le retour navigateur et le geste système iOS fonctionnent sur les sept écrans — vérifié sur appareil réel.
- [ ] Un rechargement en cours de session de tri revient sur la même route, session intacte, décision en cours préservée.
- [ ] `grep -rn "window.confirm\|window.alert" src/Scan/src` ne renvoie rien.
- [ ] Aucun composant de `src/Scan/src/app/` ne dépasse 400 lignes.
- [ ] Le dos est présent et à la même position sur les sept écrans sauf `/accueil` — vérifié en revue visuelle.
- [ ] Test : quitter `/tri` avec `scannedCount > 0` demande confirmation et **ne clôture pas** la session.
- [ ] Test : les gardes `canActivate` refusent `/tri` sans rôle `Tri` et `/caisse` sans rôle `Caisse`.
- [ ] Test : ouvrir la saisie manuelle depuis les trois écrans de scan conserve le contexte et reprend la caméra à la fermeture.
- [ ] `npm test` passe dans `src/Scan/`.
- [ ] Vérification responsive complète — c'est un lot entièrement visible.

---

## Workflow

- `.github/skills/delivery-workflow/SKILL.md` — worktree neuf depuis `origin/main`, branche `refactor/scan-navigation`, PR vers `main`.
- `.github/skills/tdd-workflow/SKILL.md` — les tests d'abord.
- `.github/skills/angular-patterns/SKILL.md` et `.github/skills/ui-ux-front-saas/SKILL.md` — lot visible de bout en bout.
- `graphify update .` après les modifications de code — ce lot déplace beaucoup de symboles, la mise à jour du graphe compte.
- Rendre l'URL de la PR. Ne pas fusionner.
