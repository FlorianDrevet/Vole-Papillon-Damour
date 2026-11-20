# Plan d'implémentation — Étiquettes des livres sans ISBN

> Exécutant : Claude. L'utilisateur a demandé l'implémentation directe (au lieu de Codex, prévu par
> `/feature`) : tout est sur `feat/book-labels`, sans PR. Spec : [`01-fonctionnel.md`](01-fonctionnel.md).
> Maquettes : [`maquettes/preview/`](maquettes/README.md).

## État initial (tâche 0)

- Backend : Domain, Application, Infrastructure et Api compilent ; 260 + 833 + 259 tests verts après
  la généralisation `Book` → `BookCode` (commits `e53d446d` → `527c1136`).
- Scanette : `ng build` et `ng test` verts (374 tests) après `npm run link:shared-ui` de Scan.
- Catalog : `ng build` vert ; `ng test` : 3 échecs préexistants dans le compte (carte de compte,
  favoris), fichiers non touchés par cette feature.

## Tâches réalisées

| # | Tâche | Règles | Commit |
|---|-------|--------|--------|
| T1 | Code interne EAN-13 préfixe 2, compteur monotone `InternalBookCodeSequence` | RG-170, RG-171 | `3104ffa6` |
| T2 | Migrations `InternalBookCodeSequence`, `BookPhotos` (cascade) | RG-170, Q11 | `3104ffa6`, `0f076d38` |
| T3 | Création ordinaire sans ISBN (`POST /books/without-isbn`, `POST /books/admin/books/without-isbn`) | RG-180, RG-173 | `3104ffa6` |
| T4 | Photos d'un livre sans ISBN (≤ 10, JPEG/PNG/WebP, ≤ 8 Mo) | Q11 | `0f076d38` |
| T5 | Lectures : liste admin, fiche publique (couverture = photo, drapeau `IsInternalCode`) | RG-181 | `0f076d38` |
| T6 | Scan, caisse, Ma sélection, alertes : codes internes reconnus, codes inconnus refusés | RG-172, RG-182, Q9 | `0c2158c5` |
| T7 | Livre rare sans ISBN : code par exemplaire, quantité 1 | RG-173 | `0f076d38` |
| T8 | Traitements ISBNdb et enrichissement : filtre `IsInternalCode` côté SQL | RG-170 | `b8daf268` |
| T9 | Scanette : entrée, page de création E2/E3, impression E5, réimpression E4 | RG-175, RG-179, RG-180 | `17502121` |
| T10 | Site Catalog : liste admin E9, formulaire E10, fiche publique E11 | RG-181, RG-183, Q6 | `1c8f784b` |
| T11 | Compteur des codes internes en séquence SQL `InternalBookCodeNumbers` (table supprimée) | RG-170, RG-171 | `b10369fc` |
| T12 | RG-174 livre ordinaire : « Ajouter un ISBN » (fusion ou création de la fiche ISBN), endpoint, écran admin | RG-174, RG-171 | `0fab602f`, `f1e3bf35` |
| T13 | RG-174 livre rare : `RareBooks.InternalCode` séparé de l'ISBN, migration des données | RG-173, RG-174 | `4ee15e33` |
| T14 | Parité EF / base : FK `FK_BookIsbndbRecords_Books_Isbn13` et `FK_Books_Books_RedirectedToIsbn13`, index | (technique) | `cbdea7af` |
| T15 | Contrainte `CK_Books_IsInternalCode_MatchesCode` (le drapeau ne contredit pas la clé) | RG-170 | `adaae1a7` |
| T16 | Achats : pas de bouton favori sur un code interne | RG-182 | `6635a4b1` |
| T17 | Test d'intégration SQL Server réel (Testcontainers, trait `SqlServerIntegration`) | RG-171, RG-170 | `4c521570` |

Tests (lot 1) : domaine 266, application 839, infrastructure 259 ; scanette 374 ; catalog 692 (+ 3 préexistants).

Tests (lot 2, état final) : domaine 275, application 872, infrastructure 266 (dont 3 tests SQL Server réels),
api 108, worker 23 ; scanette 378 ; catalog 701 (+ 4 préexistants, présents aussi au commit de départ :
AccountCard ×3, page compte ×1).

## Écarts avec la spec

- **Livre rare sans ISBN, création** : l'ancien parcours hors ligne (`startNoIsbn`) ne crée plus de
  brouillon, il ouvre la création en ligne (RG-180).
- **E9, filtre ordinaire / rare** : la liste admin ne montre que les livres ordinaires. Les livres
  rares sans ISBN apparaissent dans « Livres rares », où ils portent leur code (un par exemplaire).
- **Réponse de l'endpoint admin de création** : `200` (et non `201`), car le `Location` pointerait
  vers une lecture qui n'accepte pas encore un code interne.
- **RG-177 (date de dernière impression)** : non implémentée. Tant que l'impression est simulée
  (RG-178), rien n'est enregistré ; la liste E5 « jamais imprimées » attend le pilote réel.
- **Caisse E8 (affichage dédié)** : le code interne est résolu vers sa fiche (RG-172) ; la ligne
  « code interne reconnu » du canevas n'a pas de rendu dédié (voir ci-dessous).

## Maquettes couvertes

**Maquette :** preview/Main.html — zones E1-entree-ajouter-sans-isbn, E1-entree-imprimante
**Maquette :** preview/E2-FormulaireSansIsbn.html — zones E2-bascule-type, E2-champ-titre, E2-champ-auteur, E2-champ-editeur, E2-champ-annee, E2-champ-etat, E2-champ-quantite, E2-photos, E2-message-connexion, E2-bouton-creer
**Maquette :** preview/E3-FicheCreee.html — zones E3-code-chiffres, E3-bouton-imprimer, E3-bouton-plus-tard
**Maquette :** preview/E4-ResultatIsbn.html — zones E4-bouton-reimprimer-isbn, E4-isbn-lisible
**Maquette :** preview/E5-FicheRare.html — zones E5-bouton-imprimer, E5-bouton-plus-tard, E5-prix-ferme
**Maquette :** preview/E6-Imprimante.html — zones E6-etat, E6-appareil, E6-bandeau-simulation, E6-bouton-connecter, E6-bouton-tester
**Maquette :** preview/E7-ApercuImpression.html — zones E7-apercu-etiquette, E7-nombre-etiquettes, E7-bouton-imprimer, E7-statut-simulation, E7-bouton-annuler
**Maquette :** preview/E8-CaisseCodeInterne.html — zones E8-ligne-code-interne
**Maquette :** preview/E9-ListeAdmin.html — zones E9-bouton-ajouter
**Maquette :** preview/E10-FormulaireAdmin.html — formulaire admin, mêmes champs que E2 (pas de zone dans le canevas)
**Maquette :** preview/E11-FichePublique.html — fiche publique, sans ISBN (pas de zone dans le canevas)
**Maquette :** preview/E11-FichePubliqueMobile.html — fiche publique mobile (pas de zone dans le canevas)

## Zones non implémentées

- `E4-isbn-lisible` : le verdict affiche déjà l'ISBN ; aucune zone dédiée n'a été ajoutée.
- `E5-bouton-plus-tard` : la fiche rare n'a pas d'écran de choix après impression ; le bouton retour existant suffit.
- `E5-prix-ferme` : le prix est imprimé sur l'étiquette (RG-179) ; son affichage sur la fiche rare n'a pas été ajouté.
- `E7-bouton-annuler` : l'aperçu d'étiquette n'a pas de bouton d'annulation distinct du retour à l'accueil.
- `E8-ligne-code-interne` : la caisse résout le code interne vers sa fiche (RG-172) ; la ligne dédiée
  « code interne reconnu » du canevas n'est pas rendue séparément.

## Lot 2 : RG-174, compteur SQL, parité EF (T11 à T17)

### RG-174 : couverte

- **Livre ordinaire** (T12) : « Ajouter un ISBN » sur une fiche à code interne
  (`POST /books/admin/books/{internalCode}/add-isbn`, politique `Administration`). Le code interne reste
  valide, les deux codes résolvent vers la fiche ISBN (test scan : code interne et ISBN donnent la même fiche).
  Si la fiche ISBN existe, fusion ; sinon création avec les métadonnées saisies, toutes marquées éditées.
  La fusion est partagée avec la fusion admin (`BookMergeOperation`) : stock et mouvements de correction,
  redirection, watchlist et signalements « introuvable » repointés. Ma sélection, favoris, demandes manuelles
  et recherches suivent déjà la redirection à la lecture (`RedirectedToIsbn13`), comme pour une fusion classique.
- **Livre rare** (T13) : `RareBooks.InternalCode` garde le code d'étiquette à vie ; `Isbn13` ne porte plus
  qu'un ISBN réel. Un rare créé sans ISBN, puis complété d'un ISBN, se résout par les deux codes.

### Écarts avec la proposition

- **Séquence (T11)** : le modèle ne fixe pas `StartsAt`. Le démarrage dépend des données : la migration le
  calcule (`MAX(LastIssuedNumber) + 1`) avant de supprimer l'ancienne table. Un `StartsAt` constant dans le
  modèle aurait produit une différence à chaque migration. La séquence n'est déclarée que pour SQL Server
  (SQLite ne sait pas créer de séquence) ; `IInternalBookCodeIssuer` a une implémentation SQL Server et des fakes.
- **Lecture de la séquence (T11)** : `NEXT VALUE FOR` est interdit dans une table dérivée (SQL Server) ; un
  `Database.SqlQuery` l'y place. La lecture est donc une commande ADO.NET de premier niveau dans la transaction courante.
- **Couverture (T12)** : la couverture de la fiche interne n'est pas recopiée. Une photo marquée « éditée »
  bloquerait la couverture fournisseur que la fiche ISBN doit prendre. **Manque** : une fiche ISBN créée ainsi
  n'a pas de couverture tant que l'enrichissement fournisseur ne la fournit pas ; les photos restent sur la fiche masquée.
- **Annonces (T12)** : la fusion refuse une fiche interne qui a des annonces actives, comme la fusion classique.
- **Rares (T13)** : un rare créé avant la fonctionnalité, sans ISBN ni code, reste possible (aucun code à
  migrer). Le domaine ne l'exige qu'au retrait de l'ISBN d'un rare qui en a un. À lister et étiqueter.
- **Parité (T14)** : `RedirectedToIsbn13` est typé `BookCode?` (même type que la clé). La migration
  `DeclareBookForeignKeysInModel` a des Up/Down vides : les FK et l'index existent déjà en base. Vérifié :
  `dotnet ef migrations has-pending-model-changes` ne signale aucun changement.
- **Achats (T16)** : le type d'un achat (code interne ou ISBN) est déduit du code côté front ; le DTO n'a pas
  été modifié.
- **Front admin (T12)** : le parseur d'ISBN vit dans `shared/barcode/isbn-input.ts`. Dans
  `features/administration/`, trois specs de la page admin dépassaient le délai de 5 s ; le déplacement a suffi.
  La cause exacte n'est pas identifiée.

### Migrations du lot 2

| Migration | Contenu | Données |
|---|---|---|
| `ReplaceInternalBookCodeCounterWithSequence` | Crée la séquence `InternalBookCodeNumbers` (démarrage calculé), supprime `InternalBookCodeSequences` | Aucune perte |
| `SplitRareBookInternalCode` | Colonne `RareBooks.InternalCode`, déplacement des rares dont `Isbn13` commence par 2, index unique filtré, 2 CHECK | Déplacement, aucune perte |
| `DeclareBookForeignKeysInModel` | Snapshot seul (FK et index déjà en base) | Aucune |
| `EnforceBookInternalCodeFlag` | Recalcule `IsInternalCode` depuis la clé, ajoute `CK_Books_IsInternalCode_MatchesCode` | Drapeau recalculé, aucune ligne supprimée |

### Tests d'intégration

- `SqlServerMigrationsIntegrationTests` (trait `Category=SqlServerIntegration`, Testcontainers.MsSql 4.16.0,
  licence MIT) : applique toutes les migrations sur SQL Server 2022 ; la séquence reprend après le dernier
  numéro ; deux créations simultanées réussissent avec deux codes ; la CHECK refuse une ligne incohérente (547).
- Exécution locale avec Podman : `DOCKER_HOST=npipe://./pipe/docker_engine`, `TESTCONTAINERS_RYUK_DISABLED=true`.
  Sans conteneurs : `dotnet test --filter "Category!=SqlServerIntegration"`.
