# Audit Scanette — reprise des gestes mis de côté, états de synchronisation et navigation

**Date :** 9 septembre 2026
**Périmètre :** `src/Scan/` (PWA Scanette), plus les handlers `ScanSession` de `src/Backend/` que la synchronisation consomme.
**Méthode :** lecture statique du code (`src/Scan/src/app/offline/`, `src/Scan/src/app/scanner/`, `OpenScanSessionCommandHandler`, `CloseScanSessionCommandHandler`), confrontée à trois captures d'écran de production du 9 septembre 2026, 11 h 43.
**Base analysée :** `a77ad16`.
**Destinataire :** agent chargé des correctifs.

---

## Pourquoi cet audit existe

Il fait **suite** à [`2026-09-08-audit-catalogue-scanette.md`](2026-09-08-audit-catalogue-scanette.md), et il en corrige une recommandation.

Le correctif proposé pour **SCAN-03** (« Le changement d'utilisateur ne purge pas les données locales ») se terminait ainsi :

> *« refuser la transmission d'une session dont le `volunteerId` diffère du compte connecté (**la mettre en `Orphaned`**, comme le fait déjà `orphanPendingOutboxEntriesFromOtherSessions`) »*

Cette recommandation a été appliquée. Elle est correcte sur le fond — il ne faut pas envoyer les gestes du bénévole A sous le jeton du bénévole B. **Mais elle a été appliquée sans construire la sortie correspondante.** Le statut `Orphaned` n'a aucune transition sortante : les entrées ne sont jamais transmises, jamais listées, jamais supprimées, et le compteur qui les dénombre repasse le bandeau de synchronisation en erreur à chaque passage, indéfiniment.

Résultat observé en production le 9 septembre : deux gestes bloqués sur un téléphone, un bandeau « Synchronisation à reprendre » permanent, et un message qui demande au bénévole de « prévenir un responsable » sans qu'aucun responsable ne dispose du moindre moyen d'agir.

**Cet audit ne remet pas en cause SCAN-03. Il construit la porte de sortie qui lui manquait.**

### Anomalies du 8 septembre encore ouvertes et couvertes ici

| ID d'origine | Reprise ici | Sujet |
|---|---|---|
| `SCAN-14` | `NAV-01` | Pas de retour à l'accueil depuis Tri ni depuis le choix de mode |
| `SCAN-17` | `NAV-03` | Le panneau de synchronisation est hors charte — et dupliqué quatre fois |
| `API-02` | `RPR-05` | Réutilisation d'une session `InProgress` côté serveur (partiellement corrigé : le 409 a disparu, le re-étiquetage aveugle demeure) |

---

## Comment lire ce document

Chaque anomalie porte un identifiant — `RPR-xx` pour la mécanique de reprise et de synchronisation, `NAV-xx` pour la navigation et l'UI —, une sévérité, un symptôme observable, la cause localisée dans le code, et un correctif concret.

| Sévérité | Signification |
|---|---|
| **P1** | Perte de données, impasse fonctionnelle. À traiter avant la prochaine bourse. |
| **P2** | Information fausse affichée au bénévole, ou dégradation forte de l'usage. |
| **P3** | Incohérence d'UI/UX, dette de qualité. |

**Ordre de traitement.** Six lots, séquencés en [§ Plan d'implémentation](#plan-dimplémentation). Les prompts d'exécution correspondants vivent dans [`2026-09-09-audit-scanette-reprise-gestes/`](2026-09-09-audit-scanette-reprise-gestes/).

---

## Résumé exécutif

- **P1 :** 4 — `RPR-01`, `RPR-02`, `RPR-03`, `RPR-04`
- **P2 :** 6 — `RPR-05` à `RPR-10`
- **P3 :** 10 — `NAV-01` à `NAV-10`

Une cause de fond unique traverse les quatre P1 : **la Scanette sait mettre un geste de côté, mais n'a aucun moyen de le reprendre.**

### Ce que le bénévole a vu, et ce qui se passait réellement

| Affiché | Réalité |
|---|---|
| « Catalogue synchronisé » puis « Synchronisation à reprendre » | Le même champ `syncLabel`, qui encode cinq états sans rapport. La synchro **avait réussi**. |
| « 2 gestes sans décision a été isolés » | Message faux dans deux cas sur trois : les gestes étaient très probablement **décidés**, mais rattachés à une session liée à un `homeAccountId` antérieur. |
| « sera clôturée dès que la synchronisation aboutira » | Elle n'aboutira jamais : un geste `Pending` bloque la clôture serveur de façon définitive. |
| « 6 triés » en barre de tri, « 3 scannés » en fin de session | Compteurs tenus à la main, désynchronisés après une réutilisation de session côté serveur. |

Aucune donnée n'est perdue : tout reste dans IndexedDB (`vpd-scan`, magasin `outbox`). Le seul risque réel est **la purge des données du site avant récupération** — d'où le lot L0.

---

# Partie 1 — Mécanique de reprise et de synchronisation

## P1 — Bloquants

### RPR-01 · Le compteur de gestes isolés est un total cumulé, pas un delta

**Symptôme.** Une fois qu'un geste a été mis de côté, **toute synchronisation ultérieure affiche « Synchronisation à reprendre »**, même quand le catalogue s'est mis à jour et que tous les gestes en file sont partis. Appuyer sur « Synchroniser » ne fera jamais repasser le bandeau au vert. Le bénévole apprend en une bourse à ignorer un bandeau rouge permanent — et n'ira donc pas le lire le jour où il signale un vrai problème.

**Cause.**

- `src/Scan/src/app/offline/scan-sync.service.ts` — `createOutboxSummary()` renseigne `orphaned` avec `countOrphanedOutboxEntries()`, qui compte **toutes** les entrées `Orphaned` de la base, pas celles isolées pendant la synchronisation en cours. Idem pour `quarantined` avec `countQuarantinedOutboxEntries()`.
- `src/Scan/src/app/scanner/scanner.component.ts` — `performSync()` teste `orphaned > 0` puis `quarantined > 0` et bascule `syncStatus = 'error'` dans les deux cas.
- Aucune de ces entrées n'apparaît dans `pendingDecisionCount` ni `pendingTransmissionCount` (`getOutboxCounts()` ne retient que `Pending`, `Kept`, `Rejected`). Le panneau affiche donc **une erreur sans aucun compteur associé** : rien à faire, rien à comprendre.

**Correctif.**

1. Ajouter à `OutboxSyncSummary` deux champs de **delta**, alimentés par les compteurs locaux déjà présents dans `flushOutboxInternal()` :

   ```ts
   export interface OutboxSyncSummary {
     sent: number;
     remaining: number;
     stoppedOnError: boolean;
     quarantined?: number;      // total, pour la barre « à régler »
     orphaned?: number;         // total, pour la barre « à régler »
     newlyQuarantined: number;  // delta de CETTE synchronisation
     newlyOrphaned: number;     // delta de CETTE synchronisation
   }
   ```

2. Dans `performSync()`, ne plus dériver `syncStatus` des totaux. Un total non nul alimente la barre « à régler » (`RPR-02`), jamais l'état de synchronisation.
3. Un delta non nul déclenche un message ponctuel — « 2 livres viennent d'être mis de côté » — et non un état persistant.

---

### RPR-02 · Les états `Orphaned` et `Quarantined` n'ont aucune transition sortante

**Symptôme.** Un geste mis de côté y reste pour toujours. Il n'est jamais transmis, n'apparaît dans aucune vue, ne peut pas être supprimé, et n'a jamais quitté le téléphone — donc aucune route BackOffice ne l'atteint. Le message « Prévenez un responsable avant toute publication » désigne une action que **personne ne peut effectuer**, ni le bénévole, ni le responsable, ni l'administrateur.

**Cause.**

- `src/Scan/src/app/offline/scan-local-store.service.ts` — `listTransmittableOutboxEntries()` ne retient que `Kept` et `Rejected`. `Orphaned` et `Quarantined` sont exclus de l'envoi par construction.
- Aucun composant ne lit ces entrées : `scanner.component.html` n'affiche jamais de liste d'outbox.
- Le seul nettoyeur est `clearAccountState()`, qui efface **tout** sans distinction — et `scanner.component.ts` `logout()` refuse de l'appeler tant que `pendingDecisionCount + pendingTransmissionCount > 0` (correctif SCAN-03, correctement appliqué). Un geste `Pending` résiduel verrouille donc simultanément la clôture de session, le changement d'utilisateur et le nettoyage.

**Correctif.** Remplacer un statut fourre-tout par deux statuts qui disent *pourquoi*, et donner une sortie à chacun.

1. Faire évoluer `ScanOutboxStatus` dans `scan-offline.model.ts` :

   | Aujourd'hui | Demain | Signification | Sortie |
   |---|---|---|---|
   | `Orphaned` | `NeedsDecision` | Scanné, jamais tranché. La décision manque réellement. | Écran Reprise : Garder / Écarter / Supprimer |
   | `Orphaned` | `NeedsReattach` | Décision enregistrée, session d'origine introuvable ou liée à un autre compte. | Rattachement automatique, ou une confirmation |
   | `Quarantined` | `RejectedByServer` | Le serveur a refusé cinq fois. Cause métier, pas réseau. | Écran Reprise : détail, Réessayer / Abandonner |

2. Ajouter `setAsideReason: 'no-session' | 'other-volunteer' | 'undecided' | 'server-refused'` sur `ScanOutboxEntry`, pour afficher une cause exacte plutôt qu'un message générique.
3. Migration IndexedDB `scanDatabaseVersion` 2 → 3 : chaque `Orphaned` existant devient `NeedsDecision` si `kept === null`, `NeedsReattach` sinon. **Aucune suppression.** La migration doit être testée sur une base réellement peuplée (voir lot L0), pas à vide.
4. Créer l'écran `/reprise` : couverture, titre, ISBN, date du scan, cause. Actions par ligne — Garder, Écarter, Supprimer — et action globale « Tout garder ».
5. Rattacher automatiquement les `NeedsReattach` à la session courante au démarrage et après chaque synchronisation, puis informer discrètement : « 2 livres d'une session précédente ont été rattachés à celle-ci. »

**Invariant à écrire en test.** Pour chaque valeur de `ScanOutboxStatus`, il existe au moins un chemin utilisateur qui fait sortir l'entrée de l'outbox.

---

### RPR-03 · Une décision explicite du bénévole peut être jetée en silence

**Symptôme.** Le bénévole appuie sur « Garder ». L'interface affiche « Décision conservée · scannez le suivant ». **La décision n'a pas été appliquée** et le geste part en `Orphaned`. C'est le défaut le plus grave de cet audit : la donnée est perdue *et* l'application affirme le contraire.

**Cause.**

- `src/Scan/src/app/offline/scan-workflow.service.ts` — dans `decide()` :

  ```ts
  if (existing.scanSessionId !== session.scanSessionId && existing.status === 'Pending') {
    await this.store.orphanPendingOutboxEntriesFromOtherSessions(session.scanSessionId);
    return (await this.store.getOutboxEntry(clientGestureId))!;
  }
  ```

  L'entrée est renvoyée sans que `kept` ait été renseigné.
- `src/Scan/src/app/scanner/scanner.component.html` — le dock de décision teste `localScan.entry.status === 'Pending'`. Le statut valant désormais `Orphaned`, le gabarit bascule sur `<ng-template #savedDecision>` et affiche « Décision conservée ».

La divergence d'identifiant de session survient notamment quand une synchronisation d'arrière-plan — `trySync()` s'exécute toutes les 60 s et après chaque scan — a re-étiqueté les entrées entre l'affichage du verdict et l'appui sur le bouton (`RPR-05`).

**Correctif.**

1. `decide()` ne doit **jamais** perdre une décision. Si la session a changé, rattacher l'entrée à la session courante, **puis** appliquer la décision :

   ```ts
   if (existing.scanSessionId !== session.scanSessionId && existing.status === 'Pending') {
     await this.store.reattachOutboxEntry(clientGestureId, session.scanSessionId);
   }
   const decided = await this.store.decideOutboxEntry(clientGestureId, kept, session.mode);
   ```

2. Le gabarit ne doit pas déduire « décision enregistrée » d'un simple `status !== 'Pending'`. Tester explicitement `status === 'Kept' || status === 'Rejected'`, et afficher un état distinct pour tout autre statut.

---

### RPR-04 · Une session portant un geste non décidé ne peut jamais être clôturée

**Symptôme.** Le message « La session reste enregistrée localement et sera clôturée dès que la synchronisation aboutira » promet une résolution automatique **structurellement impossible**. La demande de clôture reste en base indéfiniment, la session demeure `InProgress` côté serveur, et les livres ne sont jamais publiés.

**Cause.**

- `src/Scan/src/app/offline/scan-sync.service.ts` — `closeRequestedSessions()` passe son tour tant que `countBlockingOutboxEntriesForSession(session.scanSessionId) > 0`.
- `src/Scan/src/app/offline/scan-local-store.service.ts` — `isBlockingScanStatus()` retourne `true` pour `Pending`, `Kept` et `Rejected`.

Un geste jamais tranché est donc bloquant pour toujours, puisque rien ne peut plus le trancher une fois la session sortie du parcours. Effet en cascade : la session restant `InProgress`, elle déclenche `RPR-05` à la prochaine ouverture.

**Correctif.**

1. Retirer `Pending` de `isBlockingScanStatus()` **pour le seul contrôle de clôture**. Un geste non décidé au moment de la clôture part en `NeedsDecision` et cesse de bloquer la session.
2. Conserver `Kept` et `Rejected` comme bloquants : une décision prise doit partir avant la clôture, c'est le comportement correct.
3. Réécrire le message : il ne doit promettre que ce que le code peut tenir (voir [recueil de textes](#recueil-de-textes)).

---

## P2 — Information fausse ou dégradation forte

### RPR-05 · Le client re-étiquette aveuglément ses gestes sur l'identifiant renvoyé par le serveur

**Symptôme.** Les compteurs divergent : « 6 triés » dans la barre de session, « 3 scannés / 2 gardés / 1 écarté » sur l'écran de fin. Le résumé de fin de session — celui qui décide de publier des livres — n'est pas fiable.

**Cause.** Le correctif `API-02` du 8 septembre a supprimé le 409, mais introduit une réutilisation silencieuse :

- `OpenScanSessionCommandHandler` renvoie `existingSession` lorsqu'une session `InProgress` du même bénévole, même mode et même `TargetAssoEventsId` existe déjà — avec **son identifiant à elle**, différent du `clientSessionId` demandé.
- `scan-workflow.service.ts` — `mergeRemoteSession()` détecte l'écart et appelle `rebindSession(current.scanSessionId, remoteSession.scanSessionId)`, qui renomme **toutes** les entrées outbox portant l'ancien identifiant.
- Deux sessions locales distinctes peuvent ainsi fusionner sur une seule session serveur. Les compteurs sont recombinés par `Math.max(current.x, remoteSession.x)`, ce qui produit un état qui ne correspond ni à l'une ni à l'autre.

**Correctif.**

1. Séparer les deux identités sur `ScanSessionSnapshot` : `clientSessionId` — immuable, généré localement, clé de regroupement de l'outbox — et `remoteSessionId` — assigné par le serveur, utilisé uniquement pour les appels API. Supprimer `rebindSession()`.
2. Côté serveur, ajouter `reusedExistingSession: bool` à `ScanSessionResponse` : le client doit savoir qu'il a reçu autre chose que ce qu'il demandait, au lieu de le déduire d'une comparaison d'identifiants.

---

### RPR-06 · La synchronisation peut ressusciter une session

**Symptôme.** Le bénévole se retrouve « en session » sans l'avoir demandé, avec des compteurs à zéro.

**Cause.** `scan-workflow.service.ts` — `mergeRemoteSession()` appelle `ensureSession(new Date(remoteSession.startedAt))`, et `ensureSession()` **crée** une session active quand aucune n'existe. Appelé après le `clearSession()` d'une clôture réussie, lors d'un flush portant sur plusieurs sessions, il fabrique une session active vierge.

**Correctif.** `mergeRemoteSession()` ne doit jamais créer de session : sortir si `getSession()` retourne `null`.

---

### RPR-07 · Les compteurs de session sont tenus à la main, pas dérivés de l'outbox

**Symptôme.** `scannedCount`, `keptCount` et `rejectedCount` dérivent de la réalité dès qu'un geste est mis de côté, qu'une session fusionne (`RPR-05`) ou qu'un `Math.max()` passe.

**Cause.** `scan-workflow.service.ts` incrémente ces champs à cinq endroits distincts — `recordScan()` (deux fois : boucle d'auto-validation et scan courant), `decide()`, `mergeRemoteSession()`, `ensureSession()` — sans source unique de vérité.

**Correctif.** Les remplacer par des fonctions dérivées de l'outbox filtrée sur `clientSessionId`. Le snapshot ne conserve que ce que l'outbox ne porte pas : `startedAt`, `lastScanAt`, `lastSyncAt`, `mode`.

---

### RPR-08 · Un seul enum encode cinq états orthogonaux

**Symptôme.** « Catalogue synchronisé » et « Synchronisation à reprendre » sont la même variable. Le bénévole ne peut pas savoir laquelle des cinq questions le bandeau est en train de répondre.

**Cause.** `scanner.component.ts` — `syncStatus: 'idle' | 'syncing' | 'success' | 'error'` et `get syncLabel()` portent simultanément : fraîcheur du catalogue, état de la file d'envoi, clôture de session, arriéré de gestes isolés, arriéré de quarantaine. Ces cinq axes sont réellement indépendants ; un enum à quatre valeurs ne peut pas les représenter.

**Correctif.** Introduire `ScanStatusSnapshot`, à quatre axes lus indépendamment :

```ts
export interface ScanStatusSnapshot {
  catalog:  {state: 'fresh' | 'stale' | 'never'; syncedAt: string | null};
  outbox:   {state: 'idle' | 'sending' | 'retrying'; queued: number};
  decision: {pending: number};          // session courante uniquement
  setAside: {total: number};            // arriéré à régler, action requise
}
```

Un axe, un état, un endroit unique dans l'interface. Supprimer `syncLabel()` et l'enum.

---

### RPR-09 · Le compte n'est jamais relié à une session existante

**Symptôme.** Après un renouvellement de `homeAccountId` — nouvelle connexion, cache MSAL régénéré, second compte sur l'appareil — toute session locale antérieure devient définitivement « celle d'un autre bénévole », et l'intégralité de ses gestes est mise de côté d'un bloc, **y compris ceux qui étaient décidés**. C'est le scénario le plus probable derrière les deux gestes observés le 9 septembre.

**Cause.** `scan-workflow.service.ts` — `bindSessionToVolunteer()` sort immédiatement si `session.volunteerId !== null`. Il lie, mais ne **re**lie jamais. `scan-sync.service.ts` — `belongsToDifferentAccount()` compare alors deux identifiants qui ne se rejoindront plus, et `orphanBlockingOutboxEntriesForSession()` met de côté `Pending`, `Kept` et `Rejected` sans distinction.

**Correctif.**

1. Détecter le changement de compte **au login**, pas pendant la synchronisation.
2. Ne jamais trancher en silence. Demander : « Des gestes d'une session précédente sont présents sur cet appareil. Les reprendre sous votre compte, ou les mettre de côté pour un responsable ? »
3. Le refus de transmission sous un mauvais jeton — l'objet réel de `SCAN-03` — reste intégralement en place : le rattachement est un acte explicite du bénévole connecté, jamais un effet de bord de la synchronisation.

---

### RPR-10 · Les compteurs « à décider » et « à transmettre » ne sont pas limités à la session courante

**Symptôme.** « 1 décision à prendre » peut désigner un geste d'une **autre** session, inatteignable depuis l'écran de tri. `endSession()` refuse alors de terminer, `logout()` refuse de changer d'utilisateur, et l'écran de tri n'offre aucun retour à l'accueil (`NAV-01`). Le bénévole est enfermé sans action possible.

**Cause.** `scan-local-store.service.ts` — `getOutboxCounts()` agrège l'outbox entière sans filtrer sur `scanSessionId`.

**Correctif.** `getOutboxCounts(clientSessionId)` devient obligatoirement paramétré. L'arriéré global — l'axe `setAside` de `RPR-08` — reste compté séparément, et n'entre pas dans les gardes de `endSession()` ni de `logout()`.

---

# Partie 2 — Navigation et UI

`ScannerComponent` compte 1 348 lignes de TypeScript et 637 lignes de gabarit pour sept écrans pilotés par un champ `screen: ScanScreen`. Il n'y a pas de routeur : donc pas d'URL, pas d'historique, pas de retour navigateur, pas de reprise après rechargement. Chaque écran a réinventé sa barre, son dos et ses conteneurs d'erreur.

| ID | Sév. | Anomalie |
|---|---|---|
| `NAV-01` | P2 | **Aucun retour à l'accueil depuis Tri ni depuis le choix de mode.** Reprise de `SCAN-14`, non corrigé. `cash` et `consultation` portent un `dock-home-link` ; `tri` et `session-mode` n'ont rien. La seule sortie du tri est « Terminer », qui publie les livres et arme le délai d'alerte. Combiné à `RPR-10`, c'est un blocage dur. |
| `NAV-02` | P3 | **Cinq idiomes de navigation coexistent.** Cartes plein écran sur l'accueil ; « Revenir au scan » en lien de dock sur la saisie manuelle ; « Accueil » en lien tertiaire de dock sur caisse et consultation ; pilule pleine largeur sur la fin de session ; rien sur le tri. Aucune position, aucun libellé, aucun niveau hiérarchique constants. |
| `NAV-03` | P3 | **Le panneau de synchronisation est dupliqué à l'identique quatre fois** dans `scanner.component.html` (écrans `tri`, `manual`, `cash`, `consultation`), à une position verticale différente selon l'écran. Toute correction de message doit être faite quatre fois. Recoupe `SCAN-17`. |
| `NAV-04` | P2 | **Pas de routeur.** Impossible d'envoyer un lien, de revenir en arrière au geste système, ou de retrouver son écran après un rechargement. Sur iOS, le geste de retour quitte l'application. Le routeur apporterait aussi les gardes manquantes : rôle `Tri`, session ouverte, arriéré à régler. |
| `NAV-05` | P3 | **Deux `window.confirm()` natifs** — `endSession()` et `returnHome()` dans `scanner.component.ts`. Non stylables, hors charte, bloquants, incohérents avec tout le reste des messages. C'est le support le plus faible possible pour la décision la plus lourde de l'application. |
| `NAV-06` | P3 | **Cinq conteneurs d'erreur, aucune échelle de gravité.** `syncError` vit dans le panneau de synchro sur quatre écrans mais dans un `<p class="message-error">` autonome sur la fin de session ; `storageError` change de classe selon l'écran (`scan-feedback`, `cash-storage-error`, `consultation-storage-error`) ; `logoutError` n'existe que sur l'accueil. Rien ne distingue « information » de « action requise » de « blocage ». |
| `NAV-07` | P3 | **La règle la plus lourde de l'application est écrite en petit dans un dock.** « Scanner le suivant vaut « garder » » est un modèle de validation implicite qui engage du stock réel, annoncé par une ligne de texte secondaire. |
| `NAV-08` | P3 | **Changer de mode oblige à publier.** Le mode est verrouillé pour la session et la seule façon d'en changer est « Terminer ». Un bénévole qui se trompe de mode au deuxième livre n'a aucune issue propre. |
| `NAV-09` | P3 | **La caméra se comporte différemment sur trois écrans.** `startCameraIfNeeded(force)` est appelé en `force` depuis caisse et consultation, sous condition depuis le tri ; `toggleCamera()` est offert sur caisse et consultation, absent du tri. Même matériel, même geste, trois comportements. |
| `NAV-10` | P3 | **La barre de session perd son bouton « Terminer » sur la saisie manuelle.** Même composant visuel, capacités différentes selon l'écran. |

### Cible de navigation

```
/                       ScanShellComponent — en-tête, barre d'état, dos, feuilles
├── /accueil            Trier · Caisse · Consulter
├── /reprise            NOUVEAU — gestes à reprendre
├── /tri/mode           choix du mode
├── /tri                scan et décision
├── /tri/fin            résumé de session
├── /caisse
└── /consulter

Feuilles modales sur la route courante (pas de remplacement d'écran) :
  · Saisie manuelle   — depuis /tri, /caisse, /consulter
  · Confirmations     — remplace window.confirm
```

Trois règles qui règlent `NAV-01`, `NAV-02`, `NAV-06`, `NAV-09` et `NAV-10` ensemble :

1. **Un seul dos, toujours au même endroit** — en haut à gauche de l'en-tête, sur tous les écrans sauf l'accueil. Le dock du bas ne porte plus jamais de navigation, uniquement l'action principale de l'écran.
2. **Une seule barre d'état** — un composant `<scan-status-bar>` dans la coquille, jamais dans les écrans.
3. **Trois niveaux de message et rien d'autre** — *info* (discret, se dissipe), *action requise* (persistant, porte son bouton), *blocage* (barre l'écran, explique la sortie).

---

## Recueil de textes

Les messages actuels décrivent l'implémentation — « geste », « isolé », « session », « quarantaine ». Un bénévole ne connaît que des livres et des décisions.

| Situation | Aujourd'hui | À écrire |
|---|---|---|
| Synchro réussie | Catalogue synchronisé | Catalogue à jour · 09/09 à 11 h 41 |
| File d'envoi | 3 gestes à transmettre | 3 livres en attente d'envoi · départ automatique |
| Hors réseau | Hors ligne · les données restent locales | Sans réseau · vos scans sont conservés sur le téléphone |
| Décision attendue | 1 décision à prendre | Choisissez Garder ou Écarter pour ce livre |
| Gestes rattachés seuls | *(n'existe pas)* | 2 livres d'une session précédente ont été rattachés à celle-ci |
| Reprise nécessaire | 2 gestes sans décision a été isolés avec la session précédente. Prévenez un responsable avant toute publication. | 2 livres attendent une décision · **Les reprendre** |
| Refus serveur répété | 1 entrée a été mise en quarantaine après plusieurs refus serveur. Prévenez un responsable. | Le serveur refuse 1 livre · **Voir le détail** |
| Session en attente de clôture | La session reste enregistrée localement et sera clôturée dès que la synchronisation aboutira. | Session enregistrée · les livres seront publiés au prochain passage en réseau |
| Échec réseau | La synchronisation a échoué ; les gestes restent conservés localement. | Pas de réseau · nouvelle tentative automatique, rien n'est perdu |
| Changement d'utilisateur bloqué | 3 gestes restent à traiter ou à transmettre. Synchronisez avant de changer d'utilisateur. | 3 livres ne sont pas encore envoyés · **Envoyer maintenant** puis changer de compte |
| Stockage non garanti | Le navigateur n'a pas garanti la conservation des données hors ligne. Gardez l'application régulièrement connectée. | Ce navigateur peut effacer les données hors ligne · synchronisez après chaque session |

**Trois règles pour toute nouvelle chaîne.**

1. Le mot « geste » ne sort jamais à l'écran — on dit « livre ».
2. Tout message d'action requise porte son bouton d'action, sur la même ligne.
3. Aucun message ne demande de « prévenir un responsable » sans dire pourquoi lui plutôt que le bénévole — sinon c'est une impasse déguisée en consigne.

Corriger au passage l'accord fautif : « 2 gestes sans décision **a** été **isolés** ». La pluralisation est faite à la main par des ternaires dans `scanner.component.ts` ; la centraliser.

---

## Plan d'implémentation

| Lot | Objet | Corrige | Effort | Prompt |
|---|---|---|---|---|
| **L0** | Filet de sécurité — écran de diagnostic et export du journal local | *(prérequis)* | ≈ 1 j | [`L0-filet-de-securite.md`](2026-09-09-audit-scanette-reprise-gestes/L0-filet-de-securite.md) |
| **L1** | Séparer les états de synchronisation et dire la vérité | `RPR-01`, `RPR-08`, `NAV-03` | ≈ 2–3 j | [`L1-separer-les-etats.md`](2026-09-09-audit-scanette-reprise-gestes/L1-separer-les-etats.md) |
| **L2** | Écran Reprise — la fonctionnalité manquante | `RPR-02`, `RPR-03` | ≈ 3–4 j | [`L2-ecran-reprise.md`](2026-09-09-audit-scanette-reprise-gestes/L2-ecran-reprise.md) |
| **L3** | Fiabiliser la session | `RPR-04` à `RPR-07`, `RPR-09`, `RPR-10` | ≈ 3–4 j | [`L3-fiabiliser-la-session.md`](2026-09-09-audit-scanette-reprise-gestes/L3-fiabiliser-la-session.md) |
| **L4** | Navigation unifiée | `NAV-01`, `NAV-02`, `NAV-04` à `NAV-06`, `NAV-09`, `NAV-10` | ≈ 4–5 j | [`L4-navigation-unifiee.md`](2026-09-09-audit-scanette-reprise-gestes/L4-navigation-unifiee.md) |
| **L5** | Filet côté responsable | conséquences serveur de `RPR-04` | ≈ 2 j | [`L5-filet-responsable.md`](2026-09-09-audit-scanette-reprise-gestes/L5-filet-responsable.md) |

**Ordre et parallélisation.** L0 d'abord, seul. L1 et L5 peuvent partir en parallèle — fronts différents. L2 dépend de L1. L3 dépend de L2 pour la migration de statuts. L4 dépend de L1 pour `<scan-status-bar>` mais est indépendant de L2 et L3.

**Pourquoi L0 en premier.** Tant que le contenu de l'outbox d'un téléphone de production n'a pas été extrait, chaque déploiement risque d'écraser la seule preuve de ce qui s'est réellement produit. Et la migration IndexedDB de L2 doit être validée sur cet export réel : une migration testée à vide passe toujours.

---

## Recette

Chaque scénario correspond à une anomalie. Tous sont jouables sur un téléphone réel avec le mode avion — c'est la seule recette qui compte pour une application hors ligne.

| # | Scénario | Attendu | Couvre |
|---|---|---|---|
| `R1` | Scanner 3 livres hors réseau, décider, rétablir le réseau, synchroniser deux fois de suite. | Deux synchros au vert. Aucun message d'erreur à la seconde. | `RPR-01` |
| `R2` | Scanner 2 livres, décider, fermer l'onglet sans terminer. Se déconnecter, se reconnecter, rouvrir. | Les 2 livres sont proposés au rattachement, la synchro passe au vert une fois traités. | `RPR-02`, `RPR-09` |
| `R3` | Scanner 1 livre, laisser la décision en attente, forcer la fermeture de l'app, rouvrir. | Bandeau « 1 livre attend une décision », écran de reprise, décision applicable, envoi effectif. | `RPR-02`, `RPR-04` |
| `R4` | Provoquer un changement d'identifiant de session pendant qu'une décision est affichée, puis appuyer sur Garder. | Le livre est bien gardé. Jamais « Décision conservée » sur une décision non appliquée. | `RPR-03` |
| `R5` | Laisser une session `InProgress` côté serveur, ouvrir une nouvelle session locale, synchroniser. | Aucun renommage d'entrées. Barre de tri et écran de fin affichent les mêmes chiffres. | `RPR-05`, `RPR-07` |
| `R6` | Terminer une session pendant que le réseau tombe, puis le rétablir. | La clôture aboutit. Aucune session active n'est recréée toute seule. | `RPR-06` |
| `R7` | Depuis chaque écran, appuyer sur le retour navigateur et sur le geste système iOS. | On remonte d'un écran dans l'app. On ne quitte jamais l'app par accident. | `NAV-01`, `NAV-02`, `NAV-04` |
| `R8` | Recharger la page au milieu d'une session de tri. | Retour sur la même route, session intacte, décision en cours préservée. | `NAV-04` |
| `R9` | Ouvrir la saisie manuelle depuis le tri, la caisse et la consultation. | Feuille modale, contexte conservé, caméra reprise à la fermeture, même dos partout. | `NAV-02`, `NAV-09` |

### À vérifier avant tout déploiement

- La migration IndexedDB v2 → v3 ne perd aucune entrée sur une base réellement peuplée — la tester sur un export L0 réel, pas sur une base vide.
- Le service worker ne doit pas servir une ancienne coquille à un IndexedDB migré : bump de version explicite dans `ngsw-config.json`.
- Sur iOS Safari, les données de site peuvent être effacées après sept jours sans usage. Le message de L1 sur le stockage non garanti doit rester visible et exact.
