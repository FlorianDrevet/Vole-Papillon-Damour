# Performance de la recherche publique et du portail d'administration — spécification fonctionnelle

> Module étendu : `docs/bourse-aux-livres/` (`04-site-public.md`, `05-administration.md`,
> `07-exigences-non-fonctionnelles.md`). Les règles nouvelles prennent la suite de la
> numérotation existante, à partir de `RG-155`.

## 1. Contexte

Deux surfaces sont devenues lentes au point de gêner l'usage courant.

**Recherche publique (`/recherche`, `/catalogue`).** Filtrer par un genre fréquent
(« Roman », environ 3 700 fiches sur 16 500) prend 3 à 4 s, sur la page 1 comme sur les
suivantes. Sans filtre, le même écran répond en 0,2 à 0,4 s. L'exigence `ENF-08` (résultats
en moins d'une seconde sur 15 000 titres) n'est plus tenue.

**Portail d'administration (`/administration`).** L'ouverture du tableau de bord prend
environ 24 s. Les pastilles chiffrées de la navigation arrivent tard. Afficher les
5 fiches du catalogue prend 1 à 6 s. Les statistiques prennent 7 s et plus.

### Constats mesurés (App Insights de production, 14 novembre 2026, et reproduction locale)

| Surface | Mesure | Constat |
|---|---|---|
| Recherche, genre Roman, disponibles, tri récent | 3,1 à 3,9 s | Un plan d'exécution SQL compilé pour un genre rare est réutilisé pour un genre fréquent. Il lit toutes les fiches du genre puis les trie, et déborde sur disque : 31 500 lectures au lieu d'environ 1 400. |
| Recherche, même requête, toutes disponibilités ou tri pertinence | 0,25 à 0,4 s | Même volume, autre plan : la lenteur ne vient pas des données. |
| Ouverture du tableau de bord | 24 s, 27 appels | Chargements en série ; tout le portail est initialisé deux fois ; chargement de sections non affichées. |
| Indicateurs du tableau de bord | 5 à 10 s | Une quinzaine de calculs en série, dont l'écart d'inventaire et les titres jamais vendus, qui relisent tout l'historique des mouvements. |
| Pastilles de navigation | 1 à 6 s chacune, demandées deux fois | Chaque nombre est obtenu en chargeant une page de liste complète. |
| Catalogue admin, 5 fiches | 1 à 6 s | Comptage complet du catalogue et lecture des fiches entières à chaque affichage. |
| Statistiques (bénévole, bénévoles, flux, bourse) | 6 à 12 s | Des tables entières sont chargées en mémoire pour calculer quelques totaux. |
| État ISBNdb | 1,7 à 7 s | Tous les relevés ISBNdb sont relus à chaque ouverture. |
| API | mémoire 443 Mo en médiane pour 512 Mo | Conteneur 0,25 vCPU ; la base S0 atteint 87 % de sa capacité pendant l'ouverture du tableau de bord. |

## 2. Objectifs

1. Revenir sous `ENF-08` : la recherche publique, avec ou sans filtre de genre, répond en
   moins d'une seconde.
2. Le tableau de bord devient utilisable en moins de 2 s : la structure s'affiche
   immédiatement, chaque bloc apparaît dès que sa donnée arrive.
3. Les pastilles de navigation s'affichent en moins de 1 s, une seule fois par ouverture.
4. Chaque espace de travail du portail ne charge que ce qu'il affiche.
5. Plus aucun écran d'administration ne dépasse 2 s en usage courant, statistiques
   comprises.
6. Retirer du tableau de bord quatre indicateurs jugés non pertinents par le responsable :
   « Écart d'inventaire », « E-mails en attente », « Rares à expertiser » et « Fiches sans
   métadonnées » (`RG-168`).

## 3. Hors périmètre

- Changer le contenu des autres indicateurs, des listes ou des statistiques : mêmes
  chiffres, mêmes libellés, mêmes règles de calcul (`05-administration.md` §1 à §4).
- Les espaces de travail liés aux indicateurs retirés restent en place : la section
  Alertes, l'espace Livres rares et la file ISBN ne changent pas.
- Refondre visuellement le portail. Seuls les états de chargement changent.
- La recherche plein texte « par mot entier » ou tolérante aux fautes.
- Le BackOffice historique (`src/BackOffice`) : il garde son comportement actuel, sauf le
  retrait des quatre mêmes cartes de son tableau de bord, qui partage le contrat d'API
  (`RG-168`).
- Le dimensionnement Azure (taille du conteneur API, niveau de la base). Il est traité à
  part, par décision du responsable, sans changement dans cette feature (voir §10).

## 4. Acteurs

- **Visiteur** du site public : cherche et filtre des livres.
- **Administrateur** : utilise le tableau de bord, les files de travail, le catalogue et les
  statistiques.
- **Bénévole livres rares** : accède aux espaces Livres rares et Signalements.
- **Bénévole tri ou caisse** : consulte ses statistiques personnelles.

## 5. Parcours

### P1 — Le visiteur filtre par genre et feuillette

1. Le visiteur choisit « Roman » dans la liste des genres.
2. Les résultats précédents restent visibles et s'estompent ; un indicateur discret signale
   le chargement.
3. Les résultats filtrés apparaissent en moins d'une seconde.
4. Il passe à la page suivante : elle s'affiche quasi instantanément, car elle a été
   préparée pendant qu'il lisait la page courante.
5. S'il change de filtre avant la fin d'un chargement, la demande précédente est abandonnée
   et seul le dernier choix compte.

### P2 — L'administrateur ouvre le portail

1. Il ouvre `/administration`. La navigation et la structure du tableau de bord
   s'affichent tout de suite, avec un emplacement de chargement par bloc et par pastille.
2. Les blocs se remplissent un à un, sans ordre imposé.
3. Les pastilles de la navigation apparaissent ensemble.
4. L'indicateur « Titres jamais vendus » affiche l'heure de son dernier calcul.
5. Il passe à « Catalogue » : seule cette liste est chargée. Le tableau de bord n'est pas
   rechargé à son retour s'il a moins d'une minute.

### P3 — L'administrateur feuillette le catalogue

1. Il ouvre « Catalogue » ; les 5 fiches s'affichent en moins d'une seconde.
2. Il change de page ou lance une recherche : la liste précédente reste visible et estompée
   jusqu'à l'arrivée de la suivante.

### P4 — Un bénévole ou un administrateur ouvre des statistiques

1. Il ouvre un onglet de statistiques : la structure de l'onglet s'affiche tout de suite,
   avec des emplacements de chargement.
2. Les chiffres arrivent en moins de 2 s.

## 6. Écrans

| Id | Écran | Nature | Écran d'origine |
|---|---|---|---|
| E1 | Tableau de bord d'administration — états de chargement par bloc, heure de calcul de « Titres jamais vendus », retrait de quatre cartes | *modifié* | `/administration/overview`, maquette `catalogue/AdminTableauDeBord.dc.html` |
| E2 | Navigation du portail d'administration — pastilles en chargement (barre latérale desktop et sélecteur mobile) | *modifié* | Navigation de `/administration` |
| E3 | Listes de travail d'administration — chargement initial et rechargement (catalogue, file ISBN, hors ISBNdb, sessions) | *modifié* | `/administration/catalogue`, maquette `catalogue/AdminCatalogue.dc.html` |
| E4 | Statistiques — onglet en chargement | *modifié* | `/administration/statistics`, `catalogue/AdminBourse.dc.html` |
| E5 | Recherche publique — résultats en cours de rafraîchissement | *modifié* | `/recherche` |

Pour chaque écran, seuls les états de chargement changent, plus le retrait des quatre cartes
sur E1. Les écrans chargés gardent sinon leur présentation actuelle à l'identique.

## 7. Règles métier

### `RG-155` — La recherche publique tient `ENF-08` quel que soit le filtre
Une recherche, avec ou sans genre, disponibilité, tri ou page, répond en moins d'une
seconde côté serveur sur le catalogue de production. Le temps ne dépend pas de l'ordre
dans lequel les genres ont été demandés.

### `RG-156` — Les résultats affichés restent visibles pendant un rafraîchissement
Pendant le chargement d'une nouvelle page, d'un nouveau filtre ou d'une nouvelle recherche,
la liste précédente reste affichée et estompée, avec un indicateur discret. L'écran ne
repasse au squelette complet que lors du tout premier chargement.

### `RG-157` — Seule la dernière demande compte
Si l'utilisateur change de filtre, de page ou de recherche avant la fin d'un chargement,
la demande précédente est abandonnée et son résultat n'est jamais affiché.

### `RG-158` — La page suivante est préparée à l'avance
Sur la recherche publique, la page suivante est préparée dès l'affichage de la page
courante, si elle existe. Une page déjà affichée dans la minute réapparaît sans attente
au retour arrière.

### `RG-159` — Le portail ne charge que la section affichée
À l'ouverture et à chaque changement de section, le portail ne charge que les données de
la section visible et les pastilles de navigation. Une section rouverte dans la minute
réaffiche ses données sans attendre, puis les rafraîchit en arrière-plan.

### `RG-160` — Le portail s'initialise une seule fois
Ouvrir `/administration`, puis passer d'une section à l'autre, ne réinitialise jamais tout
le portail. Aucune donnée n'est demandée deux fois pour le même affichage.

### `RG-161` — Chaque bloc s'affiche dès que sa donnée est prête
Le tableau de bord et les onglets de statistiques montrent leur structure immédiatement.
Chaque bloc a son propre état : chargement (squelette), chargé, erreur avec « Réessayer ».
L'échec d'un bloc n'empêche pas l'affichage des autres. Un bouton d'action n'est
désactivé que pendant sa propre action, pas pendant le chargement d'un autre bloc.

### `RG-162` — Les pastilles arrivent ensemble et vite
Les nombres de la navigation (Catalogue, ISBN en cours, Hors ISBNdb, Livres rares) sont
obtenus en une seule demande, qui répond en moins d'une seconde. Leur valeur peut avoir
jusqu'à une minute de retard. Elle est mise à jour juste après une action de
l'administrateur qui la modifie (correction de notice, ajout, retrait, publication).
Pendant le chargement, chaque pastille montre un emplacement neutre, jamais « 0 ».

### `RG-163` — « Titres jamais vendus » peut être calculé à l'avance
Les titres disponibles jamais vendus depuis leur première mise à disposition peuvent être
calculés en arrière-plan plutôt qu'à l'ouverture. La valeur a au plus 15 minutes de retard,
sans bouton de recalcul. Le bloc indique « Calculé à HH:MM » (heure de Paris). Si aucun
calcul n'a encore eu lieu, le bloc l'indique au lieu d'afficher zéro. Les seuils
d'ancienneté et de quantité restent ceux des paramètres de l'association (`05` §9).

### `RG-164` — Les autres indicateurs du tableau de bord restent à jour
Stock disponible et annoncé, activité de tri, ventes de la dernière bourse et fiches hors
ISBNdb à compléter reflètent l'état réel. Un retard d'au plus une minute est accepté.

### `RG-165` — Les mêmes chiffres qu'avant
Pour un même état de la base, chaque indicateur conservé, chaque liste et chaque
statistique affiche exactement la même valeur qu'avant cette feature, à l'exception du
décalage permis par `RG-162`, `RG-163` et `RG-164`.

### `RG-166` — Les statistiques répondent en moins de 2 s
Statistiques personnelles du bénévole, statistiques des bénévoles, flux du catalogue et
statistiques de bourse répondent en moins de 2 s sur les données de production, quelle que
soit la période ou la bourse choisie.

### `RG-167` — Les listes d'administration répondent en moins d'une seconde
Catalogue, file ISBN, fiches hors ISBNdb, sessions et livres rares affichent une page en
moins d'une seconde, avec ou sans recherche simple, sur les données de production.

### `RG-168` — Quatre indicateurs quittent le tableau de bord
Les cartes « Écart d'inventaire », « E-mails en attente », « Rares à expertiser » et
« Fiches sans métadonnées » disparaissent du tableau de bord du portail Catalog et du
BackOffice. Leur valeur n'est plus calculée. La grille des cartes restantes se
recompose sans emplacement vide. Les pastilles de navigation, la section Alertes, l'espace
Livres rares et la file ISBN, qui donnent accès au même travail, sont conservés.

## 8. Critères d'acceptation

Mesures prises en production après déploiement, ou à défaut sur une base locale peuplée
comme la production (16 500 fiches visibles, 28 genres, Roman environ 23 %).

- **CA-01** — La recherche `genre=Roman`, disponibles, tri récent, page 1 puis page 5, répond
  côté serveur en moins de 1 s, y compris juste après une recherche sur un genre à une seule
  fiche (`RG-155`).
- **CA-02** — Sur la recherche publique, changer de page affiche la page suivante sans
  attente perceptible quand elle a été préparée (`RG-158`). Pendant un rafraîchissement, les
  résultats précédents restent visibles (`RG-156`).
- **CA-03** — Ouvrir `/administration` déclenche au plus 8 appels API, sans aucun doublon
  (`RG-159`, `RG-160`).
- **CA-04** — Le tableau de bord montre sa structure en moins de 300 ms après la connexion,
  et tous ses blocs sont remplis en moins de 2 s (`RG-161`).
- **CA-05** — Les quatre pastilles apparaissent en moins de 1 s, via une seule demande
  (`RG-162`).
- **CA-06** — Le bloc « Titres jamais vendus » affiche l'heure de son calcul, à moins de
  15 min de l'heure courante (`RG-163`).
- **CA-07** — Pour un jeu de données figé, chaque indicateur conservé, liste et statistique
  donne la même valeur avant et après (`RG-165`). Des tests automatisés le vérifient.
- **CA-11** — Les quatre cartes retirées n'apparaissent plus ni dans le portail Catalog ni
  dans le BackOffice, et aucun calcul correspondant n'est exécuté à l'ouverture (`RG-168`).
- **CA-08** — Chaque endpoint d'administration a un p90 inférieur à 1 s en production sur
  7 jours, statistiques inférieures à 2 s (`RG-166`, `RG-167`).
- **CA-09** — Pendant l'ouverture du tableau de bord, la base de données reste sous 50 % de
  sa capacité et la mémoire de l'API sous 70 % de sa limite actuelle.
- **CA-10** — Un bloc en erreur affiche « Réessayer » sans bloquer les autres ; un bouton
  n'est désactivé que pendant sa propre action (`RG-161`).

## 9. Contraintes

- **Maintenabilité (`ENF-24`)** : pas de trigger ni de procédure stockée maintenue à la main
  pour tenir des compteurs. Un calcul périodique hébergé par le Worker existant est préféré.
- **Coût (`ENF-23`)** : la feature ne doit pas exiger de ressource Azure supplémentaire.
- **Données** : aucune perte, aucun changement de règle de calcul. Les migrations sont
  additives.
- **Confidentialité** : l'exclusion des fiches en cours de scan du catalogue public reste
  stricte, sans délai (règle actuelle de visibilité).
- **Compatibilité** : le BackOffice historique appelle certains des mêmes endpoints ; leurs
  contrats existants restent valides.
- **Base de comparaison** : les mesures avant/après et la méthode de reproduction locale
  (seed synthétique, plans d'exécution) sont décrites dans `02-plan.md`.

## 10. Recommandations hors code (décision du responsable)

- Le conteneur API (0,25 vCPU, 512 Mo) frôle sa limite mémoire. Passer à 0,5 vCPU / 1 Go
  coûte environ le double de l'actuel, soit quelques euros par mois. C'est le levier le plus
  sûr, à décider indépendamment de la feature.
- La base est déployée en S0 (10 DTU), alors que `NEXT.md` (L0-7, 2 septembre 2026) indique un
  passage en S1. L'écart entre Bicep et Azure est à vérifier. La capacité est à réévaluer
  **après** la feature, quand les parcours complets de tables auront disparu.

## 11. Décisions (15 novembre 2026)

1. « Titres jamais vendus » : retard d'au plus 15 minutes, sans bouton de recalcul
   (`RG-163`).
2. Pastilles et indicateurs courants : retard d'au plus une minute (`RG-162`, `RG-164`).
3. Statistiques personnelles du bénévole (`/scan/me/statistics`, application Scan) :
   incluses, côté serveur seulement, sans changement d'écran dans Scan (`RG-166`).
4. Retrait des cartes « Écart d'inventaire », « E-mails en attente », « Rares à
   expertiser » et « Fiches sans métadonnées », jugées non pertinentes (`RG-168`).

## 12. Questions ouvertes

Aucune.
