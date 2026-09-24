# 07 — Spécification : recommandations de livres

> **24 septembre 2026.** Spécification issue des échanges 1 à 10
> ([`00-journal-des-echanges.md`](00-journal-des-echanges.md)) et du benchmark
> ([`05-benchmark.md`](05-benchmark.md) §7). Plan d'implémentation :
> [`08-plan-implementation.md`](08-plan-implementation.md).
>
> **Maquettes** : canvas Claude Design
> [Recommandations de livres](https://claude.ai/artifact/GYWEeUku7qofkTt5pdKtoM), exporté
> en HTML dans [`maquettes/`](maquettes/index.html) (et dans l'archive
> `maquettes/recommandations-maquettes-html.zip`). Chaque zone citée ci-dessous
> (`R1-carte`…) est un attribut `data-zone` de ces pages.

## 1. Ce qui est construit

| Surface | Public | Maquette |
|---|---|---|
| **« Dans le même esprit »**, sous la fiche d'un livre | Tout visiteur, connecté ou non | `maquettes/html/Main.html` (R1), `FicheMobile.html` (R2), `FicheEtats.html` (R3) |
| **« Pour vous »**, sur l'accueil | Membre connecté ayant des achats associés | `AccueilPourVous.html` (R4), `AccueilPourVousMobile.html` (R5), `PourVousEtats.html` (R8-C) |
| **« Pour prolonger vos lectures »**, en tête de Mes achats | Membre connecté | `MesAchatsPourVous.html` (R6), `PourVousEtats.html` (R8-A) |
| **Suggestions personnalisées**, préférence dans Compte et données | Membre connecté | `ComptePreferences.html` (R7), `PourVousEtats.html` (R8-B) |

Hors périmètre : e-mail de suggestions, signaux de comportement (co-sélection,
co-achat), écran d'administration des voisins, ISBNdb (abandonné, échange 10).

## 2. Méthode de calcul (validée par le benchmark, `05` §7)

### 2.1 Profil de similarité d'une édition

Pour chaque fiche canonique visible (`IsHiddenFromCatalog = 0`, `RedirectedToIsbn13
IS NULL`), un **profil** est lu à la BnF (`bib.fuzzyISBN`, UNIMARC) et chez Open Library :

| Donnée | Source |
|---|---|
| Titre, sous-titre, titre de partie et numéro (`200$a $e $i $h`) | BnF |
| Auteurs : `700`, `701`, et `702` si son rôle `$4` vaut `070` ou est absent | BnF |
| Collection (`225$a`) | BnF |
| Série et tome (`461$t`, `461$v`) | BnF |
| Identifiant d'œuvre (`500$3`) | BnF |
| Formes (`608$a`), sujets (`606$a` et `606$x`), public (`333$a`), mention CNLJ (tout `$2 = CNLJ`) | BnF |
| Langues (`101$a`) | BnF |
| Résumé de l'édition (`330$a`) | BnF |
| Identifiant d'œuvre et description (édition, sinon œuvre) | Open Library |

### 2.2 Déductions (jamais d'exclusion sur une déduction)

- **Clé d'œuvre et « même œuvre »** : deux éditions sont la même œuvre si elles
  partagent l'identifiant BnF `500$3`, ou l'identifiant d'œuvre Open Library, ou le même
  titre normalisé avec au moins un nom d'auteur commun. **Garde-fou** : deux tomes de
  numéros différents ne sont jamais la même œuvre.
- **Série** : `461$t` normalisé avec `461$v`, sinon titre normalisé avec `200$h`.
- **Public** : `jeunesse` (`333` ≤ 12 ans, mention CNLJ, collection ou éditeur jeunesse),
  `ado` (`333` ≥ 13 ans), sinon `adulte`. Le public retenu pour une œuvre est **le plus
  fréquent parmi ses éditions**.
- **Forme** : `illustre` (manga, BD, éditeurs de BD), `essai` (Dewey hors 8xx, ou
  sujets sans forme « roman »), sinon `texte`.

### 2.3 Résumé d'œuvre et texte vectorisé

- Résumé d'œuvre = le plus long résumé **nettoyé** parmi les éditions de l'œuvre (BnF),
  sinon la description Open Library. Nettoyage : 80 caractères minimum, 1 500 maximum,
  rejet des extraits (commençant par `«`, `"`, `“`, `—`, `-`), du titre recopié et des
  textes d'édition (« cette édition », « ce coffret », « les points forts »…).
- Texte vectorisé (le texte composé de H6, benchmark §6.2) :

  ```
  <titre>[, <partie>][ : <sous-titre>]. Auteur : <auteurs>. Collection : <collection>.
  Série : <série>, tome <n>. Genre : <formes>. Sujets : <sujets>. Public : <333>. <résumé d'œuvre>
  ```

  Chaque segment n'apparaît que s'il est renseigné.
- Modèle : `text-embedding-3-small`, **512 dimensions** (paramètre `dimensions`), sur le
  compte Foundry `vpd-actuality-title-dev`, déploiement `text-embedding-3-small`.
- Le vecteur n'est recalculé que si le texte change (empreinte SHA-256 du texte).

### 2.4 Voisins

Score d'un candidat C pour un livre Q :

```
score = cos(Q, C)
      + 0,20 si même série  (+ 0,10 de plus si C est le tome suivant de Q)
      + 0,08 si au moins un auteur commun
      + 0,03 si même forme déduite
      − 0,10 si publics déduits opposés (jeunesse / adulte)
```

- **Exclus** : Q lui-même et toute autre édition de la même œuvre.
- On conserve les **30 meilleurs** voisins de chaque livre, tous états de stock
  confondus. La disponibilité se filtre à la lecture (décision D2).
- **Raison** stockée avec chaque voisin, par priorité : `NextTome` > `SameSeries` >
  `SameAuthor` > `Theme`.
- Recalcul **complet** chaque nuit par le worker, écrit dans une nouvelle **génération** ;
  la bascule vers la nouvelle génération est atomique, puis l'ancienne est supprimée.

## 3. Règles d'affichage

### 3.1 « Dans le même esprit » (R1, R2, R3)

| Règle | Valeur |
|---|---|
| Nombre de livres | au plus **5** (`Recommendations:SimilarMaxCount`) |
| Voisins affichables | fiche canonique visible, **disponible ou annoncée**, score ≥ seuil (`Recommendations:SimilarMinScore`, 0,50 au départ, à calibrer) |
| Minimum pour afficher la section | **2** voisins (`Recommendations:SimilarMinCount`) ; sinon la section n'existe pas (R3-masquee) |
| Chargement | après la fiche, sans bloquer son rendu ; squelette pendant le chargement (R3-chargement) |
| Étiquette | une par carte, issue de la raison : « Tome suivant », « Même série », « Même auteur », « Proche par le thème » (R1-raison, R1-legende) |
| Carte | la carte d'accueil existante (`app-book-card`, variante `home`), avec pastille de disponibilité (R1-disponibilite) et étiquette |
| Mobile | défilement horizontal, cartes de 156 px (R2-defilement) |
| Données personnelles | aucune ; mention « sans aucune donnée personnelle » (R1-mention) |
| Erreur d'API | la section n'existe pas ; aucune erreur visible |

### 3.2 « Pour vous » (R4, R5, R6, R8)

| Règle | Valeur |
|---|---|
| Graines | lignes de passages **associés** du membre, non annulées, avec ISBN ; les **20** plus récentes |
| Score d'un candidat | somme, sur les graines, de `score(graine → candidat) × poids`, poids 1,0 pour l'achat le plus récent, décroissant linéairement jusqu'à 0,5 |
| Exclus | toute édition d'une œuvre déjà achetée ; les ISBN présents dans Ma sélection ; les livres non disponibles et non annoncés ; les voisins sous le seuil |
| Raison | graine qui contribue le plus : « La suite de *X* » si la raison est `NextTome`, sinon « Parce que vous avez aimé *X* » (R4-raison) |
| Accueil (R4) | 4 cartes desktop, défilement mobile (R5) ; section absente si moins de **3** suggestions, si la préférence est désactivée, ou pour un visiteur |
| Mes achats (R6) | 4 cartes compactes en tête ; sans achat associé : invitation à présenter sa carte (R8-sans-achat) ; préférence désactivée : bandeau absent |
| Lien | « Pourquoi ces suggestions ? » / « Gérer ces suggestions » → Compte et données (R4-pourquoi) |

### 3.3 Préférence (R7, R8-B)

- **Activée par défaut** (base : intérêt légitime, profilage à faible enjeu, sans effet
  juridique).
- Désactivable et réactivable en un clic (R7-bouton). Désactivée : l'API ne calcule
  rien et renvoie un état `Disabled` ; accueil et Mes achats n'affichent rien.
- Stockage : une ligne `MemberRecommendationPreferences` **n'existe que si le membre a
  changé la valeur** (même principe que `Watchlists`, `DT-14`). Supprimée en cascade
  avec le compte.
- Texte d'information (R7-explication) : seuls les achats associés servent ; calcul sur
  nos serveurs ; aucune donnée personnelle envoyée à un service extérieur ; aucun
  e-mail.

## 4. RGPD

- « Dans le même esprit » : aucune donnée personnelle, pas de profilage.
- « Pour vous » : profilage à faible enjeu. À faire avant ouverture :
  - ajouter la ligne « Suggestions personnalisées » au tableau des traitements de
    `docs/bourse-aux-livres/09-rgpd-compte-et-droits.md` (base : intérêt légitime ;
    opposition par la préférence) ;
  - mettre à jour la page publique « Vos données et le RGPD » du catalogue.
- **Aucune donnée personnelle ne part vers Azure OpenAI** : seuls les textes des notices
  bibliographiques sont vectorisés ; l'agrégation par membre se fait dans SQL.
- La suppression du compte supprime la préférence (cascade) ; les voisins ne contiennent
  aucune donnée personnelle.

## 5. Architecture

```
Worker, chaque nuit (Recommendations, 03:30 UTC)
  1. RefreshBookSimilarityProfilesCommand  ── BnF (fuzzyISBN) + Open Library ──► BookSimilarityProfiles
  2. RecomputeBookNeighborsCommand
       ├─ regroupe par œuvre, résumé d'œuvre, texte composé
       ├─ vectorise les textes changés ──► Azure OpenAI (text-embedding-3-small, 512)
       ├─ calcule les 30 voisins de chaque livre (en mémoire, TensorPrimitives)
       └─ écrit une génération (SqlBulkCopy) puis bascule RecommendationGenerations.Current

API (lecture seule, SQL)
  GET /catalog/books/{isbn13}/similar                 anonyme, cache public 5 min
  GET /catalog/me/recommendations                     membre, no-store
  GET|PUT /catalog/me/recommendations/preference      membre
```

Tables nouvelles, toutes dans la base Azure SQL existante (décision D2) :

| Table | Rôle |
|---|---|
| `BookSimilarityProfiles` | Profil lu aux sources, texte composé, empreinte, vecteur 512 × float32 (`varbinary(2048)`) |
| `BookNeighbors` | `(GenerationId, Isbn13, Rank)` → `NeighborIsbn13`, `Score`, `Reason` |
| `RecommendationGenerations` | Ligne unique : génération courante, date, nombre de livres |
| `MemberRecommendationPreferences` | `UserId` → `Enabled`, `UpdatedAt` |

Pas de type SQL `VECTOR` (disponibilité non confirmée sur le tier S0, et inutile : le
calcul se fait dans le worker).

## 6. Configuration

| Clé | Défaut | Rôle |
|---|---|---|
| `Recommendations:Enabled` | `false` | Interrupteur général (worker et API) |
| `Recommendations:Endpoint` | — | Point d'accès Azure OpenAI |
| `Recommendations:EmbeddingDeploymentName` | `text-embedding-3-small` | Déploiement |
| `Recommendations:Dimensions` | `512` | Dimension des vecteurs |
| `Recommendations:NeighborsPerBook` | `30` | Voisins conservés |
| `Recommendations:ProfileBatchSize` | `200` | Profils rafraîchis par nuit |
| `Recommendations:SimilarMinScore` | `0.50` | Seuil d'affichage |
| `Recommendations:SimilarMaxCount` / `SimilarMinCount` | `5` / `2` | Fiche |
| `Recommendations:PersonalMaxCount` / `PersonalMinCount` | `8` / `3` | Pour vous |

## 7. Critères d'acceptation

1. Sur la fiche de *Les hommes qui n'aimaient pas les femmes*, *La fille qui rêvait d'un
   bidon d'essence et d'une allumette* apparaît en premier avec l'étiquette « Tome
   suivant », si elle est disponible ou annoncée.
2. Aucune fiche ne propose une autre édition du même livre.
3. Un livre épuisé et non annoncé n'est jamais proposé.
4. Moins de 2 voisins affichables : la section « Dans le même esprit » est absente.
5. Un visiteur anonyme voit « Dans le même esprit » et jamais « Pour vous ».
6. Un membre sans achat associé ne voit pas « Pour vous » sur l'accueil et voit
   l'invitation dans Mes achats.
7. Un membre qui désactive les suggestions ne voit plus rien sur l'accueil ni dans Mes
   achats, et l'API renvoie `Disabled` ; réactiver les rétablit.
8. Aucun texte envoyé à Azure OpenAI ne contient d'identifiant de membre (vérifiable par
   revue du code : seuls les textes de profil sont transmis).
9. Mobile 390 px : pas de défilement horizontal de la page ; seules les rangées de cartes
   défilent (R2, R5).
10. Le seuil `SimilarMinScore` est calibré sur le vrai catalogue par une relecture de 50
    fiches avant l'ouverture publique (suivi, pas un test automatique).
