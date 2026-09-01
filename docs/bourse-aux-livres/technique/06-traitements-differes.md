# 06 — Traitements différés

## 1. Où vivent ces traitements

**Dans l'API**, sous forme de services hébergés (`BackgroundService`). Pas
d'application dédiée, pas de Functions.

C'est `DT-09`, qui remplace `DT-04`. Le raisonnement initial partait de
`minReplicas: 0` : un service hébergé dans un conteneur susceptible d'être éteint ne
s'exécuterait qu'au hasard du trafic, et **les alertes de `RG-44` comme la bascule de
`RG-23` ne partiraient pas** — en silence.

**L'API passe à `minReplicas: 1`**, parce qu'elle ne peut pas être indisponible pour le
site web. La prémisse tombe, et avec elle le besoin d'un composant séparé. Gain
collatéral : la question du réveil d'une application à zéro réplica par un déclencheur
planifié — l'une des deux mesures bloquantes — **disparaît entièrement**.

## 2. Comment c'est câblé

Les services hébergés vivent dans le projet API mais s'appuient sur les mêmes
bibliothèques `Application`, `Domain` et `Infrastructure` que les contrôleurs. Aucune
logique métier ne leur est propre.

```csharp
await sender.Send(new ReleaseDueAnnouncementsCommand(), ct);   // RG-23
await sender.Send(new CloseIdleSessionsCommand(), ct);         // RG-43
```

`CloseScanSession` est **le même handler** que celui déclenché quand un bénévole appuie
sur `TERMINER`. C'est voulu : `RG-43` définit quatre causes de clôture, et il serait
absurde que la clôture manuelle et la clôture par inactivité suivent deux chemins de
code distincts — ils divergeraient au premier correctif.

### Deux répliques peuvent balayer en même temps

`maxReplicas: 2` : rien ne garantit qu'un seul processus exécute un balayage donné.

C'est acceptable **parce que toutes les opérations sont déjà en réclamation
conditionnelle** (§5) : relève d'outbox par `ClaimedUntil`, bascule filtrée sur
`Status = Announced`, clôture filtrée sur `Status = EnCours`. Aucune n'est doublonnable.

Pour la lisibilité d'exploitation plutôt que par nécessité, une **ligne de bail en
base** peut réserver l'exécution à une seule réplique : une vingtaine de lignes, et des
journaux qui ne racontent qu'une histoire à la fois.

### Trois contraintes de conception

**Aucun handler déclenché par un balayage ne dépend d'un utilisateur ambiant.** Pas
d'`IHttpContextAccessor`, pas d'« utilisateur courant » tiré du JWT : un balayage n'a
pas d'utilisateur. L'acteur se passe explicitement en paramètre, ou vaut « système » —
ce qui doit apparaître dans le `VolunteerId` des mouvements (`RG-41`).

**Le piège est plus vicieux depuis que le code partage le processus de l'API** : un
accès à `HttpContext` compile, passe les tests manuels lancés depuis une requête, puis
renvoie `null` à trois heures du matin.

**Une portée d'injection par exécution.** Dans un contrôleur, le `DbContext` vit le
temps d'une requête. Un service hébergé est un singleton : il doit ouvrir explicitement
un `IServiceScope` par exécution, sinon un `DbContext` capturé accumule les entités
suivies jusqu'à tout garder en mémoire.

**Distinguer les deux natures d'opération.** La réclamation de lignes d'outbox est du
SQL mécanique (§4), qui vit dans `Infrastructure` comme méthode de dépôt. La transition
métier — basculer une annonce, clôturer une session — passe par le domaine, parce que
c'est lui qui porte les invariants. **Jamais d'`UPDATE` direct sur les quantités** : il
court-circuiterait `RG-35`, qui exige qu'aucune quantité ne soit modifiée sans mouvement
tracé.

### La contrepartie, et la sortie de secours

Les traitements de fond partagent le processeur avec le traitement des requêtes, ce qui
pourrait dégrader `ENF-01`. Le risque est faible : le balayage se réduit à quelques
requêtes SQL toutes les cinq minutes, et l'enrichissement est limité en débit et dominé
par l'attente réseau, pas par le calcul.

Si cela devenait un problème, **l'extraction reste peu coûteuse** : la logique vit dans
`Application` et `Infrastructure`, donc un second hôte se contenterait de référencer les
mêmes bibliothèques. Changer d'hôte ne déplace aucune logique métier.

## 3. Le travail à faire

| Traitement | Règle | Cadence |
|---|---|---|
| Clôturer les sessions inactives | `RG-43` | ~5 min |
| Envoyer les alertes échues | `RG-44` | ~5 min |
| Basculer les annonces dont la bourse a commencé | `RG-23` | ~5 min |
| Rattacher les annonces sans date à une bourse créée | `RG-24` | ~5 min |
| Réaffecter les annonces d'une bourse déplacée ou annulée | `RG-38` | ~5 min |
| Rattraper les fiches `Pending` et `NotFound` | `RG-03`, `DT-05` | horaire, débit limité |
| Récupérer les couvertures | `DT-05` | horaire, débit limité |
| Enrichir le `WorkId` via Open Library | `RG-46` | horaire, débit limité |
| Supprimer les comptes inactifs depuis 3 ans | `ENF-13` | quotidien |

**Deux traitements, pas neuf** : un balayage court et fréquent, un enrichissement lent
et espacé. Neuf déploiements distincts contreviendraient à `ENF-24`.

| Nom | Cadence | Contenu |
|---|---|---|
| `sweep` | toutes les 5 min | Les cinq premières lignes ci-dessus |
| `enrich` | horaire | Rattrapage, couvertures, `WorkId`, purge quotidienne |

**C'est dans `enrich`, et nulle part ailleurs, que l'étalement des appels externes
s'applique** : N fiches par exécution, une requête à la fois. Personne n'attend de
résultat de ce côté.

## 4. La table d'outbox

`DT-03` : une table, pas un broker.

```
OutboxMessage
  Id            uniqueidentifier PK
  Kind          tinyint          NOT NULL   -- AlertEmail | ...
  PayloadJson   nvarchar(max)    NOT NULL
  DueAt         datetime2        NOT NULL   -- clôture + 2 h (RG-44)
  Status        tinyint          NOT NULL   -- Pending|Sent|Cancelled|Failed
  Attempts      int              NOT NULL DEFAULT 0
  ClaimedUntil  datetime2        NULL       -- verrou de traitement
  ScanSessionId uniqueidentifier NULL FK    -- écran admin, annulation en bloc
  MemberId      uniqueidentifier NULL FK
  CreatedAt, SentAt, LastError
```

Index : `Status` + `DueAt` (relève) ; `ScanSessionId` (annulation en bloc `RG-45`).

`ScanSessionId` est ce qui rend l'écran d'administration possible — lister, décompter,
annuler, forcer. Un message dans un broker n'aurait offert aucune de ces quatre choses.

### La relève, sans verrou distribué

```sql
UPDATE TOP (50) OutboxMessage
   SET ClaimedUntil = DATEADD(minute, 5, SYSUTCDATETIME())
 OUTPUT inserted.Id
 WHERE Status = 0                     -- Pending
   AND DueAt <= SYSUTCDATETIME()
   AND (ClaimedUntil IS NULL OR ClaimedUntil < SYSUTCDATETIME());
```

Deux exécutions qui se chevauchent ne peuvent pas réclamer la même ligne. Aucune
élection de leader n'est nécessaire.

**Relire l'état en base avant d'envoyer.** Le message n'est qu'un réveil : entre la mise
en file et l'échéance, une reprise de session a pu tout invalider (`RG-45`).

## 5. Le délai de deux heures

`RG-44` : mise en file à la clôture, envoi 2 h plus tard.

```
   scan …  scan …     clôture              envoi
   ──────────────────────●───────────────────●──────►
                         │◄─── délai 2 h ───►│
      correction sans    │   correction :    │  plus
      conséquence        │   alertes         │  rattrapable
                         │   annulées        │
```

Le délai n'est pas une temporisation technique, c'est **la fenêtre de rattrapage** :
elle survit à la clôture et couvre les sessions fermées automatiquement, dont personne
n'a vu le récapitulatif.

Latence maximale de bout en bout : 2 h de clôture par inactivité + 2 h de file + 5 min
de granularité, soit **un peu plus de 4 h**. Sans conséquence : les livres annoncés ne
sont disponibles qu'à la date de la bourse, les autres qu'à l'ouverture du local.

Les deux délais sont paramétrables (`ENF-25`).

## 6. Idempotence

Non négociable : les exécutions peuvent être relancées ou se chevaucher.

| Traitement | Garantie |
|---|---|
| Clôture de session | Une session déjà `Terminee` ne produit rien |
| Envoi d'alerte | Réclamation par `ClaimedUntil`, transition de statut unique |
| Bascule | Filtre sur `Status = Announced` ; rejouer ne double rien |
| Rattrapage | `ResolveAttempts` et `LastAttemptAt` bornent les tentatives |

Tout traitement qui suppose « je ne tourne qu'une fois » est un défaut.

## 7. Un point ouvert refermé

Une mesure bloquante figurait ici : le déclencheur planifié d'une application à zéro
réplica se réveille-t-il seul ? La documentation disait oui, des retours disaient non,
et pour `RG-44` l'échec aurait été silencieux.

**`DT-09` la rend sans objet.** L'API tourne en permanence : un service hébergé s'y
exécute sans dépendre d'un mécanisme de réveil. `QT-02` est close, et le palier 1 n'a
plus qu'une mesure bloquante devant lui au lieu de deux.

## 8. Coût

**Nul en marginal.** Le conteneur de l'API tourne de toute façon, puisqu'il doit rester
disponible pour le site web. Les balayages consomment quelques requêtes SQL toutes les
cinq minutes et de l'attente réseau — rien qui déplace la facture.

À noter pour `08-infrastructure.md` : ce n'est plus un worker qui pèse sur le quota
gratuit des Container Apps, c'est **le passage de l'API à `minReplicas: 1`**. Le
traitement différé, lui, ne coûte rien de plus.

## 9. Observabilité

Application Insights est déjà en place. Trois mesures à exposer, sans lesquelles les
pannes seront silencieuses :

- **Âge du plus vieux message `Pending` échu.** S'il croît, le balayage ne tourne plus.
  C'est l'alerte la plus importante du système.
- **Appels sortants par source et par jour.** Inutile au quotidien, décisif le jour où
  un rattrapage se met à marteler la BnF en boucle — dont les conditions prévoient un
  blocage « immédiatement et sans préavis ».
- **Annonces en retard de bascule.** Une bourse commencée dont les annonces n'ont pas
  basculé signale un balayage arrêté ou un agenda incohérent.
