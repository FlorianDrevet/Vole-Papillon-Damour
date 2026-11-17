# Plan d'implémentation — ISBN non trouvés

> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : maquettes/preview/.

Branche `feat/isbn-not-found-tab`, worktree `Vole-Papillon-Damour-isbn-not-found-tab`.
Les règles RG-xx renvoient à `01-fonctionnel.md`, les écrans E1 à E5 à la section 6.

## Décisions prises dans ce plan

1. **Nouvelle section frontend `isbn-not-found`**, route `/administration/isbn-not-found`, libellé
   « ISBN non trouvés ». Elle ne doit pas être confondue avec la section existante `not-found`
   (« Livres introuvables », rubrique « Pendant la bourse »), qui reste inchangée.
2. **Parsing et validation du CSV côté backend.** RG-16 exige de vérifier chaque ISBN contre la
   base : c'est le backend qui décide. Le frontend envoie le fichier et affiche le résultat.
   - `GET  /books/admin/books/isbn-not-found/export` : CSV (text/csv) de tous les « non trouvés ».
   - `POST /books/admin/books/isbn-not-found/import/preview` : analyse sans écriture (RG-14).
   - `POST /books/admin/books/isbn-not-found/import` : revalide chaque ligne, puis écrit (RG-19, RG-21).
   - `GET  /books/admin/books/isbn-not-found` : liste paginée (onglet E2).
3. **Pastilles** : les compteurs viennent de la vue d'ensemble admin (`GetCatalogAdminOverview`) et
   de `GetIsbndbStatus`, pas de la page chargée. Une pastille affichée n'est jamais « 0 » (RG-09).
4. **Complétion par import** : chaque ligne valide passe par `Book.ApplyManualMetadata`
   (statut `Manual`). Si le livre est absent d'ISBNdb et a un titre et des auteurs, il est aussi
   marqué complété via `Book.MarkIsbndbCompleted` (RG-20). Sans auteur, il reste « à compléter ».
   **Attention** : `MarkIsbndbCompleted` lève une exception si le livre n'est pas
   `IsAbsentFromIsbndb` (`Book.cs`, ligne 468). Voir la tâche 2 pour ce point.
5. **Entrées de navigation Catalogue et Livres rares conservées**, sans pastille (RG-10 ; maquette E4).

## Tâche 0 — État initial

- Lancer les builds et tests existants du backend (`src/Backend`) et du frontend (`src/Catalog`),
  selon les commandes de la CI et des skills `.github/skills/`.
- Noter dans le rapport final les échecs **déjà présents** avant toute modification. Ne pas les corriger.
- Pas de commit.

## Tâche 1 — Backend : la file E1 ne contient plus les « non trouvés »

**Test d'abord** (`Vole_Papillon_Damour.Application.tests/Books/Queries/Admin/GetBookMetadataQueueQueryHandlerTests.cs`) :
- un livre `NotFound` n'apparaît pas dans la file sans filtre ;
- le filtre `failed` ne retourne que les `Failed` ;
- le nouveau champ `FailedCount` vaut le nombre de `Failed` non supprimés, quel que soit le filtre (RG-11).

**Modifier** :
- `Vole_Papillon_Damour.Application/Books/Queries/Admin/GetBookMetadataQueueQueryHandler.cs` : retirer
  `BookMetadataStatus.NotFound` des conditions de base (lignes 38-43). Garder `Pending`, `Failed`,
  et les titres vides.
- `GetBookMetadataQueueQuery.cs` : le filtre `metadataStatus` n'accepte plus `NotFound`.
- Le résultat `BookMetadataQueuePageResult` (dans `Application/Books/Common/`) reçoit `FailedCount`.
- `Vole_Papillon_Damour.Api/Controllers/BookAdministrationController.cs`, endpoint
  `GetAdminBookMetadataQueue` (ligne ~214) : valeurs de filtre à jour.

**Message de commit** : `feat(isbn-queue): retire les notices non trouvées de la file d'analyse`

## Tâche 2 — Backend : liste et compteurs des « non trouvés », vérification d'absence ISBNdb

**Vérifier d'abord** (test d'intégration sur les fixtures existantes) : un livre `NotFound` est-il
toujours marqué `IsAbsentFromIsbndb` ? Dans `EnrichPendingBooksCommandHandler.cs`, ce drapeau n'est
posé qu'après une réponse ISBNdb `NotFound` (ligne 230). Un livre passé en `RecordMetadataNotFound`
(ligne 368) sans interrogation ISBNdb (quota épuisé, par exemple) ne l'a pas.

**Test d'abord** : un livre `NotFound` est listé par la nouvelle requête `GetIsbnNotFoundQuery`
avec un total exact, et pas d'autre statut.

**Créer** : `Application/Books/Queries/Admin/GetIsbnNotFoundQuery.cs` et son handler
(liste paginée, `Discarded` exclus).

**Modifier** :
- `GetCatalogAdminOverviewQueryHandler.cs`, lignes 84-86 : `isbndbAbsentToCompleteCount` exclut les
  titres vides (RG-06). Ajouter `isbnNotFoundCount` et `isbnFailedCount`.
- `Isbndb/Queries/GetIsbndbStatus/GetIsbndbStatusQueryHandler.cs`, ligne 41 : même règle pour
  `absentToComplete`.

**Point de décision** (à traiter dans la tâche si la vérification l'exige) : si un `NotFound` n'est pas
absent d'ISBNdb, l'import ne peut pas le compléter tel quel. Par défaut, le plan retient : à l'import,
un livre `NotFound` est marqué absent (`Book.MarkAbsentFromIsbndb(now)`), car E2 définit « non trouvé »
comme « aucune source, ISBNdb comprise ». Consigner la décision et le nombre de cas concernés dans le
rapport final.

**Message de commit** : `feat(isbn-not-found): liste et compteurs des ISBN non trouvés`

## Tâche 3 — Backend : export CSV (RG-07, RG-08)

**Test d'abord** (`Vole_Papillon_Damour.Application.tests/Books/Queries/Admin/`) : l'export contient
tous les « non trouvés » (pas seulement une page) ; l'en-tête reprend exactement ces 13 colonnes dans
cet ordre : `ISBN;Titre;Auteurs;Editeur;Annee_publication;Format;Langue;Genre_principal;Genres;Resume;Pages;Edition;Couverture_URL` ;
la première colonne est l'ISBN-13 ; toutes les autres sont vides ; séparateur `;` ; UTF-8 avec BOM
(comme `isbn-application-import.ts`).

**Créer** : `Application/Books/Queries/Admin/ExportIsbnNotFoundQuery.cs` et son handler.

**Modifier** : `Vole_Papillon_Damour.Api/Controllers/BookAdministrationController.cs` : endpoint
`GET .../isbn-not-found/export`, réservé à l'administrateur (RG-23), réponse `text/csv` avec le nom
`isbn-non-trouves-AAAA-MM-JJ.csv`.

**Message de commit** : `feat(isbn-not-found): export CSV des ISBN non trouvés`

## Tâche 4 — Backend : import, analyse sans écriture (RG-14 à RG-18, RG-22)

**Test d'abord** (une classe de test par règle) :
- séparateur `;`, BOM accepté, en-tête vérifié : colonnes manquantes ou inconnues → fichier refusé
  (écran E3-erreur-fichier) ;
- ISBN-10 converti en ISBN-13 ; ISBN invalide → ligne rejetée « ISBN invalide » ;
- ISBN qui ne correspond pas à un livre `NotFound` → « ISBN inconnu » (RG-16) ;
- ISBN en double dans le fichier → « ISBN en double dans le fichier » ;
- ligne sans aucune valeur de fiche → ignorée (RG-17) ;
- titre manquant → « Titre manquant » (seul champ obligatoire) ;
- limites de RG-08/RG-18 : Titre 500, Auteurs 500, Editeur 200, Annee_publication 1 à 9999, Format 50,
  Langue 10, Genre_principal 100, Genres 100 par genre séparé par « | », Resume 20 000, Pages 1 à 100 000,
  Edition 100, Couverture_URL en https ;
- fichier au-delà de 5 000 lignes ou 2 Mo → refusé (RG-22).

**Créer** :
- `Application/Books/Common/IsbnNotFoundCsv.cs` : lecture et validation du CSV.
- `Application/Books/Queries/Admin/PreviewIsbnNotFoundImportQuery.cs` et son handler (aucune écriture).

**Modifier** :
- `Vole_Papillon_Damour.Contracts/Books/Requests/AdminRequests.cs` : requête `IFormFile File` (même
  forme que la requête existante de la ligne 7).
- `BookAdministrationController.cs` : `POST .../isbn-not-found/import/preview`.

**Résultat renvoyé** : compteurs `ready`, `withoutAuthor`, `ignored`, `rejected`, et pour chaque
rejet, le numéro de ligne, l'ISBN et le motif (écrans E3-Desktop, E3-Mobile).

**Message de commit** : `feat(isbn-not-found): analyse du CSV d'import sans écriture`

## Tâche 5 — Backend : import, écriture (RG-19 à RG-21, RG-23)

**Test d'abord** :
- une ligne valide est enregistrée ; une ligne rejetée n'empêche pas les autres (RG-19) ;
- livre absent d'ISBNdb, titre et auteurs renseignés → `IsbndbCompletedAt` et `IsbndbCompletedBy`
  renseignés, marqué complété d'office (RG-20) ;
- livre sans auteur → statut `Manual` mais **pas** complété ;
- livre dont l'état a changé entre l'aperçu et la confirmation → ligne rejetée avec le motif
  « modifié depuis l'aperçu » (RG-21, écran E3-resume) ;
- réimporter le même fichier ne crée aucun doublon : « livre déjà doté d'une notice » ;
- non-administrateur → refus (RG-23).

**Modifier** :
- `Domain/BookAggregate/Book.cs` : `ApplyManualMetadata` (ligne 208) avec un `BookMetadataPatch`
  (`Domain/BookAggregate/ValueObjects/BookMetadataPatch.cs`) construit à partir des colonnes CSV.
  Mapping : Titre → Title, Auteurs → Authors, Editeur → Publisher, Annee_publication → PublicationYear,
  Format → PhysicalFormat, Langue → Language, Genre_principal → Genre, Couverture_URL → CoverUrl,
  Genres → Genres, Resume → Summary, Pages → Pages, Edition → Edition.
- Marquage complété : `MarkIsbndbCompleted` (ligne 465), après `MarkAbsentFromIsbndb` si la
  décision de la tâche 2 l'exige.
- Réutiliser le chemin de `Application/Books/Commands/UpdateBookMetadata/UpdateBookMetadataCommandHandler.cs`
  quand c'est possible, pour garder les mêmes règles de validation.
- Créer `Application/Books/Commands/ImportIsbnNotFound/ImportIsbnNotFoundCommand.cs` et son handler,
  qui revalide puis écrit dans une même transaction.
- `BookAdministrationController.cs` : `POST .../isbn-not-found/import`.

**Message de commit** : `feat(isbn-not-found): import CSV des fiches et complétion d'office`

## Tâche 6 — Backend : « Titre inconnu » sorti de Livres hors ISBNdb (RG-04, RG-06, RG-13)

**Test d'abord** :
- `GetIsbndbAbsentBooksQueryHandlerTests` : un livre absent sans titre n'est listé ni dans la page
  ni dans le total de `to-complete` ;
- `GetIsbndbStatusQueryHandlerTests` et `GetCatalogAdminOverviewQueryHandlerTests` : le compteur
  `absentToComplete` / `isbndbAbsentToCompleteCount` exclut ces livres ;
- un livre absent avec titre mais sans auteur reste listé (cas « à compléter », RG-20).

**Modifier** : `Application/Isbndb/Queries/GetIsbndbAbsentBooks/GetIsbndbAbsentBooksQueryHandler.cs`,
ligne 25 : ajouter la condition titre non vide. Mêmes règles dans les deux compteurs de la tâche 2.

**Message de commit** : `fix(isbndb): livres hors ISBNdb sans titre exclus de la liste et des compteurs`

## Tâche 7 — Frontend : client HTTP et modèles

**Test d'abord** (`src/Catalog/src/app/core/catalog-admin-api.service.spec.ts`) : chaque méthode
appelle la bonne URL et la bonne méthode HTTP ; l'export renvoie un `Blob`.

**Modifier** :
- `src/Catalog/src/app/core/catalog-admin-api.service.ts` : méthodes `getIsbnNotFoundBooks`,
  `exportIsbnNotFoundCsv`, `previewIsbnNotFoundImport`, `importIsbnNotFound`. Les méthodes existantes
  (`getBookMetadataQueue` ligne 164, `getIsbndbAbsentBooks` ligne 219) gardent leur signature.
- `src/Catalog/src/app/core/catalog.models.ts` : `CatalogAdminBookMetadataQueue` avec `failedCount`,
  types de la page « non trouvés », du résultat d'aperçu et de la confirmation, et les nouveaux
  compteurs de la vue d'ensemble.

**Message de commit** : `feat(isbn-not-found): client HTTP et modèles de l'import CSV`

## Tâche 8 — Frontend : navigation (E4) et pastilles

**Maquettes** : `preview/E4-Desktop.html` — zones E4-entree-analyse, E4-entree-non-trouves,
E4-entree-hors-isbndb, E4-entree-catalogue-sans-pastille, E4-entree-livres-rares-sans-pastille,
E4-badge-analyse, E4-badge-non-trouves, E4-badge-hors-isbndb, E4-etat-zero.
`preview/E4-Mobile.html` — zones E4-selecteur-mobile, E4-selecteur-mobile-zero, E4-option-analyse,
E4-option-catalogue, E4-option-hors-isbndb, E4-option-livres-rares, E4-option-non-trouves.

**Test d'abord** (`catalog-administration-page.component.spec.ts`, `catalog-administration-route.spec.ts`) :
- « Analyse des ISBNs » remplace « ISBN en cours d’analyse » ;
- « ISBN non trouvés » est juste après « Analyse des ISBNs » ;
- aucune pastille à 0 ; Catalogue et Livres rares n'ont aucune pastille ;
- la pastille « Analyse des ISBNs » affiche le nombre d'échecs quel que soit le filtre sélectionné
  (zone E1-pastille-independante, RG-11).

**Modifier** :
- `src/Catalog/src/app/core/catalog-administration-route.ts` (+ spec) : section `isbn-not-found`,
  chemin `/administration/isbn-not-found`.
- `src/Catalog/src/app/features/administration/catalog-administration-page.component.ts` :
  - libellés des entrées (lignes 188-191) : « Analyse des ISBNs » ; nouvelle entrée « ISBN non trouvés » ;
  - `navBadge()` (lignes 292-319) : `isbn-queue` lit `isbnFailedCount` ; `isbn-not-found` lit
    `isbnNotFoundCount` ; `isbndb-absent` garde son compteur ; tout 0 renvoie `null` ; `catalogue` et
    `rare-books` renvoient `null`.
- `catalog-administration-page.component.html`, ligne 64-65 : le rendu de la pastille ne change pas.
- `.scss` : aucune nouvelle règle nécessaire pour la navigation.

**Message de commit** : `feat(isbn-queue): navigation ISBN non trouvés et pastilles à zéro masquées`

## Tâche 9 — Frontend : « Analyse des ISBNs » (E1)

**Maquettes** : `preview/Main.html` (desktop) — zones E1-titre, E1-intro, E1-lien-non-trouves,
E1-compteur, E1-filtres, E1-filtre-toutes, E1-filtre-en-cours, E1-filtre-echecs, E1-liste, E1-ligne,
E1-actions-ligne. `preview/E1-Mobile.html` — zones E1-titre-mobile, E1-intro-mobile, E1-filtres-mobile,
E1-liste-mobile. `preview/E1-Etats.html` — zones E1-vide-toutes, E1-vide-en-cours, E1-vide-echecs,
E1-chargement, E1-erreur, E1-pastille-independante (ces états sont couverts par les tests de la
tâche 8 et par les tests ci-dessous).

**Test d'abord** :
- trois filtres seulement : Toutes, En cours, Échecs ;
- titre « Analyse des ISBNs. » et lien « ISBN non trouvés → » dans l'intro ;
- les trois états vides et le message du filtre Échecs ;
- aucun ISBN « non trouvé » dans la liste (RG-02).

**Modifier** :
- `catalog-administration-page.component.html`, section `isbn-queue` (lignes 749-802) : titre (ligne 754),
  intro (ligne 755), compteur (ligne 760), chip « Notices non trouvées » (ligne 769) retiré,
  branche `not-found` des états vides (lignes 791-792) retirée, lien vers « ISBN non trouvés ».
- Fil d'Ariane et titre de fiche : « ISBN en cours d’analyse » → « Analyse des ISBNs » (lignes 406-407).
- `catalog-administration-page.component.ts`, ligne 408 : type `metadataQueueFilter` sans `'not-found'`.

**Message de commit** : `feat(isbn-queue): rubrique Analyse des ISBNs sans les notices non trouvées`

## Tâche 10 — Frontend : « ISBN non trouvés » (E2), liste et export

**Maquettes** : `preview/E2-Desktop.html` — zones E2-titre, E2-intro, E2-compteur, E2-barre-actions,
E2-bouton-exporter, E2-bouton-importer, E2-lien-format, E2-liste, E2-ligne, E2-action-completer,
E2-action-reessayer, E2-action-supprimer, E2-pagination. `preview/E2-Mobile.html` — zones
E2-titre-mobile, E2-intro-mobile, E2-compteur-mobile, E2-barre-actions-mobile,
E2-bouton-exporter-mobile, E2-bouton-importer-mobile, E2-lien-format-mobile, E2-liste-mobile.
`preview/E2-Etats.html` — zones E2-aide-format, E2-vide, E2-export-desactive, E2-chargement, E2-erreur.

**Créer** : `src/Catalog/src/app/features/administration/isbn-not-found/isbn-not-found-view.component.ts`,
`.html`, `.scss`, `.spec.ts`, sur le modèle de `isbndb/isbndb-absent-view.component.*`.

**Test d'abord** :
- liste paginée de « non trouvés », « Compléter → » ouvre la fiche avec le fil « ISBN non trouvés » ;
- « Exporter en CSV » désactivé quand la liste est vide (E2-export-desactive) ;
- l'export télécharge `isbn-non-trouves-AAAA-MM-JJ.csv` ;
- « Format du fichier » déplie l'aide avec les 13 colonnes (E2-aide-format).

**Modifier** : `catalog-administration-page.component.html` pour afficher la nouvelle vue dans un
`@case ('isbn-not-found')`. Le fil d'Ariane de `closeBookDetail()` (ligne 406) gère ce retour.

**Message de commit** : `feat(isbn-not-found): rubrique ISBN non trouvés avec export CSV`

## Tâche 11 — Frontend : Import CSV (E3)

**Maquettes** : `preview/E3-Desktop.html` — zones E3-titre, E3-nom-fichier, E3-synthese,
E3-stat-pretes, E3-stat-sans-auteur, E3-stat-ignorees, E3-stat-rejetees, E3-table-rejets,
E3-bouton-annuler, E3-bouton-enregistrer. `preview/E3-Mobile.html` — zones E3-titre-mobile,
E3-nom-fichier-mobile, E3-synthese-mobile, E3-table-rejets-mobile, E3-bouton-enregistrer-mobile,
E3-bouton-annuler-mobile. `preview/E3-Etats.html` — zones E3-zone-depot, E3-erreur-fichier,
E3-aucune-valide, E3-progression, E3-resume, E3-erreur-enregistrement.

**Créer** : `src/Catalog/src/app/features/administration/isbn-not-found/isbn-not-found-import-dialog.component.*`.
Deux étapes : aperçu (appel à `previewIsbnNotFoundImport`), puis enregistrement (`importIsbnNotFound`).
Le frontend n'applique aucune règle métier : il affiche ce que renvoie le backend.

**Test d'abord** : aperçu avec fiches prêtes ; aucune fiche prête → bouton « Enregistrer 0 fiche »
désactivé ; fichier refusé → message d'erreur sans bouton d'enregistrement ; progression ; résumé
avec « voir le détail » sur les rejets.

**Libellés à reprendre mot pour mot** : « Vérifier le fichier avant d’enregistrer »,
« Enregistrer {n} fiches », « Choisir un autre fichier ».

**Message de commit** : `feat(isbn-not-found): dialogue d'import CSV en deux étapes`

## Tâche 12 — Frontend : Livres hors ISBNdb (E5) sans « Titre inconnu »

**Maquettes** : `preview/E5-Desktop.html` — zones E5-filtre-a-completer, E5-filtre-completees,
E5-recherche, E5-liste, E5-ligne-livre. `preview/E5-Mobile.html` — zones E5-filtre-a-completer-mobile,
E5-filtre-completees-mobile, E5-recherche-mobile, E5-liste-mobile. `preview/E5-Etats.html` — zones
E5-vide-a-completer, E5-vide-completees, E5-chargement, E5-erreur, E5-sans-titre-inconnu.

**Test d'abord** (`isbndb-absent-view.component.spec.ts`) : aucune ligne « Titre inconnu » ; les
états vides reprennent « Aucun livre à compléter » et « Aucun livre marqué comme complété » ; le
compteur des filtres est celui du backend.

**Modifier** : `features/administration/isbndb/isbndb-absent-view.component.html`, ligne 37 : supprimer
le repli `|| 'Titre inconnu'`, puisque la liste ne contient plus de titre vide. Le fichier
`isbndb-absent-view.component.ts` garde la même logique de filtres.

**Message de commit** : `fix(isbndb): retire les livres sans titre de la rubrique Livres hors ISBNdb`

## Tâche 13 — Validation complète et PR

- Builds et tests backend et frontend, avec le même périmètre que la tâche 0 : aucun nouvel échec.
- Vérification visuelle sur `/administration/isbn-queue`, `/administration/isbn-not-found` et la
  rubrique Livres hors ISBNdb, en comparant avec `preview/*.html` (desktop et mobile).
- Vérifier chaque règle RG-01 à RG-23 et chaque critère d'acceptation de la spec. Consigner les écarts.
- Commit des corrections : `fix(isbn-not-found): corrections après validation`.
- Ouvrir la PR sur `main` depuis `feat/isbn-not-found-tab`.
- Mettre à jour le statut du worktree : « PR ouvert » avec l'URL, via `manage-worktrees.ps1 status`.
- Cocher la phase 5 dans `README.md`.

## Vérifications de cohérence

- Les 99 zones des maquettes sont citées dans ce plan (`check_zones.py`).
- Les états de référence (`E1-pastille-independante`, `E5-sans-titre-inconnu`, `E4-etat-zero`, etc.)
  n'ont pas de composant dédié : ils sont couverts par les tests des tâches 8, 9 et 12.
- Aucune zone n'est volontairement non implémentée : pas de section « Zones non implémentées ».
- Les écrans mobile reprennent le même composant que le desktop, avec un CSS responsive (16 px de
  marge, aucun défilement horizontal à 390 px).

## Points à arbitrer avant l'implémentation

1. **Absence d'ISBNdb à l'import** (tâche 2) : marquer absent les « non trouvés » qui ne l'étaient pas,
   ou les laisser en « à compléter » ? Le plan retient la première option.
2. **Compteur « métadonnées à vérifier »** de la vue d'ensemble : inclut-il encore les « non trouvés » ?
   Le plan le laisse inchangé, car la spec ne le traite pas.
3. **Nom de la route** : `/administration/isbn-not-found`, pour éviter la collision avec `not-found`.
