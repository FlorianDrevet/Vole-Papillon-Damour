# 01 — Analyse critique de l'idée initiale

> **24 septembre 2026.** Consigne reçue : « Ne va pas dans mon sens pour me faire
> plaisir, je veux juste une solution qui fonctionne avec un coût pas trop élevé.
> Challenge au maximum. » Ce document fait exactement cela.

## 1. L'idée telle que proposée

1. Calculer un **embedding** (vecteur sémantique) par livre, à partir du résumé ou
   d'autres informations de la fiche.
2. Ranger les livres dans une **base graphe** : un nœud par livre, portant son
   embedding, relié aux livres les plus proches.
3. Ne calculer l'embedding **qu'une fois par livre**, et ne recalculer les relations
   **qu'à l'entrée ou à la sortie d'un livre**.
4. Objectif affiché : **minimiser le coût et la latence**.
5. Usage : à partir des livres qu'un membre a achetés (`Mes achats`), lui mettre en avant
   des livres qui pourraient lui plaire.

## 2. Verdict en une ligne par point

| Point | Verdict | Pourquoi, en bref |
|---|---|---|
| Embedding calculé une seule fois par livre | ✅ **Bonne intuition** | Mais le coût n'est pas l'argument : il se compte en centimes. L'intérêt est de sortir l'appel IA du chemin de la requête |
| Embedding « sur le résumé » | ❌ **Prémisse fausse aujourd'hui** | **Le dépôt ne stocke aucun résumé.** Aucune des trois sources n'est interrogée pour en ramener un (§3.1) |
| Relier chaque livre à ses plus proches voisins, pré-calculés | ✅ **Bonne idée** | C'est ce qui rend la lecture instantanée et indépendante du tier SQL |
| … dans une **base graphe** | ❌ **À écarter** | Parcours à un seul saut, 20 000 nœuds : c'est une table SQL de voisins. Une base de plus contredit les principes du projet et coûte de 0 à ~65 $/mois pour zéro gain (§3.2) |
| Recalcul des relations à la **sortie** d'un livre | ❌ **Contre-productif** | La similarité est une propriété du contenu, pas du stock. La disponibilité se filtre à la lecture (§3.3) |
| Recalcul des relations à l'**entrée** d'un livre | ⚠️ **Incomplet** | Un nouveau livre modifie aussi les listes de voisins des livres existants. Un recalcul complet périodique est plus simple et s'auto-répare (§3.3) |
| S'appuyer sur `Mes achats` seul | ❌ **Signal trop rare au démarrage** | Seuls les passages associés à un compte y figurent, et la fonction vient d'être livrée (PR #224) (§4.1) |

## 3. Les faits qui fondent le verdict

### 3.1 Il n'y a pas de résumé à « embedder »

Table `Books` (`technique/02-modele-de-donnees.md` §2) : `Title`, `Authors`, `Publisher`,
`PublicationYear`, `PhysicalFormat`, `Language`, `Genre`. **Pas de colonne résumé.**

Les clients bibliographiques ne demandent pas non plus de description :

| Source | Ce qui est extrait aujourd'hui | Résumé disponible chez la source ? |
|---|---|---|
| BnF SRU (`BnfSruClient`) | titre, auteurs, éditeur, année, genre (`608$a`) | Parfois (zone UNIMARC `330`), couverture inconnue |
| Open Library (`OpenLibraryClient`) | `title,author_name,publisher,first_publish_year,cover_i,key,subject,subject_facet` | Pas via `search.json` ; seulement via l'API *works*, inégalement renseignée |
| Google Books (`GoogleBooksClient`) | titre, auteurs, éditeur, date, `categories[0]` | Souvent `volumeInfo.description`, couverture inconnue |

Conséquences :

- **Sans résumé**, un embedding de « titre + auteur + genre » est faible : il rapproche
  surtout des titres qui se ressemblent lexicalement, et ne « sait » quelque chose que
  des œuvres très connues. Il n'apportera alors guère plus que des règles SQL simples.
- Le taux réel de résumés disponibles pour **notre** catalogue (occasion, poches,
  jeunesse, éditions anciennes) **n'est pas connu**. Le principe n°4 du projet (« ce qui
  n'est pas mesuré n'est pas décidé », `technique/00-vue-densemble.md`) s'applique : il
  faut le mesurer sur un échantillon d'ISBN réels avant de choisir l'approche.
- Récupérer des résumés implique de modifier deux clients, d'ajouter une colonne, et de
  relancer l'enrichissement sur les fiches existantes. Leur statut juridique (texte
  d'éditeur, conditions d'usage de Google Books) doit être vérifié, surtout s'ils
  devaient être affichés et pas seulement utilisés pour le calcul.

Point intéressant en faveur des embeddings : la colonne `Genre` mélange des
vocabulaires hétérogènes (BnF en français, « Juvenile Fiction » chez Google, sujets
libres chez Open Library). Des règles « même genre » par égalité de chaîne marcheront
mal ; un embedding, lui, rapproche « Roman jeunesse » de « Juvenile Fiction ».

### 3.2 Une base graphe n'apporte rien ici

**Volumétrie.** Environ **20 000 fiches après cinq ans** (`technique/02` §7). Avec 30
voisins par livre, cela fait 600 000 arêtes : quelques dizaines de Mo dans une table SQL.

**Forme de la requête.** La recommandation est un parcours **à un seul saut** : pour
les livres du membre, prendre leurs voisins, agréger les scores, filtrer ce qui est
disponible, exclure ce qu'il a déjà. En SQL, c'est une jointure et un `GROUP BY`. Une
base graphe se justifie pour des parcours à plusieurs sauts ou des chemins (amis
d'amis, plus court chemin), pas pour une liste d'adjacence consultée à un saut.

**Cohérence.** La disponibilité (`QuantityAvailable`, annonces) vit dans SQL. Une base
graphe obligerait soit à la synchroniser, soit à faire deux requêtes sur deux bases :
des données distribuées, et des bugs de synchronisation à diagnostiquer la veille d'une
bourse.

**Principes du projet.** `technique/00-vue-densemble.md` : « Aucune base
supplémentaire », « Ce qui n'est pas maintenable par une personne seule ne se construit
pas ». `AGENTS.md` : « Never introduce a second backend technology path ». Une base
graphe, c'est une ressource, un SDK, une sauvegarde, un émulateur local, un secret et
une panne de plus.

**Coût** (ordres de grandeur, septembre 2026) :

| Option graphe | Coût |
|---|---|
| Neo4j AuraDB Professional | à partir d'environ **65 $/mois**, soit ~780 $/an ([neo4j.com/pricing](https://neo4j.com/pricing/)) |
| Azure Cosmos DB for Apache Gremlin, serverless | pas de minimum, facturé aux RU consommées ([Microsoft Learn](https://learn.microsoft.com/en-us/azure/cosmos-db/serverless)) : peu cher, mais une ressource et une technologie de plus |
| **Table `BookNeighbors` dans la base SQL existante** | **0 €** de plus |

**Ce qu'il faut garder de l'idée :** le graphe lui-même. Une table
`BookNeighbors(Isbn13, NeighborIsbn13, Score, Rank)` *est* un graphe, stocké comme une
liste d'adjacence. On garde le concept, on écarte le produit.

### 3.3 Ce qui doit déclencher un recalcul, et ce qui ne le doit pas

**La sortie d'un livre ne change rien à sa ressemblance avec les autres.** Supprimer
des arêtes quand un livre est épuisé, c'est :

- les recréer quand un exemplaire du même ISBN revient, ce qui est fréquent en bourse
  (les doublons sont la raison d'être de `RG-10`) ;
- faire des centaines de mises à jour inutiles un jour de bourse, une par vente.

La bonne règle : **les voisins sont calculés sur tout le catalogue** (livres déjà vus,
disponibles ou non) et **la disponibilité se filtre au moment de la lecture**.

**L'entrée d'un livre est plus subtile qu'il n'y paraît.** Calculer les voisins du
nouveau livre B ne suffit pas : B peut devenir l'un des plus proches voisins d'un livre
A déjà présent, dont la liste doit alors changer. C'est faisable en incrémental (comparer B
à tous les autres, puis mettre à jour les deux côtés), mais un **recalcul complet
périodique** dans le worker est plus simple, idempotent et auto-réparateur :

- 20 000 × 20 000 / 2 = 2 × 10⁸ produits scalaires en dimension 512 ;
- estimation : de quelques secondes à environ une minute sur un petit conteneur avec
  `TensorPrimitives` (SIMD). **À mesurer.**
- mémoire : 20 000 × 512 × 4 octets ≈ **40 Mo**.

### 3.4 La recherche vectorielle dans SQL n'est pas nécessaire, et peut-être pas disponible

Azure SQL propose un type `VECTOR` et `VECTOR_DISTANCE`, en disponibilité générale
depuis juin 2025 ([annonce](https://devblogs.microsoft.com/azure-sql/announcing-general-availability-of-native-vector-type-functions-in-azure-sql/)),
et EF Core 10 le mappe via `SqlVector<float>`. Mais :

- la base est en **Standard S0 (10 DTU)** (`infra/parameters/main.dev.bicepparam`).
  La documentation Microsoft indique que S0 fournit **moins d'un vCore, sur disque
  HDD** ([Learn — modèle DTU](https://learn.microsoft.com/en-us/azure/azure-sql/database/service-tiers-dtu?view=azuresql)) :
  un scan vectoriel par requête y serait coûteux ;
- une source de recherche affirme que le type `VECTOR` n'est pas proposé sur le modèle
  DTU, mais **la documentation officielle consultée ne le confirme pas**. Point
  **non vérifié**, testable en une requête (`SELECT CAST('[1,2]' AS VECTOR(2))`) ;
- le seuil conseillé par Microsoft pour la recherche exacte est de 50 000 vecteurs
  ([Learn — vectors](https://learn.microsoft.com/en-us/sql/sql-server/ai/vectors?view=azuresqldb-current)) :
  on en sera loin.

**Conclusion.** Avec des voisins pré-calculés par le worker, l'API ne lit qu'une table
ordinaire : **aucune dépendance au tier SQL**, et aucun passage en vCore à payer.

## 4. Ce que l'idée initiale ne traite pas, et qui compte le plus

### 4.1 Le démarrage à froid : le signal `Mes achats` est rare

- `Mes achats` ne contient que les passages **associés** à un compte (`RG-61`) ; les
  ventes anonymes restent anonymes.
- La fonction vient d'être livrée (PR #224, 22 septembre 2026), et son acceptation
  terrain n'est pas faite.
- Hypothèse à vérifier : pendant des mois, **la grande majorité des comptes n'auront
  aucun achat associé**. Une recommandation fondée uniquement sur les achats n'afficherait
  rien à la plupart des gens.

**Autres signaux déjà présents dans la base, plus riches au démarrage :**

| Signal | Table | Force |
|---|---|---|
| Liste de recherche | `WatchlistItems` | Explicite et fort : « je veux ce livre » |
| Ma sélection | `MemberSelectionItems` | Intention (à prendre, à revoir) |
| Achats associés | `CheckoutPassageLines` | Fort, mais rare |

Et il faut un **repli** pour qui n'a aucun signal : ne rien afficher, ou une liste non
personnalisée (nouveautés disponibles, titres les plus demandés).

### 4.2 Les signaux les plus simples sont souvent les meilleurs pour des livres

**Même auteur, disponible maintenant**, et **tome suivant d'une série** : pour une
bourse riche en BD, jeunesse et séries, ce sont les suggestions les plus justes. Elles ne
coûtent rien (SQL seul). Limites : l'orthographe des auteurs varie d'une source à
l'autre (« Goscinny, René » ou « René Goscinny »), et aucune colonne ne porte la série.

L'embedding apporte la **proximité thématique** au-delà de l'auteur. Il vaut surtout
s'il y a un résumé (§3.1).

### 4.3 RGPD : c'est du profilage, donc une nouvelle finalité

Utiliser les achats, la sélection ou la liste de recherche pour déduire des goûts est un
**profilage** (RGPD, art. 4.4). Il est à faible enjeu (aucune décision à effet juridique :
l'art. 22 ne s'applique pas), mais :

- `10-evolution-compte-selection-achats.md` §12.2 : « L'association ne doit pas utiliser
  l'historique pour une prospection commerciale sans information et base juridique
  distinctes. »
- Le tableau des traitements de `09-rgpd-compte-et-droits.md` ne prévoit pas cette
  finalité : il faudra l'ajouter, informer, et offrir l'opposition.
- **Affichage dans le compte** : intérêt légitime plausible, avec information et
  possibilité de désactiver. **Envoi par e-mail** (« avant la bourse, voici des livres
  pour vous ») : c'est une autre affaire, probablement soumise au consentement. **À
  valider par l'association**, comme les autres bases de `09`.
- Bon point de l'approche par voisins pré-calculés : **aucune donnée personnelle ne part
  vers le service d'IA**. Seules des métadonnées de livres sont vectorisées ;
  l'agrégation par membre se fait dans SQL.

## 5. Chiffrage comparé

Hypothèses : 20 000 fiches à terme, ~330 nouvelles fiches par mois, ~400 tokens par
fiche avec résumé, `text-embedding-3-small` à **0,02 $ par million de tokens** sur
Azure OpenAI ([calculateur](https://www.helicone.ai/llm-cost/provider/azure/model/text-embedding-3-small),
à confirmer sur la grille Azure de la région).

| | A. Règles SQL seules | **B. Embeddings + voisins pré-calculés dans SQL** | C. Idée initiale (base graphe) |
|---|---|---|---|
| Nouvelle ressource Azure | Aucune | Aucune si le compte Azure OpenAI existant est activé (un déploiement de modèle en plus) | Une base graphe |
| Coût IA initial (20 000 fiches) | 0 € | ≈ **0,16 $** (8 M tokens) | ≈ 0,16 $ |
| Coût IA récurrent | 0 € | ≈ **0,003 $/mois** | ≈ 0,003 $/mois |
| Coût d'hébergement ajouté | 0 € | 0 € (≈ 40 Mo de vecteurs et ≈ 30 Mo de voisins dans la base existante) | 0 à ~65 $/mois |
| Latence de lecture | ms | ms (lecture d'une table) | ms, plus une deuxième base à interroger |
| Qualité attendue | Bonne sur auteur et série ; nulle en thématique | Bonne **si** le taux de résumés est suffisant ; moyenne sinon | Identique à B |
| Maintenance | Minimale | Un traitement worker, une colonne, un déploiement de modèle | Une technologie de plus |
| **Vrai coût** | Temps de développement | **Temps de développement** | Temps de développement **et** exploitation |

**Le coût IA est négligeable dans tous les cas.** Le vrai coût est le temps de
développement et de maintenance d'une personne seule. C'est lui qui doit trancher,
pas la facture Azure.

Alternative écartée pour l'instant : un modèle d'embedding local (ONNX, par exemple
multilingual-e5-small) dans le worker. Coût d'API nul, mais image plus lourde, CPU, et
un modèle à maintenir, pour économiser quelques centimes par an.

Option à garder en réserve : si le taux de résumés s'avère faible, faire produire par le
modèle déjà déployé (`gpt-4.1-nano`) une courte fiche de thèmes par livre avant
l'embedding. Coût de l'ordre de 1 à 2 $ pour 20 000 fiches (à confirmer), mais **risque
d'invention** sur les titres peu connus : à n'envisager qu'après mesure.

## 6. Recommandation provisoire

**B, construit par couches, précédé d'une mesure.**

1. **Mesurer d'abord** (sonde jetable) : sur ~200 ISBN réels du catalogue, quel taux de
   résumés chez Google Books et à la BnF ? Combien de comptes ont au moins un signal
   (achat, sélection, liste de recherche) ?
2. **Couche 1 — règles SQL** : même auteur et genre proche, disponibles ou annoncés,
   hors livres déjà achetés ou sélectionnés. Livrable seul, utile tout de suite.
3. **Couche 2 — voisins sémantiques**, seulement si la mesure le justifie : embedding
   calculé par le worker lors de l'enrichissement de la fiche, recalcul complet
   périodique des voisins, table `BookNeighbors`, lecture par une requête SQL.
4. **Jamais** de base graphe, ni de recalcul à chaque vente.

Cette recommandation reste **provisoire** : elle dépend de l'usage visé (où, quand et
pour quoi faire apparaître ces suggestions), qui fait l'objet des questions du journal.

## 7. Sources

- [Azure SQL — disponibilité générale du type vector](https://devblogs.microsoft.com/azure-sql/announcing-general-availability-of-native-vector-type-functions-in-azure-sql/)
- [Microsoft Learn — Vector data type](https://learn.microsoft.com/en-us/sql/t-sql/data-types/vector-data-type?view=azuresqldb-current)
- [Microsoft Learn — Vector search & vector index](https://learn.microsoft.com/en-us/sql/sql-server/ai/vectors?view=azuresqldb-current)
- [Microsoft Learn — Modèle d'achat DTU](https://learn.microsoft.com/en-us/azure/azure-sql/database/service-tiers-dtu?view=azuresql)
- [EF Core — recherche vectorielle SQL Server](https://github.com/dotnet/entityframework.docs/blob/main/entity-framework/core/providers/sql-server/vector-search.md)
- [Tarif text-embedding-3-small sur Azure](https://www.helicone.ai/llm-cost/provider/azure/model/text-embedding-3-small)
- [Neo4j — tarifs](https://neo4j.com/pricing/)
- [Azure Cosmos DB — mode serverless](https://learn.microsoft.com/en-us/azure/cosmos-db/serverless)
