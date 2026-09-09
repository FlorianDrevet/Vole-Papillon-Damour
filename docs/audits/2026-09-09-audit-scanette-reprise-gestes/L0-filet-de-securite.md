# L0 — Filet de sécurité : sortir les gestes bloqués d'un téléphone

**Audit de référence :** [`../2026-09-09-audit-scanette-reprise-gestes.md`](../2026-09-09-audit-scanette-reprise-gestes.md)
**Anomalies visées :** aucune — ce lot est un **prérequis d'observation**, pas une correction.
**Effort estimé :** ≈ 1 j
**Prérequis :** aucun. Ce lot passe en premier.

---

## Contexte

Deux gestes réels sont actuellement bloqués dans l'`outbox` d'un téléphone de production, en statut `Orphaned`. Ce statut n'a aucune transition sortante (`RPR-02`) : ils ne sont ni transmis, ni affichés, ni supprimables, et n'ont jamais quitté l'appareil — donc aucune route BackOffice ne les atteint.

**Tant que le contenu de cette `outbox` n'a pas été extrait, chaque déploiement risque d'écraser la seule preuve de ce qui s'est réellement produit.** Et la migration IndexedDB du lot L2 devra être validée sur cet export réel : une migration testée sur une base vide passe toujours.

Ce lot ne corrige rien. Il rend l'état local **lisible**.

---

## Périmètre

`src/Scan/` uniquement. Aucun fichier backend.

Fichiers à lire avant d'écrire :

- `src/Scan/src/app/offline/scan-local-store.service.ts` — les quatre magasins et leur accès
- `src/Scan/src/app/offline/scan-offline.model.ts` — `scanStoreNames`, `ScanOutboxEntry`, `ScanSessionSnapshot`
- `src/Scan/src/app/app.component.ts` — le point d'entrée conditionné par l'état d'authentification

---

## Ce qu'il faut construire

### 1. Un écran de diagnostic hors parcours

Accessible **uniquement** via le paramètre d'URL `?diagnostic=1`. Il ne doit apparaître dans aucun menu, aucun dock, aucune navigation.

Il affiche le contenu brut des quatre magasins IndexedDB de la base `vpd-scan` :

| Magasin | Contenu attendu |
|---|---|
| `catalog` | livres de la copie locale — n'afficher que le nombre d'entrées et les cinq plus récentes, le volume peut être important |
| `outbox` | **toutes** les entrées, sans filtre de statut, triées par `createdAt` |
| `sales` | toutes les entrées |
| `session` | la session active et toutes les demandes de clôture (`close-request:*`) |

Pour l'`outbox`, mettre en évidence le `status` de chaque ligne : c'est l'information qu'on vient chercher.

L'écran doit fonctionner **même si l'authentification a échoué**. Un bénévole dont le jeton a expiré doit pouvoir extraire ses données. Ne pas le placer derrière la garde d'auth de `app.component.ts`.

### 2. Un bouton « Copier le journal local »

Il écrit un JSON dans le presse-papiers via `navigator.clipboard.writeText()`.

**L'export de fichier est bloqué en PWA sur iOS** — pas de `<a download>`, pas de `showSaveFilePicker()`. Le presse-papiers est la seule voie fiable. Afficher une confirmation visible après la copie : sur mobile, rien ne signale un presse-papiers rempli.

Structure du JSON, dates en ISO 8601 UTC comme partout ailleurs dans l'application :

```json
{
  "exportedAt": "2026-09-09T09:43:00.000Z",
  "databaseVersion": 2,
  "session": {
    "scanSessionId": "…",
    "mode": "AvailableNow",
    "startedAt": "2026-09-09T09:12:00.000Z",
    "lastScanAt": "2026-09-09T09:41:00.000Z",
    "lastSyncAt": "2026-09-09T09:41:12.000Z",
    "scannedCount": 0,
    "keptCount": 0,
    "rejectedCount": 0,
    "closeRequested": false,
    "closeReason": null,
    "hasVolunteerId": true
  },
  "closeRequests": [],
  "outbox": [
    {
      "clientGestureId": "…",
      "scanSessionId": "…",
      "isbn13": "…",
      "status": "Orphaned",
      "kept": true,
      "occurredAt": "2026-09-09T09:38:00.000Z",
      "createdAt": "2026-09-09T09:38:00.100Z",
      "attemptCount": 0,
      "lastAttemptAt": null,
      "lastError": "…",
      "lastFailureKind": null
    }
  ],
  "sales": [],
  "catalogCount": 0
}
```

### 3. Interdits absolus dans l'export

Le JSON ne doit contenir **aucune** de ces valeurs :

- `volunteerId` / `homeAccountId` — identifiants de compte Entra
- tout jeton MSAL, `accessToken`, `idToken`, `refreshToken`
- toute clé du `localStorage` MSAL

`ScanSessionSnapshot.volunteerId` et `ScanSessionCloseRequest.volunteerId` doivent être **retirés** de l'objet exporté, pas masqués. Le booléen `hasVolunteerId` porte l'information de présence, qui suffit au diagnostic.

**Un test doit prouver cette absence** en sérialisant un état complet et en vérifiant qu'aucune de ces clés n'apparaît dans la chaîne produite.

### 4. Documentation

Ajouter une section à `src/Scan/README.md` expliquant à un responsable, sans jargon :

- comment atteindre l'écran (`https://scan.volepapillondamour.fr/?diagnostic=1`)
- où appuyer
- où coller le résultat
- **et surtout : ne pas vider les données du site avant d'avoir extrait le journal**

---

## Ce qu'il ne faut PAS toucher

Ce lot est un lot d'observation. Toute modification hors de cette liste est hors périmètre :

- ❌ **Ne pas modifier `ScanOutboxStatus`** ni aucun statut. La renomination `Orphaned` → `NeedsDecision` / `NeedsReattach` appartient à L2.
- ❌ **Ne pas modifier `scanDatabaseVersion`.** Aucune migration IndexedDB dans ce lot.
- ❌ **Ne pas toucher à `scan-sync.service.ts`** ni à `performSync()`. Les compteurs et les états de synchronisation appartiennent à L1.
- ❌ **Ne pas toucher à la navigation** ni au champ `screen`. Le routeur appartient à L4.
- ❌ **Ne rien supprimer de l'`outbox`.** Ni bouton de purge, ni nettoyage automatique, ni « réparation ». L'écran lit, il n'écrit pas.
- ❌ **Ne pas corriger les messages** du recueil de textes de l'audit. Ils appartiennent à L1.

Si une anomalie d'un autre lot est repérée pendant le travail, la mentionner dans le corps de la PR — ne pas la corriger.

---

## Définition de terminé

- [ ] L'écran est inaccessible sans `?diagnostic=1` — **prouvé par un test**.
- [ ] L'écran s'affiche même quand l'état d'authentification n'est pas `authorized` — prouvé par un test.
- [ ] Le JSON exporté contient les entrées `outbox` avec leur `status` réel, y compris `Orphaned` et `Quarantined`.
- [ ] Le JSON ne contient ni `volunteerId`, ni `homeAccountId`, ni jeton — **prouvé par un test** sur un état complet.
- [ ] La copie presse-papiers affiche une confirmation visible sur mobile.
- [ ] `src/Scan/README.md` documente la marche à suivre pour un responsable, avec l'avertissement sur la purge des données.
- [ ] `npm test` passe dans `src/Scan/`.
- [ ] Aucun fichier hors `src/Scan/` n'est modifié.

---

## Workflow

Appliquer sans exception :

- `.github/skills/delivery-workflow/SKILL.md` — worktree neuf depuis un `origin/main` fraîchement récupéré, branche `feat/scan-diagnostic-export`, PR vers `main`.
- `.github/skills/tdd-workflow/SKILL.md` — les tests d'abord.
- `.github/skills/angular-patterns/SKILL.md` — conventions Angular du dépôt.
- Lancer `graphify update .` après les modifications de code.
- Rendre l'URL de la PR. Ne pas fusionner.

## Après la livraison

Ce lot n'a de valeur qu'exécuté sur l'appareil concerné. Une fois la PR fusionnée et déployée :

1. Ouvrir `?diagnostic=1` sur le téléphone qui porte les deux gestes bloqués.
2. Copier le journal, le coller dans un fichier, le conserver.
3. **Ce fichier est le jeu de données de recette du lot L2.** La migration IndexedDB v2 → v3 devra être validée dessus.
