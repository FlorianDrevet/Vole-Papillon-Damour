# 02 — Étude d'architecture : stocker les voisins dans SQL ou dans une base graphe

> **24 septembre 2026.** Demande : comparer, **au-delà du prix et de la règle du dépôt
> « pas de base supplémentaire »**, le stockage des voisins dans la base relationnelle
> existante et dans une base graphe (Azure Cosmos DB for Apache Gremlin). Le coût de
> développement et de maintenance est accepté si le graphe est architecturalement
> meilleur.

Précision préalable : la base du projet n'est pas PostgreSQL mais **Azure SQL Database**
(SQL Server), en **Standard S0** (`infra/parameters/main.dev.bicepparam`).

## 1. Ce que les deux usages demandent réellement

Deux surfaces ont été retenues à l'échange 2 :

| Surface | Public | Requête |
|---|---|---|
| **« Livres similaires »** sous une fiche du catalogue | Tout le monde, connecté ou non | Les N voisins de **ce** livre, visibles au catalogue et disponibles ou annoncés |
| **« Pour vous »** sur l'accueil et dans Mon compte | Membre connecté | Les voisins de **tous** les livres lus par le membre, scores agrégés, filtrés sur la disponibilité, sans ce qu'il possède déjà |

Chaque requête :

1. part d'un ou plusieurs livres ;
2. fait **un seul saut** sur l'arête « ressemble à » ;
3. **agrège** les scores (surface 2) ;
4. **filtre sur des données qui vivent dans SQL** : stock (`QuantityAvailable`,
   annonces), visibilité (`IsHiddenFromCatalog`), fusion (`RedirectedToIsbn13`), achats
   et sélection du membre ;
5. **joint des données d'affichage qui vivent dans SQL** : titre, auteurs, couverture.

Le point 4 décide de toute l'étude : **les filtres et l'affichage sont dans SQL, et ils
changent à chaque vente.**

## 2. Les options étudiées

| | Option | Principe |
|---|---|---|
| **S1** | Table relationnelle `BookNeighbors` | `(Isbn13, NeighborIsbn13, Score, Rank)`, clé `(Isbn13, Rank)`, dans la base existante |
| **S2** | Tables graphe SQL (*SQL Graph*) | Tables `AS NODE` / `AS EDGE` et clause `MATCH`, **dans la même base Azure SQL** ([Learn](https://learn.microsoft.com/en-us/sql/relational-databases/graphs/sql-graph-overview?view=azuresqldb-current)) |
| **S3** | Azure Cosmos DB for Apache Gremlin | Un sommet par livre, une arête `similar` par voisin, compte Cosmos dédié |

S2 mérite d'être connue : c'est une **vraie base graphe** (nœuds, arêtes, `MATCH`,
`SHORTEST_PATH`), sans nouvelle ressource. Si la sémantique graphe devait un jour
servir, c'est par là qu'on commencerait.

## 3. Les requêtes, écrites

### S1 — SQL, une requête, un aller-retour

```sql
-- « Pour vous » : voisins agrégés des livres du membre
WITH Seeds AS (
    SELECT Isbn13, Weight FROM dbo.fn_MemberSeeds(@UserId)      -- achats, etc.
),
Candidates AS (
    SELECT n.NeighborIsbn13, SUM(n.Score * s.Weight) AS Score
    FROM Seeds s
    JOIN BookNeighbors n ON n.Isbn13 = s.Isbn13
    GROUP BY n.NeighborIsbn13
)
SELECT TOP (12) b.Isbn13, b.Title, b.Authors, b.CoverUrl, c.Score
FROM Candidates c
JOIN Books b ON b.Isbn13 = c.NeighborIsbn13
WHERE b.QuantityAvailable > 0            -- ou annoncé pour la prochaine bourse
  AND b.IsHiddenFromCatalog = 0
  AND b.RedirectedToIsbn13 IS NULL
  AND NOT EXISTS (SELECT 1 FROM Seeds s WHERE s.Isbn13 = b.Isbn13)
ORDER BY c.Score DESC;
```

« Livres similaires » est le cas particulier à une seule graine. Les deux sont
exprimables en LINQ avec EF Core, sans SQL brut.

### S3 — Gremlin, puis SQL, deux allers-retours

```groovy
g.V('9782070612758', '9782012101333')
 .outE('similar')
 .group().by(inV().id()).by(values('score').sum())
 .unfold().order().by(values, decr).limit(100)
```

Puis un second appel à SQL avec les 100 ISBN pour filtrer le stock, la visibilité et les
possessions, et récupérer titres et couvertures. Deux problèmes structurels :

- **Sur-lecture** : on ne sait pas d'avance combien des 100 candidats survivront au
  filtre. Un jour de bourse, beaucoup seront épuisés : il faut relire plus loin, donc
  boucler.
- **L'alternative est pire** : recopier la disponibilité sur les sommets. Chaque vente,
  bascule ou correction devient alors une **double écriture** SQL + Cosmos, sans
  transaction commune ([Cosmos Gremlin ne gère pas les
  transactions](https://learn.microsoft.com/en-us/azure/cosmos-db/gremlin/support#unsupported-features)).
  Il faut une outbox de plus, et les écarts se diagnostiquent la veille d'une bourse.

## 4. Comparaison critère par critère

| Critère | S1 — table SQL | S2 — SQL Graph | S3 — Cosmos Gremlin |
|---|---|---|---|
| **Adéquation à la requête** (1 saut + agrégat + filtres) | ✅ Jointure + `GROUP BY` : c'est le cas d'école du relationnel | ✅ Identique, syntaxe `MATCH` | ⚠️ Parcours facile, mais filtres et affichage ailleurs |
| **Cohérence avec le stock** | ✅ Même base, même transaction, lecture toujours à jour | ✅ Idem | ❌ Données réparties : sur-lecture ou double écriture |
| **Reconstruction des voisins** | ✅ `SqlBulkCopy` dans une table de travail, puis échange atomique : les lecteurs ne voient jamais une liste à moitié écrite | ✅ Idem | ⚠️ Écritures unitaires, sans transaction : pendant la reconstruction, un lecteur peut voir un mélange ancien/nouveau |
| **Latence de lecture** | ✅ Quelques ms, index `(Isbn13, Rank)` | ✅ Idem | ⚠️ Un appel réseau de plus, puis SQL |
| **Accès .NET** | ✅ EF Core 10, LINQ typé | ⚠️ EF Core ne connaît pas les tables graphe : SQL brut | ❌ Requêtes **en chaîne de caractères** uniquement (pas de *bytecode*), GraphSON v2, pilote conseillé Gremlin.Net **3.4.13** car 3.5 et 3.6 ont des incompatibilités connues ([Learn](https://learn.microsoft.com/en-us/azure/cosmos-db/gremlin/support)) |
| **Tests** | ✅ Les fixtures existantes suffisent | ⚠️ Nécessite un vrai SQL Server | ❌ Émulateur ou doublures, et un second chemin de test |
| **Authentification** | ✅ Déjà en place | ✅ Déjà en place | ⚠️ Identité managée non native d'après la [page Service Connector Gremlin](https://learn.microsoft.com/en-us/azure/service-connector/how-to-integrate-cosmos-gremlin) : clé ou chaîne de connexion à stocker (à confirmer). Le reste du projet suit la ligne « pas de clé » (`disableLocalAuth: true`) |
| **Infra, sauvegarde, supervision** | ✅ Rien de nouveau | ✅ Rien de nouveau | ❌ Module Bicep, rôles, secret, tableau de bord, restauration à tester |
| **Trajectoire du produit** | ✅ Cœur de SQL Server | ✅ Stable depuis SQL Server 2017 | ⚠️ La documentation oriente les nouveaux projets graphe analytiques et les migrations Gremlin vers *Graph in Microsoft Fabric*, et le pilote reste bloqué en 3.4.x. Pas d'annonce de retrait trouvée, mais un signal d'investissement faible |
| **Coût mensuel ajouté** | 0 € | 0 € | ≈ 0 à 15 $ (§5) |

## 5. Prix de Cosmos DB for Apache Gremlin

Tarifs relevés en septembre 2026, **à reconfirmer sur la grille Azure de la région**
([tarifs serverless](https://azure.microsoft.com/en-us/pricing/details/cosmos-db/serverless/)) :

- **Serverless** : environ **0,25 $ par million de RU**, sans minimum, plus le stockage
  (de l'ordre de 0,25 $/Go/mois) ;
- **Débit provisionné** : minimum 400 RU/s, soit **≈ 23 $/mois** ;
- **Offre gratuite** : 1 000 RU/s et 25 Go, sur un seul compte par abonnement (mode
  provisionné).

Estimation pour notre volumétrie (20 000 sommets, 30 voisins chacun, soit
600 000 arêtes). Les consommations en RU sont des **hypothèses**, à mesurer avec
`executionProfile()` :

| Poste | Hypothèse | Coût serverless |
|---|---|---|
| Stockage | < 1 Go (les embeddings n'ont pas besoin d'y être) | < 0,25 $/mois |
| Lectures « similaires » + « pour vous » | 50 000 lectures/mois × ~20 RU | ≈ 0,25 $/mois |
| Reconstruction complète des arêtes | 600 000 suppressions + 600 000 créations × ~10 RU ≈ 12 M RU | ≈ **3 $ par reconstruction** |
| Reconstruction hebdomadaire | 4 par mois | ≈ 12 $/mois |
| Mise à jour incrémentale (seules les arêtes changées) | quelques centaines de milliers de RU | < 1 $/mois |

**Total : entre quasi nul et une quinzaine de dollars par mois.** Le prix n'est donc
**pas** l'argument décisif contre Cosmos. L'argument est architectural (§3, §4).

## 6. Dans quels cas une base graphe deviendrait meilleure

La question honnête est : existe-t-il une évolution raisonnable où le graphe gagnerait ?

| Besoin futur | Graphe utile ? | Avec SQL |
|---|---|---|
| Filtrage collaboratif : « ceux qui ont acheté ce livre ont aussi acheté… » (livre → membres → livres, 2 sauts) | Oui en principe | 2 jointures. À notre échelle (quelques milliers de membres), quelques ms. Et le signal manque : seuls les passages associés existent |
| Expliquer une suggestion (« parce que vous avez lu X ») | Neutre | La requête S1 garde la graine : il suffit de la projeter |
| Chemins, plus court chemin, communautés, PageRank sur le catalogue | Oui | Calcul par lots dans le worker (en mémoire), résultat écrit en table ; ou `SHORTEST_PATH` de S2 |
| Parcours interactifs à plusieurs sauts sur des millions d'arêtes | **Oui, nettement** | C'est le vrai terrain des bases graphe. **Rien dans le projet n'y ressemble** |

Si un jour la sémantique graphe devient nécessaire, **S2 (SQL Graph)** l'apporte sans
quitter la base, et la migration depuis S1 est un simple `INSERT … SELECT`.

## 7. Conclusion

**S1, la table relationnelle `BookNeighbors`, est architecturalement meilleure pour ce
besoin.** Ce n'est pas seulement la règle « pas de base de plus » qui la fait gagner :

1. **La requête est relationnelle** : un saut, un agrégat, puis surtout des filtres et
   des jointures sur des données qui sont dans SQL et qui changent à chaque vente.
2. **La cohérence est gratuite** dans une seule base, et coûteuse dès qu'on la répartit :
   sur-lecture, ou double écriture sans transaction.
3. **La reconstruction atomique** (table de travail puis échange) n'a pas d'équivalent
   simple dans Cosmos Gremlin.
4. **L'outillage .NET** : EF Core typé d'un côté, requêtes Gremlin en chaînes avec un
   pilote figé en 3.4 de l'autre.

Le graphe ne gagnerait que sur des parcours à plusieurs sauts, sur de grands volumes et
en ligne. Aucun usage prévu n'en demande. Si la sémantique graphe devient utile,
**SQL Graph (S2)** est le chemin de repli, dans la même base.

**Prendre le coût de développement pour Cosmos n'achèterait pas une meilleure
architecture : cela achèterait une donnée répartie à synchroniser.**

## 8. Sources

- [Microsoft Learn — Cosmos DB for Apache Gremlin, vue d'ensemble](https://learn.microsoft.com/en-us/azure/cosmos-db/gremlin/overview)
- [Microsoft Learn — Compatibilité TinkerPop et limitations](https://learn.microsoft.com/en-us/azure/cosmos-db/gremlin/support)
- [Microsoft Learn — Service Connector et Cosmos Gremlin](https://learn.microsoft.com/en-us/azure/service-connector/how-to-integrate-cosmos-gremlin)
- [Microsoft Learn — Cosmos DB serverless](https://learn.microsoft.com/en-us/azure/cosmos-db/serverless)
- [Azure — Tarifs Cosmos DB serverless](https://azure.microsoft.com/en-us/pricing/details/cosmos-db/serverless/)
- [Holori — Guide des prix Cosmos DB](https://holori.com/azure-cosmos-db-pricing-guide/)
- [Microsoft Learn — SQL Graph](https://learn.microsoft.com/en-us/sql/relational-databases/graphs/sql-graph-overview?view=azuresqldb-current)
