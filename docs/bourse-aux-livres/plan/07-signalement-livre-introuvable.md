# Signalement « livre introuvable » : plan d'implémentation

> **For agentic workers (Codex / Luna) :** exécuter les tâches **dans l'ordre**, une par
> une, en TDD (test rouge → implémentation minimale → test vert → commit). Les étapes
> utilisent des cases `- [ ]`. Ne pas sauter les étapes « Run tests ». Respecter
> [`AGENTS.md`](../../../AGENTS.md) (worktree dédié, branche, PR vers `main`) et
> [`.github/skills/tdd-workflow/SKILL.md`](../../../.github/skills/tdd-workflow/SKILL.md).

**Goal :** remplacer les quatre états personnels de Ma sélection (À prendre, Acheté,
Pas trouvé, À revoir) par un bouton **« Je ne l'ai pas trouvé »** qui crée un
**signalement**, et ajouter dans l'administration une file **« Livres introuvables »** où
un bénévole vérifie en rayon puis **retire du stock**, **confirme la présence** ou
**classe sans suite**.

**Architecture :** un nouvel agrégat `BookNotFoundReport` (une table), une tranche CQRS
`NotFoundReports` dans l'Application, des routes membre sous `/catalog/me/…` et
d'administration sous `/books/admin/not-found-reports/…`. Le retrait réutilise la
logique et le mouvement `Withdrawal` (RETRAIT) existants ; une fiche rare est dépubliée.
Côté Catalog : l'onglet Ma sélection perd son sélecteur d'état et gagne le bouton et sa
modale ; l'administration gagne une section `not-found`, une tuile au tableau de bord, un
encadré sur la fiche et un réglage. Aucune ressource Azure nouvelle.

**Tech Stack :** .NET 10, ASP.NET Core minimal APIs, MediatR, ErrorOr, FluentValidation,
EF Core (SQL Server ; SQLite en tests), xUnit + FluentAssertions + NSubstitute ;
Angular 21 (`src/Catalog`, composants `standalone: false` déclarés dans `app.module.ts`,
signaux), Karma/Jasmine.

**Spec :** [`../11-signalement-livre-introuvable.md`](../11-signalement-livre-introuvable.md)
(`F-11`) — règles `RG-67` à `RG-76`, critères `CA-1` à `CA-12`, questions `Q-SIG-1` à
`Q-SIG-8`.

**Maquettes (à ouvrir avant toute tâche d'interface) :**

- Archive : [`../maquettes/signalement-introuvable.zip`](../maquettes/signalement-introuvable.zip)
  — décompresser puis ouvrir `signalement-introuvable/preview/index.html`.
- Même contenu déjà décompressé : [`../maquettes/signalement-introuvable/`](../maquettes/signalement-introuvable/README.md)
  (`preview/*.html` = HTML statique autonome, `sources/*.dc.html` = sources du canvas).
- Canvas Claude Design éditable : <https://claude.ai/artifact/KDX9jHZrcRnb2H6qGccnDZ>.

Une tâche d'interface **ne se ferme pas** sans comparaison visuelle à la maquette citée
(desktop 1440 px **et** mobile 390 px). Les maquettes fixent mise en page, libellés et
états ; le code utilise les variables CSS existantes du Catalog (`--catalog-*`), jamais
les hexadécimaux des maquettes en dur.

---

## Préalable — décisions `Q-SIG` retenues par ce plan

Le plan applique les **propositions par défaut** de `F-11` §13.1. Si l'association
tranche autrement, seules les tâches citées changent.

| Question | Hypothèse du plan | Tâches touchées si elle change |
|---|---|---|
| `Q-SIG-1` Qui vérifie | Rôle `Administration` (éditions) ; rôle `RareBooks` pour les fiches rares ; export CSV pour les bénévoles de terrain | SIG-8, SIG-12 |
| `Q-SIG-2` Reprise des « Pas trouvé » | Convertis en signalements **ouverts** si `StatusChangedAt` < 30 jours **et** édition encore disponible ; sinon remis à l'état neutre | SIG-3 |
| `Q-SIG-3` « Acheté » manuel | Supprimé ; `Purchased` n'est posé que par la caisse | SIG-3, SIG-10 |
| `Q-SIG-4` Lieu (« À la bourse / Au local ») | Champ **facultatif** présent (maquette) ; `null` accepté | SIG-5, SIG-11 |
| `Q-SIG-5` Fiche rare introuvable | « Retirer la fiche rare » = `RareBook.Unpublish(...)` + note | SIG-8 |
| `Q-SIG-6` E-mail au membre | Aucun | — |
| `Q-SIG-7` Autres points d'entrée | Ma sélection uniquement | — |
| `Q-SIG-8` Relance | Indicateur « en attente depuis plus de 7 jours » ; aucune clôture automatique hors caducité | SIG-7, SIG-13 |

Décisions **techniques** prises par ce plan (à reporter en `DT-nn`, tâche SIG-16) :

- **`DT` proposé — plafond glissant.** « 10 signalements par jour » (`RG-70`) est calculé
  sur les **24 dernières heures glissantes** (pas de notion de fuseau), valeur réglable
  `AssociationSettings.NotFoundReportDailyLimit` (défaut 10).
- **`DT` proposé — caducité à l'écriture.** Les handlers qui peuvent faire tomber le stock
  disponible d'une fiche à 0 (vente, retrait, correction, fusion, vente ou dépublication
  d'un rare) appellent `INotFoundReportLapser` **dans leur transaction**. Pas de job de
  fond, pas de filtrage implicite en lecture.
- **`DT` proposé — clôture groupée.** Une décision d'administrateur clôt **tous** les
  signalements ouverts de la fiche, avec la même décision, la même date et le même auteur.

## Global Constraints

- Vocabulaire des écrans : **« Je ne l'ai pas trouvé »**, **« Signalé introuvable »**,
  **« Livres introuvables »**, **« Retrouvé »**, **« Retirer du stock… »**,
  **« Classer sans suite »**. Jamais « réclamation », « litige », « erreur de stock ».
- Le signalement **ne modifie jamais** le stock (`RG-71`) ; seule une clôture « retiré »
  produit un mouvement `Withdrawal`.
- Un signalement n'est possible que si la ligne est dans **Ma sélection** du membre, que
  la fiche est **Disponible** (édition `QuantityAvailable > 0` visible ; rare publié non
  vendu) et que la ligne n'est pas `Purchased` (`RG-68`).
- Un seul signalement **ouvert** par membre et par fiche (`RG-69`) ; un second envoi est
  **idempotent** (renvoie l'existant, `200`), jamais une erreur.
- La file d'administration n'expose **ni nom, ni e-mail, ni identifiant** de membre
  (`RG-74`) — uniquement des compteurs, dates, lieux et commentaires.
- Commentaire : texte libre ≤ 280 caractères, `Trim()`, vide → `null`, rendu **échappé**
  (interpolation Angular standard, jamais `innerHTML`).
- Toute nouvelle route membre : `RequireAuthorization()` + `MemberIdentityClaims.TryGetMemberIdentity`.
  Route d'administration sur une édition : policy `Administration`. Sur une fiche rare :
  policy `RareBooks` (rôles `LivresRares`, `Administration`, `Admin` — `Api/Program.cs`).
- `DT-16` : logs structurés à chaque création / clôture (`reportId`, `target`, `outcome`) ;
  **ne jamais** journaliser le commentaire, l'e-mail ou l'identifiant externe.
- Nouveau `DbSet` ajouté à `IProjectDbContext` avec implémentation par défaut
  `=> throw new NotSupportedException();` (patron de `MemberSelectionItems`).
- Une migration EF par tâche qui en déclare une, nommée comme indiqué ; ne jamais modifier
  une migration déjà poussée.
- Mobile : tous les écrans utilisables à **390 px** sans défilement horizontal ; cibles
  tactiles ≥ 44 px ; focus clavier visible ; modale avec `role="dialog"`,
  `aria-modal="true"`, focus piégé et `Échap` pour fermer.

## Review Focus

1. **Double appui / rejeu réseau** sur « Envoyer le signalement » → un seul signalement
   ouvert. Tests SIG-5 (`Handle_WhenOpenReportExists_ReturnsExistingWithoutCreating`) et
   index unique filtré SIG-2.
2. **Stock à 2, bénévole en retrouve 1** → RETRAIT de 1, fiche toujours disponible, les N
   signalements clos « retiré ». Test SIG-8 (`Withdraw_WithPartialFound_WithdrawsDifference`).
3. **Vente en caisse qui épuise le stock** alors que des signalements sont ouverts → ils
   passent `Lapsed` dans la même transaction et sortent de la file. Tests SIG-9.
4. **Suppression de compte** → signalements ouverts conservés, `UserId = null`,
   commentaire effacé. Test SIG-14.
5. **Ancien client** qui appelle encore `PATCH /catalog/me/selection/{id}` → `410 Gone`
   propre, pas de 500. Test SIG-4.

## Carte des fichiers

### Backend — Domain (`src/Backend/Vole_Papillon_Damour.Domain/`)

| Fichier | Action |
|---|---|
| `NotFoundReportAggregate/BookNotFoundReport.cs` | Créer — agrégat |
| `NotFoundReportAggregate/ValueObjects/NotFoundReportStatus.cs` | Créer — `Open, Cancelled, Found, Withdrawn, Dismissed, Lapsed` |
| `NotFoundReportAggregate/ValueObjects/NotFoundReportLocation.cs` | Créer — `Fair, Premises` |
| `NotFoundReportAggregate/ValueObjects/NotFoundWithdrawalReason.cs` | Créer — `NotFoundOnShelf, Damaged, Other` |
| `Common/Errors/Errors.NotFoundReport.cs` | Créer |
| `MemberSelectionAggregate/ValueObjects/MemberSelectionStatus.cs` | Modifier — ne garder que `ToTake`, `Purchased` |
| `MemberSelectionAggregate/MemberSelectionItem.cs` | Modifier — supprimer `ChangeStatus` |
| `AssociationSettingsAggregate/AssociationSettings.cs` | Modifier — `NotFoundReportDailyLimit` |

### Backend — Application (`src/Backend/Vole_Papillon_Damour.Application/`)

| Fichier | Action |
|---|---|
| `NotFoundReports/Commands/ReportNotFound/*` | Créer — commande membre |
| `NotFoundReports/Commands/CancelNotFoundReport/*` | Créer — annulation membre |
| `NotFoundReports/Commands/Admin/{MarkFound,WithdrawNotFound,DismissNotFound}/*` | Créer — clôtures |
| `NotFoundReports/Queries/GetNotFoundReportQueue/*` | Créer — file « À vérifier » |
| `NotFoundReports/Queries/GetClosedNotFoundReports/*` | Créer — onglet « Traités » |
| `NotFoundReports/Queries/GetNotFoundReportSummary/*` | Créer — compteurs (badge, tuile, fiche) |
| `NotFoundReports/Common/{NotFoundReportResults,NotFoundReportTarget,INotFoundReportLapser,NotFoundReportLapser}.cs` | Créer |
| `Books/Commands/Admin/BookWithdrawal.cs` | Créer — logique de retrait extraite de `WithdrawBookCommandHandler` |
| `Books/Commands/Admin/WithdrawBookCommandHandler.cs` | Modifier — utilise `BookWithdrawal` + lapser |
| `Books/Commands/RegisterSale/RegisterSaleCommandHandler.cs`, `Books/Commands/AdjustQuantity/AdjustQuantityCommandHandler.cs`, `Books/Commands/Admin/MergeBooksCommandHandler.cs`, `RareBooks/Commands/MarkRareBookSold/MarkRareBookSoldCommandHandler.cs` (+ handler de dépublication rare) | Modifier — appel du lapser |
| `MemberSelection/Commands/SetSelectionItemStatus/*` | **Supprimer** |
| `MemberSelection/Common/SelectionResults.cs`, `MemberSelection/Queries/GetMySelection/GetMySelectionQueryHandler.cs` | Modifier — état du signalement par ligne |
| `Common/Interfaces/Persistence/IProjectDbContext.cs` | Modifier — `DbSet<BookNotFoundReport> BookNotFoundReports` |
| `Books/Commands/AssociationSettings/*`, `Books/Queries/GetAssociationSettings/*`, `Books/Common/AssociationSettingsResult.cs` | Modifier — nouveau réglage |

### Backend — Infrastructure / Contracts / Api

| Fichier | Action |
|---|---|
| `Infrastructure/Persistence/Configurations/BookNotFoundReportConfiguration.cs` | Créer |
| `Infrastructure/Persistence/ProjectDbContext.cs` | Modifier — `DbSet` |
| `Infrastructure/Persistence/AccountDeletionStore.cs` | Modifier — anonymisation (`RG-75`) |
| `Infrastructure/Migrations/<ts>_AddBookNotFoundReports.cs` | Créer (SIG-2) |
| `Infrastructure/Migrations/<ts>_RetireSelectionPersonalStatuses.cs` | Créer (SIG-3) |
| `Infrastructure/Migrations/<ts>_AddNotFoundReportDailyLimit.cs` | Créer (SIG-6) |
| `Contracts/NotFoundReports/*.cs` | Créer — requêtes / réponses |
| `Contracts/MemberSelection/MySelectionResponse.cs` | Modifier — `notFoundReport` par ligne |
| `Contracts/MemberSelection/SetSelectionItemStatusRequest.cs` | **Supprimer** |
| `Api/Controllers/MemberAccountController.cs` | Modifier — routes membre, `PATCH` → `410` |
| `Api/Controllers/NotFoundReportAdministrationController.cs` | Créer — routes d'administration |
| `Api/Program.cs` (là où `UseMemberAccountController()` est appelé) | Modifier — `UseNotFoundReportAdministrationController()` |

### Catalog (`src/Catalog/src/app/`)

| Fichier | Action | Maquette |
|---|---|---|
| `core/catalog.models.ts` | Modifier — types | — |
| `core/catalog-member-api.service.ts` (+ spec) | Modifier — `reportNotFound`, `cancelNotFoundReport`, retirer `setSelectionStatus` | — |
| `core/catalog-admin-api.service.ts` (+ spec) | Modifier — file, clôtures, résumé, réglage | — |
| `features/account/selection/account-selection.component.{ts,html,scss,spec.ts}` | Modifier | `Main.html`, `SelectionMobile.html`, `AvantApres.html`, `SignalementEnvoye.html` |
| `features/account/selection/not-found-report-dialog.component.{ts,html,scss,spec.ts}` | Créer | `ConfirmationSignalement.html`, `ConfirmationMobile.html`, `ConnexionRequise.html` |
| `core/catalog-administration-route.ts` (+ spec) | Modifier — section `not-found` | — |
| `features/administration/not-found-reports/not-found-reports-view.component.{ts,html,scss,spec.ts}` | Créer | `AdminIntrouvables.html`, `AdminIntrouvablesTraites.html`, `AdminIntrouvablesMobile.html` |
| `features/administration/not-found-reports/not-found-report-close-dialog.component.{ts,html,scss,spec.ts}` | Créer | `AdminRetrait.html`, `AdminRetrouve.html`, `AdminSansSuite.html`, `AdminRetraitRare.html` |
| `features/administration/not-found-reports/not-found-export.ts` (+ spec) | Créer — CSV (patron `dead-stock-export.ts`) | — |
| `features/administration/catalog-administration-page.component.{ts,html,scss,spec.ts}` | Modifier — nav, badge, tuile, encadré fiche, réglage | `AdminIntrouvables.html` (barre latérale), `AdminTableauDeBord.html`, `AdminFiche.html` |
| `app.module.ts` | Modifier — déclarer les nouveaux composants | — |

---

# Étape 1 — Backend : modèle et signalement membre

### Task SIG-1 : Agrégat `BookNotFoundReport`

**Files :**
- Create : `Domain/NotFoundReportAggregate/BookNotFoundReport.cs`, `…/ValueObjects/{NotFoundReportStatus,NotFoundReportLocation,NotFoundWithdrawalReason}.cs`, `Domain/Common/Errors/Errors.NotFoundReport.cs`
- Test : `src/Backend/Vole_Papillon_Damour.Domain.tests/NotFoundReport/BookNotFoundReportTests.cs`

**Interfaces — Produces :**

```csharp
public sealed class BookNotFoundReport : Entity<Guid>
{
    public const int CommentMaxLength = 280;
    public const int ClosureNoteMaxLength = 500;

    public UserId? UserId { get; private set; }          // null après suppression du compte
    public Isbn13? Isbn13 { get; private set; }          // exactement un des deux
    public RareBookId? RareBookId { get; private set; }
    public NotFoundReportLocation? Location { get; private set; }
    public string? Comment { get; private set; }
    public NotFoundReportStatus Status { get; private set; }
    public DateTime ReportedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public UserId? ClosedBy { get; private set; }
    public string? ClosureNote { get; private set; }
    public NotFoundWithdrawalReason? WithdrawalReason { get; private set; }
    public int? WithdrawnQuantity { get; private set; }
    public BookMovementId? WithdrawalMovementId { get; private set; }

    public static BookNotFoundReport CreateForEdition(Guid id, UserId userId, Isbn13 isbn13,
        NotFoundReportLocation? location, string? comment, DateTime reportedAt);
    public static BookNotFoundReport CreateForRareBook(Guid id, UserId userId, RareBookId rareBookId,
        NotFoundReportLocation? location, string? comment, DateTime reportedAt);

    public bool IsOpen => Status == NotFoundReportStatus.Open;
    public void Cancel(DateTime at);                                        // membre
    public void MarkFound(UserId by, string? note, DateTime at);
    public void MarkWithdrawn(UserId by, NotFoundWithdrawalReason reason, int withdrawnQuantity,
        BookMovementId? movementId, string note, DateTime at);            // movementId null pour un rare
    public void Dismiss(UserId by, string note, DateTime at);
    public void Lapse(DateTime at);
    public void DetachMember();                                             // RG-75 : UserId = null, Comment = null
}
```

Règles : toute transition part de `Open` (sinon `InvalidOperationException`) ; dates UTC
via `DomainTime.RequireUtc` ; commentaire `Trim()`, vide → `null`, > 280 →
`ArgumentException` ; note de `Dismiss` / `MarkWithdrawn` obligatoire, ≤ 500 ;
`withdrawnQuantity ≥ 0`.

- [ ] **Step 1 : tests rouges** (noms à reprendre tels quels)
  - `CreateForEdition_StartsOpenWithTrimmedComment`
  - `Create_WithBlankComment_StoresNull`
  - `Create_WithCommentLongerThan280_Throws`
  - `Create_WithNonUtcDate_Throws`
  - `Cancel_FromOpen_SetsCancelledAndClosedAt`
  - `MarkFound_FromOpen_SetsFoundAndAuthor`
  - `MarkWithdrawn_WithoutNote_Throws`
  - `AnyTransition_FromClosedState_Throws` (théorie sur les 5 transitions)
  - `Lapse_FromOpen_SetsLapsedWithoutAuthor`
  - `DetachMember_ClearsUserAndComment_KeepsTargetAndStatus`
- [ ] **Step 2 :** `dotnet test src/Backend/Vole_Papillon_Damour.Domain.tests --filter "FullyQualifiedName~BookNotFoundReportTests"` → FAIL (compilation).
- [ ] **Step 3 :** implémenter (même forme que `MemberSelectionItem` : constructeur privé, constructeur sans paramètre public pour EF).
- [ ] **Step 4 :** relancer → PASS.
- [ ] **Step 5 :** `git commit -m "feat(not-found): add book not-found report aggregate"`

---

### Task SIG-2 : Persistance et migration `AddBookNotFoundReports`

**Files :**
- Create : `Infrastructure/Persistence/Configurations/BookNotFoundReportConfiguration.cs`, migration `AddBookNotFoundReports`
- Modify : `Application/Common/Interfaces/Persistence/IProjectDbContext.cs`, `Infrastructure/Persistence/ProjectDbContext.cs`
- Test : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/NotFoundReport/BookNotFoundReportConfigurationTests.cs` (patron `MemberSelectionItemConfigurationTests`)

Table `BookNotFoundReports` : colonnes = propriétés de SIG-1 ; `Isbn13` `char(13)`,
`Comment` `nvarchar(280)`, `ClosureNote` `nvarchar(500)`, enums en `tinyint`. Index :
- `IX_BookNotFoundReports_Status_Isbn13` (`Status`, `Isbn13`) ;
- `IX_BookNotFoundReports_Status_RareBookId` (`Status`, `RareBookId`) ;
- `IX_BookNotFoundReports_UserId_ReportedAt` (`UserId`, `ReportedAt`) ;
- **unique filtré** `UX_BookNotFoundReports_OpenPerMemberEdition` sur (`UserId`, `Isbn13`) `WHERE [Status] = 0 AND [Isbn13] IS NOT NULL AND [UserId] IS NOT NULL` ;
- **unique filtré** `UX_BookNotFoundReports_OpenPerMemberRareBook` sur (`UserId`, `RareBookId`) `WHERE [Status] = 0 AND [RareBookId] IS NOT NULL AND [UserId] IS NOT NULL`.

Pas de clé étrangère vers `Books` (une fiche peut être fusionnée ; la cible est résolue
à la lecture comme dans `SelectionTargetResolver`).

- [ ] **Step 1 :** tests rouges — `Configuration_MapsTableAndMaxLengths`, `Configuration_DeclaresFilteredUniqueIndexes`, `Save_TwoOpenReportsSameMemberSameIsbn_Throws` (SQLite).
- [ ] **Step 2 :** `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter "FullyQualifiedName~BookNotFoundReportConfigurationTests"` → FAIL.
- [ ] **Step 3 :** configuration + `DbSet` + `dotnet ef migrations add AddBookNotFoundReports --project src/Backend/Vole_Papillon_Damour.Infrastructure --startup-project src/Backend/Vole_Papillon_Damour.Api`.
- [ ] **Step 4 :** PASS ; relire le SQL généré (`dotnet ef migrations script`) : filtres présents.
- [ ] **Step 5 :** `git commit -m "feat(not-found): persist not-found reports"`

---

### Task SIG-3 : Retrait des états personnels de Ma sélection

**Files :**
- Modify : `MemberSelectionStatus.cs` (garder `ToTake = 0`, `Purchased = 1` **avec ces valeurs explicites**), `MemberSelectionItem.cs` (supprimer `ChangeStatus`, garder `MarkPurchased`)
- Delete : `Application/MemberSelection/Commands/SetSelectionItemStatus/*`, leurs tests, `Contracts/MemberSelection/SetSelectionItemStatusRequest.cs`
- Create : migration `RetireSelectionPersonalStatuses`
- Test : `Domain.tests/MemberSelection/MemberSelectionItemTests.cs` (adapter), `Infrastructure.tests/MemberSelection/RetireSelectionPersonalStatusesMigrationTests.cs`

Migration (SQL brut dans `Up`, **dans cet ordre**) — `Q-SIG-2` :

```sql
-- 1. « Pas trouvé » récents sur une édition encore disponible → signalement ouvert
INSERT INTO BookNotFoundReports (Id, UserId, Isbn13, RareBookId, Location, Comment, Status, ReportedAt)
SELECT NEWID(), s.UserId, s.Isbn13, NULL, NULL, NULL, 0, s.StatusChangedAt
FROM MemberSelectionItems s
JOIN Books b ON b.Id = s.Isbn13
WHERE s.Status = 2 AND s.Isbn13 IS NOT NULL
  AND s.StatusChangedAt >= DATEADD(day, -30, SYSUTCDATETIME())
  AND b.QuantityAvailable > 0 AND b.IsHiddenFromCatalog = 0 AND b.RedirectedToIsbn13 IS NULL;
-- 2. Tous les états personnels redeviennent neutres
UPDATE MemberSelectionItems SET Status = 0 WHERE Status IN (2, 3);
```

`Down` : sans objet (`throw new NotSupportedException()` n'est pas permis en migration ;
laisser `Down` vide avec un commentaire « perte d'information assumée, F-11 §10 »).

- [ ] **Step 1 :** tests rouges — `MemberSelectionStatus_OnlyDefinesToTakeAndPurchased`, `Migration_ConvertsRecentNotFoundOnAvailableEditionToOpenReport`, `Migration_ResetsToRevisitAndOldNotFoundToToTake`.
- [ ] **Step 2 :** FAIL.
- [ ] **Step 3 :** implémenter ; corriger toutes les références (`grep -rn "NotFound\b\|ToRevisit\|SetSelectionItemStatus" src/Backend`).
- [ ] **Step 4 :** `dotnet build Vole_Papillon_Damour.slnx` puis `dotnet test` sur les trois projets → PASS.
- [ ] **Step 5 :** `git commit -m "refactor(selection): retire member personal statuses"`

---

### Task SIG-4 : Route `PATCH` obsolète → `410 Gone`

**Files :** Modify `Api/Controllers/MemberAccountController.cs` ; Test `Api.tests/MemberAccount/MemberSelectionEndpointsTests.cs`

Le `MapPatch("/catalog/me/selection/{id:guid}")` est conservé **uniquement** pour répondre
`Results.Problem(statusCode: 410, title: "Selection statuses were retired", detail: "Use POST /catalog/me/selection/{id}/not-found-report.")`
(authentification toujours exigée). Supprimer le test `PatchSelection_WithUnknownStatus_Returns400`.

- [ ] **Step 1 :** tests rouges — `PatchSelection_Authenticated_Returns410`, `PatchSelection_WithoutAuthentication_Returns401`.
- [ ] **Step 2–4 :** FAIL → implémenter → PASS (`--filter "FullyQualifiedName~MemberSelectionEndpointsTests"`).
- [ ] **Step 5 :** `git commit -m "feat(selection): answer 410 for retired status route"`

---

### Task SIG-5 : Commandes membre — signaler et annuler

**Files :**
- Create : `Application/NotFoundReports/Commands/ReportNotFound/{ReportNotFoundCommand,ReportNotFoundCommandHandler,ReportNotFoundCommandValidator}.cs`, `…/CancelNotFoundReport/{CancelNotFoundReportCommand,CancelNotFoundReportCommandHandler}.cs`, `NotFoundReports/Common/NotFoundReportResults.cs`
- Test : `Application.tests/NotFoundReports/ReportNotFoundCommandHandlerTests.cs`, `…/CancelNotFoundReportCommandHandlerTests.cs` (fixture sur le modèle de `MemberSelectionFixture`)

**Interfaces :**

```csharp
// Identité membre : mêmes champs que AddSelectionItemCommand (ExternalId, Email, FirstName, LastName)
public sealed record ReportNotFoundCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    Guid SelectionItemId,
    NotFoundReportLocation? Location,
    string? Comment) : IRequest<ErrorOr<NotFoundReportCreatedResult>>;

public sealed record NotFoundReportCreatedResult(Guid ReportId, DateTimeOffset ReportedAt, bool AlreadyOpen);

public sealed record CancelNotFoundReportCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    Guid ReportId) : IRequest<ErrorOr<Deleted>>;
```

Algorithme de `ReportNotFoundCommandHandler` :
1. Résoudre le membre (comme `AddSelectionItemCommandHandler`) ; ligne de sélection du
   membre par `SelectionItemId` → sinon `Errors.MemberSelection.NotFound`.
2. Ligne `Purchased` → `Errors.NotFoundReport.NotReportable()`.
3. Disponibilité **recalculée** avec `SelectionAvailabilityProjector` : ≠ `Available` →
   `Errors.NotFoundReport.NotReportable()` (`RG-68`). Pour une édition redirigée, viser
   l'ISBN canonique.
4. Signalement ouvert existant (membre + cible) → le renvoyer, `AlreadyOpen = true` (`RG-69`).
5. Plafond (`RG-70`) : nombre de signalements du membre avec `ReportedAt > now - 24 h`
   ≥ `AssociationSettings.NotFoundReportDailyLimit` → `Errors.NotFoundReport.DailyLimitReached(limit)`.
6. Créer, `SaveChanges`, log `NotFoundReportCreated { reportId, targetKind, alreadyOpen }`.
7. `DbUpdateException` sur l'index unique (course) → relire et renvoyer l'existant.

Validator : `Comment` ≤ 280 ; `Location` défini si présent.

`CancelNotFoundReportCommandHandler` : signalement du membre (sinon `NotFound`) ; `Open`
→ `Cancel` ; déjà clos → `Errors.NotFoundReport.AlreadyClosed()` (409).

- [ ] **Step 1 :** tests rouges —
  `Handle_OnAvailableSelectedEdition_CreatesOpenReport`,
  `Handle_WhenOpenReportExists_ReturnsExistingWithoutCreating`,
  `Handle_OnPurchasedLine_ReturnsNotReportable`,
  `Handle_OnOutOfStockEdition_ReturnsNotReportable`,
  `Handle_OnAnnouncedEdition_ReturnsNotReportable`,
  `Handle_OnSoldRareBook_ReturnsNotReportable`,
  `Handle_OnAvailableRareBook_CreatesOpenReport`,
  `Handle_WhenDailyLimitReached_ReturnsDailyLimitReached`,
  `Handle_ReportsOlderThan24h_DoNotCountTowardsLimit`,
  `Handle_OnAnotherMembersSelectionItem_ReturnsNotFound`,
  `Handle_DoesNotChangeBookQuantity` (`RG-71`),
  `Cancel_OpenReport_SetsCancelled`, `Cancel_ClosedReport_ReturnsConflict`, `Cancel_OtherMembersReport_ReturnsNotFound`.
- [ ] **Step 2 :** `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~NotFoundReports"` → FAIL.
- [ ] **Step 3 :** implémenter.
- [ ] **Step 4 :** PASS.
- [ ] **Step 5 :** `git commit -m "feat(not-found): let members report a missing book"`

---

### Task SIG-6 : Réglage `NotFoundReportDailyLimit`

**Files :** Modify `AssociationSettings.cs` (+ `Update(...)`), `AssociationSettingsConfiguration.cs`, `UpdateAssociationSettingsCommand{,Handler,Validator}.cs`, `AssociationSettingsResult.cs`, `GetAssociationSettingsQueryHandler.cs`, requêtes/réponses Contracts et mapping de la route Paramètres ; Create migration `AddNotFoundReportDailyLimit` (colonne `int NOT NULL DEFAULT 10`).
Tests : `Domain.tests/AssociationSettingsAggregateTests/AssociationSettingsTests.cs`, `Application.tests/Books/Commands/AssociationSettings/AssociationSettingsCommandHandlerTests.cs`.

- [ ] **Step 1 :** tests rouges — `Create_DefaultsNotFoundReportDailyLimitTo10`, `Validator_RejectsLimitOutside1To100`, `Handle_UpdatesNotFoundReportDailyLimit`.
- [ ] **Step 2–4 :** FAIL → implémenter (bornes 1..100) → PASS.
- [ ] **Step 5 :** `git commit -m "feat(settings): configure the daily not-found report limit"`

> Ordre : SIG-6 peut être fait avant SIG-5 ; SIG-5 a besoin de la propriété.

---

### Task SIG-7 : Ma sélection expose l'état du signalement

**Files :** Modify `SelectionResults.cs`, `GetMySelectionQueryHandler.cs`, `Contracts/MemberSelection/MySelectionResponse.cs`, mapping dans `MemberAccountController` ; Test `Application.tests/MemberSelection/GetMySelectionQueryHandlerTests.cs`.

Ajouter à `SelectionItemResult` :

```csharp
NotFoundReportSummary? NotFoundReport
public sealed record NotFoundReportSummary(Guid Id, string Status, DateTimeOffset ReportedAt, DateTimeOffset? ClosedAt);
// Status : "Open" | "Found" | "Withdrawn" | "Dismissed" | "Lapsed"
```

Règle : le **dernier** signalement du membre sur la cible de la ligne, s'il est `Open`,
ou clos **depuis moins de 30 jours** (hors `Cancelled`, jamais renvoyé). Sinon `null`.
Une seule requête groupée pour toute la sélection (pas de N+1).

Routes membre ajoutées dans cette tâche (même contrôleur) :
- `POST /catalog/me/selection/{id:guid}/not-found-report` body `{ location?: "Fair"|"Premises", comment?: string }` → `201 { reportId, reportedAt, alreadyOpen: false }` ou `200 {…, alreadyOpen: true }` ; erreurs `400` (validation), `404`, `409 NotReportable`, `429 DailyLimitReached`.
- `DELETE /catalog/me/not-found-reports/{reportId:guid}` → `204` ; `404` ; `409`.

Mapping d'erreurs : ajouter `NotFoundReport.DailyLimitReached` → `429` dans `Api/Errors/ErrorOrStatusCode.cs` (vérifier le mécanisme existant ; sinon `Error.Custom(429, …)`).

Tests Api (`Api.tests/MemberAccount/NotFoundReportEndpointsTests.cs`) : `PostReport_WithoutAuthentication_Returns401`, `PostReport_ForwardsIdentityItemAndPayload`, `PostReport_WhenAlreadyOpen_Returns200`, `PostReport_WhenLimitReached_Returns429`, `DeleteReport_Returns204`.

- [ ] **Step 1 :** tests rouges (application + API) dont `Handle_ReturnsOpenReportOnLine`, `Handle_HidesReportsClosedMoreThan30DaysAgo`, `Handle_NeverReturnsCancelledReports`, `Handle_UsesSingleQueryForReports`.
- [ ] **Step 2–4 :** FAIL → implémenter → PASS.
- [ ] **Step 5 :** `git commit -m "feat(not-found): expose report state in member selection"`

---

# Étape 2 — Backend : file d'administration

### Task SIG-8 : Clôtures d'administration (retrouvé, retrait, sans suite)

**Files :**
- Create : `Books/Commands/Admin/BookWithdrawal.cs` — extraire le cœur de `WithdrawBookCommandHandler` (résolution canonique, contrôle de quantité, `ApplyQuantityCorrection`, création du `BookMovement` `Withdrawal`) en une méthode `Task<ErrorOr<BookWithdrawalOutcome>> WithdrawAsync(IProjectDbContext db, string isbn, int quantity, string note, UserId by, DateTime at, CancellationToken ct)` **sans** gestion de transaction ; `WithdrawBookCommandHandler` l'appelle dans sa transaction (comportement inchangé, ses tests existants doivent rester verts).
- Create : `NotFoundReports/Commands/Admin/MarkFound/*`, `…/WithdrawNotFound/*`, `…/DismissNotFound/*`
- Test : `Application.tests/NotFoundReports/Admin/*Tests.cs`

**Interfaces :**

```csharp
public sealed record NotFoundReportTargetRef(string Kind, string Reference); // Kind "edition"|"rare", Reference ISBN-13 ou Guid

public sealed record MarkNotFoundTargetFoundCommand(NotFoundReportTargetRef Target, string? Note, UserId By)
    : IRequest<ErrorOr<NotFoundClosureResult>>;
public sealed record WithdrawNotFoundTargetCommand(NotFoundReportTargetRef Target, int QuantityFound,
    NotFoundWithdrawalReason Reason, string Note, UserId By) : IRequest<ErrorOr<NotFoundClosureResult>>;
public sealed record DismissNotFoundTargetCommand(NotFoundReportTargetRef Target, string Note, UserId By)
    : IRequest<ErrorOr<NotFoundClosureResult>>;

public sealed record NotFoundClosureResult(int ClosedReportCount, int? WithdrawnQuantity,
    int? QuantityAvailable, Guid? MovementId);
```

Règles :
- Aucune ouverte sur la cible → `Errors.NotFoundReport.NothingToClose()` (409).
- **Retrait édition** : `quantityToWithdraw = QuantityAvailable - QuantityFound` ;
  `QuantityFound` hors `[0, QuantityAvailable]` → `400` ; si `quantityToWithdraw == 0`
  → équivaut à « retrouvé » (clôture `Found`, pas de mouvement). Sinon
  `BookWithdrawal.WithdrawAsync(...)` avec la note saisie (défaut côté front :
  « Introuvable en rayon — N signalements ») puis `MarkWithdrawn(..., movementId, ...)` sur
  **tous** les ouverts, **une seule transaction**.
- **Retrait rare** : `QuantityFound` ignoré ; `rareBook.Unpublish(at, by)` ; `MarkWithdrawn(..., movementId: null, ...)`.
- Log `NotFoundReportsClosed { target, outcome, count, withdrawn }`.

- [ ] **Step 1 :** tests rouges —
  `MarkFound_ClosesAllOpenReportsAndKeepsStock`,
  `Withdraw_WithZeroFound_WithdrawsAllAvailable`,
  `Withdraw_WithPartialFound_WithdrawsDifference`,
  `Withdraw_WithAllFound_ClosesAsFoundWithoutMovement`,
  `Withdraw_WithFoundAboveAvailable_Returns400`,
  `Withdraw_CreatesWithdrawalMovementWithNoteAndAuthor`,
  `Withdraw_OnRedirectedIsbn_TargetsCanonicalBook`,
  `Withdraw_OnRareBook_UnpublishesAndClosesReports`,
  `Dismiss_WithoutNote_Returns400`,
  `AnyClosure_WithoutOpenReports_ReturnsConflict`,
  `AnyClosure_LeavesOtherTargetsReportsOpen`,
  et **non-régression** : suite existante de `WithdrawBookCommandHandler` verte.
- [ ] **Step 2–4 :** FAIL → implémenter → PASS.
- [ ] **Step 5 :** `git commit -m "feat(not-found): close reports from the administration"`

---

### Task SIG-9 : Caducité automatique (`RG-73`)

**Files :** Create `NotFoundReports/Common/{INotFoundReportLapser,NotFoundReportLapser}.cs` (enregistré dans le `DependencyInjection` de l'Application) ; Modify `RegisterSaleCommandHandler`, `WithdrawBookCommandHandler`, `AdjustQuantityCommandHandler`, `MergeBooksCommandHandler`, `MarkRareBookSoldCommandHandler`, et le handler qui appelle `RareBook.Unpublish` hors signalement (`grep -rn "Unpublish(" src/Backend/Vole_Papillon_Damour.Application`).

```csharp
public interface INotFoundReportLapser
{
    // Passe en Lapsed les ouverts de la cible si elle n'est plus disponible. À appeler AVANT SaveChanges.
    Task LapseIfUnavailableAsync(Isbn13 isbn13, DateTime at, CancellationToken ct);
    Task LapseRareBookAsync(RareBookId rareBookId, DateTime at, CancellationToken ct);
}
```

Pour une fusion : les ouverts sur l'ISBN absorbé sont **reportés** sur l'ISBN canonique
(nouvelle cible), puis caducité évaluée sur le canonique.

- [ ] **Step 1 :** tests rouges — `RegisterSale_WhenStockReachesZero_LapsesOpenReports`, `RegisterSale_WhenStockStaysPositive_KeepsReportsOpen`, `AdjustQuantity_ToZero_LapsesOpenReports`, `MarkRareBookSold_LapsesOpenReports`, `MergeBooks_MovesOpenReportsToCanonicalIsbn`, `WithdrawBook_ToZero_LapsesOpenReports`.
- [ ] **Step 2–4 :** FAIL → implémenter → PASS ; `dotnet test` complet des trois projets.
- [ ] **Step 5 :** `git commit -m "feat(not-found): lapse reports when stock runs out"`

---

### Task SIG-10 : Lectures d'administration

**Files :** Create `NotFoundReports/Queries/{GetNotFoundReportQueue,GetClosedNotFoundReports,GetNotFoundReportSummary}/*` ; Tests `Application.tests/NotFoundReports/Queries/*Tests.cs`.

```csharp
public sealed record GetNotFoundReportQueueQuery(string Kind /* all|edition|rare */,
    string Sort /* most-reported|oldest|newest|genre */, int Page, int PageSize, bool RareOnly)
    : IRequest<ErrorOr<NotFoundReportQueueResult>>;

public sealed record NotFoundReportQueueResult(DateTimeOffset GeneratedAt, int OpenTargetCount,
    int OpenReportCount, int OverdueTargetCount, int Page, int PageSize, int TotalCount,
    IReadOnlyList<NotFoundReportTargetResult> Items);

public sealed record NotFoundReportTargetResult(string Kind, string? Isbn13, Guid? RareBookId,
    string Title, string? Authors, string? Publisher, int? PublicationYear, string? CoverUrl,
    string? Genre, int QuantityAvailable, int ReportCount, int MemberCount,
    DateTimeOffset FirstReportedAt, DateTimeOffset LastReportedAt, bool Overdue,
    IReadOnlyList<NotFoundReportCommentResult> Comments);

public sealed record NotFoundReportCommentResult(string Text, string? Location, DateTimeOffset ReportedAt);

public sealed record GetClosedNotFoundReportsQuery(int Page, int PageSize, bool RareOnly)
    : IRequest<ErrorOr<ClosedNotFoundReportsResult>>;
// Items : { ClosedAt, Kind, Isbn13?, RareBookId?, Title, Outcome, ReportCount, WithdrawnQuantity?, ClosedByName?, Note? }
// Une ligne par clôture groupée (même cible + même ClosedAt + même ClosedBy + même Status).

public sealed record GetNotFoundReportSummaryQuery(string? Isbn13, Guid? RareBookId, bool RareOnly)
    : IRequest<ErrorOr<NotFoundReportSummaryResult>>;
// Sans cible : { OpenTargetCount, OverdueTargetCount } (badge, tuile) ;
// avec cible : { OpenReportCount, FirstReportedAt?, LatestComment? } (encadré de fiche).
```

Règles : `Overdue` = premier signalement ouvert plus vieux que **7 jours** ; `MemberCount`
= `UserId` distincts non nuls (+ 1 par signalement anonymisé) ; aucune donnée de membre
dans les résultats (`RG-74`) ; `ClosedByName` = nom du **bénévole**, pas du membre ;
`PageSize` ≤ 50 ; `RareOnly = true` pour un compte `RareBooks` sans `Administration`.

- [ ] **Step 1 :** tests rouges — `Queue_GroupsReportsByTarget`, `Queue_SortsByMostReportedByDefault`, `Queue_FlagsTargetsOlderThan7DaysAsOverdue`, `Queue_ExcludesClosedAndCancelled`, `Queue_NeverExposesMemberIdentity` (réflexion sur les propriétés du résultat), `Queue_RareOnly_ReturnsOnlyRareTargets`, `Closed_GroupsOneLinePerClosure`, `Summary_CountsOpenAndOverdueTargets`, `Summary_ForIsbn_ReturnsCountAndLatestComment`.
- [ ] **Step 2–4 :** FAIL → implémenter (projection SQL, pagination côté serveur) → PASS.
- [ ] **Step 5 :** `git commit -m "feat(not-found): read the administration queue"`

---

### Task SIG-11 : Routes d'administration

**Files :** Create `Api/Controllers/NotFoundReportAdministrationController.cs`, `Contracts/NotFoundReports/*.cs` ; Modify `Program.cs` ; Test `Api.tests/NotFoundReports/NotFoundReportAdministrationEndpointsTests.cs`.

| Méthode | Route | Policy | Corps / réponse |
|---|---|---|---|
| GET | `/books/admin/not-found-reports?kind=&sort=&page=&pageSize=` | `RareBooks` (filtre `RareOnly` si pas `Administration`) | `NotFoundReportQueueResponse` |
| GET | `/books/admin/not-found-reports/closed?page=&pageSize=` | idem | `ClosedNotFoundReportsResponse` |
| GET | `/books/admin/not-found-reports/summary?isbn13=&rareBookId=` | idem | `NotFoundReportSummaryResponse` |
| POST | `/books/admin/not-found-reports/editions/{isbn13}/found` | `Administration` | `{ note? }` → `200 NotFoundClosureResponse` |
| POST | `/books/admin/not-found-reports/editions/{isbn13}/withdrawal` | `Administration` | `{ quantityFound, reason, note }` → `200` |
| POST | `/books/admin/not-found-reports/editions/{isbn13}/dismissal` | `Administration` | `{ note }` → `200` |
| POST | `/books/admin/not-found-reports/rare-books/{rareBookId:guid}/found` | `RareBooks` | idem |
| POST | `/books/admin/not-found-reports/rare-books/{rareBookId:guid}/withdrawal` | `RareBooks` | `{ reason, note }` |
| POST | `/books/admin/not-found-reports/rare-books/{rareBookId:guid}/dismissal` | `RareBooks` | `{ note }` |

Identité de l'auteur : `TryGetUserId(principal, out var userId)` comme pour
`/books/admin/books/{isbn13}/withdrawals`.

- [ ] **Step 1 :** tests rouges — `GetQueue_WithoutAdministrationOrRareBooks_Returns403`, `GetQueue_ForRareBooksOnlyUser_SendsRareOnlyTrue`, `PostEditionWithdrawal_ForwardsQuantityReasonNoteAndAuthor`, `PostEditionWithdrawal_ForRareBooksOnlyUser_Returns403`, `PostRareWithdrawal_ForRareBooksUser_Returns200`, `PostFound_WhenNothingToClose_Returns409`.
- [ ] **Step 2–4 :** FAIL → implémenter → PASS.
- [ ] **Step 5 :** `git commit -m "feat(not-found): expose administration routes"`

---

# Étape 3 — Catalog : Ma sélection

### Task SIG-12 : Modèles et services API (Catalog)

**Files :** Modify `core/catalog.models.ts`, `core/catalog-member-api.service.ts` (+ spec), `core/catalog-admin-api.service.ts` (+ spec).

```ts
export type CatalogSelectionStatus = 'ToTake' | 'Purchased';
export type CatalogNotFoundReportStatus = 'Open' | 'Found' | 'Withdrawn' | 'Dismissed' | 'Lapsed';
export type CatalogNotFoundLocation = 'Fair' | 'Premises';
export interface CatalogNotFoundReportSummary { id: string; status: CatalogNotFoundReportStatus; reportedAt: string; closedAt: string | null; }
// CatalogSelectionItem gagne : notFoundReport: CatalogNotFoundReportSummary | null;

// CatalogMemberApiService
reportNotFound(token: string, selectionItemId: string, body: {location: CatalogNotFoundLocation | null; comment: string | null}): Observable<{reportId: string; reportedAt: string; alreadyOpen: boolean}>;
cancelNotFoundReport(token: string, reportId: string): Observable<void>;
// supprimer setSelectionStatus

// CatalogAdminApiService
getNotFoundQueue(token, params: {kind; sort; page; pageSize}): Observable<CatalogNotFoundQueue>;
getClosedNotFoundReports(token, page, pageSize): Observable<CatalogClosedNotFoundReports>;
getNotFoundSummary(token, target?: {isbn13?: string; rareBookId?: string}): Observable<CatalogNotFoundSummary>;
closeNotFound(token, target: {kind: 'edition'|'rare'; reference: string}, action: 'found'|'withdrawal'|'dismissal', body: object): Observable<CatalogNotFoundClosure>;
```

- [ ] **Step 1 :** specs rouges (`HttpTestingController`) — URL, méthode, en-tête `Authorization`, corps exact de chaque appel ; `setSelectionStatus` n'existe plus.
- [ ] **Step 2 :** `cd src/Catalog && npm test -- --watch=false --include='src/app/core/**/*.spec.ts'` → FAIL.
- [ ] **Step 3–4 :** implémenter → PASS.
- [ ] **Step 5 :** `git commit -m "feat(catalog): add not-found report api clients"`

---

### Task SIG-13 : Ma sélection — suppression du sélecteur, bouton et états

**Maquettes :** `preview/Main.html` (desktop, tous les états), `preview/SelectionMobile.html` (390 px), `preview/AvantApres.html` (ce qui disparaît), `preview/SignalementEnvoye.html` (après envoi).

**Files :** Modify `features/account/selection/account-selection.component.{ts,html,scss,spec.ts}`.

Comportement :
- Supprimer `STATUSES`, `isSelectedStatus`, `setStatus`, le bloc `.selection-status-control` et ses styles (`CA-1`).
- Filtres : `Tout`, `Encore disponible`, `Acheté`, **`Signalés`** (ligne avec `notFoundReport` non nul), `Indisponibles`. Supprimer `Prochaine visite` et `Pas trouvé`. `SelectionFilter = 'all' | 'available' | 'purchased' | 'reported' | 'unavailable'`.
- Bouton **« Je ne l'ai pas trouvé »** (icône drapeau, style contour, à gauche de « Retirer ») affiché **si et seulement si** : `availability === 'Available'` **et** `status !== 'Purchased'` **et** (`notFoundReport === null` **ou** statut `Found` / `Dismissed` / `Lapsed`). Pour une ligne locale (non connecté) : bouton affiché, ouvre l'invitation de connexion (SIG-14).
- Ligne avec signalement `Open` : encadré « Signalé introuvable le {date} — Un bénévole va vérifier en rayon. Merci pour votre aide. » + lien-bouton **« Annuler le signalement »** → `cancelNotFoundReport` puis `selection.refresh()`.
- `Found` : « Un bénévole l'a retrouvé en rayon le {closedAt}. » (bouton de nouveau visible).
- `Withdrawn` : « Retiré du catalogue après votre signalement. Merci ! ».
- `Dismissed`, `Lapsed` : aucun message.
- Après envoi réussi : toast `role="status"` « Merci, un bénévole va vérifier. » (4 s).
- Erreurs : `429` → « Vous avez déjà beaucoup signalé aujourd'hui, merci ! » ; `409` → « Ce livre n'est plus signalable : sa disponibilité a changé. » puis `refresh()` ; autre → « Le signalement n'a pas pu être envoyé. Réessayez. ».

- [x] **Step 1 :** specs rouges —
  `does not render any personal status control`,
  `shows the report button only on available non-purchased lines`,
  `hides the report button on announced, out-of-stock, rare-sold and purchased lines`,
  `shows the open report notice with a cancel action`,
  `cancelling a report calls the api and refreshes`,
  `shows the found message and the button again when a report was found`,
  `shows the withdrawn thank-you message`,
  `filters reported lines with the Signalés filter`,
  `no longer offers Prochaine visite or Pas trouvé filters`,
  `maps a 429 to the daily limit message`.
- [ ] **Step 2 :** `npm test -- --watch=false --include='src/app/features/account/selection/**/*.spec.ts'` → FAIL.
- [ ] **Step 3 :** implémenter ; styles alignés sur la maquette avec les variables `--catalog-*` existantes.
- [ ] **Step 4 :** PASS ; puis `npm run build`.
- [ ] **Step 5 :** 🧪 comparer à `Main.html` (1440 px) et `SelectionMobile.html` (390 px) dans `npm start`.
- [ ] **Step 6 :** `git commit -m "feat(catalog): replace selection statuses with a not-found report"`

---

### Task SIG-14 : Modale de signalement et invitation à se connecter

**Maquettes :** `preview/ConfirmationSignalement.html` (modale desktop), `preview/ConfirmationMobile.html` (feuille bas d'écran < 640 px), `preview/ConnexionRequise.html` (visiteur non connecté).

**Files :** Create `features/account/selection/not-found-report-dialog.component.{ts,html,scss,spec.ts}` ; Modify `app.module.ts`, `account-selection.component.*`.

Composant `app-not-found-report-dialog` (inputs : `item` titre/auteur/éditeur/couverture, `mode: 'report' | 'login'` ; outputs : `submitted({location, comment})`, `closed()`, `loginRequested()`) :
- mode `report` : titre « Vous n'avez pas trouvé ce livre ? », rappel du livre, texte « Un bénévole ira vérifier en rayon. Si le livre a disparu, il sera retiré du catalogue. Merci de nous aider à le garder juste ! », radios facultatives **« À la bourse » / « Au local »** (aucune présélection côté code ; la maquette montre un état sélectionné), `textarea` 280 caractères avec compteur, boutons « Annuler » / « Envoyer le signalement » (désactivé pendant l'envoi).
- mode `login` : « Connectez-vous pour signaler ce livre », texte de la maquette, « Se connecter » (`auth.login('/compte')`) / « Plus tard ».
- Accessibilité : `role="dialog"`, `aria-modal`, `aria-labelledby`, focus initial sur le premier contrôle, piège de focus, `Échap` = fermer, retour du focus au bouton d'origine.
- < 640 px : feuille ancrée en bas (poignée, coins supérieurs arrondis) ; ≥ 640 px : modale centrée 500 px.

- [ ] **Step 1 :** specs rouges — `emits trimmed comment and selected location`, `emits null location when none chosen`, `limits the comment to 280 characters`, `disables submit while sending`, `closes on Escape and restores focus`, `login mode requests login`.
- [ ] **Step 2–4 :** FAIL → implémenter → PASS.
- [ ] **Step 5 :** 🧪 comparaison aux trois maquettes, clavier seul, lecteur d'écran (NVDA ou VoiceOver) : annonce du titre de la modale.
- [ ] **Step 6 :** `git commit -m "feat(catalog): add the not-found report dialog"`

---

# Étape 4 — Catalog : administration

### Task SIG-15 : Section « Livres introuvables », badge, tuile, fiche, réglage

**Maquettes :** `preview/AdminIntrouvables.html` (nav + file), `preview/AdminIntrouvablesTraites.html`, `preview/AdminIntrouvablesMobile.html`, `preview/AdminRetrait.html`, `preview/AdminRetrouve.html`, `preview/AdminSansSuite.html`, `preview/AdminRetraitRare.html`, `preview/AdminTableauDeBord.html`, `preview/AdminFiche.html`.

**Files :**
- Modify `core/catalog-administration-route.ts` (+ spec) : ajouter `'not-found'` à `CATALOG_ADMIN_SECTIONS` → URL `/administration/not-found`.
- Create `features/administration/not-found-reports/not-found-reports-view.component.*` : **composant enfant** (ne pas grossir `catalog-administration-page.component.ts`, déjà > 85 Ko). Inputs : `accessToken`, `canManageEditions`, `canManageRareBooks` ; output `countsChanged`.
- Create `features/administration/not-found-reports/not-found-report-close-dialog.component.*` : les trois décisions (+ variante rare).
- Create `features/administration/not-found-reports/not-found-export.ts` (+ spec) : CSV « liste de tournée » (Titre ; Auteur ; ISBN ou fiche rare ; Rayon ; Stock ; Signalements ; Dernier signalement ; colonne vide « Trouvé ? ») — même échappement que `dead-stock-export.ts`.
- Modify `catalog-administration-page.component.*` :
  - `navGroups` : `{id: 'not-found', label: 'Livres introuvables', icon: 'flag'}` dans « Pendant la bourse » **juste après** `dead-stock` ; pour le rôle `LivresRares` seul, l'ajouter aussi dans son groupe (fiches rares seulement) ; icône `flag` dans le `@switch` de la barre latérale.
  - `navBadge('not-found')` = `openTargetCount` (masqué si 0), badge orange (maquette).
  - Tableau de bord : tuile « À vérifier en rayon — N livres introuvables — dont M signalés il y a plus de 7 jours — Ouvrir la liste → » (masquée si 0).
  - Fiche catalogue : encadré « Signalée introuvable » en tête de la colonne latérale si `summary.openReportCount > 0`, avec « Retrouvé » et « Retirer… » (ouvrent la même modale) ; dans l'historique des mouvements, pastille « suite à signalement » sur un `Withdrawal` dont l'identifiant figure dans `summary.withdrawalMovementIds` (ajouter ce champ à la réponse de résumé si besoin — SIG-10).
  - Paramètres : champ « Signalements par membre et par 24 h » (1–100) branché sur `NotFoundReportDailyLimit`.

Vue « Livres introuvables » :
- En-tête : kicker « Pendant la bourse », titre « Livres introuvables. », intro de la maquette.
- Barre : onglets **À vérifier (N)** / **Traités (N)**, sélecteurs **Type** (Tous, Éditions, Livres rares) et **Trier** (Plus signalés, Plus anciens, Plus récents, Rayon).
- Titre de liste « N fiches à vérifier » + « X signalements · Y en attente depuis plus de 7 jours » + bouton « Liste de tournée (CSV) ».
- Ligne = une fiche : couverture, badge *Livre rare*, titre — auteur, pastille « N signalements · M membres », ISBN / fiche rare, éditeur · année, **stock** (« exemplaire unique » pour un rare), rayon (`genre`), dates, « · en attente depuis K jours » si `overdue`, commentaires en italique entre guillemets ; actions **Retirer du stock…** (plein orange) / **Retrouvé** (contour) / **Classer sans suite** (texte).
- Modale retrait édition : « Combien d'exemplaires avez-vous retrouvés en rayon ? » (stepper 0..stock, défaut 0), phrase de conséquence dynamique (« N exemplaires seront retirés. La fiche passera Épuisée au catalogue. » si N = stock), motif (Introuvable en rayon / Abîmé / Autre), note **obligatoire** pré-remplie « Introuvable en rayon — {n} signalements », bouton « Retirer N exemplaires » (libellé dynamique ; si N = 0 → « Confirmer : retrouvé »).
- Onglet Traités : tableau Clôture / Fiche / Décision (pastilles Retiré du stock, Retrouvé, Sans suite, Caduc) / Stock / Bénévole / Note.
- Après chaque clôture : rechargement de la file et `countsChanged` (badge et tuile à jour).
- Mobile 390 px : cartes empilées, actions « Retrouvé » / « Retirer… » côte à côte (maquette mobile).

- [ ] **Step 1 :** specs rouges —
  `route: not-found is a valid admin section`,
  `nav lists Livres introuvables right after Désengorgement with its badge`,
  `hides the badge and the dashboard tile when nothing is open`,
  `renders one row per target without any member identity`,
  `withdraw dialog computes the quantity to withdraw from found copies`,
  `withdraw dialog switches to found when all copies are found`,
  `withdraw dialog requires a note`,
  `rare target shows Retirer la fiche rare without quantity`,
  `closing a target reloads the queue and emits new counts`,
  `closed tab renders outcomes`,
  `csv export escapes separators and quotes`,
  `book detail shows the reported box when summary has open reports`,
  `settings saves the daily report limit`.
- [x] **Step 2 :** `npm test -- --watch=false --include='src/app/features/administration/**/*.spec.ts' --include='src/app/core/catalog-administration-route.spec.ts'` → FAIL.
- [x] **Step 3–4 :** implémenter → PASS ; `npm test -- --watch=false` complet puis `npm run build`.
- [x] **Step 5 :** 🧪 comparaison aux neuf maquettes admin (1440 px et 390 px).
- [x] **Step 6 :** `git commit -m "feat(catalog): add the not-found reports administration queue"`

---

# Étape 5 — RGPD, documentation, recette

### Task SIG-16 : Suppression de compte, documentation et reprise

**Files :**
- Modify `Infrastructure/Persistence/AccountDeletionStore.cs` : avant la suppression des lignes de sélection, pour les signalements du membre : `Open` → `DetachMember()` (conservés, anonymes) ; clos → `DetachMember()` aussi (l'historique « Traités » reste cohérent). Test `Infrastructure.tests/…/AccountDeletionStoreTests.cs` : `Delete_KeepsOpenReportsWithoutMemberOrComment`.
- Modify `docs/bourse-aux-livres/06-regles-metier.md` : `RG-67` à `RG-76` (texte de `F-11` §9).
- Modify `docs/bourse-aux-livres/10-evolution-compte-selection-achats.md` : §6.3 à §6.5 et `RG-65` → renvoi « remplacé par `F-11` ».
- Modify `docs/bourse-aux-livres/09-rgpd-compte-et-droits.md` : nouvelle donnée (signalement, commentaire libre ≤ 280, anonymisation à la suppression).
- Modify `docs/bourse-aux-livres/08-questions-ouvertes.md` : `Q-SIG-1` à `Q-SIG-8` avec la décision retenue.
- Modify `docs/bourse-aux-livres/technique/01-decisions.md` : les trois `DT` du Préalable.
- Modify `docs/bourse-aux-livres/technique/02-modele-de-donnees.md` : table `BookNotFoundReports`, colonne `AssociationSettings.NotFoundReportDailyLimit`, `MemberSelectionStatus` réduit.
- Modify `docs/bourse-aux-livres/plan/README.md` (ligne `07`), `docs/bourse-aux-livres/11-signalement-livre-introuvable.md` (statut), `NEXT.md`, `.github/memory/` + `changelog.md`.
- Lancer `graphify update .`.

- [ ] **Step 1 :** test rouge RGPD → implémenter → vert.
- [ ] **Step 2 :** documentation.
- [ ] **Step 3 :** `dotnet test` (3 projets) + `cd src/Catalog && npm test -- --watch=false && npm run build`.
- [ ] **Step 4 :** `git commit -m "docs(not-found): record not-found reports delivery"`

---

## Recette manuelle 🧪 (reprend `F-11` §11 et §12)

À dérouler sur l'environnement de recette, avec un compte membre et un compte administrateur :

1. **Parcours A** — ajouter une édition disponible (stock 1) à Ma sélection, signaler
   depuis un téléphone (390 px) → ligne « Signalé… », badge admin à 1, tuile visible ;
   « Retirer du stock… », trouvés 0 → fiche Épuisée au catalogue public ; côté membre
   « Retiré… merci ». Vérifier le mouvement RETRAIT + « suite à signalement » sur la fiche.
2. **Parcours B** — signaler puis « Retrouvé » → stock inchangé, message « retrouvé » et
   bouton de nouveau disponible.
3. **Parcours C** — trois comptes signalent le même ISBN (stock 3) → une seule ligne
   « 3 signalements · 3 membres » ; trouvés 1 → RETRAIT de 2, 1 disponible.
4. **Parcours D** — signaler (stock 1) puis vendre l'exemplaire en caisse → la ligne sort
   de la file ; onglet Traités : « Caduc ».
5. **Anti-abus** — régler le plafond à 2, tenter un 3ᵉ signalement → message de plafond.
6. **Double appui** — appuyer deux fois vite sur « Envoyer le signalement » → un seul
   signalement.
7. **Ancien onglet ouvert** avant déploiement → le clic sur un ancien état ne casse rien
   (410 géré, message d'erreur générique puis rechargement).
8. **Rare** — compte `LivresRares` seul : ne voit que les fiches rares, « Retirer la fiche
   rare » dépublie la fiche.
9. **Accessibilité** — clavier seul de bout en bout (membre et admin), `Échap` ferme les
   modales, focus visible, lecteur d'écran annonce les dialogues et le toast.
10. **Suppression de compte** — supprimer un compte avec un signalement ouvert → la file
    garde la ligne, sans commentaire.

## Ordre, dépendances et découpage en PR

| PR | Tâches | Déployable seule | Dépend de |
|---|---|---|---|
| 1 — Backend signalement | SIG-1 → SIG-7 | **Non** sans PR 2 côté Catalog : `SIG-3/SIG-4` cassent l'ancien sélecteur → livrer PR 1 et PR 2 **ensemble** | — |
| 2 — Catalog Ma sélection | SIG-12 (partie membre), SIG-13, SIG-14 | Avec PR 1 | PR 1 |
| 3 — File d'administration | SIG-8 → SIG-11, SIG-12 (partie admin), SIG-15 | Oui | PR 1 |
| 4 — RGPD et documentation | SIG-16 | Oui | PR 3 |

Variante recommandée si une seule PR est souhaitée (comme #224) : tout le plan dans une
branche `feat/not-found-reports`, commits par tâche, recette 🧪 avant fusion.

## Mesures après mise en service

Requêtes SQL après la prochaine bourse (aucune instrumentation dédiée au-delà des logs) :

- nombre de signalements créés / membres distincts ;
- répartition des clôtures : `Withdrawn` / `Found` / `Dismissed` / `Lapsed` (le taux
  `Withdrawn` mesure l'écart entre stock affiché et stock réel) ;
- délai médian entre premier signalement et clôture ;
- exemplaires retirés via cette file vs via Désengorgement.
