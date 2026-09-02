# 06 — Traitements différés

## 1. Pourquoi un composant séparé

`DT-04` : une application dédiée, `Microsoft.App/containerApps` avec `kind=functionapp`,
dans le `managedEnvironment` déjà déclaré. Ce n'est **pas** un environnement nouveau.

L'argument d'origine était la fiabilité. Les Container Apps étaient toutes à
`minReplicas: 0` : un `BackgroundService` hébergé dans l'API ne se serait exécuté qu'au
hasard du trafic HTTP — sans visite, pas de conteneur, donc **pas d'alerte à +2 h et pas
de bascule**, en silence.

**Cet argument a changé de nature.** L'API est passée à `minReplicas: 1` (`36b0e50`),
pour une raison étrangère à ce chapitre : elle ne peut pas être indisponible pour le site
web. Un service hébergé s'y exécuterait donc de façon fiable. La décision est maintenue quand même, pour
trois raisons instruites au [réexamen de `DT-04`](01-decisions.md#dt-04--worker-différé-en-container-app-kindfunctionapp-dédié) :

- **L'isolation.** Le réplica permanent existe pour servir le site et le scan. Un
  rattrapage d'enrichissement ou un balayage d'outbox y partagerait le processeur avec
  le rendu SSR et les requêtes de scan, au détriment de `ENF-01`.
- **Les déclencheurs planifiés et les réessais**, déclaratifs dans le modèle Functions,
  à écrire soi-même dans un `BackgroundService` — boucle, calendrier, temporisation
  exponentielle, exclusion entre les deux répliques autorisées par `maxReplicas: 2`.
- **Des cycles de vie séparés.** L'API se déploie au rythme du site ; le worker au
  rythme du métier différé. Ni redémarrage ni mise à l'échelle de l'un n'emporte l'autre.

L'alternative — dissoudre le worker dans l'API — est instruite et écartée en `DT-09`.
Ce que le refus coûte est net : **`QT-02` reste ouverte et bloquante** (§7), puisque
c'est désormais le worker, et lui seul, qui vit à zéro réplica.

## 2. Comment le worker partage le code de l'API

**Rien n'est dupliqué.** Le worker est un second *hôte* au-dessus des mêmes
bibliothèques, par référence de projet dans la même solution.

```
Vole_Papillon_Damour.Domain           (bibliothèque)
Vole_Papillon_Damour.Application      (bibliothèque) ──► Domain
Vole_Papillon_Damour.Infrastructure   (bibliothèque) ──► Application
Vole_Papillon_Damour.Contracts        (bibliothèque)

Vole_Papillon_Damour.Api      (hôte web)       ──┐
Vole_Papillon_Damour.Worker   (hôte Functions) ──┴──► Application + Infrastructure
```

Un seul domaine, un seul `ProjectDbContext`, un seul jeu de migrations, une seule
définition de `RG-23`. Deux processus, un code — le schéma classique du monolithe
modulaire. Le découpage actuel s'y prête déjà : `Application` et `Infrastructure` sont
des bibliothèques, pas des morceaux de l'API.

### Le worker appelle les mêmes handlers

```csharp
await sender.Send(new ReleaseDueAnnouncementsCommand(), ct);   // RG-23
await sender.Send(new CloseIdleSessionsCommand(), ct);         // RG-43
```

`CloseScanSession` est **le même handler** que celui déclenché quand un bénévole appuie
sur `TERMINER`. C'est voulu : `RG-43` définit quatre causes de clôture, et il serait
absurde que la clôture manuelle et la clôture par inactivité suivent deux chemins de
code distincts — ils divergeraient au premier correctif.

### Pourquoi pas un appel HTTP vers l'API

C'est l'option qui vient naturellement, et elle reste mauvaise **ici**. Elle
introduirait une dépendance de disponibilité entre deux composants qui n'ont aucun
besoin de se parler : le worker échouerait à chaque redéploiement de l'API, pour une
opération qui est une écriture en base et rien d'autre. C'était pire encore quand l'API
était à `minReplicas: 0` — il fallait alors la réveiller à chaque balayage et subir le
démarrage à froid ; ce n'est plus le cas, mais l'objection de fond ne tient pas à ça.

S'y ajoute la surface d'attaque : réclamer des lignes d'outbox ou basculer des annonces
en masse sont des opérations internes. Les exposer en HTTP crée des routes qui ne
doivent jamais être appelables de l'extérieur — et qu'il faudrait donc protéger. Deux
sauts réseau pour ce qui est une opération de base de données.

Dupliquer le domaine est exclu : divergence garantie dès la première évolution de
`RG-15`. Un contexte borné séparé avec son propre modèle serait défendable si le worker
appartenait à une autre équipe et à un autre cycle de vie ; ici, un développeur, un
domaine, des données qui se référencent en permanence.

### Ce que ce choix coûte

| Contrepartie | Portée |
|---|---|
| **Couplage de version sur le schéma** | API et worker **doivent être construits et déployés depuis le même commit**. Deux images distinctes — le worker exige une image de base Functions, l'API une image ASP.NET — mais une seule construction et une seule étiquette de version |
| Deux composants portent les accès à la base | Inévitable dès qu'on découple le traitement de fond du trafic HTTP |
| Un changement dans `Application` impose de redéployer les deux | Sans conséquence à ce rythme de livraison |

### Trois contraintes de conception

Ce sont les pièges réels de ce montage.

**Aucun handler appelé par le worker ne dépend d'un utilisateur ambiant.** Pas
d'`IHttpContextAccessor`, pas d'« utilisateur courant » tiré du JWT : le worker n'en a
pas. L'acteur se passe explicitement en paramètre de la commande, ou vaut « système » —
ce qui doit apparaître dans le `VolunteerId` des mouvements (`RG-41`).

**Une portée d'injection par exécution.** Dans l'API, le `DbContext` vit le temps d'une
requête. Dans le worker, ouvrir explicitement un `IServiceScope` par exécution, sinon le
contexte accumule les entités suivies et finit par tout garder en mémoire — ce qui
contredirait `ENF-03`.

**Distinguer les deux natures d'opération.** La réclamation de lignes d'outbox est du
SQL mécanique (§4), qui vit dans `Infrastructure` comme méthode de dépôt. La transition
métier — basculer une annonce, clôturer une session — passe par le domaine, parce que
c'est lui qui porte les invariants. **Le worker n'écrit jamais un `UPDATE` direct sur
les quantités** : il court-circuiterait `RG-35`, qui exige qu'aucune quantité ne soit
modifiée sans mouvement tracé.

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

## 7. Le point ouvert — `QT-02`

La documentation liste le déclencheur planifié parmi ceux qui montent depuis zéro via
KEDA. Des retours indiquent l'inverse : une application descendue à zéro ne serait pas
réveillée par son minuteur. Pour `RG-44`, ce serait un échec silencieux.

Le passage de l'API à `minReplicas: 1` **ne referme pas ce point** : c'est le worker qui
porte le minuteur, et c'est lui qui vit à zéro réplica.

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

## 8. Coût

Les Container Apps offrent **180 000 vCPU-secondes et 360 000 Gio-secondes gratuits par
mois** — quota désormais absorbé par les trois applications permanentes (`08` §7) — et un
job ne facture que pendant son exécution.

288 exécutions/jour × 10 s × 0,25 vCPU ≈ **21 600 vCPU-secondes/mois**, soit 12 % du
quota mensuel. **En pratique : moins d'un euro par mois** — sauf dans l'hypothèse
`minReplicas: 1` ci-dessus, qui change d'ordre de grandeur.

À ne pas confondre avec le poste vraiment coûteux : **les trois applications existantes
à `minReplicas: 1`**, décidées pour la disponibilité du site et non pour ce chapitre.
Une seule consomme déjà bien plus que le quota gratuit. Le détail est en
[`08-infrastructure.md`](08-infrastructure.md) §7.

## 9. Observabilité

Application Insights est déjà en place. Trois mesures à exposer, sans lesquelles les
pannes seront silencieuses :

- **Âge du plus vieux message `Pending` échu.** S'il croît, le worker ne tourne plus.
  C'est l'alerte la plus importante du système.
- **Appels sortants par source et par jour.** Inutile au quotidien, décisif le jour où
  un rattrapage se met à marteler la BnF en boucle — dont les conditions prévoient un
  blocage « immédiatement et sans préavis ».
- **Annonces en retard de bascule.** Une bourse commencée dont les annonces n'ont pas
  basculé signale un worker arrêté ou un agenda incohérent.
