# L3 — Fiabiliser la session : tarir la source

**Audit de référence :** [`../2026-09-09-audit-scanette-reprise-gestes.md`](../2026-09-09-audit-scanette-reprise-gestes.md)
**Anomalies visées :** `RPR-04` (P1), `RPR-05`, `RPR-06`, `RPR-07`, `RPR-09`, `RPR-10`
**Effort estimé :** ≈ 3–4 j
**Prérequis :** L2 livré — la migration de statuts doit être en place.

---

## Contexte

L2 nettoie l'arriéré. **L3 empêche qu'il se reforme.**

Six défauts distincts concourent à produire des gestes mis de côté et des compteurs faux :

1. **`RPR-04`** — une session portant un geste `Pending` ne peut **jamais** être clôturée côté serveur. `closeRequestedSessions()` passe son tour tant que `countBlockingOutboxEntriesForSession() > 0`, et `isBlockingScanStatus()` inclut `Pending`. Le message « sera clôturée dès que la synchronisation aboutira » promet une résolution impossible. Effet en cascade : la session reste `InProgress` côté serveur, ce qui déclenche `RPR-05` à la prochaine ouverture.
2. **`RPR-05`** — le serveur peut renvoyer une session d'identifiant différent de celui demandé ; `mergeRemoteSession()` appelle alors `rebindSession()`, qui renomme **toutes** les entrées outbox. Deux sessions locales peuvent fusionner sur une seule session serveur.
3. **`RPR-06`** — `mergeRemoteSession()` appelle `ensureSession()`, qui **crée** une session quand aucune n'existe. Appelé après un `clearSession()`, il fabrique une session active vierge.
4. **`RPR-07`** — `scannedCount`, `keptCount`, `rejectedCount` sont incrémentés à la main en cinq endroits, sans source unique de vérité. D'où « 6 triés » en barre de tri contre « 3 scannés » en fin de session.
5. **`RPR-09`** — `bindSessionToVolunteer()` sort si `volunteerId !== null` : il lie, mais ne **re**lie jamais. Après un renouvellement de `homeAccountId`, toute session antérieure devient définitivement « celle d'un autre bénévole », et l'intégralité de ses gestes est mise de côté d'un bloc, y compris ceux qui étaient décidés. **C'est le scénario le plus probable derrière l'incident du 9 septembre.**
6. **`RPR-10`** — `getOutboxCounts()` agrège l'outbox entière sans filtrer par session. « 1 décision à prendre » peut désigner un geste inatteignable, ce qui verrouille simultanément `endSession()` et `logout()`.

---

## Périmètre

Client **et** serveur.

**Client** — `src/Scan/src/app/offline/` :
- `scan-offline.model.ts` — `ScanSessionSnapshot`
- `scan-workflow.service.ts` — `mergeRemoteSession()`, `bindSessionToVolunteer()`, compteurs, `ensureSession()`
- `scan-sync.service.ts` — `closeRequestedSessions()`, `belongsToDifferentAccount()`, `bindActiveSessionToCurrentAccount()`
- `scan-local-store.service.ts` — `isBlockingScanStatus()`, `getOutboxCounts()`, `rebindSession()` (suppression)

**Serveur** :
- `src/Backend/Vole_Papillon_Damour.Contracts/Books/Responses/ScanSessionResponse.cs`
- `src/Backend/Vole_Papillon_Damour.Application/Books/Common/ScanSessionResult.cs`
- `src/Backend/Vole_Papillon_Damour.Application/Books/Commands/ScanSession/OpenScanSessionCommandHandler.cs`

---

## Ce qu'il faut construire

### 1. Deux identités de session, jamais confondues — `RPR-05`

Sur `ScanSessionSnapshot` :

| Champ | Rôle |
|---|---|
| `clientSessionId` | **Immuable**, généré localement, clé de regroupement de l'outbox. Ne change jamais. |
| `remoteSessionId` | Assigné par le serveur, utilisé uniquement pour construire les URLs d'appel API. `null` tant que la session n'a pas été ouverte à distance. |

**Supprimer `rebindSession()`.** Plus aucun renommage en masse d'entrées outbox. L'outbox est groupée par `clientSessionId` ; l'appel API utilise `remoteSessionId`.

Côté serveur, ajouter `reusedExistingSession: bool` à `ScanSessionResponse` et `ScanSessionResult`. `OpenScanSessionCommandHandler` le positionne à `true` sur ses deux chemins de réutilisation — session retrouvée par `ClientSessionId`, et session `InProgress` du même bénévole/mode. Le client doit **savoir** qu'il a reçu autre chose que ce qu'il demandait, au lieu de le déduire d'une comparaison d'identifiants.

La réutilisation elle-même reste en place : c'est le correctif `API-02` du 8 septembre, il est correct. On la rend explicite et traçable.

### 2. `mergeRemoteSession()` ne crée jamais de session — `RPR-06`

Sortir immédiatement si `getSession()` retourne `null`. Ne plus appeler `ensureSession()` depuis ce chemin.

### 3. Compteurs dérivés — `RPR-07`

Remplacer `scannedCount`, `keptCount` et `rejectedCount` du snapshot par des fonctions dérivées de l'outbox filtrée sur `clientSessionId` :

- `scannedCount` = nombre d'entrées de la session, hors `CancelledLocal`
- `keptCount` = entrées `Kept` de la session
- `rejectedCount` = entrées `Rejected` de la session

Le snapshot ne conserve plus que ce que l'outbox ne porte pas : `startedAt`, `lastScanAt`, `lastSyncAt`, `mode`, `targetAssoEventsId`, `closeRequested`, `closeReason`.

Supprimer les recombinaisons `Math.max(current.x, remoteSession.x)` de `mergeRemoteSession()` : les compteurs locaux font foi, ils sont dérivés de la vérité locale.

### 4. Un geste non décidé ne gèle plus sa session — `RPR-04`

Retirer `Pending` de `isBlockingScanStatus()` **pour le seul contrôle de clôture**. Un geste non tranché au moment de la clôture part en `NeedsDecision` (statut livré par L2) et cesse de bloquer.

`Kept` et `Rejected` restent bloquants : une décision prise doit partir avant la clôture, c'est le comportement correct.

Réécrire le message correspondant selon le recueil de textes de l'audit — il ne doit promettre que ce que le code peut tenir.

### 5. Changement de compte : demander, jamais trancher en silence — `RPR-09`

Détecter le changement de `homeAccountId` **au login**, pas pendant la synchronisation.

Si une session locale existe avec un `volunteerId` différent, poser la question explicitement :

> « Des gestes d'une session précédente sont présents sur cet appareil. Les reprendre sous votre compte, ou les mettre de côté pour un responsable ? »

- **Reprendre** → relier la session au compte courant, remettre les gestes en file.
- **Mettre de côté** → statut `NeedsReattach` avec `setAsideReason: 'other-volunteer'`, visible dans l'écran Reprise de L2.

`bindSessionToVolunteer()` peut désormais relier une session déjà liée, mais **uniquement** sur cette action explicite du bénévole. Jamais en effet de bord d'une synchronisation.

> ⚠️ **Le refus de transmettre les gestes du bénévole A sous le jeton du bénévole B reste intégralement acquis** — c'est l'objet réel de `SCAN-03` (audit du 8 septembre). Le rattachement est un acte volontaire du bénévole connecté, pas un contournement.

### 6. Compteurs portés par la session courante — `RPR-10`

`getOutboxCounts(clientSessionId)` devient obligatoirement paramétré.

L'arriéré global — l'axe `setAside` de `ScanStatusSnapshot`, livré en L1 — reste compté séparément et **n'entre pas** dans les gardes de `endSession()` ni de `logout()`. Un arriéré ne doit jamais empêcher de terminer une session ni de changer d'utilisateur : il a son propre écran.

Retirer le `TODO` laissé par L1 sur l'axe `decision`.

---

## Ce qu'il ne faut PAS toucher

- ❌ **Ne pas revenir sur `SCAN-03`.** Voir l'avertissement du § 5.
- ❌ **Ne pas modifier `ScanOutboxStatus` ni `scanDatabaseVersion`** — livrés par L2. Si un statut manque, le signaler dans la PR au lieu de l'ajouter.
- ❌ **Ne pas introduire le routeur Angular** ni découper `ScannerComponent` — lot L4.
- ❌ **Ne pas modifier le comportement de réutilisation de session** dans `OpenScanSessionCommandHandler`. On ajoute un drapeau et une trace, on ne change pas la règle.
- ❌ **Ne pas toucher à `CloseIdleScanSessionsCommand`** ni au délai d'alerte — lot L5.
- ❌ **Ne pas modifier les migrations EF Core** ni le schéma SQL. `reusedExistingSession` est un champ de réponse calculé, pas une colonne.

Si une anomalie d'un autre lot est repérée, la mentionner dans le corps de la PR — ne pas la corriger.

---

## Définition de terminé

- [ ] Test : ouvrir une session alors qu'une session `InProgress` existe côté serveur **ne renomme aucune entrée locale**. `rebindSession` n'existe plus dans la base de code.
- [ ] Test : la barre de tri et l'écran de fin affichent les mêmes chiffres, y compris après réutilisation de session côté serveur.
- [ ] Test : une session portant un geste jamais décidé **se clôture**, le geste partant en `NeedsDecision`.
- [ ] Test : après un `clearSession()`, une synchronisation ne recrée **aucune** session active.
- [ ] Test : deux comptes successifs sur le même appareil ne produisent **aucun** geste mis de côté silencieusement — la question est posée.
- [ ] Test : les gestes du bénévole A ne partent jamais sous le jeton du bénévole B (non-régression `SCAN-03`).
- [ ] Test : un arriéré non nul n'empêche ni `endSession()` ni `logout()`.
- [ ] Test backend : `reusedExistingSession` vaut `true` sur les deux chemins de réutilisation, `false` sur une création.
- [ ] `dotnet test .\src\Backend\Vole_Papillon_Damour.slnx` passe.
- [ ] `npm test` passe dans `src/Scan/`.

---

## Workflow

- `.github/skills/delivery-workflow/SKILL.md` — worktree neuf depuis `origin/main`, branche `fix/scan-session-integrity`, PR vers `main`.
- `.github/skills/tdd-workflow/SKILL.md` — les tests d'abord.
- `.github/skills/cqrs-feature/SKILL.md`, `.github/skills/dotnet-patterns/SKILL.md` et `.github/skills/xunit-unit-testing/SKILL.md` pour la partie backend.
- `.github/skills/angular-patterns/SKILL.md` pour la partie client.
- `graphify update .` après les modifications de code.
- Rendre l'URL de la PR. Ne pas fusionner.
