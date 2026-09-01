# 06 — Traitements différés

## 1. Pourquoi un composant séparé

Les trois Container Apps existantes sont configurées avec `minReplicas: 0`. Un
`BackgroundService` hébergé dans l'API ne s'exécuterait donc qu'au hasard du trafic
HTTP : sans visite, pas de conteneur, donc **pas d'alerte à +2 h et pas de bascule**.
L'échec serait silencieux.

D'où `DT-04` : une application dédiée, `Microsoft.App/containerApps` avec
`kind=functionapp`, dans le `managedEnvironment` déjà déclaré. Ce n'est **pas** un
environnement nouveau.

## 2. Le travail à faire

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

## 3. La table d'outbox

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

## 4. Le délai de deux heures

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

## 5. Idempotence

Non négociable : les exécutions peuvent être relancées ou se chevaucher.

| Traitement | Garantie |
|---|---|
| Clôture de session | Une session déjà `Terminee` ne produit rien |
| Envoi d'alerte | Réclamation par `ClaimedUntil`, transition de statut unique |
| Bascule | Filtre sur `Status = Announced` ; rejouer ne double rien |
| Rattrapage | `ResolveAttempts` et `LastAttemptAt` bornent les tentatives |

Tout traitement qui suppose « je ne tourne qu'une fois » est un défaut.

## 6. Le point ouvert — `QT-02`

La documentation liste le déclencheur planifié parmi ceux qui montent depuis zéro via
KEDA. Des retours indiquent l'inverse : une application descendue à zéro ne serait pas
réveillée par son minuteur. Pour `RG-44`, ce serait un échec silencieux.

**À mesurer avant de construire dessus** : déployer une fonction planifiée avec
`minReplicas: 0`, ne pas y toucher pendant deux heures, vérifier qu'elle s'est exécutée.

| Si | Alors |
|---|---|
| Le réveil fonctionne | `minReplicas: 0`, coût nul |
| Le réveil ne fonctionne pas | `minReplicas: 1` — un conteneur permanent, de l'ordre d'une dizaine d'euros par mois |
| On veut éviter les deux | **Temporisation par file Azure Queue Storage** : un message peut rester invisible jusqu'à sept jours, et son déclencheur réveille bien une application à zéro |

La troisième voie mérite attention : le compte de stockage est **obligatoire** pour
toute Function sur Container Apps, et le projet en a déjà un. Le délai de `RG-44`
devient natif. La table reste la source de vérité ; le message n'est qu'un réveil.

Resteraient les traitements réellement périodiques — bascule, rattrapage — qu'un **ACA
Job en cron** couvrirait : planification garantie par la plateforme, facturation à
l'exécution seule.

## 7. Coût

Les Container Apps offrent **180 000 vCPU-secondes et 360 000 Gio-secondes gratuits par
mois**, et un job ne facture que pendant son exécution.

288 exécutions/jour × 10 s × 0,25 vCPU ≈ **21 600 vCPU-secondes/mois**, soit 12 % du
quota, partagé avec trois applications à zéro réplica la plupart du temps.
**En pratique : gratuit** — sauf dans l'hypothèse `minReplicas: 1` ci-dessus.

## 8. Observabilité

Application Insights est déjà en place. Trois mesures à exposer, sans lesquelles les
pannes seront silencieuses :

- **Âge du plus vieux message `Pending` échu.** S'il croît, le worker ne tourne plus.
  C'est l'alerte la plus importante du système.
- **Appels sortants par source et par jour.** Inutile au quotidien, décisif le jour où
  un rattrapage se met à marteler la BnF en boucle — dont les conditions prévoient un
  blocage « immédiatement et sans préavis ».
- **Annonces en retard de bascule.** Une bourse commencée dont les annonces n'ont pas
  basculé signale un worker arrêté ou un agenda incohérent.
