# Compte membre — Ma sélection, carte de compte et Mes achats : plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** livrer les étapes 1 à 4 de `F-10` §17 : Ma sélection (anonyme puis synchronisée), la carte de compte QR, l'association facultative d'un passage en caisse (hors ligne compris) et l'historique Mes achats, suppression RGPD comprise.

**Architecture:** quatre nouvelles tranches CQRS dans le backend existant (`MemberSelection`, `MemberCards`, `CheckoutPassages`, `Purchases`), trois tables SQL (plus une colonne), aucune ressource Azure nouvelle hors un secret de signature. Le **passage en caisse** n'existe aujourd'hui que dans la Scanette : il devient un identifiant client `checkoutPassageId`, porté par chaque vente **uniquement lorsqu'un compte est associé**, et le serveur construit un `CheckoutPassage` et ses lignes (avec instantané bibliographique) quel que soit l'ordre d'arrivée des ventes et de l'association. Le jeton QR est un HMAC recalculable (rien de secret n'est stocké en clair), révocable par incrément de version.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, MediatR, ErrorOr, FluentValidation, EF Core (SQL Server, SQLite en tests), xUnit + FluentAssertions + NSubstitute ; Angular 21 (Catalog SSR, Scan PWA avec IndexedDB), Karma/Jasmine ; `qrcode` (npm) côté Catalog ; `@zxing/library` déjà présent côté Scan (lit déjà `QR_CODE`).

**Spec:** [`../10-evolution-compte-selection-achats.md`](../10-evolution-compte-selection-achats.md) (`F-10`), règles `RG-52` à `RG-66` dans [`../06-regles-metier.md`](../06-regles-metier.md), question ouverte `Q-12` dans [`../08-questions-ouvertes.md`](../08-questions-ouvertes.md).

**Maquettes :** les écrans (onglets du compte, bouton de fiche, carte, états de caisse) seront fournis via Claude Design. Les tâches d'interface ci-dessous fixent **le comportement, les libellés et les tests** ; la mise en forme (HTML/SCSS) sera alignée sur les maquettes lorsqu'elles seront ajoutées à `../maquettes/`. Une tâche d'interface ne se ferme pas sans comparaison à la maquette correspondante si elle existe.

---

## Préalable — décisions `F-10` §16 à confirmer

Le plan suit **toutes les recommandations** de `F-10` §16. Si l'association en décide autrement, seules les tâches citées changent :

| Décision | Hypothèse du plan | Tâches touchées si elle change |
|---|---|---|
| Livres rares dans Ma sélection | **Inclus** (fiche rare publiée et visible) | CS-1, CS-4, CS-9 |
| Portée d'une association | Tout le passage validé | CS-17, CS-20 |
| Repli par e-mail vérifié | **Non livré en v1** (QR puis code de secours puis vente anonyme) | — |
| Association après validation | Pas de parcours public ; **dissociation** administrateur seulement | CS-22 |
| Annulation après synchronisation | Ligne affichée **« Annulée »** (pas retirée) | CS-18, CS-24 |
| Wallet | Hors plan (étape 5) | — |

Deux décisions **techniques** prises par ce plan, à relire :

- **`DT` proposé — jeton de carte HMAC.** Le QR porte `VPDC1.<base64url(cardId‖version)>.<base64url(HMAC-SHA256)>`. Rien n'est stocké côté serveur hormis `cardId`, `version` et le code de secours : un vol de base ne permet pas de fabriquer une carte, et la révocation est un `version++`. Même mécanique que `UnsubscribeTokenService` (PR #207).
- **`DT` proposé — le passage n'est créé côté serveur que s'il est associé.** Une vente anonyme reste strictement identique à aujourd'hui (aucun `checkoutPassageId` transmis) : c'est ce qui garantit `RG-57` et la compatibilité des Scanettes anciennes (`F-10` §15).

À la fin du plan, ces deux choix sont reportés en `DT-nn` dans `../technique/01-decisions.md` (tâche CS-26).

## Global Constraints

- Vocabulaire des écrans (F-10 §3) : **« Ma sélection »**, **« Mes achats »**, **« Ma carte »**, **« Carte de compte »**, **« Vente anonyme »**, **« Associer un compte »**. Jamais « panier », « commande », « carte de fidélité », « réserver », « mettre de côté », « achat garanti ».
- États de sélection : `ToTake` = **À PRENDRE**, `Purchased` = **ACHETÉ**, `NotFound` = **PAS TROUVÉ**, `ToRevisit` = **À REVOIR**. État initial `ToTake`.
- Le QR ne contient **ni e-mail, ni nom, ni `oid` Entra** (`RG-59`). Préfixe fixe `VPDC1.`.
- La caisse n'affiche que le **prénom** (ou « Membre » à défaut) ; jamais l'e-mail, la liste de recherche ou l'historique (`F-10` §8.3, §12.2).
- Une vente anonyme est **toujours** valide ; aucune erreur d'association ne bloque `VALIDER` ni la synchronisation des ventes (`RG-57`).
- Aucun prix, total ou montant dans Mes achats (`RG-63`).
- Passage à ACHETÉ **uniquement** sur ISBN exact ou identifiant exact de fiche rare (`RG-65`) ; jamais de suppression automatique d'une ligne de sélection.
- Sélection anonyme : `localStorage`, clé `vpd.catalog.selection.v1`, jamais envoyée sans connexion explicite (`RG-55`). Tout accès `localStorage` est protégé par `isPlatformBrowser` **et** `try/catch` (SSR, navigation privée).
- Mobile : les trois espaces sont utilisables à **390 px** sans défilement horizontal (`F-10` §15).
- Mes achats paginé par passage : **10 passages par page** au plus.
- Toute nouvelle route membre : `RequireAuthorization()` + identité lue par `TryGetMemberIdentity(principal, …)`. Toute route caisse : policy **`Caisse`**. Toute route d'administration : policy **`Administration`**.
- `DT-16` : chaque tâche produisant du code ajoute ses logs structurés et ne journalise **ni e-mail ni jeton brut** (`F-10` §15, audit).
- Nouveaux `DbSet` ajoutés à `IProjectDbContext` avec une implémentation par défaut `=> throw new NotSupportedException();` (patron de `RareBookTombstones`) pour ne pas toucher aux 11 contextes de test existants.
- Migrations EF : une migration par étape, nommée comme indiqué dans la tâche ; jamais de modification d'une migration déjà poussée.

## Review Focus

1. **Association reçue avant les ventes (ou l'inverse), hors ligne puis rejouée plusieurs fois** — le passage n'a qu'une ligne par vente, et le membre voit exactement les livres validés. → tests CS-17 (`Handle_WhenAssociationArrivesBeforeSales…`, `…ReplayedTwice…`) et CS-20.
2. **Vente annulée localement (« Annuler la dernière vente ») alors qu'une association est en file** — aucune ligne fantôme n'apparaît dans Mes achats ; un passage sans ligne active est invisible. → tests CS-20 et CS-23 (`Handle_WhenPassageHasNoActiveLine_IsHidden`).
3. **ISBN redirigé (fusion de fiches) entre l'ajout à la sélection et la vente** — la ligne de sélection sur l'ancien ISBN passe quand même à ACHETÉ, mais une autre édition du même titre ne passe jamais. → test CS-16 (`MarkSelectionPurchased_MarksSelectionOnRequestedAndCanonicalIsbn`).
4. **Carte renouvelée ou compte supprimé entre la lecture hors ligne du QR et la synchronisation** — la vente reste valide et anonyme, le passage est `Unresolved`, rien n'est rattaché à un autre compte. → tests CS-17 (`…WithRevokedToken_RecordsUnresolved`) et CS-25.
5. **Code de secours saisi avec casse, espaces ou tiret différents** (`lune42`, `LUNE 4271`, `lune-4271 `) — il est reconnu ; un code d'un autre format est refusé sans révéler si un compte existe. → tests CS-11 (`Normalize…`) et CS-12.

---

## Carte des fichiers

### Backend — Domain (`src/Backend/Vole_Papillon_Damour.Domain/`)

| Fichier | Responsabilité |
|---|---|
| `MemberSelectionAggregate/MemberSelectionItem.cs` | Une entrée de Ma sélection (édition ou fiche rare), ses états |
| `MemberSelectionAggregate/ValueObjects/MemberSelectionStatus.cs` | `ToTake`, `Purchased`, `NotFound`, `ToRevisit` |
| `MemberCardAggregate/MemberCard.cs` | Carte d'un membre : version, code de secours, révocation |
| `MemberCardAggregate/MemberCardRecoveryCode.cs` | Format et normalisation du code de secours |
| `CheckoutPassageAggregate/CheckoutPassage.cs` | Passage associé : compte, statut, dissociation, anonymisation |
| `CheckoutPassageAggregate/CheckoutPassageLine.cs` | Ligne d'achat avec instantané bibliographique, annulation |
| `CheckoutPassageAggregate/ValueObjects/CheckoutPassageStatus.cs` | `PendingAssociation`, `Associated`, `Unresolved`, `Dissociated` |
| `Common/Errors/Errors.MemberSelection.cs`, `Errors.MemberCard.cs`, `Errors.CheckoutPassage.cs` | Catalogues d'erreurs |
| `BookMovementAggregate/BookMovement.cs` (modifié) | Colonne `CheckoutPassageId` |

### Backend — Application (`src/Backend/Vole_Papillon_Damour.Application/`)

| Dossier | Contenu |
|---|---|
| `MemberSelection/Commands/{AddSelectionItem,RemoveSelectionItem,SetSelectionItemStatus,MergeSelection}/` | Commandes membre |
| `MemberSelection/Queries/GetMySelection/` | Lecture avec disponibilité actuelle |
| `MemberSelection/Common/SelectionResults.cs`, `SelectionAvailabilityProjector.cs` | Résultats et calcul de disponibilité |
| `MemberCards/Queries/GetMyCard/`, `MemberCards/Commands/RotateMyCard/`, `MemberCards/Queries/ResolveMemberCard/` | Carte membre et résolution caisse |
| `CheckoutPassages/Common/CheckoutPassageRecorder.cs` | **Seul** point qui crée passages/lignes et marque la sélection ACHETÉ |
| `CheckoutPassages/Commands/AssociateCheckoutPassage/`, `CheckoutPassages/Commands/DissociateCheckoutPassage/` | Association caisse, correction admin |
| `Purchases/Queries/GetMyPurchases/` | Mes achats paginé |
| `Common/Interfaces/Services/IMemberCardTokenService.cs` | Contrat du jeton QR |
| `Books/Commands/RegisterSale/*` (modifié), `Books/Commands/VoidSale/*` (modifié), `RareBooks/Commands/MarkRareBookSold/*` (modifié), `RareBooks/Commands/RestoreRareBookAvailability/*` (modifié) | Propagation de `CheckoutPassageId` et annulation des lignes |

### Backend — Infrastructure / Api

| Fichier | Responsabilité |
|---|---|
| `Infrastructure/Persistence/Configurations/{MemberSelectionItem,MemberCard,CheckoutPassage,CheckoutPassageLine}Configuration.cs` | Mapping EF |
| `Infrastructure/Services/MemberCards/MemberCardTokenService.cs`, `MemberCardTokenOptions.cs` | HMAC du QR |
| `Infrastructure/Persistence/AccountDeletionStore.cs` (modifié) | Suppression/anonymisation RGPD |
| `Infrastructure/Migrations/*_AddMemberSelection.cs`, `*_AddMemberCards.cs`, `*_AddCheckoutPassages.cs` | Migrations |
| `Api/Controllers/MemberAccountController.cs` | Routes `/catalog/me/selection`, `/catalog/me/card`, `/catalog/me/purchases` |
| `Api/Controllers/CheckoutPassageController.cs` | Routes `/scan/member-cards/resolve`, `/scan/passages/{id}/member`, `/administration/checkout-passages/{id}/dissociate` |
| `Api/Controllers/BookController.cs`, `RareBookController.cs` (modifiés) | `checkoutPassageId` optionnel sur les ventes |
| `infra/…`, `.github/workflows/infra-deploy.yml` (modifiés) | Secret `MEMBER_CARD_SIGNING_KEY` |

### Catalog (`src/Catalog/src/app/`)

| Fichier | Responsabilité |
|---|---|
| `core/selection/local-selection.store.ts` | Sélection anonyme dans `localStorage` |
| `core/selection/selection-merge.ts` | Calcul pur de la fusion locale/compte |
| `core/selection/catalog-selection.service.ts` | Façade : anonyme ou synchronisée, fusion à la connexion |
| `core/catalog-member-api.service.ts` (modifié), `core/catalog.models.ts` (modifié) | Appels et types |
| `shared/components/selection-button/selection-button.component.ts` | Bouton « Ajouter à Ma sélection » / « Dans Ma sélection » + « Retirer » |
| `features/account/selection/account-selection.component.ts` | Onglet Ma sélection |
| `features/account/card/account-card.component.ts` | Onglet Ma carte |
| `features/account/purchases/account-purchases.component.ts` | Onglet Mes achats |
| `features/account/catalog-account-page.component.*` (modifiés) | Nouveaux onglets |
| `features/book-detail/*`, `features/rare-books/catalog-rare-book-detail-page.component.*`, `features/work/*` (modifiés) | Intégration du bouton |
| `features/legal/*` (modifié) | Page données personnelles |

### Scan (`src/Scan/src/app/`)

| Fichier | Responsabilité |
|---|---|
| `offline/scan-offline.model.ts` (modifié) | Version IndexedDB 6, store `passageAssociations`, `checkoutPassageId` sur les entrées de vente |
| `offline/scan-local-store.service.ts` (modifié) | CRUD du store `passageAssociations` |
| `offline/scan-member-card.ts` | Reconnaissance d'un QR carte vs ISBN, normalisation du code de secours |
| `offline/scan-passage-association.service.ts` | État d'association du passage courant, résolution en ligne |
| `offline/scan-workflow.service.ts`, `offline/scan-rare-cash.service.ts`, `offline/scan-sync.service.ts`, `offline/scan-api.service.ts` (modifiés) | Propagation, synchronisation, rejeu |
| `scanner/scanner.component.*` (modifiés) | Bandeau client, « Associer un compte », confirmation |

---

# Étape 1 — Ma sélection

### Task CS-1: Domaine `MemberSelectionItem`

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Domain/MemberSelectionAggregate/MemberSelectionItem.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Domain/MemberSelectionAggregate/ValueObjects/MemberSelectionStatus.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Domain/Common/Errors/Errors.MemberSelection.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Domain.tests/MemberSelection/MemberSelectionItemTests.cs`

**Interfaces:**
- Consumes: `UserId`, `Isbn13`, `RareBookId`, `Entity<Guid>`, `DomainTime.RequireUtc`.
- Produces:
  - `enum MemberSelectionStatus : byte { ToTake, Purchased, NotFound, ToRevisit }`
  - `MemberSelectionItem.CreateForEdition(Guid id, UserId userId, Isbn13 isbn13, DateTime addedAt)`
  - `MemberSelectionItem.CreateForRareBook(Guid id, UserId userId, RareBookId rareBookId, DateTime addedAt)`
  - `void ChangeStatus(MemberSelectionStatus status, DateTime changedAt)`
  - `bool MarkPurchased(DateTime purchasedAt)` — `true` si l'état a changé
  - propriétés `UserId`, `Isbn13?`, `RareBookId?`, `Status`, `AddedAt`, `StatusChangedAt`, `PurchasedAt?`
  - `Errors.MemberSelection.{NotFound(Guid), InvalidTarget(), InvalidStatus(), NotInCatalog(string), TooManyItems(int)}`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.MemberSelection;

public sealed class MemberSelectionItemTests
{
    private static readonly UserId Member = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
    private static readonly DateTime AddedAt = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateForEdition_StartsAsToTake()
    {
        Isbn13.TryCreate("9782070612758", out var isbn).Should().BeTrue();

        var item = MemberSelectionItem.CreateForEdition(Guid.NewGuid(), Member, isbn, AddedAt);

        item.Status.Should().Be(MemberSelectionStatus.ToTake);
        item.Isbn13.Should().Be(isbn);
        item.RareBookId.Should().BeNull();
        item.AddedAt.Should().Be(AddedAt);
        item.StatusChangedAt.Should().Be(AddedAt);
        item.PurchasedAt.Should().BeNull();
    }

    [Fact]
    public void CreateForRareBook_KeepsOnlyTheRareIdentifier()
    {
        var rareBookId = RareBookId.Create(Guid.NewGuid());

        var item = MemberSelectionItem.CreateForRareBook(Guid.NewGuid(), Member, rareBookId, AddedAt);

        item.RareBookId.Should().Be(rareBookId);
        item.Isbn13.Should().BeNull();
    }

    [Fact]
    public void Create_RejectsNonUtcTimestamp()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);

        var act = () => MemberSelectionItem.CreateForEdition(
            Guid.NewGuid(), Member, isbn, DateTime.SpecifyKind(AddedAt, DateTimeKind.Local));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkPurchased_SetsPurchasedOnceAndKeepsFirstDate()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);
        var item = MemberSelectionItem.CreateForEdition(Guid.NewGuid(), Member, isbn, AddedAt);

        item.MarkPurchased(AddedAt.AddDays(3)).Should().BeTrue();
        item.MarkPurchased(AddedAt.AddDays(5)).Should().BeFalse();

        item.Status.Should().Be(MemberSelectionStatus.Purchased);
        item.PurchasedAt.Should().Be(AddedAt.AddDays(3));
    }

    [Fact]
    public void ChangeStatus_AwayFromPurchased_ClearsPurchaseDate()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);
        var item = MemberSelectionItem.CreateForEdition(Guid.NewGuid(), Member, isbn, AddedAt);
        item.MarkPurchased(AddedAt.AddDays(1));

        item.ChangeStatus(MemberSelectionStatus.ToRevisit, AddedAt.AddDays(2));

        item.Status.Should().Be(MemberSelectionStatus.ToRevisit);
        item.PurchasedAt.Should().BeNull();
        item.StatusChangedAt.Should().Be(AddedAt.AddDays(2));
    }

    [Fact]
    public void ChangeStatus_ToPurchasedManually_RecordsDate()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);
        var item = MemberSelectionItem.CreateForEdition(Guid.NewGuid(), Member, isbn, AddedAt);

        item.ChangeStatus(MemberSelectionStatus.Purchased, AddedAt.AddHours(1));

        item.PurchasedAt.Should().Be(AddedAt.AddHours(1));
    }

    [Fact]
    public void ChangeStatus_RejectsUndefinedValue()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);
        var item = MemberSelectionItem.CreateForEdition(Guid.NewGuid(), Member, isbn, AddedAt);

        var act = () => item.ChangeStatus((MemberSelectionStatus)42, AddedAt);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Domain.tests --filter "FullyQualifiedName~MemberSelectionItemTests"`
Expected: FAIL — compilation error, `MemberSelectionItem` introuvable.

- [ ] **Step 3: Write minimal implementation**

`ValueObjects/MemberSelectionStatus.cs` :

```csharp
namespace Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;

public enum MemberSelectionStatus : byte
{
    ToTake,
    Purchased,
    NotFound,
    ToRevisit
}
```

`MemberSelectionItem.cs` :

```csharp
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.MemberSelectionAggregate;

public sealed class MemberSelectionItem : Entity<Guid>
{
    public const int MaxItemsPerMember = 500;

    public UserId UserId { get; private set; } = null!;
    public Isbn13? Isbn13 { get; private set; }
    public RareBookId? RareBookId { get; private set; }
    public MemberSelectionStatus Status { get; private set; }
    public DateTime AddedAt { get; private set; }
    public DateTime StatusChangedAt { get; private set; }
    public DateTime? PurchasedAt { get; private set; }

    private MemberSelectionItem(
        Guid id,
        UserId userId,
        Isbn13? isbn13,
        RareBookId? rareBookId,
        DateTime addedAt) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A selection item identifier is required.", nameof(id));
        }

        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        if ((isbn13 is null) == (rareBookId is null))
        {
            throw new ArgumentException("A selection item targets exactly one edition or one rare book.");
        }

        UserId = userId;
        Isbn13 = isbn13;
        RareBookId = rareBookId;
        Status = MemberSelectionStatus.ToTake;
        AddedAt = DomainTime.RequireUtc(addedAt, nameof(addedAt));
        StatusChangedAt = AddedAt;
    }

    public static MemberSelectionItem CreateForEdition(Guid id, UserId userId, Isbn13 isbn13, DateTime addedAt)
    {
        ArgumentNullException.ThrowIfNull(isbn13);
        return new MemberSelectionItem(id, userId, isbn13, null, addedAt);
    }

    public static MemberSelectionItem CreateForRareBook(Guid id, UserId userId, RareBookId rareBookId, DateTime addedAt)
    {
        ArgumentNullException.ThrowIfNull(rareBookId);
        return new MemberSelectionItem(id, userId, null, rareBookId, addedAt);
    }

    public MemberSelectionItem()
    {
    }

    public void ChangeStatus(MemberSelectionStatus status, DateTime changedAt)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown selection status.");
        }

        var utcChangedAt = DomainTime.RequireUtc(changedAt, nameof(changedAt));
        PurchasedAt = status == MemberSelectionStatus.Purchased
            ? PurchasedAt ?? utcChangedAt
            : null;
        Status = status;
        StatusChangedAt = utcChangedAt;
    }

    public bool MarkPurchased(DateTime purchasedAt)
    {
        if (Status == MemberSelectionStatus.Purchased)
        {
            return false;
        }

        var utcPurchasedAt = DomainTime.RequireUtc(purchasedAt, nameof(purchasedAt));
        Status = MemberSelectionStatus.Purchased;
        PurchasedAt = utcPurchasedAt;
        StatusChangedAt = utcPurchasedAt;
        return true;
    }
}
```

`Errors.MemberSelection.cs` :

```csharp
using ErrorOr;

namespace Vole_Papillon_Damour.Domain.Common.Errors;

public static partial class Errors
{
    public static class MemberSelection
    {
        public static Error NotFound(Guid itemId) => Error.NotFound(
            code: "MemberSelection.NotFound",
            description: $"Selection item not found: {itemId}.");

        public static Error InvalidTarget() => Error.Validation(
            code: "MemberSelection.InvalidTarget",
            description: "Provide exactly one ISBN-13 or one rare book identifier.");

        public static Error InvalidStatus() => Error.Validation(
            code: "MemberSelection.InvalidStatus",
            description: "The selection status is not recognised.");

        public static Error NotInCatalog(string reference) => Error.Validation(
            code: "MemberSelection.NotInCatalog",
            description: $"Only a record visible in the public catalogue can be selected: {reference}.");

        public static Error TooManyItems(int max) => Error.Validation(
            code: "MemberSelection.TooManyItems",
            description: $"A selection cannot contain more than {max} items.");
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Domain.tests --filter "FullyQualifiedName~MemberSelectionItemTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Backend/Vole_Papillon_Damour.Domain src/Backend/Vole_Papillon_Damour.Domain.tests
git commit -m "feat(selection): add member selection item domain"
```

---

### Task CS-2: Persistance de Ma sélection

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/Configurations/MemberSelectionItemConfiguration.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Application/Common/Interfaces/Persistence/IProjectDbContext.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/ProjectDbContext.cs`
- Create (généré): `src/Backend/Vole_Papillon_Damour.Infrastructure/Migrations/<timestamp>_AddMemberSelection.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/MemberSelection/MemberSelectionItemConfigurationTests.cs`

**Interfaces:**
- Consumes: `MemberSelectionItem` (CS-1).
- Produces: `DbSet<MemberSelectionItem> MemberSelectionItems` sur `IProjectDbContext` ; table `MemberSelectionItems` avec index uniques filtrés `(UserId, Isbn13)` et `(UserId, RareBookId)`.

- [ ] **Step 1: Write the failing test**

Réutiliser le patron des tests de configuration existants de `Infrastructure.tests` (contexte `ProjectDbContext` sur SQLite en mémoire). Chercher un exemple avec `grep -rl "EnsureCreatedAsync" src/Backend/Vole_Papillon_Damour.Infrastructure.tests` et reprendre sa fabrique de contexte.

```csharp
[Fact]
public async Task SaveChanges_WhenSameIsbnIsSelectedTwiceByOneMember_Throws()
{
    await using var fixture = await InfrastructureSqliteFixture.CreateAsync();
    Isbn13.TryCreate("9782070612758", out var isbn);
    var member = UserId.Create(Guid.NewGuid());
    var now = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);
    fixture.Context.MemberSelectionItems.Add(MemberSelectionItem.CreateForEdition(Guid.NewGuid(), member, isbn, now));
    fixture.Context.MemberSelectionItems.Add(MemberSelectionItem.CreateForEdition(Guid.NewGuid(), member, isbn, now));

    var act = () => fixture.Context.SaveChangesAsync();

    await act.Should().ThrowAsync<DbUpdateException>();
}

[Fact]
public async Task SaveChanges_WhenTwoMembersSelectSameIsbn_Succeeds()
{
    await using var fixture = await InfrastructureSqliteFixture.CreateAsync();
    Isbn13.TryCreate("9782070612758", out var isbn);
    var now = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);
    fixture.Context.MemberSelectionItems.Add(MemberSelectionItem.CreateForEdition(Guid.NewGuid(), UserId.Create(Guid.NewGuid()), isbn, now));
    fixture.Context.MemberSelectionItems.Add(MemberSelectionItem.CreateForEdition(Guid.NewGuid(), UserId.Create(Guid.NewGuid()), isbn, now));

    await fixture.Context.SaveChangesAsync();

    (await fixture.Context.MemberSelectionItems.CountAsync()).Should().Be(2);
}
```

Si `InfrastructureSqliteFixture` n'existe pas sous ce nom, utiliser la fabrique réellement présente dans le projet et **ne pas** en créer une seconde.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter "FullyQualifiedName~MemberSelectionItemConfigurationTests"`
Expected: FAIL — `MemberSelectionItems` n'existe pas sur le contexte.

- [ ] **Step 3: Write minimal implementation**

`IProjectDbContext.cs` — ajouter après `RareBookTombstones` :

```csharp
DbSet<MemberSelectionItem> MemberSelectionItems => throw new NotSupportedException();
```

`ProjectDbContext.cs` :

```csharp
public DbSet<MemberSelectionItem> MemberSelectionItems => Set<MemberSelectionItem>();
```

`MemberSelectionItemConfiguration.cs` — reprendre les conversions de `WatchlistItemConfiguration` pour `UserId`, `Isbn13` et `RareBookId` (même `HasConversion`, mêmes longueurs) :

```csharp
public sealed class MemberSelectionItemConfiguration : IEntityTypeConfiguration<MemberSelectionItem>
{
    public void Configure(EntityTypeBuilder<MemberSelectionItem> builder)
    {
        builder.ToTable("MemberSelectionItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();

        builder.Property(item => item.UserId)
            .HasConversion(id => id.Value, value => UserId.Create(value))
            .IsRequired();
        builder.Property(item => item.Isbn13)
            .HasConversion(isbn => isbn!.Value, value => Isbn13.Create(value))
            .HasMaxLength(13);
        builder.Property(item => item.RareBookId)
            .HasConversion(id => id!.Value, value => RareBookId.Create(value));
        builder.Property(item => item.Status).HasConversion<byte>().IsRequired();
        builder.Property(item => item.AddedAt).IsRequired();
        builder.Property(item => item.StatusChangedAt).IsRequired();

        builder.HasIndex(item => new { item.UserId, item.Isbn13 })
            .IsUnique()
            .HasFilter("[Isbn13] IS NOT NULL");
        builder.HasIndex(item => new { item.UserId, item.RareBookId })
            .IsUnique()
            .HasFilter("[RareBookId] IS NOT NULL");
        builder.HasIndex(item => item.Isbn13);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

Vérifier dans `WatchlistItemConfiguration` le nom exact de la fabrique d'`Isbn13` utilisée dans les conversions (`Isbn13.Create` ou équivalent) et s'aligner dessus.

Générer la migration :

```bash
dotnet ef migrations add AddMemberSelection \
  --project src/Backend/Vole_Papillon_Damour.Infrastructure \
  --startup-project src/Backend/Vole_Papillon_Damour.Api
```

Relire le fichier généré : une table, deux index uniques filtrés, un index `Isbn13`, une FK en cascade vers `Users`. Rien d'autre.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter "FullyQualifiedName~MemberSelectionItemConfigurationTests"`
Expected: PASS (2 tests). Puis `dotnet build src/Backend/Vole_Papillon_Damour.slnx` : 0 erreur.

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(selection): persist member selection items"
```

📌 Migration `AddMemberSelection` à appliquer en DEV au déploiement — à consigner dans `NEXT.md`.

---

### Task CS-3: Commandes Ma sélection (ajout, retrait, état)

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberSelection/Commands/AddSelectionItem/{AddSelectionItemCommand,AddSelectionItemCommandHandler,AddSelectionItemCommandValidator}.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberSelection/Commands/RemoveSelectionItem/{RemoveSelectionItemCommand,RemoveSelectionItemCommandHandler}.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberSelection/Commands/SetSelectionItemStatus/{SetSelectionItemStatusCommand,SetSelectionItemStatusCommandHandler}.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberSelection/Common/SelectionResults.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberSelection/Common/SelectionTargetResolver.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Application.tests/MemberSelection/SelectionCommandHandlerTests.cs`
- Test fixture: `src/Backend/Vole_Papillon_Damour.Application.tests/MemberSelection/MemberSelectionFixture.cs`

**Interfaces:**
- Consumes: `MemberIdentityService.EnsureAsync(externalId, email, firstName, lastName, ct)` → `User` ; `MemberSelectionItem` (CS-1) ; `IProjectDbContext.MemberSelectionItems` (CS-2).
- Produces:
  - `record AddSelectionItemCommand(string ExternalId, string Email, string? FirstName, string? LastName, string? Isbn13, Guid? RareBookId) : IRequest<ErrorOr<SelectionItemAddedResult>>`
  - `record SelectionItemAddedResult(Guid Id, bool AlreadyPresent)`
  - `record RemoveSelectionItemCommand(string ExternalId, string Email, string? FirstName, string? LastName, Guid ItemId) : IRequest<ErrorOr<Deleted>>`
  - `record SetSelectionItemStatusCommand(string ExternalId, string Email, string? FirstName, string? LastName, Guid ItemId, MemberSelectionStatus Status) : IRequest<ErrorOr<Updated>>`
  - `SelectionTargetResolver.ResolveAsync(IProjectDbContext db, string? isbn, Guid? rareBookId, CancellationToken ct) : Task<ErrorOr<SelectionTarget>>` avec `record SelectionTarget(Isbn13? Isbn13, RareBookId? RareBookId)`.

Règles portées par le handler d'ajout :
- `RG-53` : exactement une cible ; un ISBN doit correspondre à un `Book` **ou** à une annonce (`BookAnnouncements`) ; un `Book` redirigé est résolu vers son ISBN canonique ; un livre masqué (`IsHiddenFromCatalog`) est refusé (`NotInCatalog`).
- Une fiche rare doit être `Published` (vendue ou non : une fiche vendue visible reste sélectionnable, `F-10` §6.1).
- Une référence jamais reçue (ni `Book`, ni annonce) → `NotInCatalog` : elle relève de la liste de recherche.
- Ajout d'une cible déjà présente → `AlreadyPresent = true`, pas de doublon, pas d'erreur (idempotence du bouton).
- Plafond `MemberSelectionItem.MaxItemsPerMember` → `TooManyItems`.
- `RG-52` : ne touche jamais `Watchlist` / `WatchlistItems`.

- [ ] **Step 1: Write the failing tests**

`MemberSelectionFixture` : copier la structure de `ScanBookFixture` (SQLite en mémoire, `EnsureCreatedAsync`) avec un `MemberSelectionTestDbContext : DbContext, IProjectDbContext` exposant `Users`, `Books`, `BookAnnouncements`, `RareBooks`, `RareBookPhotos`, `Watchlists`, `WatchlistItems`, `MemberSelectionItems`, et une méthode `CreateMemberIdentityService()` construisant le vrai `MemberIdentityService` sur ce contexte (voir `MemberIdentityServiceTests` pour ses dépendances). Méthodes d'aide : `AddBookAsync(string isbn, int quantityAvailable = 1, bool hidden = false, string? redirectTo = null)`, `AddAnnouncementAsync(string isbn)`, `AddRareBookAsync(bool published = true, bool sold = false)`.

```csharp
public sealed class SelectionCommandHandlerTests
{
    private const string ExternalId = "7f0b0f0e-0000-0000-0000-00000000c0de";
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Add_WhenEditionIsInCatalogue_CreatesToTakeItem()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");

        var result = await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);

        result.IsError.Should().BeFalse();
        result.Value.AlreadyPresent.Should().BeFalse();
        var item = await fixture.Context.MemberSelectionItems.SingleAsync();
        item.Status.Should().Be(MemberSelectionStatus.ToTake);
        (await fixture.Context.WatchlistItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Add_WhenAddedTwice_IsIdempotent()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");
        var handler = fixture.CreateAddHandler();

        var first = await handler.Handle(Add(isbn: "9782070612758"), default);
        var second = await handler.Handle(Add(isbn: "978-2-07-061275-8"), default);

        second.Value.Id.Should().Be(first.Value.Id);
        second.Value.AlreadyPresent.Should().BeTrue();
        (await fixture.Context.MemberSelectionItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Add_WhenOnlyAnnounced_Succeeds()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddAnnouncementAsync("9782070612758");

        var result = await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Add_WhenNeverReceived_ReturnsNotInCatalog()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);

        var result = await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);

        result.FirstError.Code.Should().Be("MemberSelection.NotInCatalog");
    }

    [Fact]
    public async Task Add_WhenHidden_ReturnsNotInCatalog()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758", hidden: true);

        var result = await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);

        result.FirstError.Code.Should().Be("MemberSelection.NotInCatalog");
    }

    [Fact]
    public async Task Add_WhenRedirected_StoresCanonicalIsbn()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070584628");
        await fixture.AddBookAsync("9782070612758", redirectTo: "9782070584628");

        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);

        var item = await fixture.Context.MemberSelectionItems.SingleAsync();
        item.Isbn13!.Value.Should().Be("9782070584628");
    }

    [Fact]
    public async Task Add_WhenBothTargetsGiven_ReturnsInvalidTarget()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);

        var result = await fixture.CreateAddHandler().Handle(
            Add(isbn: "9782070612758", rareBookId: Guid.NewGuid()), default);

        result.FirstError.Code.Should().Be("MemberSelection.InvalidTarget");
    }

    [Fact]
    public async Task Add_WhenRareBookIsDraft_ReturnsNotInCatalog()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var rare = await fixture.AddRareBookAsync(published: false);

        var result = await fixture.CreateAddHandler().Handle(Add(rareBookId: rare.Id.Value), default);

        result.FirstError.Code.Should().Be("MemberSelection.NotInCatalog");
    }

    [Fact]
    public async Task Remove_WhenItemBelongsToAnotherMember_ReturnsNotFound()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var foreignItem = await fixture.AddSelectionItemForOtherMemberAsync("9782070612758");

        var result = await fixture.CreateRemoveHandler().Handle(
            new RemoveSelectionItemCommand(ExternalId, "camille@example.test", "Camille", null, foreignItem.Id), default);

        result.FirstError.Code.Should().Be("MemberSelection.NotFound");
        (await fixture.Context.MemberSelectionItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SetStatus_ChangesOnlyOwnItem()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");
        var added = await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);

        var result = await fixture.CreateSetStatusHandler().Handle(
            new SetSelectionItemStatusCommand(ExternalId, "camille@example.test", "Camille", null,
                added.Value.Id, MemberSelectionStatus.NotFound), default);

        result.IsError.Should().BeFalse();
        (await fixture.Context.MemberSelectionItems.SingleAsync()).Status.Should().Be(MemberSelectionStatus.NotFound);
    }

    private static AddSelectionItemCommand Add(string? isbn = null, Guid? rareBookId = null) =>
        new(ExternalId, "camille@example.test", "Camille", null, isbn, rareBookId);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~SelectionCommandHandlerTests"`
Expected: FAIL — types introuvables.

- [ ] **Step 3: Write minimal implementation**

`SelectionTargetResolver.cs` :

```csharp
public sealed record SelectionTarget(Isbn13? Isbn13, RareBookId? RareBookId);

public static class SelectionTargetResolver
{
    public static async Task<ErrorOr<SelectionTarget>> ResolveAsync(
        IProjectDbContext dbContext,
        string? isbn,
        Guid? rareBookId,
        CancellationToken cancellationToken)
    {
        var hasIsbn = !string.IsNullOrWhiteSpace(isbn);
        var hasRare = rareBookId is { } id && id != Guid.Empty;
        if (hasIsbn == hasRare)
        {
            return Errors.MemberSelection.InvalidTarget();
        }

        if (hasRare)
        {
            var rareId = RareBookId.Create(rareBookId!.Value);
            var published = await dbContext.RareBooks.AsNoTracking()
                .AnyAsync(book => book.Id == rareId && book.Status == RareBookStatus.Published, cancellationToken);
            return published
                ? new SelectionTarget(null, rareId)
                : Errors.MemberSelection.NotInCatalog(rareBookId.Value.ToString());
        }

        if (!Isbn13.TryCreate(isbn!, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(isbn!);
        }

        var book = await dbContext.Books.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        if (book?.RedirectedToIsbn13 is { } canonical)
        {
            isbn13 = canonical;
            book = await dbContext.Books.AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == canonical, cancellationToken);
        }

        if (book is not null)
        {
            return book.IsHiddenFromCatalog
                ? Errors.MemberSelection.NotInCatalog(isbn13.Value)
                : new SelectionTarget(isbn13, null);
        }

        var announced = await dbContext.BookAnnouncements.AsNoTracking()
            .AnyAsync(announcement => announcement.Isbn13 == isbn13, cancellationToken);
        return announced
            ? new SelectionTarget(isbn13, null)
            : Errors.MemberSelection.NotInCatalog(isbn13.Value);
    }
}
```

`AddSelectionItemCommandHandler.cs` :

```csharp
public sealed class AddSelectionItemCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddSelectionItemCommand, ErrorOr<SelectionItemAddedResult>>
{
    public async Task<ErrorOr<SelectionItemAddedResult>> Handle(
        AddSelectionItemCommand command,
        CancellationToken cancellationToken)
    {
        var target = await SelectionTargetResolver.ResolveAsync(
            dbContext, command.Isbn13, command.RareBookId, cancellationToken);
        if (target.IsError)
        {
            return target.Errors;
        }

        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId, command.Email, command.FirstName, command.LastName, cancellationToken);

        var existing = await dbContext.MemberSelectionItems
            .AsNoTracking()
            .Where(item => item.UserId == user.Id)
            .Where(item =>
                (target.Value.Isbn13 != null && item.Isbn13 == target.Value.Isbn13) ||
                (target.Value.RareBookId != null && item.RareBookId == target.Value.RareBookId))
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is { } existingId)
        {
            return new SelectionItemAddedResult(existingId, AlreadyPresent: true);
        }

        var count = await dbContext.MemberSelectionItems.CountAsync(item => item.UserId == user.Id, cancellationToken);
        if (count >= MemberSelectionItem.MaxItemsPerMember)
        {
            return Errors.MemberSelection.TooManyItems(MemberSelectionItem.MaxItemsPerMember);
        }

        var now = dateTimeProvider.UtcNow;
        var item = target.Value.Isbn13 is { } isbn13
            ? MemberSelectionItem.CreateForEdition(Guid.NewGuid(), user.Id, isbn13, now)
            : MemberSelectionItem.CreateForRareBook(Guid.NewGuid(), user.Id, target.Value.RareBookId!, now);
        dbContext.MemberSelectionItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SelectionItemAddedResult(item.Id, AlreadyPresent: false);
    }
}
```

`RemoveSelectionItemCommandHandler` : `EnsureAsync`, charge l'item par `Id` **et** `UserId == user.Id`, `NotFound` sinon, `Remove`, `SaveChangesAsync`, `Result.Deleted`.

`SetSelectionItemStatusCommandHandler` : même garde de propriété, `Enum.IsDefined` sinon `InvalidStatus`, `item.ChangeStatus(command.Status, dateTimeProvider.UtcNow)`, `Result.Updated`.

`AddSelectionItemCommandValidator` : `ExternalId` et `Email` non vides.

Enregistrer `MemberIdentityService` : il l'est déjà (utilisé par `GetMyWatchlistQueryHandler`) ; ne rien ajouter si MediatR scanne l'assembly (vérifier `DependencyInjection.cs`).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~SelectionCommandHandlerTests"`
Expected: PASS (10 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(selection): add, remove and update member selection items"
```

---

### Task CS-4: Lecture de Ma sélection avec disponibilité

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberSelection/Queries/GetMySelection/{GetMySelectionQuery,GetMySelectionQueryHandler}.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberSelection/Common/SelectionAvailabilityProjector.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Application/MemberSelection/Common/SelectionResults.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Application.tests/MemberSelection/GetMySelectionQueryHandlerTests.cs`

**Interfaces:**
- Consumes: fixture CS-3 ; `GetPublicNextBookFair` (pour la prochaine bourse — réutiliser la requête existante `WhereActiveBookFair()` / résolveur de la prochaine bourse utilisé par `GetPublicNextBookFairQueryHandler`).
- Produces:
  - `record GetMySelectionQuery(string ExternalId, string Email, string? FirstName, string? LastName) : IRequest<ErrorOr<MySelectionResult>>`
  - `record MySelectionResult(DateTimeOffset GeneratedAt, NextFairSummary? NextFair, IReadOnlyList<SelectionItemResult> Items)`
  - `record NextFairSummary(Guid Id, DateTimeOffset StartsAt)`
  - `record SelectionItemResult(Guid Id, string Kind, string? Isbn13, Guid? RareBookId, string? RareBookSlug, string Title, string? Authors, string? Publisher, int? PublicationYear, string? PhysicalFormat, string? CoverUrl, SelectionAvailability Availability, DateTimeOffset AvailabilityCheckedAt, MemberSelectionStatus Status, DateTimeOffset AddedAt, DateTimeOffset? PurchasedAt)`
  - `enum SelectionAvailability { Available, Announced, OutOfStock, RareSold, Unavailable }`

Règles :
- `Available` : `Book.QuantityAvailable > 0` et non masqué, ou fiche rare publiée non vendue.
- `Announced` : pas de stock mais une annonce `Announced`.
- `OutOfStock` : `Book` connu, stock 0, pas d'annonce.
- `RareSold` : fiche rare vendue.
- `Unavailable` : fiche masquée ou dépubliée depuis l'ajout — la ligne reste listée (`F-10` §6.5 « fiches devenues indisponibles »).
- `AvailabilityCheckedAt` = `GeneratedAt` (fraîcheur, `F-10` §6.3).
- Tri : `AddedAt` décroissant, puis `Id`.
- Aucune ligne n'est retirée par la requête.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData(2, false, false, SelectionAvailability.Available)]
[InlineData(0, true, false, SelectionAvailability.Announced)]
[InlineData(0, false, false, SelectionAvailability.OutOfStock)]
[InlineData(3, false, true, SelectionAvailability.Unavailable)]
public async Task Handle_ProjectsEditionAvailability(int quantity, bool announced, bool hidden, SelectionAvailability expected)
{
    await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
    await fixture.AddBookAsync("9782070612758", quantityAvailable: quantity, hidden: false);
    if (announced) await fixture.AddAnnouncementAsync("9782070612758");
    await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);
    if (hidden) await fixture.HideBookAsync("9782070612758");

    var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

    result.Value.Items.Should().ContainSingle()
        .Which.Availability.Should().Be(expected);
}

[Fact]
public async Task Handle_WhenRareBookSold_ReturnsRareSold()
{
    await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
    var rare = await fixture.AddRareBookAsync();
    await fixture.CreateAddHandler().Handle(Add(rareBookId: rare.Id.Value), default);
    await fixture.MarkRareBookSoldAsync(rare.Id);

    var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

    result.Value.Items.Single().Availability.Should().Be(SelectionAvailability.RareSold);
}

[Fact]
public async Task Handle_ReturnsOnlyOwnItems_NewestFirst()
{
    await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
    await fixture.AddBookAsync("9782070612758");
    await fixture.AddBookAsync("9782070584628");
    await fixture.AddSelectionItemForOtherMemberAsync("9782070612758");
    await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);
    fixture.Clock.Advance(TimeSpan.FromMinutes(1));
    await fixture.CreateAddHandler().Handle(Add(isbn: "9782070584628"), default);

    var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

    result.Value.Items.Select(item => item.Isbn13).Should().Equal("9782070584628", "9782070612758");
}

[Fact]
public async Task Handle_WhenEmpty_ReturnsEmptyList()
{
    await using var fixture = await MemberSelectionFixture.CreateAsync(Now);

    var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

    result.IsError.Should().BeFalse();
    result.Value.Items.Should().BeEmpty();
}
```

(`fixture.Clock` : un `IDateTimeProvider` de test avec `Advance`. S'il n'existe pas dans `Application.tests`, l'ajouter dans `MemberSelectionFixture` — un champ `DateTime` mutable derrière `Substitute.For<IDateTimeProvider>()`.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~GetMySelectionQueryHandlerTests"`
Expected: FAIL — types introuvables.

- [ ] **Step 3: Write minimal implementation**

Le handler suit la forme de `GetMyWatchlistQueryHandler` : `EnsureAsync`, puis items du membre, puis **un** chargement groupé de `Books` (par ISBN), `BookAnnouncements` (statut `Announced`), `RareBooks` (par id, avec la première photo pour la couverture). `SelectionAvailabilityProjector` est une classe statique pure :

```csharp
public static class SelectionAvailabilityProjector
{
    public static SelectionAvailability ForEdition(Book? book, bool announced)
    {
        if (book is null)
        {
            return announced ? SelectionAvailability.Announced : SelectionAvailability.Unavailable;
        }

        if (book.IsHiddenFromCatalog || book.RedirectedToIsbn13 is not null)
        {
            return SelectionAvailability.Unavailable;
        }

        if (book.QuantityAvailable > 0)
        {
            return SelectionAvailability.Available;
        }

        return announced ? SelectionAvailability.Announced : SelectionAvailability.OutOfStock;
    }

    public static SelectionAvailability ForRareBook(RareBook? rareBook)
    {
        if (rareBook is null || rareBook.Status != RareBookStatus.Published)
        {
            return SelectionAvailability.Unavailable;
        }

        return rareBook.IsSold ? SelectionAvailability.RareSold : SelectionAvailability.Available;
    }
}
```

Titre de repli pour une édition sans métadonnées : `"ISBN " + isbn`. Prochaine bourse : réutiliser le code de `GetPublicNextBookFairQueryHandler` (extraire une méthode statique partagée si nécessaire plutôt que dupliquer).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~GetMySelectionQueryHandlerTests"`
Expected: PASS (7 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(selection): read member selection with current availability"
```

---

### Task CS-5: Fusion d'une sélection locale

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberSelection/Commands/MergeSelection/{MergeSelectionCommand,MergeSelectionCommandHandler,MergeSelectionCommandValidator}.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Application.tests/MemberSelection/MergeSelectionCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `SelectionTargetResolver` (CS-3).
- Produces:
  - `record MergeSelectionEntry(string? Isbn13, Guid? RareBookId, DateTime AddedAt)`
  - `record MergeSelectionCommand(string ExternalId, string Email, string? FirstName, string? LastName, IReadOnlyList<MergeSelectionEntry> Entries) : IRequest<ErrorOr<MergeSelectionResult>>`
  - `record MergeSelectionResult(int Added, int AlreadyPresent, IReadOnlyList<string> Rejected)` — `Rejected` contient la référence (ISBN ou id rare) de chaque entrée refusée.

Règles (`RG-56`) : ajoute les entrées absentes avec leur `AddedAt` local (borné à `≤ now`, sinon `now`) ; ne supprime **jamais** une entrée distante ; une entrée invalide ou hors catalogue est rejetée sans faire échouer le lot ; doublons internes au lot dédupliqués ; au plus **200** entrées par appel (validator) ; plafond global `MaxItemsPerMember` respecté (le surplus part dans `Rejected`).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Merge_AddsMissing_KeepsRemote_Deduplicates()
{
    await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
    await fixture.AddBookAsync("9782070612758");
    await fixture.AddBookAsync("9782070584628");
    await fixture.AddBookAsync("9782253006329");
    await fixture.CreateAddHandler().Handle(Add(isbn: "9782253006329"), default); // fiche déjà sur le compte

    var result = await fixture.CreateMergeHandler().Handle(Merge(
        new MergeSelectionEntry("9782070612758", null, Now.AddDays(-2)),
        new MergeSelectionEntry("9782070584628", null, Now.AddDays(-1)),
        new MergeSelectionEntry("9782070584628", null, Now.AddDays(-1)),
        new MergeSelectionEntry("9782253006329", null, Now.AddDays(-3))), default);

    result.Value.Added.Should().Be(2);
    result.Value.AlreadyPresent.Should().Be(1);
    (await fixture.Context.MemberSelectionItems.CountAsync()).Should().Be(3);
    (await fixture.Context.MemberSelectionItems.SingleAsync(item => item.Isbn13 == Isbn("9782070612758")))
        .AddedAt.Should().Be(Now.AddDays(-2));
}

[Fact]
public async Task Merge_RejectsUnknownWithoutFailingBatch()
{
    await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
    await fixture.AddBookAsync("9782070612758");

    var result = await fixture.CreateMergeHandler().Handle(Merge(
        new MergeSelectionEntry("9782070612758", null, Now),
        new MergeSelectionEntry("9780000000002", null, Now)), default);

    result.Value.Added.Should().Be(1);
    result.Value.Rejected.Should().Equal("9780000000002");
}

[Fact]
public async Task Merge_ClampsFutureLocalDates()
{
    await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
    await fixture.AddBookAsync("9782070612758");

    await fixture.CreateMergeHandler().Handle(Merge(
        new MergeSelectionEntry("9782070612758", null, Now.AddYears(1))), default);

    (await fixture.Context.MemberSelectionItems.SingleAsync()).AddedAt.Should().Be(Now);
}

[Fact]
public async Task Merge_WithEmptyBatch_ChangesNothing()
{
    await using var fixture = await MemberSelectionFixture.CreateAsync(Now);

    var result = await fixture.CreateMergeHandler().Handle(Merge(), default);

    result.Value.Should().Be(new MergeSelectionResult(0, 0, []));
}
```

(`Isbn(string)` : helper local `Isbn13.TryCreate(value, out var isbn) ? isbn : throw …`.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~MergeSelectionCommandHandlerTests"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed class MergeSelectionCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<MergeSelectionCommand, ErrorOr<MergeSelectionResult>>
{
    public async Task<ErrorOr<MergeSelectionResult>> Handle(
        MergeSelectionCommand command,
        CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId, command.Email, command.FirstName, command.LastName, cancellationToken);
        var now = dateTimeProvider.UtcNow;

        var existing = await dbContext.MemberSelectionItems
            .Where(item => item.UserId == user.Id)
            .Select(item => new { item.Isbn13, item.RareBookId })
            .ToListAsync(cancellationToken);
        var remoteKeys = existing
            .Select(e => e.Isbn13 is not null ? $"edition:{e.Isbn13.Value}" : $"rare:{e.RareBookId!.Value}")
            .ToHashSet(StringComparer.Ordinal);
        var seenInBatch = new HashSet<string>(StringComparer.Ordinal);
        var remaining = MemberSelectionItem.MaxItemsPerMember - existing.Count;

        var added = 0;
        var alreadyPresent = 0;
        var rejected = new List<string>();

        foreach (var entry in command.Entries)
        {
            var reference = entry.Isbn13 ?? entry.RareBookId?.ToString() ?? string.Empty;
            var target = await SelectionTargetResolver.ResolveAsync(
                dbContext, entry.Isbn13, entry.RareBookId, cancellationToken);
            if (target.IsError)
            {
                rejected.Add(reference);
                continue;
            }

            var key = target.Value.Isbn13 is { } isbn
                ? $"edition:{isbn.Value}"
                : $"rare:{target.Value.RareBookId!.Value}";
            if (!seenInBatch.Add(key))
            {
                continue; // duplicate inside the local list: neither added nor counted
            }

            if (remoteKeys.Contains(key))
            {
                alreadyPresent++;
                continue;
            }

            if (remaining <= 0)
            {
                rejected.Add(reference);
                continue;
            }

            var addedAt = entry.AddedAt.Kind == DateTimeKind.Utc && entry.AddedAt <= now ? entry.AddedAt : now;
            dbContext.MemberSelectionItems.Add(target.Value.Isbn13 is { } isbn13
                ? MemberSelectionItem.CreateForEdition(Guid.NewGuid(), user.Id, isbn13, addedAt)
                : MemberSelectionItem.CreateForRareBook(Guid.NewGuid(), user.Id, target.Value.RareBookId!, addedAt));
            added++;
            remaining--;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new MergeSelectionResult(added, alreadyPresent, rejected);
    }
}
```

Validator : `Entries.Count <= 200`, chaque entrée a exactement une cible.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~MergeSelectionCommandHandlerTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(selection): merge a local selection without deleting remote items"
```

---

### Task CS-6: Routes API de Ma sélection

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Api/Controllers/MemberAccountController.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Contracts/MemberSelection/{AddSelectionItemRequest,SetSelectionItemStatusRequest,MergeSelectionRequest,MySelectionResponse}.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Api/Program.cs` (ou l'endroit où `UseAccountController()` est appelé) — ajouter `UseMemberAccountController()`
- Test: `src/Backend/Vole_Papillon_Damour.Api.tests/MemberAccount/MemberSelectionEndpointsTests.cs`

**Interfaces:**
- Consumes: commandes/requêtes CS-3 à CS-5 ; helper `TryGetMemberIdentity` de `BookController` — **le déplacer** dans un fichier partagé `Api/Common/MemberIdentityClaims.cs` (`internal static class`) et l'utiliser depuis les deux contrôleurs (pas de copie).
- Produces (routes, toutes `RequireAuthorization()`) :
  - `GET /catalog/me/selection` → `200 MySelectionResponse`
  - `POST /catalog/me/selection` body `{ isbn13?: string, rareBookId?: string }` → `200 { id, alreadyPresent }`
  - `DELETE /catalog/me/selection/{id:guid}` → `204`
  - `PATCH /catalog/me/selection/{id:guid}` body `{ status: "ToTake"|"Purchased"|"NotFound"|"ToRevisit" }` → `204`
  - `POST /catalog/me/selection/merge` body `{ entries: [{ isbn13?, rareBookId?, addedAt }] }` → `200 { added, alreadyPresent, rejected }`

- [ ] **Step 1: Write the failing tests**

Suivre le patron des tests d'API existants (`grep -rl "WebApplicationFactory" src/Backend/Vole_Papillon_Damour.Api.tests`) et leur manière d'injecter un principal authentifié.

```csharp
[Fact]
public async Task GetSelection_WithoutAuthentication_Returns401()
{
    using var client = factory.CreateClient();

    var response = await client.GetAsync("/catalog/me/selection");

    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}

[Fact]
public async Task PatchSelection_WithUnknownStatus_Returns400()
{
    using var client = factory.CreateAuthenticatedMemberClient();

    var response = await client.PatchAsJsonAsync(
        $"/catalog/me/selection/{Guid.NewGuid()}", new { status = "Reserved" });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task PostSelection_ForwardsIdentityAndTargetToMediator()
{
    var mediator = factory.Mediator;
    mediator.Send(Arg.Any<AddSelectionItemCommand>(), Arg.Any<CancellationToken>())
        .Returns(new SelectionItemAddedResult(Guid.Parse("11111111-1111-1111-1111-111111111111"), false));
    using var client = factory.CreateAuthenticatedMemberClient();

    var response = await client.PostAsJsonAsync("/catalog/me/selection", new { isbn13 = "9782070612758" });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    await mediator.Received(1).Send(
        Arg.Is<AddSelectionItemCommand>(command =>
            command.Isbn13 == "9782070612758" && command.RareBookId == null),
        Arg.Any<CancellationToken>());
}
```

Si la factory existante ne propose pas `Mediator` substituable, écrire ces tests au niveau des **mappings** comme le font les tests d'API actuels (le patron existant fait foi).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Api.tests --filter "FullyQualifiedName~MemberSelectionEndpointsTests"`
Expected: FAIL — 404 sur les routes.

- [ ] **Step 3: Write minimal implementation**

Même forme que `BookController` (`public static class MemberAccountController`, `UseEndpoints`, `MapGet/MapPost/…`, `result.Match(…, error => error.Result())`). Le `status` du PATCH est parsé par `Enum.TryParse<MemberSelectionStatus>(…, ignoreCase: true)` + `Enum.IsDefined`, sinon `DomainErrors.MemberSelection.InvalidStatus().Result()`. Les dates de fusion sont reçues en `DateTimeOffset` et converties en UTC (`.UtcDateTime`).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Api.tests --filter "FullyQualifiedName~MemberSelectionEndpointsTests"` puis `dotnet test src/Backend/Vole_Papillon_Damour.Api.tests`
Expected: PASS ; aucune régression des tests de watchlist après déplacement de `TryGetMemberIdentity`.

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(selection): expose member selection endpoints"
```

---

### Task CS-7: Catalog — sélection locale et calcul de fusion

**Files:**
- Create: `src/Catalog/src/app/core/selection/local-selection.store.ts`
- Create: `src/Catalog/src/app/core/selection/selection-merge.ts`
- Test: `src/Catalog/src/app/core/selection/local-selection.store.spec.ts`
- Test: `src/Catalog/src/app/core/selection/selection-merge.spec.ts`

**Interfaces:**
- Produces:
  - `type SelectionRef = {kind: 'edition'; isbn13: string} | {kind: 'rare'; rareBookId: string}`
  - `interface LocalSelectionEntry { ref: SelectionRef; title: string; addedAt: string }` (`title` sert uniquement à l'affichage hors ligne)
  - `selectionKey(ref: SelectionRef): string` → `edition:<isbn>` ou `rare:<id>`
  - `class LocalSelectionStore` (`@Injectable({providedIn: 'root'})`) : `list(): LocalSelectionEntry[]`, `has(ref): boolean`, `add(entry): void`, `remove(ref): void`, `clear(): void`, `readonly available: boolean`
  - `planSelectionMerge(local: LocalSelectionEntry[], remoteKeys: ReadonlySet<string>): { toSend: LocalSelectionEntry[]; alreadyRemote: number; needsConfirmation: boolean }` — `needsConfirmation` vaut `true` si **les deux** listes ont au moins une entrée (`F-10` §6.6 point 5).

Stockage : clé `vpd.catalog.selection.v1`, valeur `{version: 1, entries: LocalSelectionEntry[]}`. Lecture corrompue → liste vide (et la clé est laissée intacte, pour ne rien détruire). Hors navigateur (SSR) ou `localStorage` qui lève → `available = false`, `list()` renvoie `[]`, `add/remove` sont sans effet.

- [ ] **Step 1: Write the failing tests**

```ts
describe('LocalSelectionStore', () => {
  const key = 'vpd.catalog.selection.v1';
  const entry = (isbn13: string) => ({ref: {kind: 'edition' as const, isbn13}, title: 'Le Petit Prince', addedAt: '2026-09-23T10:00:00.000Z'});

  beforeEach(() => localStorage.removeItem(key));

  function create(platformId: object = 'browser'): LocalSelectionStore {
    TestBed.configureTestingModule({providers: [{provide: PLATFORM_ID, useValue: platformId}]});
    return TestBed.inject(LocalSelectionStore);
  }

  it('survives a reload', () => {
    create().add(entry('9782070612758'));
    TestBed.resetTestingModule();

    expect(create().list().map(e => selectionKey(e.ref))).toEqual(['edition:9782070612758']);
  });

  it('does not duplicate the same edition', () => {
    const store = create();
    store.add(entry('9782070612758'));
    store.add(entry('9782070612758'));

    expect(store.list().length).toBe(1);
  });

  it('returns an empty list when storage is corrupted and keeps the raw value', () => {
    localStorage.setItem(key, '{not json');

    expect(create().list()).toEqual([]);
    expect(localStorage.getItem(key)).toBe('{not json');
  });

  it('is inert on the server', () => {
    const store = create('server');
    store.add(entry('9782070612758'));

    expect(store.available).toBeFalse();
    expect(store.list()).toEqual([]);
    expect(localStorage.getItem(key)).toBeNull();
  });

  it('is inert when localStorage throws', () => {
    spyOn(Storage.prototype, 'getItem').and.throwError('SecurityError');

    const store = create();

    expect(store.available).toBeFalse();
    expect(store.list()).toEqual([]);
  });
});

describe('planSelectionMerge', () => {
  const local = (isbn13: string) => ({ref: {kind: 'edition' as const, isbn13}, title: 't', addedAt: '2026-09-20T10:00:00.000Z'});

  it('sends only entries missing remotely and asks confirmation when both lists are non-empty', () => {
    const plan = planSelectionMerge([local('1'), local('2')], new Set(['edition:2', 'edition:3']));

    expect(plan.toSend.map(e => selectionKey(e.ref))).toEqual(['edition:1']);
    expect(plan.alreadyRemote).toBe(1);
    expect(plan.needsConfirmation).toBeTrue();
  });

  it('does not ask confirmation when the account is empty', () => {
    expect(planSelectionMerge([local('1')], new Set()).needsConfirmation).toBeFalse();
  });

  it('does nothing when the local list is empty', () => {
    const plan = planSelectionMerge([], new Set(['edition:3']));

    expect(plan.toSend).toEqual([]);
    expect(plan.needsConfirmation).toBeFalse();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless --include src/app/core/selection`
Expected: FAIL — modules introuvables.

- [ ] **Step 3: Write minimal implementation**

```ts
// selection-merge.ts
export type SelectionRef = {kind: 'edition'; isbn13: string} | {kind: 'rare'; rareBookId: string};

export interface LocalSelectionEntry {
  ref: SelectionRef;
  title: string;
  addedAt: string;
}

export function selectionKey(ref: SelectionRef): string {
  return ref.kind === 'edition' ? `edition:${ref.isbn13}` : `rare:${ref.rareBookId}`;
}

export function planSelectionMerge(
  local: readonly LocalSelectionEntry[],
  remoteKeys: ReadonlySet<string>,
): {toSend: LocalSelectionEntry[]; alreadyRemote: number; needsConfirmation: boolean} {
  const toSend = local.filter(entry => !remoteKeys.has(selectionKey(entry.ref)));
  return {
    toSend,
    alreadyRemote: local.length - toSend.length,
    needsConfirmation: local.length > 0 && remoteKeys.size > 0,
  };
}
```

```ts
// local-selection.store.ts
const STORAGE_KEY = 'vpd.catalog.selection.v1';

@Injectable({providedIn: 'root'})
export class LocalSelectionStore {
  readonly available: boolean;

  constructor(@Inject(PLATFORM_ID) platformId: object) {
    this.available = isPlatformBrowser(platformId) && LocalSelectionStore.probe();
  }

  list(): LocalSelectionEntry[] {
    if (!this.available) {
      return [];
    }
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return [];
      }
      const parsed = JSON.parse(raw) as {version?: number; entries?: LocalSelectionEntry[]};
      return parsed.version === 1 && Array.isArray(parsed.entries) ? parsed.entries : [];
    } catch {
      return [];
    }
  }

  has(ref: SelectionRef): boolean {
    const key = selectionKey(ref);
    return this.list().some(entry => selectionKey(entry.ref) === key);
  }

  add(entry: LocalSelectionEntry): void {
    if (this.has(entry.ref)) {
      return;
    }
    this.write([entry, ...this.list()]);
  }

  remove(ref: SelectionRef): void {
    const key = selectionKey(ref);
    this.write(this.list().filter(entry => selectionKey(entry.ref) !== key));
  }

  clear(): void {
    if (!this.available) {
      return;
    }
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Storage refused: nothing left to clear on this device.
    }
  }

  private write(entries: LocalSelectionEntry[]): void {
    if (!this.available) {
      return;
    }
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({version: 1, entries}));
    } catch {
      // Quota or privacy mode: the anonymous selection is best-effort by design (RG-55).
    }
  }

  private static probe(): boolean {
    try {
      localStorage.getItem(STORAGE_KEY);
      return true;
    } catch {
      return false;
    }
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless --include src/app/core/selection`
Expected: PASS (8 specs).

- [ ] **Step 5: Commit**

```bash
git add src/Catalog/src/app/core/selection
git commit -m "feat(catalog): keep an anonymous selection on the device"
```

---

### Task CS-8: Catalog — façade de sélection, API membre et fusion à la connexion

**Files:**
- Modify: `src/Catalog/src/app/core/catalog.models.ts`
- Modify: `src/Catalog/src/app/core/catalog-member-api.service.ts`
- Create: `src/Catalog/src/app/core/selection/catalog-selection.service.ts`
- Test: `src/Catalog/src/app/core/catalog-member-api.service.spec.ts` (créer s'il n'existe pas)
- Test: `src/Catalog/src/app/core/selection/catalog-selection.service.spec.ts`

**Interfaces:**
- Consumes: `LocalSelectionStore`, `planSelectionMerge`, `selectionKey` (CS-7) ; `CatalogAuthService` (jeton d'accès — reprendre la méthode utilisée par `CatalogAccountPageComponent` pour obtenir le token) ; routes CS-6.
- Produces:
  - Modèles : `CatalogSelectionStatus = 'ToTake' | 'Purchased' | 'NotFound' | 'ToRevisit'`, `CatalogSelectionAvailability = 'Available' | 'Announced' | 'OutOfStock' | 'RareSold' | 'Unavailable'`, `CatalogSelectionItem`, `CatalogSelectionResponse`, `CatalogSelectionMergeResult`.
  - `CatalogMemberApiService` : `getSelection(token)`, `addSelectionItem(token, {isbn13?, rareBookId?})`, `removeSelectionItem(token, id)`, `setSelectionStatus(token, id, status)`, `mergeSelection(token, entries)`.
  - `CatalogSelectionService` (`providedIn: 'root'`) :
    - `readonly keys: Signal<ReadonlySet<string>>` — clés présentes (locales si anonyme, distantes si connecté)
    - `readonly mode: Signal<'local' | 'synced' | 'local-unsynced'>` — `local-unsynced` quand l'utilisateur connecté a refusé la fusion
    - `readonly pendingMerge: Signal<{count: number} | null>` — non nul quand une confirmation est attendue
    - `add(ref: SelectionRef, title: string): Promise<void>`
    - `remove(ref: SelectionRef): Promise<void>`
    - `refresh(): Promise<CatalogSelectionResponse | null>`
    - `onSignedIn(): Promise<void>` — calcule le plan ; si `needsConfirmation` → `pendingMerge`, sinon fusion directe
    - `confirmMerge(): Promise<CatalogSelectionMergeResult>` — envoie, puis **vide le stockage local** seulement si l'appel réussit, et conserve localement les entrées `rejected`
    - `declineMerge(): void` — passe en `local-unsynced`, ne supprime rien

- [ ] **Step 1: Write the failing tests**

```ts
describe('CatalogSelectionService', () => {
  let api: jasmine.SpyObj<CatalogMemberApiService>;
  let auth: {isAuthenticated: WritableSignal<boolean>; acquireAccessToken: jasmine.Spy};
  let local: LocalSelectionStore;

  beforeEach(() => {
    localStorage.removeItem('vpd.catalog.selection.v1');
    api = jasmine.createSpyObj('CatalogMemberApiService', [
      'getSelection', 'addSelectionItem', 'removeSelectionItem', 'setSelectionStatus', 'mergeSelection']);
    auth = {isAuthenticated: signal(false), acquireAccessToken: jasmine.createSpy().and.resolveTo('token')};
    TestBed.configureTestingModule({providers: [
      {provide: CatalogMemberApiService, useValue: api},
      {provide: CatalogAuthService, useValue: auth},
    ]});
    local = TestBed.inject(LocalSelectionStore);
  });

  it('stores locally and never calls the API when anonymous', async () => {
    const service = TestBed.inject(CatalogSelectionService);

    await service.add({kind: 'edition', isbn13: '9782070612758'}, 'Le Petit Prince');

    expect(service.keys().has('edition:9782070612758')).toBeTrue();
    expect(api.addSelectionItem).not.toHaveBeenCalled();
  });

  it('asks for confirmation when both lists have entries, then merges and clears local storage', async () => {
    local.add({ref: {kind: 'edition', isbn13: '1'}, title: 'a', addedAt: '2026-09-20T10:00:00.000Z'});
    local.add({ref: {kind: 'edition', isbn13: '2'}, title: 'b', addedAt: '2026-09-20T10:00:00.000Z'});
    api.getSelection.and.returnValue(of(selectionResponse(['3'])));
    api.mergeSelection.and.returnValue(of({added: 2, alreadyPresent: 0, rejected: []}));
    const service = TestBed.inject(CatalogSelectionService);
    auth.isAuthenticated.set(true);

    await service.onSignedIn();
    expect(service.pendingMerge()).toEqual({count: 2});
    expect(api.mergeSelection).not.toHaveBeenCalled();

    api.getSelection.and.returnValue(of(selectionResponse(['1', '2', '3'])));
    await service.confirmMerge();

    expect(local.list()).toEqual([]);
    expect(service.keys().size).toBe(3);
    expect(service.mode()).toBe('synced');
  });

  it('keeps rejected entries locally after merge', async () => {
    local.add({ref: {kind: 'edition', isbn13: '1'}, title: 'a', addedAt: '2026-09-20T10:00:00.000Z'});
    api.getSelection.and.returnValue(of(selectionResponse([])));
    api.mergeSelection.and.returnValue(of({added: 0, alreadyPresent: 0, rejected: ['1']}));
    const service = TestBed.inject(CatalogSelectionService);
    auth.isAuthenticated.set(true);

    await service.onSignedIn();

    expect(local.list().length).toBe(1);
  });

  it('keeps local entries and marks them unsynced when the merge is declined', async () => {
    local.add({ref: {kind: 'edition', isbn13: '1'}, title: 'a', addedAt: '2026-09-20T10:00:00.000Z'});
    api.getSelection.and.returnValue(of(selectionResponse(['3'])));
    const service = TestBed.inject(CatalogSelectionService);
    auth.isAuthenticated.set(true);

    await service.onSignedIn();
    service.declineMerge();

    expect(service.mode()).toBe('local-unsynced');
    expect(local.list().length).toBe(1);
    expect(api.mergeSelection).not.toHaveBeenCalled();
  });

  it('keeps local storage when the merge call fails', async () => {
    local.add({ref: {kind: 'edition', isbn13: '1'}, title: 'a', addedAt: '2026-09-20T10:00:00.000Z'});
    api.getSelection.and.returnValue(of(selectionResponse([])));
    api.mergeSelection.and.returnValue(throwError(() => new HttpErrorResponse({status: 503})));
    const service = TestBed.inject(CatalogSelectionService);
    auth.isAuthenticated.set(true);

    await expectAsync(service.onSignedIn()).toBeRejected();

    expect(local.list().length).toBe(1);
  });
});
```

(`selectionResponse(isbns)` : helper local qui construit un `CatalogSelectionResponse` minimal. Remplacer `acquireAccessToken` par le nom réel de la méthode de `CatalogAuthService` qui fournit le jeton.)

Ajouter pour `CatalogMemberApiService` un test par méthode vérifiant l'URL, le verbe, le corps et l'en-tête `Authorization`, via `HttpTestingController`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless --include src/app/core`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Méthodes API : même forme que `addWatchlistItem`/`removeWatchlistItem`. Façade : signaux privés `remoteKeys`, `mode`, `pendingMerge` ; `keys` est un `computed` qui renvoie les clés distantes en mode `synced`, sinon les clés locales. `add()` en mode `synced` appelle l'API puis `refresh()` ; sinon `local.add()`. En cas d'erreur 401 à l'ajout, **ne pas** basculer silencieusement en local : lever l'erreur pour que le composant propose la reconnexion (même comportement que la liste de recherche).

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless --include src/app/core`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Catalog/src/app/core
git commit -m "feat(catalog): synchronise the selection after sign-in"
```

---

### Task CS-9: Catalog — bouton « Ajouter à Ma sélection » sur les fiches

**Files:**
- Create: `src/Catalog/src/app/shared/components/selection-button/selection-button.component.{ts,html,scss,spec.ts}`
- Modify: `src/Catalog/src/app/features/book-detail/catalog-book-detail-page.component.{ts,html}`
- Modify: `src/Catalog/src/app/features/rare-books/catalog-rare-book-detail-page.component.{ts,html}`
- Modify: `src/Catalog/src/app/features/work/catalog-work-page.component.{ts,html}`
- Modify: le module qui déclare les composants partagés (`app.module.ts` ou module partagé existant)

**Interfaces:**
- Consumes: `CatalogSelectionService` (CS-8).
- Produces: `<app-selection-button [ref]="SelectionRef" [title]="string">`.

Comportement (`F-10` §6.1) :
- Absent : bouton secondaire **« Ajouter à Ma sélection »**.
- Présent : texte **« Dans Ma sélection »** (état, non cliquable) + bouton **« Retirer »**.
- Pendant l'appel : bouton désactivé, `aria-busy="true"`.
- Après ajout anonyme, message discret : « Enregistré sur cet appareil. Connectez-vous pour le retrouver ailleurs. » avec le lien de connexion existant (`catalog-auth-prompt`). **Aucune** redirection forcée (`F-10` §5.2).
- Message d'erreur `role="alert"` si l'API échoue ; aucun changement d'état.
- Page d'œuvre : **pas** de bouton global ; le bouton est posé sur chaque édition listée (`RG-53`).
- Jamais le mot « réserver » ; ligne d'aide sous le bouton : « Ne réserve pas le livre. »

- [ ] **Step 1: Write the failing test**

```ts
describe('SelectionButtonComponent', () => {
  const ref = {kind: 'edition' as const, isbn13: '9782070612758'};
  let keys: WritableSignal<ReadonlySet<string>>;
  let selection: jasmine.SpyObj<CatalogSelectionService>;

  beforeEach(async () => {
    keys = signal(new Set<string>());
    selection = jasmine.createSpyObj('CatalogSelectionService', ['add', 'remove'], {keys, mode: signal('local')});
    selection.add.and.callFake(async () => keys.set(new Set(['edition:9782070612758'])));
    selection.remove.and.callFake(async () => keys.set(new Set()));
    await TestBed.configureTestingModule({
      declarations: [SelectionButtonComponent],
      providers: [{provide: CatalogSelectionService, useValue: selection}],
    }).compileComponents();
  });

  function render(): ComponentFixture<SelectionButtonComponent> {
    const fixture = TestBed.createComponent(SelectionButtonComponent);
    fixture.componentRef.setInput('ref', ref);
    fixture.componentRef.setInput('title', 'Le Petit Prince');
    fixture.detectChanges();
    return fixture;
  }

  it('adds then shows the in-selection state with a remove action', async () => {
    const fixture = render();
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(selection.add).toHaveBeenCalledWith(ref, 'Le Petit Prince');
    expect(fixture.nativeElement.textContent).toContain('Dans Ma sélection');
    expect(fixture.nativeElement.textContent).toContain('Retirer');
  });

  it('never uses reservation wording', () => {
    const text = (render().nativeElement.textContent as string).toLowerCase();

    for (const forbidden of ['panier', 'réserv', 'mettre de côté', 'commande']) {
      expect(text).not.toContain(forbidden);
    }
  });

  it('shows an alert and keeps state when adding fails', async () => {
    selection.add.and.rejectWith(new Error('boom'));
    const fixture = render();
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Ajouter à Ma sélection');
  });
});
```

Ajouter dans les specs des pages : la fiche d'édition affiche `app-selection-button` avec l'ISBN de la fiche ; la fiche rare avec son id ; la page d'œuvre n'affiche **pas** de bouton hors des lignes d'édition.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Composant `standalone: false`, `ChangeDetectionStrategy.OnPush`, `input.required<SelectionRef>()`, `input.required<string>()`, `busy = signal(false)`, `error = signal<string | null>(null)`, `inSelection = computed(() => selection.keys().has(selectionKey(this.ref())))`. Styles : reprendre les classes de bouton secondaire déjà utilisées par l'action « Me prévenir » de la fiche (liste de recherche) ; aligner sur la maquette lorsqu'elle est fournie.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: PASS, aucune régression.

- [ ] **Step 5: Commit**

```bash
git add src/Catalog/src/app
git commit -m "feat(catalog): add the selection action on public records"
```

🧪 En local, sans compte : ouvrir une fiche disponible, ajouter, fermer l'onglet, rouvrir → « Dans Ma sélection ». Vérifier à 390 px que le bouton ne déborde pas.

---

### Task CS-10: Catalog — onglet « Ma sélection » et fusion

**Files:**
- Create: `src/Catalog/src/app/features/account/selection/account-selection.component.{ts,html,scss,spec.ts}`
- Modify: `src/Catalog/src/app/features/account/catalog-account-page.component.{ts,html,spec.ts}`
- Modify: `src/Catalog/src/app/app-routing.module.ts` (si une route `/mon-compte/selection` est souhaitée par la maquette ; sinon onglet seul)

**Interfaces:**
- Consumes: `CatalogSelectionService` (CS-8) ; `CatalogMemberApiService.setSelectionStatus`, `removeSelectionItem`.
- Produces: `CatalogAccountTab` étendu à `'selection' | 'purchases' | 'watchlist' | 'card' | 'contribution' | 'preferences'` (les onglets `purchases` et `card` sont branchés en CS-24 et CS-13). Ordre visible : **Ma sélection, Mes achats, Mes recherches, Ma carte, Compte et données** (`F-10` §5) ; « Ma contribution » reste réservé aux bénévoles. Onglet par défaut : **Ma sélection**.

Comportement :
- Liste (F-10 §6.3) : couverture ou placeholder, titre, auteur, éditeur · année · format, ISBN, badge de disponibilité, prochaine bourse, « Ajouté le … », sélecteur d'état (4 valeurs), action Retirer.
- Badges : `Available` → « Disponible », `Announced` → « Annoncé », `OutOfStock` → « Épuisé », `RareSold` → « Fiche vendue », `Unavailable` → « Plus visible au catalogue ».
- Ligne de fraîcheur : « Disponibilité vérifiée le {date} à {heure}. Elle peut changer avant votre visite. »
- Filtres (`F-10` §6.5) : Tout · Prochaine visite (`ToTake` + `ToRevisit`) · Encore disponible · Acheté · Pas trouvé · Indisponibles.
- État vide : « Ajoutez un livre depuis le catalogue pour le retrouver à la bourse. » + lien vers la recherche.
- Anonyme : même vue alimentée par `LocalSelectionStore` (titres locaux, sans disponibilité), avec le bandeau « Cette sélection est enregistrée sur cet appareil uniquement. » et l'appel à se connecter.
- `local-unsynced` : bandeau « Sélection de cet appareil, non synchronisée » + bouton « Ajouter à mon compte » qui relance la fusion.
- `pendingMerge` : boîte de dialogue « Vous avez {n} livre(s) sélectionné(s) sur cet appareil. Les ajouter à votre compte ? » — **Ajouter** / **Garder sur cet appareil**. Aucun livre du compte n'est retiré.

- [ ] **Step 1: Write the failing tests**

```ts
it('shows the empty state wording', () => { /* selection vide → texte exact F-10 §5.1 */ });
it('filters "Prochaine visite" to ToTake and ToRevisit', () => { /* 4 items, 2 attendus */ });
it('changes a status through the API and re-renders', async () => { /* setSelectionStatus appelé avec (token, id, "NotFound") */ });
it('asks before merging and keeps local items when declined', async () => { /* pendingMerge {count:2} → clic "Garder sur cet appareil" → declineMerge appelé */ });
it('renders the freshness line with the generation date', () => { /* "Disponibilité vérifiée le" */ });
it('orders tabs as the spec requires and opens Ma sélection by default', () => {
  expect(tabLabels()).toEqual(['Ma sélection', 'Mes achats', 'Mes recherches', 'Ma carte', 'Compte et données']);
});
```

Chaque `it` ci-dessus doit être écrit **complètement** en suivant la structure de `catalog-account-page.component.spec.ts` (mêmes stubs d'auth et d'API) : état initial, action, assertion sur le DOM. Aucune spec vide ne doit être commitée.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless --include src/app/features/account`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Composant enfant `AccountSelectionComponent` (logique de liste/filtre/état) ; la page compte ne fait qu'héberger l'onglet et déclencher `selection.onSignedIn()` une fois l'authentification initialisée. La mise en page suit la maquette Claude Design dès qu'elle est disponible ; en attendant, réutiliser les cartes de la liste de recherche (`watchlist`) pour rester cohérent.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless` puis `cd src/Catalog && npm run build`
Expected: PASS ; build SSR sans erreur (aucun accès `localStorage` au rendu serveur).

- [ ] **Step 5: Commit**

```bash
git add src/Catalog/src/app
git commit -m "feat(catalog): add the Ma sélection account tab"
```

🧪 Parcours A et B de `F-10` §14 en local : deux fiches anonymes, connexion à un compte possédant déjà une troisième fiche, confirmation, trois fiches une seule fois, liste de recherche inchangée. Vérifier 390 px.

🚀 Étape 1 déployable seule (backend + Catalog) : la Scanette n'est pas touchée.

---

# Étape 2 — Carte de compte

### Task CS-11: Domaine `MemberCard`, code de secours et jeton HMAC

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Domain/MemberCardAggregate/MemberCard.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Domain/MemberCardAggregate/MemberCardRecoveryCode.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Domain/Common/Errors/Errors.MemberCard.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Application/Common/Interfaces/Services/IMemberCardTokenService.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/MemberCards/{MemberCardTokenService,MemberCardTokenOptions}.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Domain.tests/MemberCards/{MemberCardTests,MemberCardRecoveryCodeTests}.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/MemberCards/MemberCardTokenServiceTests.cs`

**Interfaces:**
- Produces:
  - `MemberCard : Entity<Guid>` : `UserId UserId`, `int Version`, `string RecoveryCode`, `DateTime IssuedAt`, `DateTime? RotatedAt`, `DateTime? RevokedAt`, `bool IsActive => RevokedAt is null` ; `static MemberCard Issue(Guid id, UserId userId, string recoveryCode, DateTime issuedAt)` ; `void Rotate(string newRecoveryCode, DateTime rotatedAt)` (incrémente `Version`, change le code) ; `void Revoke(DateTime revokedAt)`.
  - `static class MemberCardRecoveryCode` : `const int DigitCount = 4` ; `IReadOnlyList<string> Words` (64 mots français de 4 à 6 lettres, sans accent ni ambiguïté, ex. `LUNE`, `PLUME`, `ROSE`, `SABLE`…) ; `string Generate(Func<int, int> nextInt)` → `LUNE-4271` ; `bool TryNormalize(string? input, out string normalized)` — majuscules, retire espaces et tirets puis réinsère **un** tiret avant les 4 chiffres ; refuse tout autre format.
  - `interface IMemberCardTokenService { string Create(Guid cardId, int version); bool TryRead(string? token, out Guid cardId, out int version); }` — format `VPDC1.<b64url(16 octets id + 4 octets version big-endian)>.<b64url(HMAC-SHA256)>`, comparaison en temps constant (`CryptographicOperations.FixedTimeEquals`).
  - `MemberCardTokenOptions { const string SectionName = "MemberCards"; string SigningKey { get; init; } }` — clé ≥ 32 octets une fois décodée (Base64), validée au démarrage (`ValidateOnStart`).
  - `Errors.MemberCard.{NotRecognised(), Revoked(), InvalidRecoveryCode()}` — `NotRecognised` **ne révèle pas** si le compte existe.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class MemberCardRecoveryCodeTests
{
    [Theory]
    [InlineData("LUNE-4271", "LUNE-4271")]
    [InlineData("lune-4271", "LUNE-4271")]
    [InlineData(" lune 4271 ", "LUNE-4271")]
    [InlineData("LUNE4271", "LUNE-4271")]
    [InlineData("lune--4271", "LUNE-4271")]
    public void TryNormalize_AcceptsCommonTypingVariants(string input, string expected)
    {
        MemberCardRecoveryCode.TryNormalize(input, out var normalized).Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("LUNE-42")]
    [InlineData("LUNE-42712")]
    [InlineData("1234-LUNE")]
    [InlineData("LUNÉ-4271")]
    [InlineData("VPDC1.abc.def")]
    public void TryNormalize_RejectsOtherShapes(string? input)
    {
        MemberCardRecoveryCode.TryNormalize(input, out _).Should().BeFalse();
    }

    [Fact]
    public void Generate_ProducesANormalisedCode()
    {
        var code = MemberCardRecoveryCode.Generate(max => max - 1);

        MemberCardRecoveryCode.TryNormalize(code, out var normalized).Should().BeTrue();
        normalized.Should().Be(code);
    }

    [Fact]
    public void Words_AreUniqueUppercaseAsciiWithoutAmbiguousLetters()
    {
        MemberCardRecoveryCode.Words.Should().OnlyHaveUniqueItems().And.HaveCount(64);
        MemberCardRecoveryCode.Words.Should().OnlyContain(word => word.All(c => c is >= 'A' and <= 'Z'));
    }
}

public sealed class MemberCardTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Rotate_IncrementsVersionAndReplacesCode()
    {
        var card = MemberCard.Issue(Guid.NewGuid(), UserId.Create(Guid.NewGuid()), "LUNE-4271", Now);

        card.Rotate("ROSE-1180", Now.AddDays(1));

        card.Version.Should().Be(2);
        card.RecoveryCode.Should().Be("ROSE-1180");
        card.RotatedAt.Should().Be(Now.AddDays(1));
        card.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Issue_RejectsNonNormalisedCode()
    {
        var act = () => MemberCard.Issue(Guid.NewGuid(), UserId.Create(Guid.NewGuid()), "lune 4271", Now);

        act.Should().Throw<ArgumentException>();
    }
}

public sealed class MemberCardTokenServiceTests
{
    private static MemberCardTokenService Create(string key = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=") =>
        new(Options.Create(new MemberCardTokenOptions { SigningKey = key }));

    [Fact]
    public void RoundTrip_ReturnsCardAndVersion()
    {
        var service = Create();
        var cardId = Guid.NewGuid();

        var token = service.Create(cardId, 3);

        token.Should().StartWith("VPDC1.");
        service.TryRead(token, out var readId, out var version).Should().BeTrue();
        readId.Should().Be(cardId);
        version.Should().Be(3);
    }

    [Fact]
    public void TryRead_RejectsTamperedPayload()
    {
        var service = Create();
        var token = service.Create(Guid.NewGuid(), 1);
        var parts = token.Split('.');
        var forged = $"{parts[0]}.{Base64Url.EncodeToString(new byte[20])}.{parts[2]}";

        service.TryRead(forged, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryRead_RejectsTokenSignedWithAnotherKey()
    {
        var token = Create("ZmVkY2JhOTg3NjU0MzIxMGZlZGNiYTk4NzY1NDMyMTA=").Create(Guid.NewGuid(), 1);

        Create().TryRead(token, out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("9782070612758")]
    [InlineData("VPDC1.")]
    [InlineData("VPDC1.a.b.c")]
    [InlineData("VPDC2.AAAA.AAAA")]
    public void TryRead_RejectsMalformedInput(string? token)
    {
        Create().TryRead(token, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Token_DoesNotContainTheCardIdInClearText()
    {
        var cardId = Guid.NewGuid();

        Create().Create(cardId, 1).Should().NotContain(cardId.ToString("N")).And.NotContain(cardId.ToString());
    }
}
```

(`Base64Url` : même utilitaire que `UnsubscribeTokenService` — `System.Buffers.Text.Base64Url`.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Domain.tests --filter "FullyQualifiedName~MemberCard"` puis `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter "FullyQualifiedName~MemberCardTokenServiceTests"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

`MemberCardRecoveryCode.TryNormalize` :

```csharp
public static bool TryNormalize(string? input, out string normalized)
{
    normalized = string.Empty;
    if (string.IsNullOrWhiteSpace(input))
    {
        return false;
    }

    var compact = new string(input.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray())
        .ToUpperInvariant();
    if (compact.Length <= DigitCount)
    {
        return false;
    }

    var word = compact[..^DigitCount];
    var digits = compact[^DigitCount..];
    if (!digits.All(char.IsAsciiDigit) || !Words.Contains(word, StringComparer.Ordinal))
    {
        return false;
    }

    normalized = $"{word}-{digits}";
    return true;
}

public static string Generate(Func<int, int> nextInt)
{
    var word = Words[nextInt(Words.Count)];
    var digits = nextInt(10_000).ToString("D4", CultureInfo.InvariantCulture);
    return $"{word}-{digits}";
}
```

`MemberCardTokenService` :

```csharp
public sealed class MemberCardTokenService(IOptions<MemberCardTokenOptions> options) : IMemberCardTokenService
{
    private const string Prefix = "VPDC1";
    private readonly byte[] _key = Convert.FromBase64String(options.Value.SigningKey);

    public string Create(Guid cardId, int version)
    {
        var payload = new byte[20];
        cardId.TryWriteBytes(payload.AsSpan(0, 16));
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(16, 4), version);
        return string.Join('.', Prefix, Base64Url.EncodeToString(payload), Base64Url.EncodeToString(Sign(payload)));
    }

    public bool TryRead(string? token, out Guid cardId, out int version)
    {
        cardId = Guid.Empty;
        version = 0;
        var parts = token?.Split('.');
        if (parts is not { Length: 3 } || parts[0] != Prefix)
        {
            return false;
        }

        byte[] payload;
        byte[] signature;
        try
        {
            payload = Base64Url.DecodeFromChars(parts[1]);
            signature = Base64Url.DecodeFromChars(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (payload.Length != 20 || !CryptographicOperations.FixedTimeEquals(signature, Sign(payload)))
        {
            return false;
        }

        cardId = new Guid(payload.AsSpan(0, 16));
        version = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(16, 4));
        return true;
    }

    private byte[] Sign(byte[] payload) => HMACSHA256.HashData(_key, payload);
}
```

Le jeton ne contient pas l'id en clair **textuel**, mais son encodage base64 reste déterministe : c'est acceptable car `cardId` est un GUID aléatoire sans lien avec l'identité (pas d'`oid`, pas d'e-mail — `RG-59`).

Enregistrement DI dans `Infrastructure/DependencyInjection.cs`, à côté de `UnsubscribeTokenService` :

```csharp
services.AddOptions<MemberCardTokenOptions>()
    .Bind(configuration.GetSection(MemberCardTokenOptions.SectionName))
    .Validate(o => TryDecodeKey(o.SigningKey, out var key) && key.Length >= 32,
        "MemberCards:SigningKey must be a Base64 key of at least 32 bytes.")
    .ValidateOnStart();
services.AddSingleton<IMemberCardTokenService, MemberCardTokenService>();
```

- [ ] **Step 4: Run tests to verify they pass**

Run: les deux commandes du Step 2.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(card): add member card domain and signed QR token"
```

---

### Task CS-12: Carte — persistance, lecture, rotation et résolution en caisse

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/Configurations/MemberCardConfiguration.cs`
- Modify: `IProjectDbContext.cs`, `ProjectDbContext.cs`
- Create (généré): `…/Migrations/<timestamp>_AddMemberCards.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberCards/Common/MemberCardIssuer.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberCards/Queries/GetMyCard/{GetMyCardQuery,GetMyCardQueryHandler}.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberCards/Commands/RotateMyCard/{RotateMyCardCommand,RotateMyCardCommandHandler}.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberCards/Queries/ResolveMemberCard/{ResolveMemberCardQuery,ResolveMemberCardQueryHandler}.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Application/MemberCards/Common/MemberCardResolver.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Api/Controllers/MemberAccountController.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Api/Controllers/CheckoutPassageController.cs`
- Modify: configuration du rate limiting (chercher `AddRateLimiter` dans `Api`)
- Test: `src/Backend/Vole_Papillon_Damour.Application.tests/MemberCards/MemberCardHandlersTests.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Api.tests/MemberAccount/MemberCardEndpointsTests.cs`

**Interfaces:**
- Consumes: `MemberCard`, `MemberCardRecoveryCode`, `IMemberCardTokenService` (CS-11) ; `MemberIdentityService`.
- Produces:
  - `DbSet<MemberCard> MemberCards` ; table `MemberCards`, index unique `UserId`, index unique filtré `RecoveryCode WHERE RevokedAt IS NULL`.
  - `MemberCardIssuer.GetOrIssueAsync(IProjectDbContext, UserId, DateTime, CancellationToken) : Task<MemberCard>` — tire un code, réessaie sur collision (5 essais max, puis exception).
  - `record MyCardResult(string QrPayload, string RecoveryCode, string DisplayLabel, DateTimeOffset IssuedAt)` ; `DisplayLabel` = prénom du compte, sinon `"Membre"`.
  - `GetMyCardQuery(ExternalId, Email, FirstName, LastName)` → `MyCardResult` (émet la carte au premier appel, `F-10` §5.1).
  - `RotateMyCardCommand(ExternalId, Email, FirstName, LastName)` → `MyCardResult` (nouvelle version + nouveau code : l'ancien QR **et** l'ancien code cessent d'être reconnus).
  - `MemberCardResolver.ResolveAsync(IProjectDbContext, IMemberCardTokenService, string credential, CancellationToken) : Task<ErrorOr<ResolvedMemberCard>>` avec `record ResolvedMemberCard(UserId UserId, string DisplayLabel)` — accepte un QR `VPDC1.…` **ou** un code de secours ; refuse une carte révoquée, une version périmée, un utilisateur anonymisé (`User.AnonymizedAt != null`). Toute erreur renvoie `Errors.MemberCard.NotRecognised()`.
  - `ResolveMemberCardQuery(string Credential)` → `record MemberCardConfirmation(string DisplayLabel)` — **seul** le libellé sort (`F-10` §8.3).
  - Routes :
    - `GET /catalog/me/card` → `200 { qrPayload, recoveryCode, displayLabel, issuedAt }` — `RequireAuthorization()`, en-tête `Cache-Control: no-store`.
    - `POST /catalog/me/card/rotate` → idem.
    - `POST /scan/member-cards/resolve` body `{ credential }` → `200 { displayLabel }` ou `404` (erreur `NotRecognised`) — policy `Caisse`, rate limit `member-card-resolve` : **30 requêtes / minute / utilisateur**, `429` au-delà.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task GetMyCard_IssuesOnceAndReturnsSamePayload()
{
    await using var fixture = await MemberCardFixture.CreateAsync(Now);

    var first = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(), default);
    var second = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(), default);

    second.Value.QrPayload.Should().Be(first.Value.QrPayload);
    second.Value.RecoveryCode.Should().Be(first.Value.RecoveryCode);
    (await fixture.Context.MemberCards.CountAsync()).Should().Be(1);
}

[Fact]
public async Task GetMyCard_PayloadDoesNotContainEmailOrName()
{
    await using var fixture = await MemberCardFixture.CreateAsync(Now);

    var card = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(email: "camille@example.test", firstName: "Camille"), default);

    card.Value.QrPayload.Should().NotContainAny("camille", "Camille", "example.test", fixture.ExternalId);
    card.Value.DisplayLabel.Should().Be("Camille");
}

[Fact]
public async Task Rotate_InvalidatesPreviousQrAndCode()
{
    await using var fixture = await MemberCardFixture.CreateAsync(Now);
    var before = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(), default);

    var after = await fixture.CreateRotateHandler().Handle(fixture.RotateCommand(), default);

    (await fixture.Resolve(before.Value.QrPayload)).FirstError.Code.Should().Be("MemberCard.NotRecognised");
    (await fixture.Resolve(before.Value.RecoveryCode)).FirstError.Code.Should().Be("MemberCard.NotRecognised");
    (await fixture.Resolve(after.Value.QrPayload)).Value.DisplayLabel.Should().Be("Camille");
    (await fixture.Resolve(after.Value.RecoveryCode.ToLowerInvariant().Replace("-", " "))).IsError.Should().BeFalse();
}

[Fact]
public async Task Resolve_WhenUserAnonymised_ReturnsNotRecognised()
{
    await using var fixture = await MemberCardFixture.CreateAsync(Now);
    var card = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(), default);
    await fixture.AnonymiseMemberAsync();

    (await fixture.Resolve(card.Value.QrPayload)).FirstError.Code.Should().Be("MemberCard.NotRecognised");
}

[Fact]
public async Task Resolve_WhenNameMissing_UsesGenericLabel()
{
    await using var fixture = await MemberCardFixture.CreateAsync(Now);
    var card = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(firstName: null), default);

    (await fixture.Resolve(card.Value.QrPayload)).Value.DisplayLabel.Should().Be("Membre");
}

[Fact]
public async Task Issue_WhenRecoveryCodeCollides_RetriesWithAnotherCode()
{
    await using var fixture = await MemberCardFixture.CreateAsync(Now, randomSequence: [0, 0, 0, 0, 1, 1]);
    await fixture.IssueCardForOtherMemberAsync(); // consomme LUNE-0000 (indices 0,0)

    var card = await fixture.CreateGetMyCardHandler().Handle(fixture.MyCardQuery(), default);

    card.Value.RecoveryCode.Should().NotBe(fixture.OtherMemberRecoveryCode);
}
```

(`MemberCardFixture` : même principe que `MemberSelectionFixture`, avec un `IMemberCardTokenService` réel instancié sur une clé de test, et un générateur aléatoire injectable `Func<int,int>` — injecter via une petite interface `IRandomIndexSource` enregistrée en DI avec une implémentation `RandomNumberGenerator.GetInt32`.)

Tests d'API : `POST /scan/member-cards/resolve` sans rôle `Caisse` → `403` ; avec rôle → délègue à MediatR ; la réponse ne contient **que** `displayLabel` (désérialiser en `JsonObject` et vérifier `Count == 1`) ; `GET /catalog/me/card` renvoie `Cache-Control: no-store`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~MemberCardHandlersTests"` et `dotnet test src/Backend/Vole_Papillon_Damour.Api.tests --filter "FullyQualifiedName~MemberCardEndpointsTests"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

`MemberCardResolver` :

```csharp
public static async Task<ErrorOr<ResolvedMemberCard>> ResolveAsync(
    IProjectDbContext dbContext,
    IMemberCardTokenService tokens,
    string credential,
    CancellationToken cancellationToken)
{
    MemberCard? card = null;
    if (tokens.TryRead(credential, out var cardId, out var version))
    {
        card = await dbContext.MemberCards.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == cardId && c.Version == version && c.RevokedAt == null, cancellationToken);
    }
    else if (MemberCardRecoveryCode.TryNormalize(credential, out var code))
    {
        card = await dbContext.MemberCards.AsNoTracking()
            .SingleOrDefaultAsync(c => c.RecoveryCode == code && c.RevokedAt == null, cancellationToken);
    }

    if (card is null)
    {
        return Errors.MemberCard.NotRecognised();
    }

    var user = await dbContext.Users.AsNoTracking()
        .SingleOrDefaultAsync(u => u.Id == card.UserId && u.AnonymizedAt == null, cancellationToken);
    if (user is null)
    {
        return Errors.MemberCard.NotRecognised();
    }

    var label = string.IsNullOrWhiteSpace(user.Name?.FirstName) ? "Membre" : user.Name!.FirstName.Trim();
    return new ResolvedMemberCard(user.Id, label);
}
```

(Vérifier le nom réel de la propriété de prénom dans le value object `Name` et s'y conformer.)

Journalisation (`DT-16`) : `ResolveMemberCardQueryHandler` logge `MemberCardResolved` / `MemberCardNotRecognised` avec **uniquement** la forme du credential (`qr` / `recovery-code` / `other`) — jamais sa valeur.

Rate limiting : ajouter une politique `member-card-resolve` au même endroit que les politiques existantes (fenêtre fixe, 30/min, partition par `oid`), puis `.RequireRateLimiting("member-card-resolve")` sur la route.

Migration :

```bash
dotnet ef migrations add AddMemberCards \
  --project src/Backend/Vole_Papillon_Damour.Infrastructure \
  --startup-project src/Backend/Vole_Papillon_Damour.Api
```

- [ ] **Step 4: Run tests to verify they pass**

Run: les commandes du Step 2, puis `dotnet test src/Backend/Vole_Papillon_Damour.slnx`.
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(card): issue, rotate and resolve member cards"
```

---

### Task CS-13: Secret de signature et onglet « Ma carte »

**Files:**
- Modify: `infra/parameters/main.dev.bicepparam` (et le fichier de prod s'il existe), module Bicep de l'API — paramètre `memberCardSigningKey`, secret Key Vault, variable d'environnement `MemberCards__SigningKey`
- Modify: `.github/workflows/infra-deploy.yml` — `MEMBER_CARD_SIGNING_KEY` **dans les deux étapes** (validation *et* déploiement), comme `BOOK_ALERTS_UNSUBSCRIBE_SIGNING_KEY`
- Modify: `src/Backend/Vole_Papillon_Damour.Api/appsettings.Development.json` — clé de développement factice (Base64 32 octets) clairement marquée
- Modify: `src/Catalog/package.json` — dépendance `qrcode` (+ `@types/qrcode` en dev)
- Create: `src/Catalog/src/app/features/account/card/account-card.component.{ts,html,scss,spec.ts}`
- Modify: `src/Catalog/src/app/core/catalog-member-api.service.ts`, `catalog.models.ts`
- Modify: `src/Catalog/src/app/features/account/catalog-account-page.component.{ts,html}`

**Interfaces:**
- Consumes: `GET /catalog/me/card`, `POST /catalog/me/card/rotate` (CS-12).
- Produces: `CatalogMemberApiService.getCard(token)`, `rotateCard(token)` ; `CatalogMemberCard { qrPayload; recoveryCode; displayLabel; issuedAt }` ; onglet `card`.

Comportement (`F-10` §7.1, §15) :
- La carte se charge **indépendamment** de l'historique (appel propre, aucun `await` sur Mes achats).
- Affiche « Vole Papillon d'Amour », « Ma carte de compte », le QR (SVG généré **dans le navigateur** avec `QRCode.toString(payload, {type: 'svg', errorCorrectionLevel: 'M', margin: 2})`), le libellé, « Code de secours : LUNE-4271 », « Présentez cette carte avant la validation de votre passage. »
- Le QR a `role="img"` et `aria-label="QR code de votre carte de compte. Code de secours : LUNE-4271"` ; le code de secours est un texte sélectionnable (`F-10` §15 accessibilité).
- Pas de rendu du QR côté serveur (SSR) : afficher un espace réservé tant que `isPlatformBrowser` est faux.
- Action « Renouveler ma carte » avec confirmation : « L'ancien QR code et l'ancien code de secours ne fonctionneront plus. »
- Aucune donnée de la carte n'est persistée dans `localStorage` (le cache MSAL suffit pour rouvrir la page).
- Bloc d'information (`F-10` §12.1) : la présentation est facultative, la vente anonyme reste possible, le bénévole ne voit que le prénom.

- [ ] **Step 1: Write the failing tests**

```ts
it('renders an accessible QR and the recovery code', async () => {
  api.getCard.and.returnValue(of({qrPayload: 'VPDC1.AAAA.BBBB', recoveryCode: 'LUNE-4271', displayLabel: 'Camille', issuedAt: '2026-09-23T10:00:00Z'}));
  const fixture = await render();

  const qr = fixture.nativeElement.querySelector('[role="img"]');
  expect(qr.getAttribute('aria-label')).toContain('LUNE-4271');
  expect(qr.querySelector('svg')).not.toBeNull();
  expect(fixture.nativeElement.textContent).toContain('Code de secours : LUNE-4271');
  expect(fixture.nativeElement.textContent).not.toContain('@');
});

it('does not call the purchases endpoint to display the card', async () => {
  await render();

  expect(api.getPurchases).not.toHaveBeenCalled();
});

it('asks confirmation before rotating and shows the new code', async () => { /* clic → confirmation → rotateCard → nouveau code affiché */ });

it('renders a placeholder instead of the QR on the server', async () => { /* PLATFORM_ID 'server' → pas de svg */ });
```

Écrire intégralement les deux derniers `it` sur le modèle des deux premiers.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/Catalog && npm install qrcode && npm install -D @types/qrcode && npm test -- --watch=false --browsers=ChromeHeadless --include src/app/features/account/card`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Import dynamique pour ne pas alourdir le bundle initial : `const {toString} = await import('qrcode');` dans une méthode appelée seulement côté navigateur.

Bicep / CI : reproduire **exactement** le chemin de `bookAlertsUnsubscribeSigningKey` (paramètre `readEnvironmentVariable('MEMBER_CARD_SIGNING_KEY', '')`, secret du Container App API, variable `MemberCards__SigningKey`), **et** le garde `if [ -z "${MEMBER_CARD_SIGNING_KEY:-}" ]` dans le workflow. La PR #207 a montré qu'un oubli dans l'étape de déploiement fait échouer l'infra : vérifier les **deux** étapes.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless && npm run build` ; `az bicep build --file infra/main.bicep` (ou la commande de validation Bicep utilisée par le workflow).
Expected: PASS ; build SSR et Bicep sans erreur.

- [ ] **Step 5: Commit**

```bash
git add infra .github/workflows src/Backend/Vole_Papillon_Damour.Api/appsettings.Development.json src/Catalog
git commit -m "feat(card): show the member card with its QR code"
```

📌 Créer le secret GitHub `MEMBER_CARD_SIGNING_KEY` (environnement DEV puis PROD) : `openssl rand -base64 32`. **Ne jamais** l'écrire dans le dépôt ni dans `NEXT.md` ; consigner seulement « secret créé le … ».

🧪 Sur le téléphone réel : ouvrir Ma carte, luminosité normale, puis tester la lecture par la Scanette (après CS-21) à 20 cm et 40 cm, écran incliné. Noter le taux de lecture au premier essai (`F-10` §18).

🚀 Étape 2 déployable seule : la carte s'affiche, la caisse ne l'utilise pas encore.

---

# Étape 3 — Association de vente

### Task CS-14: Domaine `CheckoutPassage` et lignes d'achat

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Domain/CheckoutPassageAggregate/{CheckoutPassage,CheckoutPassageLine}.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Domain/CheckoutPassageAggregate/ValueObjects/CheckoutPassageStatus.cs`
- Create: `src/Backend/Vole_Papillon_Damour.Domain/Common/Errors/Errors.CheckoutPassage.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Domain/BookMovementAggregate/BookMovement.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Domain.tests/CheckoutPassages/{CheckoutPassageTests,CheckoutPassageLineTests}.cs`

**Interfaces:**
- Produces:
  - `enum CheckoutPassageStatus : byte { PendingAssociation, Associated, Unresolved, Dissociated }`
  - `CheckoutPassage : Entity<Guid>` (Id = `checkoutPassageId` client) : `UserId? UserId`, `CheckoutPassageStatus Status`, `DateTime OccurredAt`, `DateTime CreatedAt`, `UserId? AssociatedByVolunteerId`, `DateTime? AssociatedAt`, `string? UnresolvedReason`, `DateTime? DissociatedAt`, `UserId? DissociatedByUserId`.
    - `static CheckoutPassage OpenFromSale(Guid id, DateTime occurredAt, DateTime createdAt)` → `PendingAssociation`, `UserId = null`.
    - `static CheckoutPassage OpenFromAssociation(Guid id, DateTime occurredAt, DateTime createdAt)` → `PendingAssociation` (même état ; l'association est appliquée ensuite par `Associate` / `MarkUnresolved`).
    - `bool Associate(UserId userId, UserId volunteerId, DateTime at)` — idempotent pour le **même** `userId` (`false`) ; ignoré si `Dissociated` ; un autre `userId` sur un passage déjà `Associated` lève `InvalidOperationException` (`RG-58` : pas de changement silencieux après validation).
    - `void MarkUnresolved(string reason, DateTime at)` — seulement depuis `PendingAssociation`.
    - `void Dissociate(UserId byUserId, DateTime at)` — `UserId = null`, `Status = Dissociated`.
    - `void Anonymize(DateTime at)` — `UserId = null`, `AssociatedByVolunteerId = null`, `Status = Dissociated`, `DissociatedByUserId = null`.
    - `bool IsVisibleToMember => Status == Associated && UserId is not null`.
  - `CheckoutPassageLine : Entity<Guid>` : `Guid CheckoutPassageId`, `BookMovementId? SaleMovementId`, `RareBookId? RareBookId`, `Isbn13? Isbn13`, `string? RequestedIsbn13` (ISBN scanné avant redirection, pour `RG-65`), `int Quantity`, `string Title`, `string? Authors`, `string? Publisher`, `int? PublicationYear`, `string? PhysicalFormat`, `AssoEventsId? AssoEventsId`, `DateTime OccurredAt`, `DateTime? VoidedAt`.
    - `static ForOrdinarySale(Guid id, Guid passageId, BookMovement movement, Book book, string? requestedIsbn13 = null)` — instantané pris sur `book` (titre de repli `"ISBN <isbn>"`), `Quantity = Math.Abs(movement.Quantity)`.
    - `static ForRareSale(Guid id, Guid passageId, RareBook rareBook, DateTime occurredAt, AssoEventsId? fairId)` — `Quantity = 1`, `Authors = AuthorMention`.
    - `bool Void(DateTime at)` — idempotent.
  - `BookMovement.CheckoutPassageId : Guid?` — nouveau paramètre optionnel **en dernière position** de `Create(…, Guid? checkoutPassageId = null)` ; aucun appel existant ne change.
  - `Errors.CheckoutPassage.{NotFound(Guid), AlreadyAssociatedToAnotherMember(), InvalidId()}`.

- [ ] **Step 1: Write the failing tests**

```csharp
public sealed class CheckoutPassageTests
{
    private static readonly DateTime Now = new(2026, 3, 14, 15, 0, 0, DateTimeKind.Utc);
    private static readonly UserId Camille = UserId.Create(Guid.NewGuid());
    private static readonly UserId Volunteer = UserId.Create(Guid.NewGuid());

    [Fact]
    public void Associate_IsIdempotentForSameMember()
    {
        var passage = CheckoutPassage.OpenFromSale(Guid.NewGuid(), Now, Now);

        passage.Associate(Camille, Volunteer, Now).Should().BeTrue();
        passage.Associate(Camille, Volunteer, Now.AddMinutes(5)).Should().BeFalse();

        passage.Status.Should().Be(CheckoutPassageStatus.Associated);
        passage.AssociatedAt.Should().Be(Now);
        passage.IsVisibleToMember.Should().BeTrue();
    }

    [Fact]
    public void Associate_ToAnotherMemberAfterAssociation_Throws()
    {
        var passage = CheckoutPassage.OpenFromSale(Guid.NewGuid(), Now, Now);
        passage.Associate(Camille, Volunteer, Now);

        var act = () => passage.Associate(UserId.Create(Guid.NewGuid()), Volunteer, Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Associate_AfterDissociation_IsIgnored()
    {
        var passage = CheckoutPassage.OpenFromSale(Guid.NewGuid(), Now, Now);
        passage.Associate(Camille, Volunteer, Now);
        passage.Dissociate(UserId.Create(Guid.NewGuid()), Now.AddDays(1));

        passage.Associate(Camille, Volunteer, Now.AddDays(2)).Should().BeFalse();

        passage.UserId.Should().BeNull();
        passage.IsVisibleToMember.Should().BeFalse();
    }

    [Fact]
    public void MarkUnresolved_KeepsPassageAnonymous()
    {
        var passage = CheckoutPassage.OpenFromAssociation(Guid.NewGuid(), Now, Now);

        passage.MarkUnresolved("card-not-recognised", Now);

        passage.Status.Should().Be(CheckoutPassageStatus.Unresolved);
        passage.UserId.Should().BeNull();
    }

    [Fact]
    public void Anonymize_RemovesEveryPersonalLink()
    {
        var passage = CheckoutPassage.OpenFromSale(Guid.NewGuid(), Now, Now);
        passage.Associate(Camille, Volunteer, Now);

        passage.Anonymize(Now.AddDays(10));

        passage.UserId.Should().BeNull();
        passage.AssociatedByVolunteerId.Should().BeNull();
        passage.Status.Should().Be(CheckoutPassageStatus.Dissociated);
    }
}

public sealed class CheckoutPassageLineTests
{
    [Fact]
    public void ForOrdinarySale_SnapshotsBookMetadataAndPositiveQuantity()
    {
        var book = TestBooks.WithMetadata("9782070612758", "Le Petit Prince", "Antoine de Saint-Exupéry", "Gallimard", 1999, "Poche");
        var movement = TestMovements.Sale(book.Id, quantity: -2);

        var line = CheckoutPassageLine.ForOrdinarySale(Guid.NewGuid(), Guid.NewGuid(), movement, book);

        line.Title.Should().Be("Le Petit Prince");
        line.Publisher.Should().Be("Gallimard");
        line.Quantity.Should().Be(2);
        line.SaleMovementId.Should().Be(movement.Id);
    }

    [Fact]
    public void ForOrdinarySale_WithoutTitle_UsesIsbnFallback()
    {
        var book = Book.Create(Isbn("9782070612758"), DateTime.UtcNow);
        var line = CheckoutPassageLine.ForOrdinarySale(Guid.NewGuid(), Guid.NewGuid(), TestMovements.Sale(book.Id, -1), book);

        line.Title.Should().Be("ISBN 9782070612758");
    }

    [Fact]
    public void Void_IsIdempotent()
    {
        var line = CheckoutPassageLine.ForOrdinarySale(Guid.NewGuid(), Guid.NewGuid(), TestMovements.Sale(Isbn("9782070612758"), -1), Book.Create(Isbn("9782070612758"), DateTime.UtcNow));
        var at = new DateTime(2026, 3, 14, 16, 0, 0, DateTimeKind.Utc);

        line.Void(at).Should().BeTrue();
        line.Void(at.AddHours(1)).Should().BeFalse();
        line.VoidedAt.Should().Be(at);
    }
}
```

(`TestBooks` / `TestMovements` : helpers de test à créer dans `Domain.tests/CheckoutPassages/`. `TestBooks.WithMetadata` applique les métadonnées via la méthode publique existante de `Book` qui les renseigne — chercher `ApplyMetadata`/`UpdateMetadata` dans `Book.cs` ; si aucune n'est accessible sans patch, utiliser celle qu'emploie `UpdateBookMetadataCommandHandler`.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Domain.tests --filter "FullyQualifiedName~CheckoutPassage"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Implémenter les méthodes selon les contrats ci-dessus. `Associate` :

```csharp
public bool Associate(UserId userId, UserId volunteerId, DateTime at)
{
    ArgumentNullException.ThrowIfNull(userId);
    if (Status == CheckoutPassageStatus.Dissociated)
    {
        return false;
    }

    if (Status == CheckoutPassageStatus.Associated)
    {
        return UserId == userId
            ? false
            : throw new InvalidOperationException("A validated passage cannot be moved to another member.");
    }

    UserId = userId;
    AssociatedByVolunteerId = volunteerId;
    AssociatedAt = DomainTime.RequireUtc(at, nameof(at));
    UnresolvedReason = null;
    Status = CheckoutPassageStatus.Associated;
    return true;
}
```

Note : un passage `Unresolved` peut encore être associé (cas d'un rejeu après correction du jeton) — c'est voulu et couvert en CS-17.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Domain.tests`
Expected: PASS, tests existants de `BookMovement` inchangés.

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(passage): add checkout passage and purchase line domain"
```

---

### Task CS-15: Persistance des passages

**Files:**
- Create: `…/Configurations/{CheckoutPassageConfiguration,CheckoutPassageLineConfiguration}.cs`
- Modify: `…/Configurations/BookMovementConfiguration.cs` (colonne + index filtré `CheckoutPassageId`)
- Modify: `IProjectDbContext.cs`, `ProjectDbContext.cs`
- Create (généré): `…/Migrations/<timestamp>_AddCheckoutPassages.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/CheckoutPassages/CheckoutPassageConfigurationTests.cs`

**Interfaces:**
- Produces: `DbSet<CheckoutPassage> CheckoutPassages`, `DbSet<CheckoutPassageLine> CheckoutPassageLines` ; contraintes : index unique filtré `CheckoutPassageLines(SaleMovementId) WHERE SaleMovementId IS NOT NULL`, index unique filtré `(CheckoutPassageId, RareBookId) WHERE RareBookId IS NOT NULL`, index `CheckoutPassages(UserId, OccurredAt DESC)`, FK `CheckoutPassageLines → CheckoutPassages` en cascade, FK `CheckoutPassages.UserId → Users` **`SetNull`** (jamais de cascade : les lignes servent l'audit), `Title` max 500, `Authors` max 500, `RequestedIsbn13` max 13, `Publisher` max 200, `PhysicalFormat` max 100, `UnresolvedReason` max 64.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task SaveChanges_RejectsTwoLinesForSameSaleMovement() { /* deux ForOrdinarySale même movement → DbUpdateException */ }

[Fact]
public async Task SaveChanges_RejectsSameRareBookTwiceInPassage() { /* deux ForRareSale même passage + rare → DbUpdateException */ }

[Fact]
public async Task DeletingUser_KeepsPassageAndLines() { /* Users.Remove → passage.UserId null, lignes présentes */ }
```

Écrire chaque test complètement avec la fabrique SQLite du projet (voir CS-2).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter "FullyQualifiedName~CheckoutPassageConfigurationTests"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Configurations EF selon les contraintes ci-dessus, puis :

```bash
dotnet ef migrations add AddCheckoutPassages \
  --project src/Backend/Vole_Papillon_Damour.Infrastructure \
  --startup-project src/Backend/Vole_Papillon_Damour.Api
```

Relire : deux tables, une colonne nullable `BookMovements.CheckoutPassageId`, un index filtré, **aucune** modification de données existantes.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(passage): persist checkout passages and purchase lines"
```

📌 Migration `AddCheckoutPassages` à appliquer.

---

### Task CS-16: `CheckoutPassageRecorder` — seul point d'écriture des lignes

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Application/CheckoutPassages/Common/CheckoutPassageRecorder.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Application.tests/CheckoutPassages/CheckoutPassageRecorderTests.cs`
- Test fixture: `src/Backend/Vole_Papillon_Damour.Application.tests/CheckoutPassages/CheckoutPassageFixture.cs`

**Interfaces:**
- Consumes: `CheckoutPassage`, `CheckoutPassageLine` (CS-14) ; `MemberSelectionItem.MarkPurchased` (CS-1).
- Produces (classe `sealed`, enregistrée `Scoped`) :
  - `Task RecordOrdinarySaleAsync(Guid passageId, BookMovement movement, Book book, string requestedIsbn, CancellationToken ct)`
  - `Task RecordRareSaleAsync(Guid passageId, RareBook rareBook, DateTime occurredAt, AssoEventsId? fairId, CancellationToken ct)`
  - `Task<CheckoutPassage> GetOrOpenAsync(Guid passageId, DateTime occurredAt, CancellationToken ct)`
  - `Task MarkSelectionPurchasedAsync(CheckoutPassage passage, CancellationToken ct)` — pour chaque ligne **non annulée** du passage, si `passage.IsVisibleToMember` : passe à ACHETÉ les items de sélection du membre dont l'ISBN est **exactement** l'ISBN de la ligne, ou l'ISBN demandé à l'origine (`requestedIsbn`, avant redirection), ou dont `RareBookId` est celui de la ligne. Aucune correspondance par œuvre ni par titre.
  - Le recorder **n'appelle pas** `SaveChangesAsync` : l'appelant garde sa transaction.

Pour retrouver l'ISBN demandé au moment d'une association tardive, le recorder passe `requestedIsbn` (normalisé par `Isbn13.TryCreate`) à `CheckoutPassageLine.ForOrdinarySale`, qui le stocke dans `RequestedIsbn13` (colonne créée en CS-15).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task RecordOrdinarySale_OpensPassageOnceAndAddsOneLinePerMovement()
{
    await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
    var passageId = Guid.NewGuid();
    var (book, movement1) = await fixture.AddSaleAsync("9782070612758", passageId);
    var (_, movement2) = await fixture.AddSaleAsync("9782070612758", passageId);
    var recorder = fixture.CreateRecorder();

    await recorder.RecordOrdinarySaleAsync(passageId, movement1, book, "9782070612758", default);
    await recorder.RecordOrdinarySaleAsync(passageId, movement2, book, "9782070612758", default);
    await recorder.RecordOrdinarySaleAsync(passageId, movement1, book, "9782070612758", default); // rejeu
    await fixture.Context.SaveChangesAsync();

    (await fixture.Context.CheckoutPassages.CountAsync()).Should().Be(1);
    (await fixture.Context.CheckoutPassageLines.CountAsync()).Should().Be(2);
}

[Fact]
public async Task MarkSelectionPurchased_MatchesExactIsbnOnly()
{
    await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
    var member = await fixture.AddMemberAsync();
    var sameEdition = await fixture.AddSelectionAsync(member, "9782070612758");
    var otherEditionSameWork = await fixture.AddSelectionAsync(member, "9782070584628");
    var passage = await fixture.AddAssociatedPassageWithSaleAsync(member, "9782070612758");

    await fixture.CreateRecorder().MarkSelectionPurchasedAsync(passage, default);
    await fixture.Context.SaveChangesAsync();

    (await fixture.Reload(sameEdition)).Status.Should().Be(MemberSelectionStatus.Purchased);
    (await fixture.Reload(otherEditionSameWork)).Status.Should().Be(MemberSelectionStatus.ToTake);
}

[Fact]
public async Task MarkSelectionPurchased_MarksSelectionOnRequestedAndCanonicalIsbn()
{
    await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
    var member = await fixture.AddMemberAsync();
    var onOldIsbn = await fixture.AddSelectionAsync(member, "9782070612758");
    await fixture.RedirectBookAsync(from: "9782070612758", to: "9782070584628");
    var passage = await fixture.AddAssociatedPassageWithSaleAsync(member, requestedIsbn: "9782070612758");

    await fixture.CreateRecorder().MarkSelectionPurchasedAsync(passage, default);
    await fixture.Context.SaveChangesAsync();

    (await fixture.Reload(onOldIsbn)).Status.Should().Be(MemberSelectionStatus.Purchased);
}

[Fact]
public async Task MarkSelectionPurchased_IgnoresVoidedLinesAndOtherMembers()
{
    await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
    var member = await fixture.AddMemberAsync();
    var other = await fixture.AddMemberAsync();
    var otherSelection = await fixture.AddSelectionAsync(other, "9782070612758");
    var voidedSelection = await fixture.AddSelectionAsync(member, "9782253006329");
    var passage = await fixture.AddAssociatedPassageWithSaleAsync(member, "9782070612758");
    await fixture.AddVoidedLineAsync(passage, "9782253006329");

    await fixture.CreateRecorder().MarkSelectionPurchasedAsync(passage, default);
    await fixture.Context.SaveChangesAsync();

    (await fixture.Reload(otherSelection)).Status.Should().Be(MemberSelectionStatus.ToTake);
    (await fixture.Reload(voidedSelection)).Status.Should().Be(MemberSelectionStatus.ToTake);
}

[Fact]
public async Task MarkSelectionPurchased_WhenPassageNotAssociated_DoesNothing() { /* passage PendingAssociation → aucun changement */ }

[Fact]
public async Task RecordRareSale_IsIdempotentPerRareBook() { /* 2 appels même rare → 1 ligne */ }
```

Écrire les deux derniers tests complètement sur le modèle des précédents.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~CheckoutPassageRecorderTests"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

`GetOrOpenAsync` cherche d'abord dans le `ChangeTracker` (`dbContext.CheckoutPassages.Local`) puis en base, sinon `OpenFromSale` + `Add`. Idempotence d'une ligne ordinaire : `AnyAsync(line => line.SaleMovementId == movement.Id)` **plus** vérification dans `.Local`. `MarkSelectionPurchasedAsync` construit l'ensemble des ISBN (canonique + demandé) et des ids rares des lignes non annulées, puis une **seule** requête `MemberSelectionItems.Where(item => item.UserId == passage.UserId && (isbns.Contains(item.Isbn13) || rareIds.Contains(item.RareBookId)))` et appelle `MarkPurchased(line.OccurredAt)` (date de la ligne correspondante).

Log (`DT-16`) : `CheckoutPassageLineRecorded { PassageId, Kind, AlreadyRecorded }`, `SelectionMarkedPurchased { PassageId, Count }` — jamais d'`UserId` en clair dans un log accessible aux bénévoles.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~CheckoutPassageRecorderTests"`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(passage): record purchase lines and mark exact selection matches"
```

---

### Task CS-17: Association d'un passage et propagation dans les ventes

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Application/CheckoutPassages/Commands/AssociateCheckoutPassage/{AssociateCheckoutPassageCommand,AssociateCheckoutPassageCommandHandler,AssociateCheckoutPassageCommandValidator}.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Application/Books/Commands/RegisterSale/{RegisterSaleCommand,RegisterSaleCommandHandler}.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Application/RareBooks/Commands/MarkRareBookSold/{MarkRareBookSoldCommand,MarkRareBookSoldCommandHandler}.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Application.tests/CheckoutPassages/AssociateCheckoutPassageCommandHandlerTests.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Application.tests/Books/Commands/RegisterSale/RegisterSaleCommandHandlerTests.cs` (ajouts)

**Interfaces:**
- Consumes: `CheckoutPassageRecorder` (CS-16), `MemberCardResolver` (CS-12).
- Produces:
  - `record AssociateCheckoutPassageCommand(Guid CheckoutPassageId, string Credential, DateTime OccurredAt, UserId VolunteerId) : IRequest<ErrorOr<CheckoutPassageAssociationResult>>`
  - `record CheckoutPassageAssociationResult(Guid CheckoutPassageId, string Status, string? DisplayLabel, bool AlreadyProcessed)` — `Status` ∈ `"Associated"`, `"Unresolved"`, `"Dissociated"`.
  - `RegisterSaleCommand` gagne `Guid? CheckoutPassageId = null` **en dernier paramètre** ; `MarkRareBookSoldCommand` gagne `Guid? CheckoutPassageId = null` en dernier.

Règles :
- Carte reconnue → `Associate` puis `MarkSelectionPurchasedAsync` ; lignes déjà présentes (ventes arrivées avant) incluses.
- Carte non reconnue / révoquée / compte anonymisé → `MarkUnresolved("card-not-recognised")`, **pas d'erreur HTTP** : la réponse est `200` avec `Status = "Unresolved"` pour que la Scanette sorte l'entrée de sa file (`F-10` §10 « le serveur ne retrouve jamais le jeton »).
- Passage déjà `Associated` au même membre → `AlreadyProcessed = true`.
- Passage déjà `Associated` à **un autre** membre (même `checkoutPassageId` réutilisé) → `Errors.CheckoutPassage.AlreadyAssociatedToAnotherMember()` (`409`) ; aucune modification.
- `RegisterSale` avec `CheckoutPassageId` : écrit `BookMovement.CheckoutPassageId`, appelle `RecordOrdinarySaleAsync` dans **la même transaction**, puis `MarkSelectionPurchasedAsync` si le passage est déjà associé. Sur le chemin idempotent (`existingMovement`), ne rien réécrire.
- `RegisterSale` **sans** `CheckoutPassageId` : comportement strictement inchangé (tous les tests existants passent sans modification).
- `MarkRareBookSold` avec `CheckoutPassageId` : `RecordRareSaleAsync` seulement si `changed` **ou** si la fiche est déjà vendue dans ce même passage (rejeu).
- Log d'audit `CheckoutPassageAssociated { PassageId, Status, VolunteerId }` (`F-10` §15 audit), sans e-mail ni credential.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Handle_WhenAssociationArrivesBeforeSales_LinesJoinTheAssociatedPassage()
{
    await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
    var member = await fixture.AddMemberWithCardAsync("Camille");
    var passageId = Guid.NewGuid();

    var association = await fixture.CreateAssociateHandler().Handle(
        new AssociateCheckoutPassageCommand(passageId, member.QrPayload, Now, fixture.Volunteer), default);
    await fixture.RegisterSaleAsync("9782070612758", passageId);
    await fixture.RegisterSaleAsync("9782203001015", passageId);

    association.Value.Status.Should().Be("Associated");
    association.Value.DisplayLabel.Should().Be("Camille");
    var passage = await fixture.Context.CheckoutPassages.SingleAsync();
    passage.UserId.Should().Be(member.UserId);
    (await fixture.Context.CheckoutPassageLines.CountAsync(l => l.CheckoutPassageId == passageId)).Should().Be(2);
}

[Fact]
public async Task Handle_WhenSalesArriveBeforeAssociation_SameResult()
{
    await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
    var member = await fixture.AddMemberWithCardAsync("Camille");
    var passageId = Guid.NewGuid();
    await fixture.RegisterSaleAsync("9782070612758", passageId);
    await fixture.RegisterSaleAsync("9782203001015", passageId);

    await fixture.CreateAssociateHandler().Handle(
        new AssociateCheckoutPassageCommand(passageId, member.QrPayload, Now, fixture.Volunteer), default);

    (await fixture.Context.CheckoutPassages.SingleAsync()).Status.Should().Be(CheckoutPassageStatus.Associated);
    (await fixture.Context.CheckoutPassageLines.CountAsync()).Should().Be(2);
}

[Fact]
public async Task Handle_ReplayedTwice_ProducesOnePassageAndSameLines()
{
    await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
    var member = await fixture.AddMemberWithCardAsync("Camille");
    var passageId = Guid.NewGuid();
    var gesture = Guid.NewGuid();
    var command = new AssociateCheckoutPassageCommand(passageId, member.QrPayload, Now, fixture.Volunteer);

    await fixture.CreateAssociateHandler().Handle(command, default);
    await fixture.RegisterSaleAsync("9782070612758", passageId, gesture);
    await fixture.RegisterSaleAsync("9782070612758", passageId, gesture);
    var replay = await fixture.CreateAssociateHandler().Handle(command, default);

    replay.Value.AlreadyProcessed.Should().BeTrue();
    (await fixture.Context.CheckoutPassages.CountAsync()).Should().Be(1);
    (await fixture.Context.CheckoutPassageLines.CountAsync()).Should().Be(1);
    (await fixture.Context.BookMovements.CountAsync()).Should().Be(1);
}

[Fact]
public async Task Handle_WithRevokedToken_RecordsUnresolvedAndKeepsSalesAnonymous()
{
    await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
    var member = await fixture.AddMemberWithCardAsync("Camille");
    var oldPayload = member.QrPayload;
    await fixture.RotateCardAsync(member);
    var passageId = Guid.NewGuid();
    await fixture.RegisterSaleAsync("9782070612758", passageId);

    var result = await fixture.CreateAssociateHandler().Handle(
        new AssociateCheckoutPassageCommand(passageId, oldPayload, Now, fixture.Volunteer), default);

    result.IsError.Should().BeFalse();
    result.Value.Status.Should().Be("Unresolved");
    result.Value.DisplayLabel.Should().BeNull();
    (await fixture.Context.CheckoutPassages.SingleAsync()).UserId.Should().BeNull();
}

[Fact]
public async Task Handle_WhenPassageAlreadyBelongsToAnotherMember_ReturnsConflictAndChangesNothing()
{
    await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
    var camille = await fixture.AddMemberWithCardAsync("Camille");
    var dominique = await fixture.AddMemberWithCardAsync("Dominique");
    var passageId = Guid.NewGuid();
    await fixture.CreateAssociateHandler().Handle(new(passageId, camille.QrPayload, Now, fixture.Volunteer), default);

    var result = await fixture.CreateAssociateHandler().Handle(new(passageId, dominique.QrPayload, Now, fixture.Volunteer), default);

    result.FirstError.Code.Should().Be("CheckoutPassage.AlreadyAssociatedToAnotherMember");
    (await fixture.Context.CheckoutPassages.SingleAsync()).UserId.Should().Be(camille.UserId);
}

[Fact]
public async Task Handle_MarksMatchingSelectionPurchased()
{
    await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
    var member = await fixture.AddMemberWithCardAsync("Camille");
    var selected = await fixture.AddSelectionAsync(member.User, "9782070612758");
    var otherEdition = await fixture.AddSelectionAsync(member.User, "9782070584628");
    var passageId = Guid.NewGuid();
    await fixture.RegisterSaleAsync("9782070612758", passageId);

    await fixture.CreateAssociateHandler().Handle(new(passageId, member.RecoveryCode, Now, fixture.Volunteer), default);

    (await fixture.Reload(selected)).Status.Should().Be(MemberSelectionStatus.Purchased);
    (await fixture.Reload(otherEdition)).Status.Should().Be(MemberSelectionStatus.ToTake);
}
```

Dans `RegisterSaleCommandHandlerTests`, ajouter :

```csharp
[Fact]
public async Task Handle_WithoutPassage_DoesNotCreatePassage()
{
    await using var fixture = await ScanBookFixture.CreateAsync();
    var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);

    await fixture.CreateRegisterSaleHandler().Handle(CreateCommand(book.Isbn13.Value), CancellationToken.None);

    (await fixture.Context.CheckoutPassages.CountAsync()).Should().Be(0);
    (await fixture.Context.BookMovements.SingleAsync()).CheckoutPassageId.Should().BeNull();
}
```

(Ajouter `CheckoutPassages`, `CheckoutPassageLines`, `MemberSelectionItems`, `MemberCards` au `ScanBookTestDbContext` et passer un `CheckoutPassageRecorder` réel à `CreateRegisterSaleHandler`.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~AssociateCheckoutPassage|FullyQualifiedName~RegisterSaleCommandHandlerTests"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Handler d'association (dans une transaction, comme `RegisterSale`) :

```csharp
public async Task<ErrorOr<CheckoutPassageAssociationResult>> Handle(
    AssociateCheckoutPassageCommand command,
    CancellationToken cancellationToken)
{
    if (command.CheckoutPassageId == Guid.Empty)
    {
        return Errors.CheckoutPassage.InvalidId();
    }

    var now = dateTimeProvider.UtcNow;
    var occurredAt = command.OccurredAt.Kind == DateTimeKind.Utc && command.OccurredAt <= now
        ? command.OccurredAt
        : now;

    await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
    var passage = await recorder.GetOrOpenAsync(command.CheckoutPassageId, occurredAt, cancellationToken);
    var resolved = await MemberCardResolver.ResolveAsync(dbContext, tokens, command.Credential, cancellationToken);

    if (resolved.IsError)
    {
        if (passage.Status == CheckoutPassageStatus.PendingAssociation)
        {
            passage.MarkUnresolved("card-not-recognised", now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("CheckoutPassageAssociated {PassageId} {Status}", passage.Id, passage.Status);
        return new CheckoutPassageAssociationResult(passage.Id, passage.Status.ToString(), null, false);
    }

    bool changed;
    try
    {
        changed = passage.Associate(resolved.Value.UserId, command.VolunteerId, now);
    }
    catch (InvalidOperationException)
    {
        return Errors.CheckoutPassage.AlreadyAssociatedToAnotherMember();
    }

    await recorder.MarkSelectionPurchasedAsync(passage, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    logger.LogInformation("CheckoutPassageAssociated {PassageId} {Status}", passage.Id, passage.Status);

    return new CheckoutPassageAssociationResult(
        passage.Id,
        passage.Status.ToString(),
        passage.IsVisibleToMember ? resolved.Value.DisplayLabel : null,
        AlreadyProcessed: !changed);
}
```

`RegisterSaleCommandHandler` — dans la branche de création, après `dbContext.BookMovements.Add(movement)` :

```csharp
if (command.CheckoutPassageId is { } passageId && passageId != Guid.Empty)
{
    await checkoutPassageRecorder.RecordOrdinarySaleAsync(passageId, movement, book, command.Isbn, cancellationToken);
    var passage = await checkoutPassageRecorder.GetOrOpenAsync(passageId, occurredAt, cancellationToken);
    await checkoutPassageRecorder.MarkSelectionPurchasedAsync(passage, cancellationToken);
}
```

et passer `command.CheckoutPassageId` au dernier argument de `BookMovement.Create`.

`MarkRareBookSoldCommandHandler` — après le `MarkSold` réussi, même logique avec `RecordRareSaleAsync(passageId, rareBook, command.OccurredAt, command.AssoEventsId, ct)`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests`
Expected: PASS, y compris **tous** les tests préexistants de `RegisterSale` et `MarkRareBookSold`.

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(passage): associate a checkout passage from a member card"
```

---

### Task CS-18: Annulation d'une vente associée

**Files:**
- Modify: `src/Backend/Vole_Papillon_Damour.Application/Books/Commands/VoidSale/VoidSaleCommandHandler.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Application/RareBooks/Commands/RestoreRareBookAvailability/RestoreRareBookAvailabilityCommandHandler.cs` (nom exact à vérifier dans le dossier)
- Test: ajouts dans `RegisterSaleCommandHandlerTests.cs` (qui teste déjà `VoidSale`) et dans les tests de restauration rare

**Interfaces:**
- Consumes: `CheckoutPassageLine.Void` (CS-14).
- Produces: une correction (`VoidSale`) ou une restauration rare marque la ligne correspondante `VoidedAt` ; la ligne de sélection passée à ACHETÉ **n'est pas** remise à À PRENDRE automatiquement (`RG-65` : aucun changement automatique autre que vers ACHETÉ) ; le mouvement inverse append-only reste celui d'aujourd'hui (`RG-64`).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task VoidSale_MarksAssociatedLineAsVoided()
{
    await using var fixture = await ScanBookFixture.CreateAsync();
    var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
    var passageId = Guid.NewGuid();
    var sale = await fixture.CreateRegisterSaleHandler().Handle(CreateCommand(book.Isbn13.Value) with { CheckoutPassageId = passageId }, default);

    await fixture.CreateVoidSaleHandler().Handle(CreateVoidCommand(sale.Value.SaleMovementId), default);

    (await fixture.Context.CheckoutPassageLines.SingleAsync()).VoidedAt.Should().NotBeNull();
    (await fixture.Context.BookMovements.CountAsync(m => m.Type == BookMovementType.Correction)).Should().Be(1);
}

[Fact]
public async Task VoidSale_WithoutPassage_BehavesAsBefore() { /* aucune ligne, correction créée */ }

[Fact]
public async Task RestoreRareBook_MarksRareLineAsVoided() { /* vente rare associée puis restauration → VoidedAt */ }
```

Écrire les deux derniers tests complètement (même structure).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~VoidSale|FullyQualifiedName~RestoreRareBook"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Dans chaque handler, après la création de la correction / la restauration, dans la même transaction :

```csharp
var line = await dbContext.CheckoutPassageLines
    .SingleOrDefaultAsync(candidate => candidate.SaleMovementId == saleMovement.Id, cancellationToken);
line?.Void(receivedAt);
```

(Pour le rare : `candidate.RareBookId == rareBook.Id && candidate.VoidedAt == null`, la plus récente.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(passage): mark purchase lines as cancelled on sale correction"
```

> Limite connue, **hors plan** : `VoidSaleCommand` n'est exposée par aucune route aujourd'hui (seule l'annulation locale avant synchronisation existe pour les ventes ordinaires). Le branchement est prêt ; l'exposition d'une annulation après synchronisation reste une décision de caisse séparée.

---

### Task CS-19: Routes API de caisse

**Files:**
- Modify: `src/Backend/Vole_Papillon_Damour.Api/Controllers/CheckoutPassageController.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Api/Controllers/BookController.cs` — `RegisterSaleRequest.CheckoutPassageId?`
- Modify: `src/Backend/Vole_Papillon_Damour.Api/Controllers/RareBookController.cs` — `MarkRareBookSoldRequest.CheckoutPassageId?`
- Modify: les records de requête dans `Contracts`
- Test: `src/Backend/Vole_Papillon_Damour.Api.tests/CheckoutPassages/CheckoutPassageEndpointsTests.cs`

**Interfaces:**
- Produces :
  - `PUT /scan/passages/{checkoutPassageId:guid}/member` body `{ credential: string, occurredAt: DateTimeOffset }` → `200 { checkoutPassageId, status, displayLabel, alreadyProcessed }` ; `409` si autre membre ; policy `Caisse`.
  - `POST /scan/sales` et `POST /rare-books/cash/{id}/sold` acceptent `checkoutPassageId` optionnel ; un GUID vide est traité comme absent.
  - Une Scanette qui n'envoie pas le champ obtient exactement la réponse actuelle (`F-10` §15 compatibilité).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task PutMember_WithoutCaisseRole_Returns403() { … }

[Fact]
public async Task PutMember_ForwardsPassageCredentialAndVolunteer() { … }

[Fact]
public async Task PostSale_WithoutPassageField_SendsNullPassage()
{
    // corps JSON historique : { isbn, quantity, occurredAt, clientGestureId }
    // → RegisterSaleCommand.CheckoutPassageId == null
}

[Fact]
public async Task PostSale_WithEmptyGuidPassage_SendsNullPassage() { … }
```

Écrire chaque test complètement selon le patron d'API retenu en CS-6.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Api.tests --filter "FullyQualifiedName~CheckoutPassageEndpointsTests"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Même forme que `/scan/sales`. `TryGetUserId(principal, out var volunteerId)` pour le bénévole. `occurredAt.UtcDateTime`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Api.tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(passage): expose checkout passage association for the cash desk"
```

🚀 Backend de l'étape 3 déployable **avant** la Scanette : sans `checkoutPassageId`, rien ne change.

---

### Task CS-20: Scanette — file d'association hors ligne et rejeu idempotent

**Files:**
- Modify: `src/Scan/src/app/offline/scan-offline.model.ts`
- Modify: `src/Scan/src/app/offline/scan-local-store.service.ts`
- Modify: `src/Scan/src/app/offline/scan-workflow.service.ts` (`recordCashSales`)
- Modify: `src/Scan/src/app/offline/scan-rare-cash.service.ts` (`recordSales`)
- Modify: `src/Scan/src/app/offline/scan-api.service.ts`
- Modify: `src/Scan/src/app/offline/scan-sync.service.ts`
- Create: `src/Scan/src/app/offline/scan-member-card.ts`
- Test: `scan-local-store.service.spec.ts`, `scan-sync.service.spec.ts`, `scan-workflow.service.spec.ts`, `scan-member-card.spec.ts`

**Interfaces:**
- Produces :
  - `scanDatabaseVersion = 6` ; `scanStoreNames.passageAssociations = 'passageAssociations'` (keyPath `checkoutPassageId`).
  - `type ScanMemberCredential = {kind: 'qr' | 'recovery-code'; value: string}`
  - `type ScanPassageAssociationStatus = 'Pending' | 'Accepted' | 'Unresolved' | 'Quarantined'`
  - `interface ScanPassageAssociationEntry { checkoutPassageId: string; credential: ScanMemberCredential; occurredAt: string; createdAt: string; status: ScanPassageAssociationStatus; attemptCount: number; lastAttemptAt: string | null; lastError: string | null; lastFailureKind?: ScanFailureKind | null }` — **aucun** libellé, e-mail ni historique persisté (`F-10` §11.2).
  - `ScanSaleOutboxEntry.checkoutPassageId?: string | null` ; `ScanRareSaleOutboxEntry.checkoutPassageId?: string | null`.
  - `scan-member-card.ts` : `parseMemberCredential(raw: string): ScanMemberCredential | null` — `VPDC1.` → `qr` ; code de secours reconnu par la regex `/^[A-Za-z]{3,6}[\s-]*\d{4}$/` après `trim()` → `recovery-code` (normalisé en majuscules `MOT-1234`) ; ISBN ou autre → `null`.
  - `ScanWorkflowService.recordCashSales(isbns13, occurredAt?, checkoutPassageId?: string | null)`
  - `ScanRareCashService.recordSales(rareBookIds, occurredAt, session, checkoutPassageId?: string | null)`
  - `ScanLocalStoreService` : `putPassageAssociation(entry)`, `listPassageAssociations()`, `markPassageAssociationAttempt(id, patch)`, `deletePassageAssociation(id)`.
  - `ScanApiService.associatePassage(checkoutPassageId, {credential, occurredAt}) : Observable<ScanPassageAssociationResponse>` ; `registerSale` et la vente rare transmettent `checkoutPassageId` s'il est présent.
  - Ordre de `ScanSyncService` : sorties de tri → **ventes** → **ventes rares** → **associations**. Une association `Accepted` ou `Unresolved` est **supprimée** de la file après succès ; `409` → `Quarantined` (visible dans le diagnostic existant) ; erreurs réseau → nouvel essai selon `isRetryDue` ; une erreur d'association n'interrompt jamais la synchronisation des ventes.

Migration IndexedDB : dans `onupgradeneeded`, `if (!database.objectStoreNames.contains(scanStoreNames.passageAssociations)) database.createObjectStore(…, {keyPath: 'checkoutPassageId'})`. Les entrées de vente existantes (sans `checkoutPassageId`) restent valides : **aucune** réécriture.

- [ ] **Step 1: Write the failing tests**

```ts
describe('parseMemberCredential', () => {
  it('recognises a card QR', () => {
    expect(parseMemberCredential('VPDC1.AAAA.BBBB')).toEqual({kind: 'qr', value: 'VPDC1.AAAA.BBBB'});
  });
  it('recognises and normalises a recovery code', () => {
    expect(parseMemberCredential(' lune 4271 ')).toEqual({kind: 'recovery-code', value: 'LUNE-4271'});
  });
  it('ignores an ISBN', () => {
    expect(parseMemberCredential('9782070612758')).toBeNull();
  });
});

describe('ScanSyncService — passage associations', () => {
  it('sends sales before the association of the same passage', async () => {
    await store.addSaleOutboxEntries([sale('g1', 'p1')], []);
    await store.putPassageAssociation(association('p1'));

    await service.synchronize();

    expect(callOrder).toEqual(['registerSale:g1', 'associatePassage:p1']);
    expect(api.registerSale.calls.mostRecent().args[0].checkoutPassageId).toBe('p1');
  });

  it('removes the association once the server answered Unresolved', async () => {
    api.associatePassage.and.returnValue(of({checkoutPassageId: 'p1', status: 'Unresolved', displayLabel: null, alreadyProcessed: false}));
    await store.putPassageAssociation(association('p1'));

    await service.synchronize();

    expect(await store.listPassageAssociations()).toEqual([]);
  });

  it('keeps syncing sales when the association call fails', async () => {
    api.associatePassage.and.returnValue(throwError(() => new HttpErrorResponse({status: 0})));
    await store.addSaleOutboxEntries([sale('g1', 'p1')], []);
    await store.putPassageAssociation(association('p1'));

    await service.synchronize();

    expect(await store.listSaleOutboxEntries()).toEqual([]);
    expect((await store.listPassageAssociations())[0].attemptCount).toBe(1);
  });

  it('quarantines a 409 conflict', async () => { /* status 409 → 'Quarantined' */ });

  it('replays an association with the same passage id after a reconnection', async () => {
    api.associatePassage.and.returnValues(
      throwError(() => new HttpErrorResponse({status: 0})),
      of({checkoutPassageId: 'p1', status: 'Associated', displayLabel: 'Camille', alreadyProcessed: false}));
    await store.putPassageAssociation(association('p1'));

    await service.synchronize();
    await advanceRetryClock();
    await service.synchronize();

    expect(api.associatePassage.calls.allArgs().map(args => args[0])).toEqual(['p1', 'p1']);
  });
});

describe('ScanLocalStoreService — v6 upgrade', () => {
  it('opens a v5 database and keeps existing sales', async () => { /* créer une base v5 avec une vente, ouvrir v6, vente présente, store passageAssociations présent */ });
});
```

Écrire complètement les `it` restants en suivant `scan-sync.service.spec.ts` (mêmes fakes d'API et de store).

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/Scan && npm test -- --watch=false --browsers=ChromeHeadless --include src/app/offline`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Dans `ScanSyncService`, après la boucle `rareSales`, ajouter une boucle `associations` calquée sur la boucle des ventes (mêmes `isRetryDue`, `markAttempt`, classification d'erreur), sans `throw` qui sortirait de `synchronize()`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/Scan && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: PASS, aucune régression (notamment `scan-session-integrity.spec.ts`).

- [ ] **Step 5: Commit**

```bash
git add src/Scan
git commit -m "feat(scan): queue passage associations offline and replay them idempotently"
```

---

### Task CS-21: Scanette — « Associer un compte » en caisse

**Files:**
- Create: `src/Scan/src/app/offline/scan-passage-association.service.ts`
- Test: `src/Scan/src/app/offline/scan-passage-association.service.spec.ts`
- Modify: `src/Scan/src/app/scanner/scanner.component.{ts,html,scss,spec.ts}`
- Modify: `src/Scan/src/app/offline/scan-api.service.ts` — `resolveMemberCard(credential): Observable<{displayLabel: string}>`

**Interfaces:**
- Consumes: `parseMemberCredential`, store et API (CS-20).
- Produces : `ScanPassageAssociationService` (`providedIn: 'root'`) :
  - `readonly state: Signal<ScanPassageAssociationState>` avec
    `type ScanPassageAssociationState = {kind: 'anonymous'} | {kind: 'resolving'} | {kind: 'associated'; displayLabel: string} | {kind: 'pending-offline'} | {kind: 'not-recognised'}`
  - `startAssociation(): void` — met la caméra/gâchette en attente d'une carte pour **ce** passage
  - `submitCredential(raw: string): Promise<void>` — `parseMemberCredential` ; si `null` → `not-recognised` ; en ligne → `resolveMemberCard` (succès → `associated`, `404` → `not-recognised`) ; hors ligne ou erreur réseau → `pending-offline` (le credential est gardé **en mémoire** jusqu'à VALIDER)
  - `clear(): void` — retour à Vente anonyme
  - `commit(checkoutPassageId: string, occurredAt: Date): Promise<boolean>` — appelé par `validateCash` ; écrit l'entrée `passageAssociations` si l'état est `associated` ou `pending-offline` ; renvoie `true` si une association a été mise en file.

Comportement dans `scanner.component` (`F-10` §8, §11.1) :
- En-tête de vente : **« Client : Vente anonyme »** par défaut, bouton **« Associer un compte »**. Aucun avertissement si rien n'est associé.
- Pendant l'attente d'une carte, le prochain scan (caméra ou gâchette) est routé vers `submitCredential` **s'il** ressemble à une carte ; un ISBN scanné à ce moment reste traité comme un livre (on ne perd jamais un livre, `F-10` §8.3).
- Saisie manuelle du code de secours via le champ « Saisir un code » existant (même routage).
- Confirmation en ligne : « Compte reconnu : Camille — Les livres de ce passage seront visibles dans Mes achats. » + **Continuer** / **Changer de compte**.
- Hors ligne : « Association en attente — elle sera transmise au retour du réseau. » (jamais « visible dans Mes achats »).
- Non reconnu : « Carte non reconnue. Réessayez, saisissez le code de secours ou continuez en vente anonyme. »
- `validateCash()` : génère `checkoutPassageId = createClientId()` **seulement si** l'état n'est pas `anonymous`, le transmet à `recordCashSales` et `recordSales`, puis `commit(...)`, puis `clear()`. Message après validation : « {n} livres enregistrés. Association en attente de synchronisation. » ou « … associés au compte de Camille. » selon l'état.
- `cancelLastSale()` : ne touche pas l'association ; les ventes supprimées localement ne seront jamais envoyées, donc le passage n'aura pas de ligne (invisible côté membre — Review Focus 2).
- Le QR est lu **une fois par passage** ; aucun geste par livre.
- Annonce accessible : la zone d'état client a `role="status"` et `aria-live="polite"`.
- La Scanette n'affiche ni e-mail, ni liste de recherche, ni achats précédents.

- [ ] **Step 1: Write the failing tests**

```ts
describe('ScanPassageAssociationService', () => {
  it('resolves online and exposes only the display label', async () => {
    api.resolveMemberCard.and.returnValue(of({displayLabel: 'Camille'}));
    service.startAssociation();

    await service.submitCredential('VPDC1.AAAA.BBBB');

    expect(service.state()).toEqual({kind: 'associated', displayLabel: 'Camille'});
  });

  it('falls back to pending-offline on network error', async () => {
    api.resolveMemberCard.and.returnValue(throwError(() => new HttpErrorResponse({status: 0})));
    await service.submitCredential('VPDC1.AAAA.BBBB');

    expect(service.state().kind).toBe('pending-offline');
  });

  it('marks not-recognised on 404 and keeps the sale anonymous on commit', async () => {
    api.resolveMemberCard.and.returnValue(throwError(() => new HttpErrorResponse({status: 404})));
    await service.submitCredential('VPDC1.AAAA.BBBB');

    expect(await service.commit('p1', new Date())).toBeFalse();
    expect(await store.listPassageAssociations()).toEqual([]);
  });

  it('persists only passage id, credential and dates on commit', async () => {
    api.resolveMemberCard.and.returnValue(of({displayLabel: 'Camille'}));
    await service.submitCredential('lune 4271');

    await service.commit('p1', new Date('2026-03-14T15:00:00Z'));

    const [entry] = await store.listPassageAssociations();
    expect(Object.keys(entry).sort()).toEqual(
      ['attemptCount', 'checkoutPassageId', 'createdAt', 'credential', 'lastAttemptAt', 'lastError', 'occurredAt', 'status']);
    expect(JSON.stringify(entry)).not.toContain('Camille');
  });
});

describe('ScannerComponent — cash association', () => {
  it('shows "Vente anonyme" and validates without any warning', async () => { … });
  it('sends the same checkoutPassageId on every sale of an associated passage', async () => { … });
  it('does not create a passage id for an anonymous sale', async () => { … });
  it('treats an ISBN scanned while waiting for a card as a book', async () => { … });
  it('never claims visibility in Mes achats while offline', async () => { … });
  it('returns to "Vente anonyme" after validation', async () => { … });
});
```

Écrire intégralement les six `it` du composant en suivant `scanner.component.spec.ts` (mêmes stubs `scanWorkflow`, `scanRareCash`).

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/Scan && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Service conforme à l'interface. Dans `scanner.component.ts`, le routage des scans en mode caisse passe d'abord par `parseMemberCredential` **uniquement** si `association.state().kind` n'est pas `associated` ou si l'attente a été ouverte par « Associer un compte » ; sinon flux actuel inchangé. Mise en page : bandeau client au-dessus de la liste `cash-items`, conforme à la maquette `F-10` §8.1 et à la maquette Claude Design quand elle est fournie.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/Scan && npm test -- --watch=false --browsers=ChromeHeadless && npm run build`
Expected: PASS ; build de production OK.

- [ ] **Step 5: Commit**

```bash
git add src/Scan
git commit -m "feat(scan): associate a member card to the current checkout passage"
```

🧪 **Le test qui décide** (`F-10` §16) — deux appareils, seul :
1. Téléphone A : Catalog, compte de test, Ma carte ouverte.
2. Appareil B : Scanette, CAISSE, **mode avion**, « Associer un compte », scanner le QR de A → « Association en attente ».
3. Scanner deux livres, VALIDER.
4. Sur B : « Annuler la dernière vente » sur un **second** passage associé (pour vérifier qu'il ne remonte rien).
5. Couper le mode avion ; attendre la synchronisation ; forcer un rafraîchissement de la Scanette (rejeu).
6. Sur A, Mes achats (après CS-24) : **un** passage, **deux** livres, aucun doublon, rien pour le passage annulé.
Consigner le résultat dans `NEXT.md` (📌). La fonctionnalité n'est pas présentée comme terminée sans ce test.

🚀 Scanette : nouvel APK/PWA selon la procédure `DT-15` ; une Scanette non mise à jour continue de vendre en anonyme.

---

### Task CS-22: Correction d'une association par un administrateur

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Application/CheckoutPassages/Commands/DissociateCheckoutPassage/{DissociateCheckoutPassageCommand,DissociateCheckoutPassageCommandHandler}.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Api/Controllers/CheckoutPassageController.cs`
- Modify: `src/Catalog/src/app/core/catalog-admin-api.service.ts` et la page d'administration (section « Comptes » ou « Caisse » existante — reprendre l'emplacement de l'administration des comptes)
- Test: `src/Backend/Vole_Papillon_Damour.Application.tests/CheckoutPassages/DissociateCheckoutPassageCommandHandlerTests.cs`, spec Catalog de l'admin

**Interfaces:**
- Produces :
  - `record DissociateCheckoutPassageCommand(Guid CheckoutPassageId, UserId AdministratorId, string Reason) : IRequest<ErrorOr<Success>>` — `Reason` obligatoire, 3 à 500 caractères.
  - `POST /administration/checkout-passages/{id:guid}/dissociate` body `{ reason }` → `204` ; policy `Administration`.
  - Référence de passage montrée au membre dans Mes achats : **8 premiers caractères** de l'id en majuscules (ex. `3F2A9C1B`) ; l'écran d'administration accepte cette référence courte **ou** l'id complet et affiche date, nombre de lignes et prénom avant confirmation.
  - `GET /administration/checkout-passages/lookup?reference=3F2A9C1B` → `200 { id, occurredAt, lineCount, displayLabel }` ou `404` ; `409` si plusieurs passages partagent le préfixe (demander l'id complet).
- Règles : pas de réaffectation vers un autre compte en v1 (`F-10` §16) ; les mouvements de stock ne changent pas ; la sélection ACHETÉ du membre n'est pas modifiée ; log d'audit `CheckoutPassageDissociated { PassageId, AdministratorId, Reason }`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Dissociate_HidesPassageFromMemberAndKeepsLines() { … }

[Fact]
public async Task Dissociate_IsIdempotent() { … }

[Fact]
public async Task Dissociate_WithoutReason_ReturnsValidationError() { … }

[Fact]
public async Task Lookup_WithAmbiguousPrefix_ReturnsConflict() { … }
```

Écrire intégralement chaque test (fixture CS-16).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~DissociateCheckoutPassage"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Handler : charge le passage, `Dissociate(command.AdministratorId, now)`, `SaveChangesAsync`, log. Lookup : `StartsWith` sur la représentation `N` de l'id — **à faire côté SQL** via une colonne calculée ou en limitant la recherche aux 90 derniers jours si EF ne traduit pas `Guid.ToString()` ; choisir la variante qui produit une requête SQL (vérifier avec `ToQueryString()` dans le test).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.slnx` puis `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Backend src/Catalog
git commit -m "feat(passage): let administrators dissociate a wrong association"
```

---

# Étape 4 — Historique

### Task CS-23: Lecture de Mes achats

**Files:**
- Create: `src/Backend/Vole_Papillon_Damour.Application/Purchases/Queries/GetMyPurchases/{GetMyPurchasesQuery,GetMyPurchasesQueryHandler,GetMyPurchasesQueryValidator}.cs`
- Modify: `src/Backend/Vole_Papillon_Damour.Api/Controllers/MemberAccountController.cs`
- Test: `src/Backend/Vole_Papillon_Damour.Application.tests/Purchases/GetMyPurchasesQueryHandlerTests.cs`

**Interfaces:**
- Produces :
  - `record GetMyPurchasesQuery(string ExternalId, string Email, string? FirstName, string? LastName, string? Cursor, int Limit = 10) : IRequest<ErrorOr<MyPurchasesPage>>`
  - `record MyPurchasesPage(IReadOnlyList<PurchasePassageResult> Passages, string? NextCursor)`
  - `record PurchasePassageResult(Guid Id, string Reference, DateTimeOffset OccurredAt, Guid? FairId, string? FairLabel, int ActiveBookCount, IReadOnlyList<PurchaseLineResult> Lines)`
  - `record PurchaseLineResult(Guid Id, string Kind, string? Isbn13, Guid? RareBookId, string Title, string? Authors, string? Publisher, int? PublicationYear, string? PhysicalFormat, int Quantity, string State, string? CurrentCoverUrl)` — `State` ∈ `"Associated"`, `"Cancelled"`.
  - `GET /catalog/me/purchases?cursor=&limit=` → `200 MyPurchasesPage` ; `limit` borné à `[1, 10]`.
- Règles :
  - Seuls les passages `IsVisibleToMember` du membre (`RG-61`) ayant **au moins une ligne** ; un passage dont toutes les lignes sont annulées reste visible, lignes marquées « Annulée » (décision §16).
  - Tri `OccurredAt DESC, Id DESC` ; curseur opaque = base64url de `"{ticks}:{id}"`.
  - Titres et éditions depuis l'**instantané** de la ligne (`RG-62`) ; `CurrentCoverUrl` est un bonus lu sur `Book`/`RareBook` actuels, `null` si absent.
  - **Aucun** champ de prix (`RG-63`) — vérifié par un test de sérialisation.
  - `FairLabel` : « Bourse aux livres » + date, depuis l'`AssoEvents` de la ligne (`AssoEventsId` de la première ligne non nulle).

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Handle_ReturnsOnlyOwnAssociatedPassages_NewestFirst() { … }

[Fact]
public async Task Handle_WhenPassageHasNoActiveLine_IsHidden()
{
    // passage associé SANS aucune ligne (ventes annulées localement avant envoi) → absent
}

[Fact]
public async Task Handle_KeepsSnapshotWhenBookMetadataChangesLater()
{
    // vente de « Le Petit Prince » ; puis UpdateMetadata titre = « Autre » → la ligne affiche « Le Petit Prince »
}

[Fact]
public async Task Handle_ShowsCancelledLines() { … }

[Fact]
public async Task Handle_PaginatesWithStableCursor()
{
    // 12 passages → page 1 : 10 + NextCursor ; page 2 : 2 + NextCursor null ; aucun doublon entre pages
}

[Fact]
public async Task Handle_HidesUnresolvedAndDissociatedPassages() { … }

[Fact]
public void PurchaseLineResult_HasNoPriceField()
{
    typeof(PurchaseLineResult).GetProperties().Select(p => p.Name)
        .Should().NotContain(name => name.Contains("Price", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Amount", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Total", StringComparison.OrdinalIgnoreCase));
}
```

Écrire intégralement chaque test (fixture CS-16 étendue d'un `AddAssociatedPassageAsync(member, occurredAt, params string[] isbns)`).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~GetMyPurchasesQueryHandlerTests"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Deux requêtes : (1) page de passages `Where(UserId == me && Status == Associated && Lines.Any())` avec `Take(limit + 1)` ; (2) lignes de ces passages. Pas d'`Include` massif. `EnsureAsync` d'abord, comme la liste de recherche.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~GetMyPurchasesQueryHandlerTests"` et `dotnet test src/Backend/Vole_Papillon_Damour.Api.tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Backend
git commit -m "feat(purchases): read associated purchases page by page"
```

---

### Task CS-24: Catalog — onglet « Mes achats »

**Files:**
- Create: `src/Catalog/src/app/features/account/purchases/account-purchases.component.{ts,html,scss,spec.ts}`
- Modify: `catalog-member-api.service.ts`, `catalog.models.ts`, `catalog-account-page.component.{ts,html}`

**Interfaces:**
- Consumes: `GET /catalog/me/purchases` (CS-23).
- Produces : `CatalogMemberApiService.getPurchases(token, cursor?: string)` ; onglet `purchases`.

Comportement (`F-10` §9) :
- Groupes par passage : « 14 mars 2026 · Bourse aux livres », « 3 livres », puis lignes « Titre · Éditeur · Année » (+ auteur, format, ISBN en secondaire, quantité si > 1).
- Ligne annulée : style atténué + badge **« Annulée »**.
- Bouton **« Afficher des achats plus anciens »** tant que `nextCursor` existe ; aucun chargement complet de l'historique.
- Mention permanente sous la liste : « Seuls les passages associés à ce compte sont visibles ici. Les ventes anonymes ne peuvent pas être retrouvées automatiquement. » (`F-10` §9.4).
- État vide : « Aucun achat n'est encore associé à ce compte. » + la mention ci-dessus.
- Lien discret par passage : « Signaler une erreur » → affiche la référence (`Reference`) et l'adresse de contact de l'association déjà utilisée par les pages légales.
- Aucun prix, total ou montant affiché.

- [ ] **Step 1: Write the failing tests**

```ts
it('groups lines by passage with date and count', () => { … });
it('shows cancelled lines with the "Annulée" badge', () => { … });
it('loads the next page only on demand', () => { … });
it('always shows the anonymous-sales notice', () => { … });
it('never renders a euro sign', () => { expect(fixture.nativeElement.textContent).not.toContain('€'); });
it('shows the empty state wording', () => { … });
```

Écrire intégralement chaque `it` (stubs identiques à CS-10).

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless --include src/app/features/account/purchases`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Composant enfant OnPush ; chargement **au premier affichage de l'onglet** seulement (comme « Ma contribution »), pour que la carte et la sélection n'attendent pas l'historique. Mise en page selon la maquette.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless && npm run build`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Catalog
git commit -m "feat(catalog): add the Mes achats account tab"
```

🧪 Parcours C, D, F et G de `F-10` §14 de bout en bout en DEV, 390 px compris.

---

### Task CS-25: Suppression du compte et information RGPD

**Files:**
- Modify: `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/AccountDeletionStore.cs` (`RemoveMemberDataAsync`)
- Modify: la politique de rétention utilisée par `HasRetainedSalesMovementsAsync` si elle doit considérer les passages
- Modify: `src/Catalog/src/app/features/legal/*` — page `/donnees-personnelles` (et `/confidentialite` si elle liste les traitements)
- Modify: `docs/bourse-aux-livres/09-rgpd-compte-et-droits.md` — passer la section de la proposition à l'état « implémenté », durées de conservation retenues
- Test: `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/AccountDeletion/AccountDeletionStoreTests.cs` (ajouts)

**Interfaces:**
- Consumes: `CheckoutPassage.Anonymize` (CS-14), `MemberSelectionItems`, `MemberCards`.
- Produces : à la finalisation (`F-10` §12.3, `RG-66`) :
  1. suppression de tous les `MemberSelectionItems` du membre ;
  2. suppression de sa `MemberCard` (le QR et le code cessent d'être reconnus) ;
  3. `Anonymize(completedAt)` sur tous ses `CheckoutPassages` — les lignes (instantanés bibliographiques, quantités, dates) restent pour l'audit, **sans** lien personnel ;
  4. `BookMovements` inchangés ;
  5. dans la **même transaction** que le reste de `FinalizeAsync`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task FinalizeAsync_RemovesSelectionAndCard_AndAnonymisesPassages()
{
    await using var fixture = await AccountDeletionFixture.CreateAsync();
    var member = await fixture.AddMemberAsync();
    await fixture.AddSelectionItemAsync(member.Id, "9782070612758");
    await fixture.AddMemberCardAsync(member.Id);
    var passage = await fixture.AddAssociatedPassageWithLineAsync(member.Id, "9782070612758");
    var workItem = await fixture.RequestDeletionAsync(member);

    await fixture.Store.FinalizeAsync(workItem, fixture.Now, default);

    (await fixture.Context.MemberSelectionItems.CountAsync()).Should().Be(0);
    (await fixture.Context.MemberCards.CountAsync()).Should().Be(0);
    var reloaded = await fixture.Context.CheckoutPassages.SingleAsync(p => p.Id == passage.Id);
    reloaded.UserId.Should().BeNull();
    reloaded.AssociatedByVolunteerId.Should().BeNull();
    (await fixture.Context.CheckoutPassageLines.CountAsync()).Should().Be(1);
}
```

Reprendre la fixture réelle d'`AccountDeletionStoreTests` (nom à vérifier) plutôt qu'en créer une.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter "FullyQualifiedName~AccountDeletionStoreTests"`
Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

Dans `RemoveMemberDataAsync`, à la suite de la suppression des `WatchlistItems` :

```csharp
var selectionItems = await dbContext.MemberSelectionItems
    .Where(item => item.UserId == userId)
    .ToListAsync(cancellationToken);
dbContext.MemberSelectionItems.RemoveRange(selectionItems);

var cards = await dbContext.MemberCards
    .Where(card => card.UserId == userId)
    .ToListAsync(cancellationToken);
dbContext.MemberCards.RemoveRange(cards);

var passages = await dbContext.CheckoutPassages
    .Where(passage => passage.UserId == userId || passage.AssociatedByVolunteerId == userId)
    .ToListAsync(cancellationToken);
foreach (var passage in passages)
{
    passage.Anonymize(completedAt);
}
```

(Passer `completedAt` à `RemoveMemberDataAsync`. Un bénévole qui supprime son compte perd aussi son lien `AssociatedByVolunteerId` ; si l'anonymisation du passage d'**un autre** membre par ce biais est jugée excessive, remplacer par une simple mise à `null` d'`AssociatedByVolunteerId` via une méthode `ForgetVolunteer()` — test à ajuster en conséquence.)

Page légale : ajouter la section « Ma sélection, carte de compte et achats » avec les points de `F-10` §12.1 et ce qui reste anonymisé (§12.3, dernier alinéa). Mettre à jour le test existant de la page légale s'il vérifie des titres.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/Backend/Vole_Papillon_Damour.slnx` et `cd src/Catalog && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Backend src/Catalog docs/bourse-aux-livres/09-rgpd-compte-et-droits.md
git commit -m "feat(account): erase selection, card and purchase links on deletion"
```

🧪 Parcours H de `F-10` §14 en DEV : compte de test avec sélection, carte et un passage ; suppression ; vérifier que la carte n'est plus reconnue par la Scanette et que le passage reste dans la base sans `UserId`.

---

### Task CS-26: Documentation, décisions et état de reprise

**Files:**
- Modify: `docs/bourse-aux-livres/plan/README.md` — ligne `06` dans le tableau des lots
- Modify: `docs/bourse-aux-livres/technique/01-decisions.md` — deux `DT-nn` (jeton HMAC ; passage créé seulement s'il est associé)
- Modify: `docs/bourse-aux-livres/technique/02-modele-de-donnees.md` — tables `MemberSelectionItems`, `MemberCards`, `CheckoutPassages`, `CheckoutPassageLines`, colonne `BookMovements.CheckoutPassageId`
- Modify: `docs/bourse-aux-livres/10-evolution-compte-selection-achats.md` — statut « Implémenté (étapes 1 à 4) » une fois les 🧪 passés
- Modify: `docs/bourse-aux-livres/08-questions-ouvertes.md` — `Q-12` : décisions retenues
- Modify: `.github/memory/` (fichier thématique concerné) et `.github/memory/changelog.md`
- Modify: `NEXT.md`

- [ ] **Step 1:** Rédiger les deux `DT-nn` (contexte, décision, alternatives écartées : jeton aléatoire stocké haché ; passage créé pour toute vente).
- [ ] **Step 2:** Mettre à jour le modèle de données et le tableau des lots.
- [ ] **Step 3:** `NEXT.md` : migrations appliquées ou non, secret `MEMBER_CARD_SIGNING_KEY` créé (date seulement), résultat du test deux appareils, mesures de `F-10` §18 à relever à la prochaine bourse.
- [ ] **Step 4:** Lancer `graphify update .` (consigne du dépôt).
- [ ] **Step 5: Commit**

```bash
git add docs .github/memory NEXT.md graphify-out
git commit -m "docs(account): record member selection and purchases delivery"
```

---

## Ordre, dépendances et découpage en PR

| PR | Tâches | Déployable seule | Dépend de |
|---|---|---|---|
| 1 — Ma sélection | CS-1 → CS-10 | Oui (backend + Catalog) | — |
| 2 — Carte | CS-11 → CS-13 | Oui | PR 1 (onglets du compte) |
| 3 — Association serveur | CS-14 → CS-19 | Oui (sans effet tant que la Scanette n'envoie rien) | PR 2 |
| 4 — Association Scanette | CS-20 → CS-21 | Oui | PR 3 déployée |
| 5 — Historique, correction, RGPD | CS-22 → CS-26 | Oui | PR 4 |

## Mesures après mise en service (`F-10` §18)

Aucune instrumentation dédiée n'est ajoutée au-delà des logs `DT-16` ci-dessus ; les indicateurs se calculent en requête SQL après une bourse :

- part de passages associés : `CheckoutPassages` `Associated` / nombre de validations (log Scanette `CashValidated`) ;
- associations restées en attente : entrées `passageAssociations` non vides dans les diagnostics Scanette exportés ;
- `Unresolved` et `Dissociated` : comptage direct ;
- sélections passées à ACHETÉ : `MemberSelectionItems` `Purchased` avec `PurchasedAt` dans la fenêtre de la bourse ;
- taux de lecture du QR au premier essai et durée ajoutée : relevés manuels 🧪.
