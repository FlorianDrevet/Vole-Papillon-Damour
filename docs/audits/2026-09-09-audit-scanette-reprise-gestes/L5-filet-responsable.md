# L5 — Filet côté responsable : voir et débloquer sans toucher au téléphone

**Audit de référence :** [`../2026-09-09-audit-scanette-reprise-gestes.md`](../2026-09-09-audit-scanette-reprise-gestes.md)
**Anomalies visées :** conséquences serveur de `RPR-04`, plus l'observabilité qui manque pour vérifier L3
**Effort estimé :** ≈ 2 j
**Prérequis :** aucun. **Peut partir en parallèle de L1** — c'est un autre front.

---

## Contexte

Deux manques distincts, tous deux côté responsable.

**Premier manque — les sessions gelées.** Une session portant un geste `Pending` ne peut jamais être clôturée depuis la Scanette (`RPR-04`). Elle reste `InProgress` côté serveur indéfiniment. `CloseIdleScanSessionsCommand` la libère après `sessionIdleTimeoutMinutes`, mais uniquement dans les cas qu'il couvre — et aucun responsable ne dispose aujourd'hui d'une vue sur ces sessions ni d'un moyen d'agir. Le message « Prévenez un responsable » désigne une personne qui n'a aucun outil.

**Second manque — l'observabilité.** Rien ne mesure combien de gestes sont mis de côté, ni pourquoi. Sans cette mesure, on ne saura pas si L3 a réellement tari la source, ni si le problème réapparaît après une bourse. On aura corrigé à l'aveugle.

---

## Périmètre

- `src/BackOffice/src/app/` — nouvelle vue d'administration
- `src/Backend/Vole_Papillon_Damour.Application/Books/Queries/Admin/` — `GetAdminScanSessionsQuery` existe déjà, l'étendre si besoin
- `src/Backend/Vole_Papillon_Damour.Application/Books/Commands/ScanSession/CloseScanSessionCommand.cs` — réutiliser tel quel
- `src/Scan/src/app/application-insights.ts` — émission de l'événement
- `src/Backend/Vole_Papillon_Damour.Application/Common/Observability/BookScanTelemetry.cs` — conventions de télémétrie existantes

Lire ces fichiers avant d'écrire : l'essentiel du travail est de la composition, pas de la création.

---

## Ce qu'il faut construire

### 1. Vue « Sessions de tri ouvertes » dans le BackOffice

Liste des sessions `InProgress` ouvertes depuis plus de 24 h, avec :

| Colonne | Source |
|---|---|
| Bénévole | `VolunteerId` résolu en nom affichable |
| Mode | `AvailableNow` / `NextFair` |
| Ouverte depuis | `StartedAt`, en heures |
| Dernier scan | `LastScanAt` |
| Mouvements | nombre de livres déjà enregistrés |

S'appuyer sur `GetAdminScanSessionsQuery`, déjà présent. Ne créer un nouveau handler que si le filtre « ouvertes depuis plus de 24 h » ne peut pas être exprimé avec l'existant — et le dire dans la PR.

Le seuil de 24 h est une **constante nommée et documentée**, pas un littéral disséminé.

### 2. Action « Clôturer d'office »

Réutilise `CloseScanSessionCommand`. Ajouter une valeur dédiée à `ScanCloseReason` — par exemple `AdminForced` — pour distinguer cette clôture de `Manual`, `Inactivity`, `Disconnect` et `TokenExpired` dans le ledger.

⚠️ La clôture déclenche `IBookAlertOutbox.QueueForSessionAsync` — donc l'envoi différé des alertes aux personnes qui recherchaient ces livres. **La confirmation doit le dire explicitement**, avec le nombre de livres concernés et le délai avant envoi (`alertDelayMinutes`, 120 min par défaut). Un responsable ne doit pas découvrir après coup qu'il a déclenché des e-mails.

L'action est réservée aux rôles d'administration déjà en place dans le BackOffice. Ne pas inventer un nouveau rôle.

### 3. Télémétrie des mises de côté

Émettre un événement Application Insights à **chaque** mise de côté d'un geste, depuis `src/Scan/` :

```
nom : scan_gesture_set_aside
propriétés :
  setAsideReason  : 'no-session' | 'other-volunteer' | 'undecided' | 'server-refused'
  isbn13          : ISBN du livre
  clientGestureId : identifiant du geste
```

**Aucun identifiant de compte.** Ni `volunteerId`, ni `homeAccountId`, ni adresse. La cause et le livre suffisent au diagnostic.

Si L2 n'est pas encore livré, `setAsideReason` n'existe pas encore : émettre l'événement avec la fonction d'origine (`orphanPendingOutboxEntriesFromOtherSessions`, `orphanOutboxEntry`, `orphanBlockingOutboxEntriesForSession`, `quarantineOutboxEntry`) comme valeur, et documenter la correspondance dans la PR pour que L2 la reprenne.

### 4. Tableau de bord minimal

Nombre de mises de côté par jour et par cause. Une requête Application Insights sauvegardée suffit — pas besoin d'une page dédiée. La documenter dans `docs/bourse-aux-livres/technique/11-observabilite.md`.

**C'est la mesure qui dira si L3 a fonctionné.** Sans elle, les lots suivants sont livrés à l'aveugle.

---

## Ce qu'il ne faut PAS toucher

- ❌ **Ne pas modifier `ScanOutboxStatus` ni la migration IndexedDB** — lot L2.
- ❌ **Ne pas modifier `isBlockingScanStatus()`, `rebindSession()`, `mergeRemoteSession()` ni les compteurs de session** — lot L3.
- ❌ **Ne pas modifier `CloseIdleScanSessionsCommand`** ni `sessionIdleTimeoutMinutes`. La clôture automatique existante reste en place ; on ajoute une clôture manuelle, on ne remplace pas l'autre.
- ❌ **Ne pas modifier `alertDelayMinutes`** ni le mécanisme d'alertes. On l'expose dans la confirmation, on ne le change pas.
- ❌ **Ne pas toucher à la navigation de la Scanette** — lot L4.
- ❌ **Ne pas construire de vue sur les gestes mis de côté.** Ils n'ont jamais quitté le téléphone : aucune donnée serveur ne les porte. C'est précisément ce que L0 et L2 traitent.

Si une anomalie d'un autre lot est repérée, la mentionner dans le corps de la PR — ne pas la corriger.

---

## Définition de terminé

- [ ] Une session `InProgress` ouverte depuis plus de 24 h apparaît dans la vue BackOffice.
- [ ] Elle peut être clôturée d'office, avec un motif distinct dans le ledger.
- [ ] La confirmation de clôture annonce explicitement le nombre de livres concernés et le délai avant envoi des alertes.
- [ ] Test : l'action est refusée à un compte sans rôle d'administration.
- [ ] Test : l'événement `scan_gesture_set_aside` est émis sur chacun des chemins de mise de côté.
- [ ] Test : l'événement ne contient **aucun** identifiant de compte.
- [ ] La requête de tableau de bord est documentée dans `docs/bourse-aux-livres/technique/11-observabilite.md`.
- [ ] `dotnet test .\src\Backend\Vole_Papillon_Damour.slnx` passe.
- [ ] `npm test` passe dans `src/BackOffice/` et `src/Scan/`.
- [ ] Vérification responsive de la vue BackOffice.

---

## Workflow

- `.github/skills/delivery-workflow/SKILL.md` — worktree neuf depuis `origin/main`, branche `feat/admin-scan-sessions`, PR vers `main`.
- `.github/skills/tdd-workflow/SKILL.md` — les tests d'abord.
- `.github/skills/cqrs-feature/SKILL.md`, `.github/skills/dotnet-patterns/SKILL.md`, `.github/skills/xunit-unit-testing/SKILL.md` pour le backend.
- `.github/skills/angular-patterns/SKILL.md` et `.github/skills/ui-ux-front-saas/SKILL.md` pour le BackOffice.
- `graphify update .` après les modifications de code.
- Rendre l'URL de la PR. Ne pas fusionner.

## Critère de succès à moyen terme

Deux semaines après la mise en production de L3, le tableau de bord doit montrer une **courbe de mises de côté décroissante**, et les causes restantes doivent être identifiables. Si la courbe reste plate, L3 n'a pas tari la source et il faut rouvrir l'analyse.
