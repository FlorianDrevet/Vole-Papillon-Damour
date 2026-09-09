# L2 — L'écran Reprise : la fonctionnalité manquante

**Audit de référence :** [`../2026-09-09-audit-scanette-reprise-gestes.md`](../2026-09-09-audit-scanette-reprise-gestes.md)
**Anomalies visées :** `RPR-02` (P1), `RPR-03` (P1)
**Effort estimé :** ≈ 3–4 j
**Prérequis :** L1 livré. **Et l'export L0 récupéré depuis un téléphone réel** — il sert de jeu de données de recette pour la migration.

---

## Contexte

C'est le cœur du correctif.

La Scanette sait mettre un geste de côté. Elle n'a **aucun moyen de le reprendre**. Le statut `Orphaned` n'a pas de transition sortante : `listTransmittableOutboxEntries()` ne retient que `Kept` et `Rejected`, aucun composant ne lit ces entrées, rien ne les supprime, et elles n'ont jamais quitté le téléphone — donc aucune route BackOffice ne les atteint. Le message « Prévenez un responsable avant toute publication » désigne une action que **personne ne peut effectuer**.

Trois fonctions distinctes produisent ce statut unique, avec trois significations incompatibles :

| Fonction | Le geste était… |
|---|---|
| `orphanPendingOutboxEntriesFromOtherSessions()` | non décidé — le message « sans décision » est juste |
| `orphanOutboxEntry()` dans `flushOutboxInternal()` | **décidé**, mais sans session locale correspondante |
| `orphanBlockingOutboxEntriesForSession()` | **tous statuts**, mis de côté parce que la session appartient à un autre compte |

Le message affiché dit « sans décision » dans les trois cas. Il est donc faux deux fois sur trois.

Second défaut, plus grave : `decide()` peut **jeter une décision explicite en silence**, et le gabarit affiche « Décision conservée · scannez le suivant ». La donnée est perdue *et* l'application affirme le contraire.

---

## Périmètre

`src/Scan/` uniquement. Aucun fichier backend.

- `src/Scan/src/app/offline/scan-offline.model.ts` — statuts, `setAsideReason`, `scanDatabaseVersion`
- `src/Scan/src/app/offline/scan-local-store.service.ts` — migration, requêtes, `reattachOutboxEntry()`
- `src/Scan/src/app/offline/scan-workflow.service.ts` — `decide()`, rattachement automatique
- `src/Scan/src/app/offline/scan-sync.service.ts` — alimentation de `setAsideReason`
- `src/Scan/src/app/scanner/scanner.component.html` — dock de décision
- nouveau : écran Reprise

---

## Ce qu'il faut construire

### 1. Des statuts qui disent *pourquoi*

Faire évoluer `ScanOutboxStatus` :

| Aujourd'hui | Demain | Signification | Sortie |
|---|---|---|---|
| `Orphaned` | `NeedsDecision` | Scanné, jamais tranché. La décision manque réellement. | Écran Reprise : Garder / Écarter / Supprimer |
| `Orphaned` | `NeedsReattach` | Décision enregistrée, session d'origine introuvable ou liée à un autre compte. | Rattachement automatique, ou une confirmation |
| `Quarantined` | `RejectedByServer` | Le serveur a refusé cinq fois. Cause métier, pas réseau. | Écran Reprise : détail, Réessayer / Abandonner |

Ajouter sur `ScanOutboxEntry` :

```ts
setAsideReason?: 'no-session' | 'other-volunteer' | 'undecided' | 'server-refused' | null;
```

Chaque site d'appel qui met un geste de côté renseigne la cause exacte. Le message affiché en découle — plus de texte générique.

### 2. Migration IndexedDB v2 → v3

`scanDatabaseVersion` passe de `2` à `3`.

Règle de conversion, à appliquer sur chaque entrée `Orphaned` existante :

- `kept === null` → `NeedsDecision`, `setAsideReason: 'undecided'`
- sinon → `NeedsReattach`, `setAsideReason: 'no-session'`

Chaque entrée `Quarantined` → `RejectedByServer`, `setAsideReason: 'server-refused'`.

**Aucune suppression. Aucune perte.** Le nombre d'entrées avant et après migration doit être identique.

> ⚠️ **Tester la migration sur le JSON exporté par L0 depuis le téléphone de production**, pas sur une base vide. Une migration IndexedDB testée à vide passe toujours. Rejouer cet export dans un test doit produire deux entrées rattachables automatiquement.

Bumper aussi la version dans `ngsw-config.json` : le service worker ne doit pas servir une ancienne coquille à un IndexedDB migré.

### 3. Rattachement automatique des `NeedsReattach`

Au démarrage de l'application et après chaque synchronisation : rattacher ces entrées à la session courante et les remettre en file d'envoi, **sans rien demander**. Leur décision est connue ; seul le rattachement manquait.

Informer discrètement, sans bloquer : « 2 livres d'une session précédente ont été rattachés à celle-ci. » C'est un message de niveau *info*, pas une erreur.

S'il n'y a pas de session courante, ne pas en créer — attendre la prochaine. Ne pas anticiper `RPR-06`, traité en L3.

### 4. L'écran Reprise

Pour les `NeedsDecision` et les `RejectedByServer`. Chaque ligne affiche : couverture, titre, ISBN, date du scan, et la cause en clair issue de `setAsideReason`.

Actions par ligne : **Garder**, **Écarter**, **Supprimer**.
Action globale : **Tout garder**.
Pour un `RejectedByServer` : afficher `lastError` en clair, avec **Réessayer** et **Abandonner**.

Chaque suppression volontaire écrit une trace applicative (Application Insights, `src/Scan/src/app/application-insights.ts`) portant `clientGestureId` et `isbn13` — jamais d'identifiant de compte.

À ce stade il n'y a pas encore de routeur (lot L4) : brancher l'écran sur le mécanisme `screen` existant, en ajoutant une valeur à `ScanScreen`. Le bandeau `setAside` livré en L1 y conduit.

### 5. `decide()` ne perd plus jamais une décision

Dans `scan-workflow.service.ts`, remplacer le court-circuit actuel :

```ts
// AVANT — la décision demandée est ignorée, l'entrée part en Orphaned
if (existing.scanSessionId !== session.scanSessionId && existing.status === 'Pending') {
  await this.store.orphanPendingOutboxEntriesFromOtherSessions(session.scanSessionId);
  return (await this.store.getOutboxEntry(clientGestureId))!;
}

// APRÈS — on rattache, puis on applique
if (existing.scanSessionId !== session.scanSessionId && existing.status === 'Pending') {
  await this.store.reattachOutboxEntry(clientGestureId, session.scanSessionId);
}
const decided = await this.store.decideOutboxEntry(clientGestureId, kept, session.mode);
```

Et dans `scanner.component.html`, le dock de décision ne doit plus déduire « décision enregistrée » d'un simple `status !== 'Pending'`. Tester explicitement `status === 'Kept' || status === 'Rejected'`, et afficher un état distinct pour tout autre statut.

---

## L'invariant à tenir

> **Pour chaque valeur de `ScanOutboxStatus`, il existe au moins un chemin utilisateur qui fait sortir l'entrée de l'outbox.**

À écrire comme test paramétré sur l'union complète des statuts. Ce test est la garantie qu'on ne recrée pas un cul-de-sac dans six mois.

---

## Ce qu'il ne faut PAS toucher

- ❌ **Ne pas revenir sur `SCAN-03`** de l'audit du 8 septembre. Le refus de transmettre les gestes du bénévole A sous le jeton du bénévole B reste acquis. On construit la sortie qui manquait, on ne rouvre pas la porte.
- ❌ **Ne pas toucher à `rebindSession()`, `mergeRemoteSession()`, `bindSessionToVolunteer()`, `isBlockingScanStatus()`, ni aux compteurs de session** — ce sont `RPR-04` à `RPR-07`, `RPR-09`, `RPR-10`, lot L3.
- ❌ **Ne pas introduire le routeur Angular** ni découper `ScannerComponent` — lot L4.
- ❌ **Ne pas modifier le backend.** Aucun fichier sous `src/Backend/`.
- ❌ **Ne pas supprimer d'entrée automatiquement.** La suppression est toujours un acte explicite du bénévole, tracé.

Si une anomalie d'un autre lot est repérée, la mentionner dans le corps de la PR — ne pas la corriger.

---

## Définition de terminé

- [ ] **Test d'invariant :** paramétré sur toutes les valeurs de `ScanOutboxStatus`, il prouve l'existence d'un chemin utilisateur sortant pour chacune.
- [ ] **Test de migration sur données réelles :** le JSON exporté par L0 depuis le téléphone de production, rejoué, produit deux entrées rattachées automatiquement et un bandeau d'arriéré qui disparaît.
- [ ] Test : la migration v2 → v3 conserve exactement le même nombre d'entrées, sur une base peuplée.
- [ ] **Test de non-régression `RPR-03` :** décider un geste dont l'identifiant de session a changé applique bien `Garder` ou `Écarter`, et ne produit jamais « Décision conservée » sur une décision non appliquée.
- [ ] Test : un `NeedsReattach` est rattaché sans intervention et repart en file d'envoi.
- [ ] Test : une suppression volontaire écrit une trace portant `clientGestureId` et `isbn13`, et aucun identifiant de compte.
- [ ] `ngsw-config.json` porte une version bumpée.
- [ ] `npm test` passe dans `src/Scan/`.
- [ ] Vérification responsive de l'écran Reprise.

---

## Workflow

- `.github/skills/delivery-workflow/SKILL.md` — worktree neuf depuis `origin/main`, branche `feat/scan-reprise-gestes`, PR vers `main`.
- `.github/skills/tdd-workflow/SKILL.md` — les tests d'abord.
- `.github/skills/angular-patterns/SKILL.md` et `.github/skills/ui-ux-front-saas/SKILL.md`.
- `graphify update .` après les modifications de code.
- Rendre l'URL de la PR. Ne pas fusionner.
