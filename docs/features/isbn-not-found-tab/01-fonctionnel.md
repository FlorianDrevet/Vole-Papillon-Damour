# ISBN non trouvés — onglet dédié, export et import CSV

Statut : brouillon à relire · Langue : français · Périmètre : portail d'administration Catalog
(`/administration/…`), rôle Administrateur.

## 1. Contexte

L'administration contient aujourd'hui, dans « Le fonds de livres » :

- **ISBN en cours d'analyse** (`/administration/isbn-queue`) : file des ISBN dont la notice
  n'est pas encore exploitable. Filtres *Toutes · En cours · Échecs · Notices non trouvées*.
- **Livres hors ISBNdb** (`/administration/isbndb-absent`) : livres qu'ISBNdb ne connaît pas
  et dont la fiche vient d'une source de secours (BnF, Open Library, Google Books). Elle
  affiche aussi des lignes « Titre inconnu » : ISBN qu'aucune source n'a su décrire.

Les « notices non trouvées » (aucune source — ISBNdb, BnF, Open Library, Google Books — ne
connaît l'ISBN) se retrouvent donc mélangées à des ISBN en attente ou en erreur, et
apparaissent en double dans « Livres hors ISBNdb » sous le titre « Titre inconnu ». Elles ne
peuvent être réglées qu'à la main, une fiche à la fois, ce qui n'est pas tenable sur un lot
important.

La navigation latérale porte par ailleurs des pastilles de comptage (Catalogue, ISBN en cours
d'analyse, Livres hors ISBNdb, Livres rares, Sessions, etc.) qui s'affichent même à 0, et dont
certaines ne sont pas utiles.

## 2. Objectifs

1. Donner aux ISBN sans notice trouvée un **onglet dédié**, « ISBN non trouvés », séparé de
   la file d'analyse.
2. Permettre de **compléter ces fiches en lot** : exporter un fichier CSV pré-rempli avec les
   ISBN, le faire remplir par un agent (Codex ou Claude), puis le réimporter.
3. **Clarifier la file d'analyse** (renommage, filtres, pastille) et **épurer « Livres hors
   ISBNdb »**.
4. **Alléger la navigation** : pastilles masquées à 0, pastilles Catalogue et Livres rares
   supprimées.

## 3. Hors périmètre

- Appeler soi-même un agent IA depuis le site : le remplissage du fichier se fait hors du
  site, par l'administrateur.
- Modifier la façon dont les notices sont cherchées (ordre ISBNdb → BnF → Open Library →
  Google Books) ou relancées automatiquement.
- Import de couvertures en fichier image : seule une **adresse web** de couverture est
  acceptée dans le CSV.
- Modification d'un livre déjà doté d'une notice par import CSV (l'import ne touche que les
  ISBN de l'onglet).
- Pastilles des autres rubriques que celles citées (voir RG-09 à RG-13 pour l'exact
  périmètre).
- Rôle « Gestionnaire de livres rares » : sa navigation ne change pas.

## 4. Acteurs

- **Administrateur** : seul acteur de ces écrans.
- **Agent externe (Codex / Claude)** : remplit le fichier CSV, hors du site. Il n'a aucun accès
  au site.

## 5. Parcours

**P1 — Traiter un lot d'ISBN non trouvés**
1. L'administrateur ouvre « ISBN non trouvés » et consulte la liste.
2. Il clique sur **Exporter en CSV** : un fichier contenant tous les ISBN de la liste (pas
   seulement la page affichée) est téléchargé, avec toutes les colonnes de fiche vides.
3. Il remet le fichier à un agent qui renseigne les colonnes.
4. De retour sur le site, il clique sur **Importer un CSV** et dépose le fichier complété.
5. Le site **analyse le fichier sans rien enregistrer** et affiche un aperçu : nombre de
   fiches prêtes, lignes ignorées, lignes en erreur avec leur motif.
6. L'administrateur confirme. Les fiches valides sont enregistrées ; un résumé indique ce qui a
   été fait. Les livres complétés quittent l'onglet.

**P2 — Compléter une fiche à la main**
Depuis une ligne de l'onglet, **Compléter →** ouvre la fiche existante (formulaire de
correction). Au retour, l'administrateur revient sur « ISBN non trouvés ».

**P3 — Surveiller la file d'analyse**
La pastille de « Analyse des ISBNs » indique en permanence le nombre d'**échecs**, quel que
soit le filtre choisi dans la page.

## 6. Écrans

| Id | Écran | Nature | Origine |
|---|---|---|---|
| **E1** | Analyse des ISBNs | *modifié* | « ISBN en cours d'analyse » (`/administration/isbn-queue`) |
| **E2** | ISBN non trouvés (liste, export, import) | *nouveau* | même grammaire que E1 et « Livres hors ISBNdb » |
| **E3** | Import CSV : aperçu puis résumé | *nouveau* | panneau d'import ISBN existant (aperçu avant écriture) |
| **E4** | Navigation latérale (et sélecteur mobile) | *modifié* | navigation de l'administration |
| **E5** | Livres hors ISBNdb | *modifié* | `/administration/isbndb-absent` |

### E1 — Analyse des ISBNs (modifié)
- Titre de page, entrée de navigation, fil d'Ariane de la fiche et libellés associés :
  **« Analyse des ISBNs »** (ancien « ISBN en cours d'analyse »).
- Le filtre **« Notices non trouvées » disparaît**. Restent *Toutes · En cours · Échecs*.
- Le texte d'introduction ne parle plus de notices non trouvées ; il renvoie vers l'onglet
  « ISBN non trouvés » pour ces cas.
- « Toutes » n'inclut plus les ISBN sans notice trouvée.
- Les états vides, les actions par ligne (*Corriger, Réessayer, Supprimer*) et la pagination
  sont conservés.

### E2 — ISBN non trouvés (nouveau)
- En-tête de page dans la grammaire de l'administration (kicker « Le fonds de livres »,
  titre « ISBN non trouvés. », phrase d'introduction).
- Compteur « N ISBN sans notice trouvée ».
- Barre d'actions : **Exporter en CSV** et **Importer un CSV**, plus un lien d'aide
  « Format du fichier » qui déplie la description des colonnes à transmettre à l'agent.
- Liste paginée : pour chaque ISBN, son motif / date du dernier essai / nombre de tentatives,
  et les actions **Compléter →**, **Réessayer**, **Supprimer**.
- États : vide, chargement, erreur de chargement.

### E3 — Import CSV (nouveau)
- Zone de dépôt (ou sélection) d'un fichier `.csv`.
- **Aperçu** avant enregistrement : nombre de lignes lues, fiches prêtes à enregistrer,
  lignes ignorées (non renseignées), lignes en erreur avec numéro de ligne, ISBN et motif.
- Boutons **Annuler** et **Enregistrer N fiches**.
- **Résumé** après enregistrement : fiches enregistrées, ignorées, rejetées.
- États : fichier illisible / mauvais format, aucune ligne valide, erreur d'enregistrement.

### E4 — Navigation latérale (modifié)
- Entrée **« Analyse des ISBNs »** (renommée), nouvelle entrée **« ISBN non trouvés »** placée
  juste après, puis « Livres hors ISBNdb ».
- Pastilles : voir RG-09 à RG-13.
- Le sélecteur de rubrique en affichage mobile reprend les mêmes libellés et valeurs.

### E5 — Livres hors ISBNdb (modifié)
- Plus aucune ligne « Titre inconnu ». Les compteurs *À compléter · N* et *Complétées · N*
  reflètent uniquement les livres affichés.

## 7. Règles métier

**Classement des ISBN**

- **RG-01** — Un ISBN est dit **« non trouvé »** lorsque aucune source de notice
  (ISBNdb, BnF, Open Library, Google Books) n'a pu le décrire et qu'aucun administrateur ne
  l'a complété. Il apparaît dans E2, et seulement là.
- **RG-02** — Les ISBN « non trouvés » n'apparaissent plus dans E1, quel que soit le filtre,
  y compris « Toutes ».
- **RG-03** — Les ISBN rejetés par l'administrateur (supprimés) n'apparaissent dans aucun de
  ces onglets, comme aujourd'hui.
- **RG-04** — **Livres hors ISBNdb** (E5) ne liste que les livres absents d'ISBNdb **qui ont
  une fiche exploitable** (au moins un titre, venant d'une source de secours ou d'une saisie
  manuelle). Les ISBN sans titre en sont exclus.
- **RG-05** — Un livre « non trouvé » qui est complété quitte E2 et entre dans « Livres hors
  ISBNdb » (E5) : **à la main**, comme tout autre livre absent d'ISBNdb « à compléter » ;
  **par import CSV**, directement dans l'état « complété » (RG-20).
- **RG-06** — Les compteurs de l'application qui annoncent « livres hors ISBNdb à compléter »
  (carte d'état ISBNdb, tableau de bord) comptent selon la même définition que E5 (RG-04),
  pour que les chiffres concordent.

**Export**

- **RG-07** — L'export contient **tous** les ISBN « non trouvés » (pas seulement la page
  affichée), un par ligne, dans la **première colonne**. Toutes les autres colonnes de fiche
  sont présentes dans l'en-tête et **vides**.
- **RG-08** — Le fichier est compatible Excel et agents : séparateur point-virgule, encodage
  UTF-8 avec marque d'ordre des octets, première ligne = en-têtes, nom
  `isbn-non-trouves-AAAA-MM-JJ.csv`. Même convention que l'import « Nombre_de_photos » existant.
  S'il n'y a aucun ISBN « non trouvé », le bouton d'export est désactivé.

  Colonnes (ordre fixe) :

  | # | En-tête | Contenu attendu |
  |---|---|---|
  | 1 | `ISBN` | ISBN-13 (pré-rempli) |
  | 2 | `Titre` | texte, 500 caractères max. **Obligatoire** pour qu'une ligne soit enregistrée |
  | 3 | `Auteurs` | texte, 500 max. |
  | 4 | `Editeur` | texte, 200 max. |
  | 5 | `Annee_publication` | entier 1–9999 |
  | 6 | `Format` | texte, 50 max. (ex. poche, grand format) |
  | 7 | `Langue` | code langue, 10 max. (ex. `fr`) |
  | 8 | `Genre_principal` | texte, 100 max. |
  | 9 | `Genres` | plusieurs genres séparés par ` \| `, 100 max. chacun |
  | 10 | `Resume` | texte, 20 000 max. |
  | 11 | `Pages` | entier 1–100 000 |
  | 12 | `Edition` | texte, 100 max. |
  | 13 | `Couverture_URL` | adresse web de l'image de couverture |

**Navigation**

- **RG-09** — Une pastille de comptage **ne s'affiche pas** quand sa valeur est 0 (ni vide, ni
  « 0 »). Cela vaut pour toutes les rubriques, dans la navigation latérale **et** dans le
  sélecteur mobile (le libellé n'affiche alors pas de nombre entre parenthèses).
- **RG-10** — Les pastilles **Catalogue** et **Livres rares** sont supprimées, quelle que soit
  leur valeur.
- **RG-11** — La pastille **Analyse des ISBNs** affiche **uniquement le nombre d'échecs**,
  en permanence, indépendamment du filtre (*Toutes / En cours / Échecs*) sélectionné dans la
  page. Changer de filtre ne modifie pas la pastille.
- **RG-12** — La pastille **ISBN non trouvés** affiche le nombre d'ISBN restant à compléter
  (RG-01).
- **RG-13** — La pastille **Livres hors ISBNdb** affiche le nombre de livres « à compléter »
  de E5 (RG-04). Les pastilles Sessions, Livres introuvables et Demandes manuelles restent
  inchangées hors RG-09.

**Import**

- **RG-14** — L'import se fait en **deux temps** : analyse du fichier sans écriture (aperçu),
  puis confirmation par l'administrateur. Rien n'est enregistré avant la confirmation.
- **RG-15** — L'en-tête doit reprendre les 13 colonnes de RG-08 (ordre libre, noms exacts,
  sans tenir compte de la casse ni des accents). Sinon, le fichier est refusé avec la liste des
  colonnes manquantes ou inconnues.
- **RG-16** — **Pour chaque ligne**, l'ISBN doit : être un ISBN valide (ISBN-10 accepté, converti
  en ISBN-13), **correspondre à un livre actuellement « non trouvé »** (RG-01). Sinon la ligne
  est **rejetée** avec son motif : *ISBN invalide*, *ISBN inconnu*, *livre déjà doté d'une
  notice* (existe mais n'est pas dans E2), *ISBN en double dans le fichier*.
- **RG-17** — Une ligne dont toutes les colonnes de fiche sont vides est **ignorée** (pas une
  erreur) : le livre reste dans E2. Une ligne partiellement remplie **sans titre** est
  rejetée (« titre manquant »).
- **RG-18** — Chaque valeur est contrôlée selon le tableau de RG-08 (longueurs, entiers, plage
  d'année et de pages, adresse de couverture valide). Une valeur invalide rejette **toute la
  ligne**, avec le nom de la colonne fautive.
- **RG-19** — Les lignes valides sont enregistrées **même si d'autres lignes sont rejetées** ;
  le résumé liste les rejets pour que l'administrateur corrige le fichier et le redépose. Un
  fichier redéposé ne crée pas de doublon : une ligne déjà enregistrée est rejetée
  « livre déjà doté d'une notice ».
- **RG-20** — Une fiche enregistrée par import est traitée comme une **fiche saisie à la main** :
  elle sort de E2, n'est plus remplacée par une recherche automatique, et apparaît avec
  la source « manuelle ». Elle est en plus **marquée « complétée » d'office** dans E5 (onglet
  *Complétées*), au nom de l'administrateur qui a confirmé l'import, puisque le fichier est
  censé contenir toutes les informations. Exception : si la ligne n'a pas d'**auteur**, la
  fiche est enregistrée mais reste « à compléter » dans E5, comme pour une saisie manuelle
  (un livre sans titre ni auteur ne peut pas être marqué complété). Le résumé d'import (E3)
  indique combien de fiches sont ainsi restées « à compléter ».
- **RG-21** — Si l'état d'un livre change entre l'aperçu et la confirmation (par ex. complété
  entre-temps), la ligne est rejetée au moment de l'enregistrement, avec le motif correspondant ;
  les autres lignes ne sont pas affectées.
- **RG-22** — Taille maximale du fichier et nombre maximal de lignes : limites raisonnables
  annoncées dans l'écran (voir Questions ouvertes).
- **RG-23** — Seul un administrateur voit E2 et peut exporter/importer ; la contrainte vaut côté
  serveur, pas seulement dans l'interface.

## 8. Contraintes

- Réutiliser la convention visuelle de l'administration (`V2-CONVENTION.md`) et les composants
  de liste, onglets de filtre et aperçu d'import existants.
- L'import CSV « Nombre_de_photos » existant reste inchangé ; E3 est un import distinct avec
  d'autres colonnes.
- Les volumes attendus sont de l'ordre de quelques centaines à quelques milliers de lignes :
  l'export et l'import ne doivent pas bloquer la page.
- Accessibilité : boutons d'export/import, zone de dépôt et messages de résultat utilisables au
  clavier et annoncés aux lecteurs d'écran, comme le reste de l'administration.
- Un livre dont la fiche a déjà été corrigée à la main ne doit jamais être écrasé par un import.
- Pour être marquée « complétée » (RG-20), une fiche doit être absente d'ISBNdb : le plan devra
  vérifier que tout livre « non trouvé » l'est bien et, sinon, l'enregistrer comme tel à l'import.

## 9. Critères d'acceptation

1. Le menu affiche **« Analyse des ISBNs »** puis **« ISBN non trouvés »** puis « Livres hors
   ISBNdb » ; l'ancien libellé « ISBN en cours d'analyse » n'apparaît plus nulle part.
2. Sur E1, il n'existe plus de filtre « Notices non trouvées » et « Toutes » n'affiche aucun
   ISBN sans notice trouvée.
3. Avec 7 ISBN en attente, 3 en échec : la pastille d'« Analyse des ISBNs » affiche **3**, sous
   *Toutes*, *En cours* comme *Échecs*. Avec 0 échec, **aucune pastille** n'est affichée.
4. Aucune pastille n'affiche « 0 », y compris dans le sélecteur mobile.
5. Les rubriques Catalogue et Livres rares n'ont aucune pastille.
6. E5 ne contient plus aucune ligne sans titre ; ses compteurs concordent avec la pastille
   de la navigation et la carte d'état ISBNdb.
7. L'export d'une liste de 250 ISBN (paginée par 50) produit un fichier de 250 lignes de
   données, ISBN en colonne 1, 12 colonnes vides, ouvert correctement dans Excel avec les
   accents.
8. Un fichier de 10 lignes complétées dont 2 invalides (un ISBN inconnu, une année « abc »)
   affiche un aperçu « 8 prêtes, 2 rejetées » avec numéro de ligne et motif ; après
   confirmation, 8 livres quittent E2, leur fiche contient toutes les valeurs saisies et ils
   apparaissent dans « Livres hors ISBNdb » > Complétées (ceux sans auteur dans À compléter).
9. Un ISBN de livre qui a déjà une notice, présent dans le fichier, est rejeté ; sa fiche
   n'est pas modifiée.
10. Redéposer le même fichier ne modifie rien et ne crée pas de doublon.
11. Un non-administrateur ne peut ni voir E2 ni appeler l'export/import.

## 10. Questions ouvertes

Chaque question a une réponse par défaut, appliquée sauf avis contraire.

*Arbitrées par l'utilisateur le 2026-11-15 : question 2 → les livres importés par CSV sont marqués « complétés » d'office ; les autres défauts sont validés.*

1. **« Livres hors ISBNdb … que ceux qui ont eu une correspondance »** : j'interprète que E5
   garde les livres absents d'ISBNdb qui ont une fiche venant d'une **autre source**
   (BnF, Open Library, Google Books) ou d'une saisie, et exclut ceux **sans aucun titre**
   (qui vont dans E2). *Défaut : oui.*
2. **Livres complétés par import** : **tranché** — marqués « complétés » d'office (RG-20).
3. **Pastille de la nouvelle rubrique** (RG-12) : non demandée explicitement, ajoutée pour
   cohérence. *Défaut : oui.*
4. **Colonnes obligatoires** : seul le **titre** est obligatoire (RG-17). L'auteur n'est pas
   exigé, mais sans auteur la fiche n'est pas marquée « complétée » (RG-20).
5. **Limites du fichier** (RG-22) : *défaut : 5 000 lignes, 2 Mo.*
6. **Texte d'aide « Format du fichier »** : une description des colonnes à copier-coller dans
   la consigne donnée à l'agent. *Défaut : inclus dans E2.*
