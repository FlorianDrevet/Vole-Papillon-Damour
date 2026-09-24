# 00 — Journal des échanges

Journal chronologique de la conception. Chaque échange consigne la demande, la réponse,
les décisions prises et les questions laissées ouvertes. Les analyses longues vivent
dans les documents numérotés ; ce journal y renvoie.

---

## Échange 1 — 24 septembre 2026 : l'idée initiale

### Demande

Maintenant que les membres disposent de la liste des livres qu'ils ont achetés
(`Mes achats`), leur mettre en avant des livres susceptibles de leur plaire.

Piste technique proposée :

- un **embedding** calculé sur le résumé ou d'autres informations du livre ;
- une **base graphe** où chaque nœud est un livre portant son embedding, relié aux
  livres les plus proches ;
- l'embedding calculé **une seule fois** par livre, et les relations calculées **une
  fois**, à l'entrée ou à la sortie d'un livre, pour réduire au maximum le coût et la
  latence.

Consigne explicite : ne pas aller dans ce sens pour faire plaisir, **challenger au
maximum** ; une solution totalement différente est acceptable ; l'objectif est une
solution qui fonctionne à coût modéré.

### Réponse — résumé

Analyse complète : [`01-analyse-critique-idee-initiale.md`](01-analyse-critique-idee-initiale.md).

- ✅ **À garder** : un embedding calculé une fois par livre, hors du chemin de la
  requête ; des voisins **pré-calculés** pour une lecture instantanée.
- ❌ **À écarter** : la base graphe. Parcours à un seul saut sur environ 20 000 fiches :
  c'est une table SQL `BookNeighbors`. Une base de plus contredit les principes du
  projet et peut coûter jusqu'à ~65 $/mois sans aucun gain.
- ❌ **À corriger** : pas de recalcul à la sortie d'un livre. La similarité dépend du
  contenu, pas du stock ; la disponibilité se filtre à la lecture. À l'entrée, un
  recalcul complet périodique est plus simple et plus sûr qu'un recalcul incrémental.
- ⚠️ **Prémisse à vérifier** : le dépôt **ne stocke aucun résumé** et ne le demande à
  aucune source. Sans résumé, l'embedding apporte peu par rapport à des règles simples.
- ⚠️ **Signal rare** : `Mes achats` ne contient que les passages associés, et la
  fonction vient d'être livrée. Il faut aussi utiliser la liste de recherche et Ma
  sélection, et prévoir un repli.
- ⚠️ **RGPD** : c'est du profilage, une finalité nouvelle à documenter dans `09`.
  L'affichage dans le compte et l'envoi par e-mail n'ont pas la même base légale.
- 💶 **Coût IA** : ≈ 0,16 $ pour 20 000 fiches, puis quelques millièmes de dollar par
  mois. **Le coût réel est le temps de développement**, pas la facture Azure.

Recommandation provisoire : mesurer d'abord (taux de résumés, comptes ayant au moins un
signal), puis construire par couches — règles SQL, puis voisins sémantiques si la mesure
le justifie.

### Décisions

Aucune pour l'instant.

### Questions ouvertes

- **Q1** — Où et quand le membre voit-il ces suggestions, et dans quel but ?
  *(répondue à l'échange 2)*
- **Q2** — Quels signaux utiliser : achats seuls, ou aussi liste de recherche et Ma
  sélection ?
- **Q3** — Accepte-t-on une sonde de mesure (taux de résumés sur un échantillon d'ISBN
  réels, nombre de comptes ayant au moins un signal) avant de choisir l'approche ?
- **Q4** — Le compte Azure OpenAI de génération de titres
  (`TITLE_GENERATION_ENABLED`) est-il activé en production ? Il conditionne l'absence
  de nouvelle ressource.

---

## Échange 2 — 24 septembre 2026 : surfaces d'affichage, prix de Cosmos Gremlin, étude SQL ou graphe

### Demande

**Réponse à Q1 — deux surfaces :**

1. **Sous une fiche cliquée dans le catalogue**, pour tout visiteur, **même non
   connecté** : des livres proches de celui-ci.
2. **Pour un membre connecté**, sur la page d'accueil et dans son compte : une section
   de livres susceptibles de lui plaire, **à partir de ses anciennes lectures**.

**Nouvelle demande :** chiffrer Cosmos DB avec Gremlin pour le graphe, et mener une
**étude d'architecture** « voisins en base relationnelle ou en base graphe », en
dehors du prix et de la règle du dépôt. Le coût de développement et de maintenance est
accepté si le graphe est architecturalement meilleur. Il ne s'agit pas de viser le
graphe à tout prix.

### Réponse — résumé

Étude complète : [`02-etude-stockage-voisins-sql-ou-graphe.md`](02-etude-stockage-voisins-sql-ou-graphe.md).

- Correction de vocabulaire : la base est **Azure SQL Database** (SQL Server, S0), pas
  PostgreSQL.
- **Prix de Cosmos Gremlin en serverless** : ≈ 0,25 $ par million de RU, sans minimum.
  Pour notre volumétrie, entre quasi nul et ~15 $/mois selon la fréquence des
  reconstructions complètes. En débit provisionné, ≈ 23 $/mois minimum, ou 0 avec
  l'offre gratuite d'abonnement. **Le prix n'est pas l'argument décisif.**
- **Verdict architectural : la table relationnelle `BookNeighbors` (S1) est meilleure**,
  pour des raisons propres au besoin et pas seulement à cause de la règle du dépôt :
  - la requête est un saut, un agrégat, puis des filtres et jointures sur le stock,
    la visibilité et les possessions, qui vivent dans SQL et changent à chaque vente ;
  - avec Cosmos, il faut soit sur-lire puis filtrer dans SQL (deux allers-retours, en
    boucle un jour de bourse), soit recopier le stock dans le graphe (double écriture
    sans transaction) ;
  - SQL permet une reconstruction atomique (table de travail puis échange), pas Cosmos
    Gremlin ;
  - l'outillage .NET est très inégal : EF Core typé d'un côté, requêtes Gremlin en
    chaînes et pilote figé en 3.4.13 de l'autre.
- Si la sémantique graphe devient un jour utile : **SQL Graph** (tables `NODE`/`EDGE`,
  `MATCH`) existe **dans la même base Azure SQL**.
- Conséquence de la réponse à Q1 : la surface 1 (« livres similaires », anonyme) ne
  relève **pas** du profilage. Elle est utile dès le premier jour, sans attendre les
  achats, ce qui règle en grande partie le démarrage à froid. Seule la surface 2 relève
  de la finalité RGPD nouvelle.

### Décisions

- **D1** — Deux surfaces : « livres similaires » sous une fiche (tout public) et « pour
  vous » sur l'accueil et dans Mon compte (membre connecté).
- Stockage des voisins : **S1 recommandé, en attente de validation**.

### Questions ouvertes

- **Q2** — Pour « pour vous » : les achats seuls (« anciennes lectures »), ou aussi la
  liste de recherche et Ma sélection pour les membres sans achat associé ?
- **Q3** — Sonde de mesure (taux de résumés, comptes avec au moins un signal) avant de
  choisir entre règles simples et embeddings.
- **Q4** — Le compte Azure OpenAI (`TITLE_GENERATION_ENABLED`) est-il activé en
  production ?
- **Q5** — Validation du stockage S1. *(répondue à l'échange 3)*

---

## Échange 3 — 24 septembre 2026 : validation du stockage

### Demande

« Je valide le stockage des voisins dans une table SQL `BookNeighbors`. »

### Décisions

- **D2** — Les voisins sont stockés dans une **table relationnelle `BookNeighbors`** de
  la base Azure SQL existante (option S1 de
  [`02`](02-etude-stockage-voisins-sql-ou-graphe.md)). Pas de base graphe, pas de
  Cosmos DB. Elle est reconstruite de façon atomique par le worker (table de travail
  puis échange), et le stock est filtré à la lecture.

### Questions ouvertes

- **Q2** — Signaux de « pour vous » : achats seuls, ou aussi liste de recherche et Ma
  sélection ?
- **Q3** — Sonde de mesure avant de choisir la façon de calculer les voisins.
  *(répondue à l'échange 4)*
- **Q4** — Compte Azure OpenAI activé en production ?

---

## Échange 4 — 24 septembre 2026 : mesure des résumés

### Demande

Prendre des livres connus et vérifier si la BnF et Google Books ont des résumés. Ne pas
compter les membres ayant des achats : le site vient d'être mis en ligne et n'est pas
encore public, ce chiffre est donc nul et non pertinent.

### Réponse — résumé

Mesure complète : [`03-mesure-couverture-resumes.md`](03-mesure-couverture-resumes.md).

- 46 titres connus, 138 éditions prises dans le catalogue BnF.
- **Par édition** : résumé BnF 34 % (58 % pour les éditions 2017 et après, 14 % avant
  2007) ; Open Library 21 % ; **BnF ou Open Library 51 %**.
- **Par œuvre** (au moins une édition avec résumé) : **BnF ou Open Library 80 %**. Le
  résumé de l'œuvre suffit pour les recommandations, à condition de savoir regrouper
  les éditions.
- **Google Books non mesuré** : l'accès anonyme est refusé (quota à 0). Une clé est
  nécessaire.
- **Défaut existant découvert** : `BnfSruClient` ne cherche qu'en ISBN-13 et ne retrouve
  **aucune édition d'avant 2007** (0/55). Avec un repli ISBN-10, il les retrouve toutes
  (55/55). À corriger à part des recommandations.

### Décisions

- **D3** — Le nombre de membres ayant des achats n'entre pas dans les critères de
  décision : le site n'est pas encore public.

### Questions ouvertes

- **Q2** — Signaux de « pour vous » : achats seuls, ou aussi liste de recherche et Ma
  sélection ?
- **Q4** — Compte Azure OpenAI activé en production ?
- **Q6** — Clé Google Books pour terminer la mesure. *(posée)*
- **Q7** — Le défaut ISBN-10 de la BnF : correctif séparé, dans une autre branche ?
  *(répondue à l'échange 5)*

---

## Échange 5 — 24 septembre 2026 : correctif prioritaire du défaut ISBN-10

### Demande

Écrire le plan d'implémentation du repli ISBN-10 et le prompt à transmettre à Luna, avec
branche et worktree, car le correctif est prioritaire.

### Réponse — résumé

- Mesure complémentaire : l'index SRU **`bib.fuzzyISBN`**, déjà prévu par
  `technique/07-integrations-externes.md` §2, retrouve les **138 éditions sur 138** avec
  l'ISBN-13 seul. Le premier enregistrement porte l'ISBN demandé dans tous les cas.
  Aucun repli par un second appel n'est donc nécessaire : il suffit de changer d'index.
- Branche **`fix/bnf-isbn10-lookup`**, depuis `origin/main`, dans le worktree
  `C:\Users\florian.drevet\RiderProjects\Vole-Papillon-Damour-bnf-isbn10-lookup`.
- Plan commité sur cette branche :
  `docs/bourse-aux-livres/plan/07-recherche-bnf-isbn10.md`. Trois tâches : client de
  résolution, client de recherche de référence, puis vérification réelle et
  documentation.
- Hors périmètre, à décider : relire les fiches déjà `Resolved` par une autre source,
  ou `NotFound` après trois tentatives.

### Décisions

- **D4** — Le défaut ISBN-10 est corrigé dans une branche séparée, en priorité, en
  passant à l'index `bib.fuzzyISBN`, hors du chantier des recommandations.

---

## Échange 6 — 24 septembre 2026 : le résumé suffit-il ?

### Demande

« Pour trouver les livres les plus proches, est-ce que le résumé suffira ? Est-ce qu'il
y a d'autres infos qui pourraient être pertinentes ? »

### Réponse — résumé

Analyse complète : [`04-signaux-de-similarite.md`](04-signaux-de-similarite.md).

- **Non.** Le résumé dit de quoi parle un livre, pas pour qui ni sous quelle forme. Il
  ignore l'ordre des séries et fait remonter les autres éditions de la même œuvre.
  Exemple réel : les adaptations BD de *1984* ou des *Misérables* ont presque le résumé
  du roman.
- **Les notices BnF contiennent bien plus que le résumé.** Remplissage mesuré sur 138
  éditions : auteur 91 %, collection 61 %, identifiant d'œuvre (`500`) 41 %, série et
  tome (`461`) 34 % (71 % des BD), classement 73 %. Public et sujets sont rares.
- **Chaque information a un rôle** :
  - des **filtres** : jamais la même œuvre, même public, même langue ;
  - des **bonus** : tome suivant, même auteur, même forme ;
  - un **texte d'embedding composé** : titre, auteur, collection, genre, sujets,
    résumé.
- Les signaux de comportement (co-sélection, co-achat) viendront avec le trafic.

### Questions ouvertes

- **Q8** — Lancer une sonde de qualité : embedding des 138 éditions, avec et sans champs
  enrichis, puis lecture des voisins, avant d'écrire la spécification ? *(répondue à
  l'échange 7 : un benchmark complet)*

---

## Échange 7 — 24 septembre 2026 : benchmark complet et tutoriel Foundry

### Demande

Préparer un vrai benchmark complet de plusieurs méthodes de calcul de livres proches, avec
le modèle Azure OpenAI `text-embedding-3-small`. Préparer entièrement son exécution, avec
un vrai rapport lisible contenant remarques et conseils. Fournir un tutoriel pour
configurer Foundry avec le modèle, et dire ce qu'il faut pour l'utiliser.

### Réponse — résumé

- **Outil** : `tools/recommendation-benchmark/` (Python). Corpus réel de **158 œuvres et
  165 éditions** lues à la BnF et chez Open Library, annotées en 31 familles, avec des
  pièges volontaires (doubles éditions, adaptations BD, séries, publics voisins).
- **Onze méthodes** : règles seules, TF-IDF, quatre variantes d'embedding (titre, résumé
  d'édition, résumé d'œuvre, texte composé), filtres, bonus, 512 et 256 dimensions.
- **Critères de décision fixés avant les résultats** : [`05-benchmark.md`](05-benchmark.md) §5.
- **Premiers résultats, sans IA** :
  - règles seules : nDCG@5 0,216, parfaites sur les séries, mais **13 %** de couverture ;
  - TF-IDF : nDCG@5 0,316, mais renvoie l'autre édition du même livre dans 100 % des cas ;
  - le partage du résumé entre éditions fait passer sa couverture de 40 % à **89 %**.
- **Q4 résolue** : le compte Azure OpenAI existe (`francecentral`, clés désactivées). Il
  suffit d'y ajouter un déploiement :
  [`06-tutoriel-foundry-embeddings.md`](06-tutoriel-foundry-embeddings.md).
- Coût du benchmark : environ **0,001 $**.

### Décisions

- **D5** — Le choix de la méthode se fera sur le benchmark, selon les critères fixés à
  l'avance dans `05` §5.

### Questions ouvertes

- **Q9** — Déploiement et rôle Azure faits (tutoriel, étapes 2 à 6) ? Chemin du profil
  CLI, pour que je lance le benchmark et rédige l'analyse. *(répondue à l'échange 8)*
- **Q2** — Signaux de « pour vous » : achats seuls, ou aussi liste de recherche et Ma
  sélection ?

---

## Échange 8 — 24 septembre 2026 : déploiement Azure et exécution du benchmark

### Demande

« Je viens de m'az login, est-ce que tu peux faire le déploiement et faire le
benchmark ? »

### Actions réalisées dans Azure

Session : profil Azure CLI par défaut, abonnement « Florian - 15-07-2026 ».

1. Compte Foundry du projet identifié : **`vpd-actuality-title-dev`** (groupe
   `rg-vpd-dev`, `francecentral`, clés désactivées). Seul déploiement existant :
   `actuality-title`.
2. **Déploiement créé** : `text-embedding-3-small`, version 1, `GlobalStandard`,
   capacité 50 (50 000 tokens/min ; pas de coût fixe).
3. **Rôle attribué** : *Cognitive Services OpenAI User* au compte de Florian, sur ce
   seul compte Foundry. Le rôle *Owner* déjà détenu ne donne pas le droit d'appeler
   les modèles.
4. `.env.local` créé localement (endpoint et nom de déploiement, aucun secret, ignoré
   par git).

**Ces deux changements Azure ne sont pas décrits dans le Bicep.** Ils le seront si
l'approche est retenue (`06` §10).

### Réponse — résumé

Analyse complète : [`05-benchmark.md`](05-benchmark.md) §6 et §7.

- **Les embeddings gagnent nettement** : 0,586 de nDCG@5 (H6) contre 0,316 pour la
  meilleure méthode gratuite. L'écart est significatif.
- **Confirmé** :
  - le résumé de l'œuvre bat le résumé de l'édition ;
  - le texte composé bat le résumé seul ;
  - 512 dimensions suffisent, 256 dégradent ;
  - le tome suivant est trouvé dans 100 % des cas grâce au bonus ;
  - la fuite « même œuvre » passe de 100 % à 0 % grâce au filtre.
- **Découverte** : le filtre **strict** de public, appliqué sur un public déduit faux
  dans 11 % des cas, fait plus de mal que de bien. Une **pénalité** fait mieux (+0,022,
  significatif).
- **Critère non atteint** : précision@5 de 46 % pour 50 % visés. Il faut afficher
  « jusqu'à 5 voisins au-dessus d'un seuil », seuil à calibrer sur le vrai catalogue.
- Coût total du benchmark : **0,002 $**.

### Décisions

Aucune ; recommandation de méthode soumise à validation (`05` §7).

### Questions ouvertes

- **Q10** — Valider la méthode recommandée (`05` §7) pour passer à la spécification.
  *(posée)*
- **Q2** — Signaux de « pour vous ».
