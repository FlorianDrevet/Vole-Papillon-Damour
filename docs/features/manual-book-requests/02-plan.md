# Demandes manuelles — plan d'implémentation

> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : maquettes/preview/.

**Objectif.** Un administrateur saisit des demandeurs sans compte et les livres qu'ils
cherchent ; au tri, un exemplaire correspondant est mis de côté (sans stock ni alerte
membre) avec les coordonnées affichées en ligne ; l'administration le marque donné ou
refusé, et un refus le remet en stock en alertant immédiatement les membres.

**Architecture.** Nouvel agrégat de domaine `ManualRequestAggregate` (demandeur + demandes,
deux tables), un nouveau type de mouvement `ManualRequestHold` pour l'exemplaire mis de
côté au scan, un slice CQRS `Application/ManualRequests/` (commandes admin, requêtes,
services communs de correspondance, de stock et de remise en stock), une méthode
d'outbox `QueueForReleaseAsync` pour les alertes immédiates, des endpoints minimal API
sous `/books/admin/manual-requests*` (policy `Administration`) et
`/scan/manual-requests/candidate` (policy `Tri`). Front : une section d'administration
`manual-requests` dans `src/Catalog`, et un verdict « À mettre de côté » dans `src/Scan`.

**Stack.** .NET 10, EF Core (SQL Server, SQLite en test), MediatR, ErrorOr, xUnit +
FluentAssertions + NSubstitute ; Angular (Catalog, Scan), Jasmine/Karma (`npm test`).

## Contraintes globales

- Langue de l'interface : français, libellés **mot pour mot** des maquettes.
- Aucune donnée personnelle de demandeur dans le delta catalogue de la scanette ni dans
  son stockage local (`RG-80`). Le nom, le prénom et le téléphone ne transitent vers la
  scanette que par `GET /scan/manual-requests/candidate`, pour l'ISBN scanné.
- Seul le rôle `Administration` accède aux endpoints et à la section admin (`RG-77`) ;
  l'endpoint `candidate` exige `Tri`.
- Effacement automatique : **3 mois** (90 jours) après la clôture de la dernière demande
  d'un demandeur sans demande ouverte (`RG-89`).
- Un « donné » n'est jamais compté comme une vente (`RG-83`).
- Alertes de refus : `DueAt = maintenant`, sans le délai `AlertDelayMinutes` (`RG-84`).
- Couleur du nouveau verdict : pétrole `#0d5c63`, fond clair `#e3f1f2`, déclarés comme
  variables à côté des autres couleurs de verdict, jamais en dur dans les composants.
- Aucun déploiement, aucune commande `az` en écriture. La migration est générée, pas
  appliquée à une base distante.

## Points d'attention pour la revue

1. **Deux postes qui scannent le même titre au même moment.** `ManualBookRequest.Version`
   est un jeton de concurrence : le second `SaveChanges` échoue (`DbUpdateConcurrencyException`),
   la requête répond 500, la scanette rejoue le geste (échec transitoire) et le second
   exemplaire sert la demande suivante ou suit le circuit normal. Test : tâche 2
   (`Version` est un jeton de concurrence) et tâche 9 (rejeu idempotent).
2. **Livre jamais reçu demandé en portée œuvre.** Au scan, la fiche n'a pas encore de
   `WorkId` : le serveur le résout par `IBibliographicMetadataResolver` (cache déjà chauffé
   par l'appel `/books/{isbn}/metadata` de la scanette). Test : tâche 9.
3. **Refus d'un exemplaire scanné en `PROCHAINE BOURSE` dont la bourse est passée ou
   annulée** : il devient disponible. Test : tâche 5.
4. **Annulation d'une session contenant un exemplaire déjà donné** : la demande donnée ne
   bouge pas, l'exemplaire n'est pas remis en stock. Test : tâche 11.
5. **Ancien cache de la scanette sans `isManuallyRequested`** : traité comme `false`,
   aucune erreur. Test : tâche 18.

## Arbitrages pris par le plan (à valider)

- **« Immédiatement » = au plus tard au prochain passage du Worker.** L'outbox reçoit des
  messages dus tout de suite ; l'API tente en plus un envoi immédiat (`BookAlertDeliveryService`)
  après le refus, sans faire échouer le geste si l'envoi est indisponible. Sinon le Sweep
  horaire les envoie.
- **Prélèvement** : un exemplaire disponible en priorité (l'édition la plus fournie pour
  une demande d'œuvre), sinon un exemplaire annoncé **rattaché à une bourse datée**. Les
  annonces sans date ne sont pas prélevables.
- **« Recherché par »** à la scanette garde son affichage actuel (valeur issue du delta) ;
  le « 3 membres » de la maquette est illustratif.
- **Mode appliqué au refus** : relu sur la session de scan au moment du refus. Un
  changement de mode de la session (`RG-25`) n'a donc rien à modifier sur la demande.

## Fichiers

| Couche | Fichiers créés | Fichiers modifiés |
|---|---|---|
| Domain | `ManualRequestAggregate/ManualRequester.cs`, `ManualBookRequest.cs`, `ValueObjects/ManualRequestEnums.cs`, `Common/Errors/Errors.ManualRequest.cs` | `BookMovementAggregate/ValueObjects/BookMovementType.cs` |
| Infrastructure | `Persistence/Configurations/ManualRequesterConfiguration.cs`, `ManualBookRequestConfiguration.cs`, migration `AddManualBookRequests` | `Persistence/ProjectDbContext.cs`, `Services/BookAlerts/BookAlertOutbox.cs` |
| Application | `ManualRequests/**` | `Common/Interfaces/Persistence/IProjectDbContext.cs`, `IBookAlertOutbox.cs`, `Books/Commands/ScanBook/ScanBookCommandHandler.cs`, `Books/Common/ScanBookResult.cs`, `ScanCatalogBookResult.cs`, `Books/Queries/GetCatalogDelta/GetCatalogDeltaQueryHandler.cs`, `Books/Commands/Admin/RemoveScanSessionMovementCommandHandler.cs`, `CancelScanSessionCommandHandler.cs`, `Books/Queries/Admin/GetAdminVolunteerStatisticsQueryHandler.cs`, `Books/Queries/GetVolunteerStatistics/GetVolunteerStatisticsQueryHandler.cs` |
| Contracts / Api | `Contracts/ManualRequests/*.cs`, `Api/Controllers/ManualRequestAdministrationController.cs` | `Contracts/Books/Responses/ScanBookResponse.cs`, `ScanCatalogBookResponse.cs`, `Api/Controllers/BookController.cs`, `Api/Program.cs` |
| Worker | — | `BookSweepFunction.cs` |
| Catalog | `features/administration/manual-requests/**` | `core/catalog.models.ts`, `core/catalog-admin-api.service.ts`, `core/catalog-administration-route.ts`, `features/administration/catalog-administration-page.component.{ts,html,scss}`, page légale « données personnelles » |
| Scan | `offline/scan-manual-request.service.ts` | `offline/scan-offline.model.ts`, `scan-api.service.ts`, `scan-verdict.service.ts`, `scan-sync.service.ts`, `scan-local-store.service.ts`, `scanner/scanner.component.{ts,html,scss}` |

Chemins backend relatifs à `src/Backend/Vole_Papillon_Damour.<Projet>/`, front à
`src/<App>/src/app/`.

---

### Tâche 0 : état initial

- [ ] `rtk git status` propre sur `feat/manual-book-requests`.
- [ ] `dotnet build src/Backend/Vole_Papillon_Damour.slnx`
- [ ] `dotnet test` sur `Domain.tests`, `Application.tests`, `Infrastructure.tests`,
  `Api.tests`, `Worker.tests`.
- [ ] `npm ci && npm test -- --watch=false && npm run build` dans `src/Catalog` puis `src/Scan`.
- [ ] Noter les échecs déjà présents (ex. 3 échecs `AccountCard` connus côté Catalog) dans
  les notes de PR, **sans les corriger**. Pas de commit.

---

### Tâche 1 : domaine — demandeur, demande, type de mouvement, erreurs

**Files :**
- Create : `Domain/ManualRequestAggregate/ValueObjects/ManualRequestEnums.cs`,
  `Domain/ManualRequestAggregate/ManualRequester.cs`,
  `Domain/ManualRequestAggregate/ManualBookRequest.cs`,
  `Domain/Common/Errors/Errors.ManualRequest.cs`
- Modify : `Domain/BookMovementAggregate/ValueObjects/BookMovementType.cs`
- Test : `Domain.tests/ManualRequest/ManualRequesterTests.cs`,
  `Domain.tests/ManualRequest/ManualBookRequestTests.cs`

**Interfaces produites :** `ManualRequester`, `ManualBookRequest`,
`ManualBookRequestScope`, `ManualBookRequestStatus`, `ManualBookRequestFoundOrigin`,
`BookMovementType.ManualRequestHold`, `Errors.ManualRequest.*` (signatures ci-dessous).

Ce code a été compilé et ses 24 tests passent contre le projet Domain réel : recopie-le.

- [ ] **Étape 1 : tests d'abord.** `ManualRequesterTests.cs` :

```csharp
using FluentAssertions;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.ManualRequest;

public sealed class ManualRequesterTests
{
    private static readonly UserId Admin = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
    private static readonly DateTime At = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public void Create_WithConsent_TrimsNamesAndNormalizesPhone()
    {
        var requester = ManualRequester.Create(Id, "  Martin ", " Jeanne", "06 12.34-56 78", consentGiven: true, Admin, At);

        requester.LastName.Should().Be("Martin");
        requester.FirstName.Should().Be("Jeanne");
        requester.Phone.Should().Be("0612345678");
        requester.ConsentRecordedAt.Should().Be(At);
        requester.ConsentRecordedBy.Should().Be(Admin);
        requester.IsErased.Should().BeFalse();
    }

    [Fact]
    public void Create_WithoutConsent_Throws()
    {
        var act = () => ManualRequester.Create(Id, "Martin", "Jeanne", "0612345678", consentGiven: false, Admin, At);

        act.Should().Throw<ArgumentException>().WithParameterName("consentGiven");
    }

    [Theory]
    [InlineData("+33 6 12 34 56 78", "+33612345678")]
    [InlineData("(02) 41 22 18 90", "0241221890")]
    public void NormalizePhone_AcceptsFrenchAndInternationalNumbers(string input, string expected)
    {
        ManualRequester.NormalizePhone(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("06 12 34 56 7")]
    [InlineData("06-12-34-56-78 poste 2")]
    [InlineData("+33 6 12 34 56 78 90 12 34")]
    public void NormalizePhone_RejectsInvalidNumbers(string input)
    {
        var act = () => ManualRequester.NormalizePhone(input);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Erase_ClearsContactAndBlocksUpdates()
    {
        var requester = ManualRequester.Create(Id, "Martin", "Jeanne", "0612345678", true, Admin, At);

        requester.Erase(At.AddDays(90));

        requester.IsErased.Should().BeTrue();
        requester.LastName.Should().BeNull();
        requester.FirstName.Should().BeNull();
        requester.Phone.Should().BeNull();
        requester.ErasedAt.Should().Be(At.AddDays(90));
        var act = () => requester.UpdateContact("Durand", "Paul", "0612345678", At.AddDays(91));
        act.Should().Throw<InvalidOperationException>();
    }
}
```

`ManualBookRequestTests.cs` :

```csharp
using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.ManualRequest;

public sealed class ManualBookRequestTests
{
    private static readonly UserId Admin = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
    private static readonly UserId Volunteer = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000b2"));
    private static readonly DateTime RequestedAt = new(2026, 9, 3, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FoundAt = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid RequesterId = Guid.Parse("00000000-0000-0000-0000-0000000000c3");
    private static readonly Isbn13 Gallimard = Parse("9782070612758");
    private static readonly Isbn13 Folio = Parse("9782070584628");

    [Fact]
    public void CreateWork_MatchesAnyEditionOfTheWork()
    {
        var request = WorkRequest();

        request.Status.Should().Be(ManualBookRequestStatus.Searching);
        request.Matches(Folio, "OL123W").Should().BeTrue();
        request.Matches(Folio, "OL999W").Should().BeFalse();
        request.Matches(Folio, null).Should().BeFalse();
    }

    [Fact]
    public void CreateEdition_MatchesOnlyItsIsbn()
    {
        var request = ManualBookRequest.CreateEdition(Guid.NewGuid(), RequesterId, Gallimard, "Le Petit Prince", null, null, null, RequestedAt);

        request.Matches(Gallimard, "OL123W").Should().BeTrue();
        request.Matches(Folio, "OL123W").Should().BeFalse();
    }

    [Fact]
    public void CreateWork_WithoutWorkId_Throws()
    {
        var act = () => ManualBookRequest.CreateWork(Guid.NewGuid(), RequesterId, " ", null, null, null, null, RequestedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkFoundByScan_RecordsHoldAndIncrementsVersion()
    {
        var request = WorkRequest();
        var session = ScanSessionId.CreateUnique();
        var movement = BookMovementId.CreateUnique();

        request.MarkFoundByScan(Folio, session, movement, Volunteer, FoundAt);

        request.Status.Should().Be(ManualBookRequestStatus.Found);
        request.FoundOrigin.Should().Be(ManualBookRequestFoundOrigin.Scan);
        request.FoundIsbn13.Should().Be(Folio);
        request.FoundScanSessionId.Should().Be(session);
        request.HoldMovementId.Should().Be(movement);
        request.FoundBy.Should().Be(Volunteer);
        request.Version.Should().Be(1);
        request.RequestedAt.Should().Be(RequestedAt);
    }

    [Fact]
    public void MarkFoundFromStock_FromAnnouncement_RemembersTheFair()
    {
        var request = WorkRequest();
        var fair = AssoEventsId.Create(Guid.NewGuid());
        var announcementSession = ScanSessionId.CreateUnique();

        request.MarkFoundFromStock(Folio, fair, announcementSession, BookMovementId.CreateUnique(), Admin, FoundAt);

        request.FoundOrigin.Should().Be(ManualBookRequestFoundOrigin.StockAnnounced);
        request.PickedFromFairId.Should().Be(fair);
        request.FoundScanSessionId.Should().Be(announcementSession);
    }

    [Fact]
    public void MarkFoundFromStock_FairWithoutSession_Throws()
    {
        var request = WorkRequest();

        var act = () => request.MarkFoundFromStock(Folio, AssoEventsId.Create(Guid.NewGuid()), null, BookMovementId.CreateUnique(), Admin, FoundAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkFound_Twice_Throws()
    {
        var request = WorkRequest();
        request.MarkFoundByScan(Folio, ScanSessionId.CreateUnique(), BookMovementId.CreateUnique(), Volunteer, FoundAt);

        var act = () => request.MarkFoundByScan(Gallimard, ScanSessionId.CreateUnique(), BookMovementId.CreateUnique(), Volunteer, FoundAt);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkGiven_ClosesTheRequest()
    {
        var request = FoundRequest();

        request.MarkGiven(Admin, FoundAt.AddDays(1));

        request.Status.Should().Be(ManualBookRequestStatus.Given);
        request.IsOpen.Should().BeFalse();
        request.ClosedBy.Should().Be(Admin);
        request.ReleaseMovementId.Should().BeNull();
    }

    [Fact]
    public void Refuse_AndClose_KeepsReleaseMovement()
    {
        var request = FoundRequest();
        var release = BookMovementId.CreateUnique();

        request.Refuse(Admin, keepSearching: false, release, FoundAt.AddDays(1));

        request.Status.Should().Be(ManualBookRequestStatus.Refused);
        request.ReleaseMovementId.Should().Be(release);
    }

    [Fact]
    public void Refuse_AndKeepSearching_ReturnsToSearchingWithOriginalSeniority()
    {
        var request = FoundRequest();

        request.Refuse(Admin, keepSearching: true, BookMovementId.CreateUnique(), FoundAt.AddDays(1));

        request.Status.Should().Be(ManualBookRequestStatus.Searching);
        request.RequestedAt.Should().Be(RequestedAt);
        request.FoundIsbn13.Should().BeNull();
        request.HoldMovementId.Should().BeNull();
        request.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void Cancel_FoundWithoutRelease_Throws()
    {
        var request = FoundRequest();

        var act = () => request.Cancel(Admin, releaseMovementId: null, FoundAt);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Cancel_Searching_ClosesWithoutRelease()
    {
        var request = WorkRequest();

        request.Cancel(Admin, releaseMovementId: null, FoundAt);

        request.Status.Should().Be(ManualBookRequestStatus.Cancelled);
    }

    [Fact]
    public void RevertScanHold_ReturnsToSearching()
    {
        var request = FoundRequest();

        request.RevertScanHold(FoundAt.AddMinutes(30));

        request.Status.Should().Be(ManualBookRequestStatus.Searching);
        request.FoundScanSessionId.Should().BeNull();
        request.Version.Should().Be(2);
    }

    [Fact]
    public void RevertScanHold_OnStockPick_Throws()
    {
        var request = WorkRequest();
        request.MarkFoundFromStock(Folio, null, null, BookMovementId.CreateUnique(), Admin, FoundAt);

        var act = () => request.RevertScanHold(FoundAt);

        act.Should().Throw<InvalidOperationException>();
    }

    private static ManualBookRequest WorkRequest()
        => ManualBookRequest.CreateWork(Guid.NewGuid(), RequesterId, "OL123W", "Le Petit Prince", "Antoine de Saint-Exupéry", null, null, RequestedAt);

    private static ManualBookRequest FoundRequest()
    {
        var request = WorkRequest();
        request.MarkFoundByScan(Folio, ScanSessionId.CreateUnique(), BookMovementId.CreateUnique(), Volunteer, FoundAt);
        return request;
    }

    private static Isbn13 Parse(string value)
        => Isbn13.TryCreate(value, out var isbn) ? isbn : throw new InvalidOperationException(value);
}
```

- [ ] **Étape 2 :** `dotnet test src/Backend/Vole_Papillon_Damour.Domain.tests --filter FullyQualifiedName~ManualRequest`
  → échec de compilation (types absents).

- [ ] **Étape 3 : implémentation.** `ValueObjects/ManualRequestEnums.cs` :

```csharp
namespace Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

public enum ManualBookRequestScope : byte
{
    Work = 0,
    Edition = 1
}

public enum ManualBookRequestStatus : byte
{
    Searching = 0,
    Found = 1,
    Given = 2,
    Refused = 3,
    Cancelled = 4
}

public enum ManualBookRequestFoundOrigin : byte
{
    Scan = 0,
    StockAvailable = 1,
    StockAnnounced = 2
}
```

`ManualRequester.cs` :

```csharp
using System.Text;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.ManualRequestAggregate;

/// <summary>
/// A person without an account whose wanted books are entered by an administrator
/// (spec manual-book-requests, RG-78, RG-87). Contact details are erased in place so
/// closed requests keep their history without personal data (RG-88, RG-89).
/// </summary>
public sealed class ManualRequester : AggregateRoot<Guid>
{
    public const int NameMaxLength = 100;
    public const int PhoneMaxLength = 16;

    public string? LastName { get; private set; }
    public string? FirstName { get; private set; }
    public string? Phone { get; private set; }
    public DateTime ConsentRecordedAt { get; private set; }
    public UserId ConsentRecordedBy { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ErasedAt { get; private set; }

    private ManualRequester(
        Guid id,
        string lastName,
        string firstName,
        string phone,
        UserId recordedBy,
        DateTime at) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A requester identifier is required.", nameof(id));
        }

        ValidateAuthor(recordedBy);
        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        LastName = NormalizeName(lastName, nameof(lastName));
        FirstName = NormalizeName(firstName, nameof(firstName));
        Phone = NormalizePhone(phone);
        ConsentRecordedAt = utcAt;
        ConsentRecordedBy = recordedBy;
        CreatedAt = utcAt;
        UpdatedAt = utcAt;
    }

    public ManualRequester() : base()
    {
    }

    public bool IsErased => ErasedAt is not null;

    public static ManualRequester Create(
        Guid id,
        string lastName,
        string firstName,
        string phone,
        bool consentGiven,
        UserId recordedBy,
        DateTime at)
    {
        if (!consentGiven)
        {
            throw new ArgumentException("The requester's consent is required (RG-87).", nameof(consentGiven));
        }

        return new ManualRequester(id, lastName, firstName, phone, recordedBy, at);
    }

    public void UpdateContact(string lastName, string firstName, string phone, DateTime at)
    {
        EnsureNotErased();
        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        LastName = NormalizeName(lastName, nameof(lastName));
        FirstName = NormalizeName(firstName, nameof(firstName));
        Phone = NormalizePhone(phone);
        UpdatedAt = utcAt;
    }

    public void Erase(DateTime at)
    {
        if (IsErased)
        {
            return;
        }

        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        LastName = null;
        FirstName = null;
        Phone = null;
        ErasedAt = utcAt;
        UpdatedAt = utcAt;
    }

    /// <summary>
    /// Keeps a leading "+" and the digits; spaces, dots, dashes and parentheses are
    /// dropped. A French number has 10 digits starting with 0; an international one
    /// has 8 to 15 digits after "+".
    /// </summary>
    public static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("A phone number is required.", nameof(phone));
        }

        var builder = new StringBuilder(phone.Length);
        var trimmed = phone.Trim();
        for (var index = 0; index < trimmed.Length; index++)
        {
            var character = trimmed[index];
            if (char.IsAsciiDigit(character))
            {
                builder.Append(character);
            }
            else if (character == '+' && index == 0)
            {
                builder.Append(character);
            }
            else if (character is not (' ' or '.' or '-' or '(' or ')'))
            {
                throw new ArgumentException("The phone number contains invalid characters.", nameof(phone));
            }
        }

        var normalized = builder.ToString();
        var isFrench = normalized.Length == 10 && normalized[0] == '0';
        var isInternational = normalized.StartsWith('+') && normalized.Length is >= 9 and <= PhoneMaxLength;
        if (!isFrench && !isInternational)
        {
            throw new ArgumentException("The phone number is not valid.", nameof(phone));
        }

        return normalized;
    }

    private void EnsureNotErased()
    {
        if (IsErased)
        {
            throw new InvalidOperationException("An erased requester cannot be changed.");
        }
    }

    private static string NormalizeName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A name is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > NameMaxLength)
        {
            throw new ArgumentException($"A name cannot exceed {NameMaxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static void ValidateAuthor(UserId? author)
    {
        if (author is null || author.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid administrator identifier is required.", nameof(author));
        }
    }
}
```

`ManualBookRequest.cs` :

```csharp
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.ManualRequestAggregate;

/// <summary>
/// One wanted book of a <see cref="ManualRequester"/>. Life cycle: Searching → Found →
/// Given | Refused, or back to Searching; Searching/Found → Cancelled (spec §5).
/// <see cref="Version"/> is an optimistic concurrency token: two scan stations cannot
/// claim the same request (spec §10).
/// </summary>
public sealed class ManualBookRequest : Entity<Guid>
{
    public const int TextMaxLength = 500;
    public const int WorkIdMaxLength = 64;

    public Guid RequesterId { get; private set; }
    public ManualBookRequestScope Scope { get; private set; }
    public string? WorkId { get; private set; }
    public Isbn13? Isbn13 { get; private set; }
    public string? Title { get; private set; }
    public string? Authors { get; private set; }
    public string? Publisher { get; private set; }
    public int? PublicationYear { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public ManualBookRequestStatus Status { get; private set; } = ManualBookRequestStatus.Searching;

    public ManualBookRequestFoundOrigin? FoundOrigin { get; private set; }
    public Isbn13? FoundIsbn13 { get; private set; }
    public DateTime? FoundAt { get; private set; }
    public UserId? FoundBy { get; private set; }
    /// <summary>Scan session of the hold, or of the announcement a copy was picked from.</summary>
    public ScanSessionId? FoundScanSessionId { get; private set; }
    public AssoEventsId? PickedFromFairId { get; private set; }
    public BookMovementId? HoldMovementId { get; private set; }

    public DateTime? ClosedAt { get; private set; }
    public UserId? ClosedBy { get; private set; }
    public BookMovementId? ReleaseMovementId { get; private set; }

    public int Version { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ManualBookRequest(
        Guid id,
        Guid requesterId,
        ManualBookRequestScope scope,
        string? workId,
        Isbn13? isbn13,
        string? title,
        string? authors,
        string? publisher,
        int? publicationYear,
        DateTime requestedAt) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A request identifier is required.", nameof(id));
        }

        if (requesterId == Guid.Empty)
        {
            throw new ArgumentException("A requester identifier is required.", nameof(requesterId));
        }

        var normalizedWorkId = string.IsNullOrWhiteSpace(workId) ? null : workId.Trim();
        if (scope == ManualBookRequestScope.Work)
        {
            if (normalizedWorkId is null || normalizedWorkId.Length > WorkIdMaxLength || isbn13 is not null)
            {
                throw new ArgumentException("A work request requires a work identifier only.", nameof(workId));
            }
        }
        else if (scope == ManualBookRequestScope.Edition)
        {
            if (isbn13 is null || normalizedWorkId is not null)
            {
                throw new ArgumentException("An edition request requires an ISBN-13 only.", nameof(isbn13));
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        RequesterId = requesterId;
        Scope = scope;
        WorkId = normalizedWorkId;
        Isbn13 = isbn13;
        Title = NormalizeText(title, nameof(title));
        Authors = NormalizeText(authors, nameof(authors));
        Publisher = NormalizeText(publisher, nameof(publisher));
        PublicationYear = publicationYear;
        RequestedAt = DomainTime.RequireUtc(requestedAt, nameof(requestedAt));
        UpdatedAt = RequestedAt;
    }

    public ManualBookRequest() : base()
    {
    }

    public bool IsOpen => Status is ManualBookRequestStatus.Searching or ManualBookRequestStatus.Found;

    public static ManualBookRequest CreateWork(
        Guid id,
        Guid requesterId,
        string workId,
        string? title,
        string? authors,
        string? publisher,
        int? publicationYear,
        DateTime requestedAt)
        => new(id, requesterId, ManualBookRequestScope.Work, workId, null, title, authors, publisher, publicationYear, requestedAt);

    public static ManualBookRequest CreateEdition(
        Guid id,
        Guid requesterId,
        Isbn13 isbn13,
        string? title,
        string? authors,
        string? publisher,
        int? publicationYear,
        DateTime requestedAt)
        => new(id, requesterId, ManualBookRequestScope.Edition, null, isbn13, title, authors, publisher, publicationYear, requestedAt);

    /// <summary>RG-79: this ISBN, or an edition of this work.</summary>
    public bool Matches(Isbn13 scannedIsbn13, string? scannedWorkId)
    {
        return Scope == ManualBookRequestScope.Edition
            ? Isbn13 == scannedIsbn13
            : scannedWorkId is not null && string.Equals(WorkId, scannedWorkId, StringComparison.Ordinal);
    }

    public void MarkFoundByScan(
        Isbn13 foundIsbn13,
        ScanSessionId scanSessionId,
        BookMovementId holdMovementId,
        UserId volunteerId,
        DateTime at)
    {
        ArgumentNullException.ThrowIfNull(scanSessionId);
        ArgumentNullException.ThrowIfNull(holdMovementId);
        MarkFound(foundIsbn13, ManualBookRequestFoundOrigin.Scan, scanSessionId, null, holdMovementId, volunteerId, at);
    }

    /// <summary>
    /// RG-82. A copy taken from an announcement remembers its fair and the scan session
    /// of that announcement, so a refusal can announce it again on the same fair.
    /// </summary>
    public void MarkFoundFromStock(
        Isbn13 foundIsbn13,
        AssoEventsId? pickedFromFairId,
        ScanSessionId? pickedAnnouncementSessionId,
        BookMovementId pickMovementId,
        UserId administratorId,
        DateTime at)
    {
        ArgumentNullException.ThrowIfNull(pickMovementId);
        if ((pickedFromFairId is null) != (pickedAnnouncementSessionId is null))
        {
            throw new ArgumentException("An announced copy needs both its fair and its announcement session.", nameof(pickedAnnouncementSessionId));
        }

        var origin = pickedFromFairId is null
            ? ManualBookRequestFoundOrigin.StockAvailable
            : ManualBookRequestFoundOrigin.StockAnnounced;
        MarkFound(foundIsbn13, origin, pickedAnnouncementSessionId, pickedFromFairId, pickMovementId, administratorId, at);
    }

    /// <summary>RG-83.</summary>
    public void MarkGiven(UserId administratorId, DateTime at)
    {
        EnsureStatus(ManualBookRequestStatus.Found);
        Close(ManualBookRequestStatus.Given, administratorId, releaseMovementId: null, at);
    }

    /// <summary>RG-84. The found copy has been released into stock by <paramref name="releaseMovementId"/>.</summary>
    public void Refuse(UserId administratorId, bool keepSearching, BookMovementId releaseMovementId, DateTime at)
    {
        EnsureStatus(ManualBookRequestStatus.Found);
        ArgumentNullException.ThrowIfNull(releaseMovementId);
        if (keepSearching)
        {
            ValidateAuthor(administratorId);
            ClearFound();
            Status = ManualBookRequestStatus.Searching;
            Touch(at);
            return;
        }

        Close(ManualBookRequestStatus.Refused, administratorId, releaseMovementId, at);
    }

    /// <summary>RG-86. A found request needs the movement that released its copy.</summary>
    public void Cancel(UserId administratorId, BookMovementId? releaseMovementId, DateTime at)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Only an open request can be cancelled.");
        }

        if (Status == ManualBookRequestStatus.Found && releaseMovementId is null)
        {
            throw new ArgumentNullException(nameof(releaseMovementId), "A found request releases its copy when cancelled.");
        }

        Close(ManualBookRequestStatus.Cancelled, administratorId, releaseMovementId, at);
    }

    /// <summary>RG-85: the scan that held the copy was removed or its session cancelled.</summary>
    public void RevertScanHold(DateTime at)
    {
        EnsureStatus(ManualBookRequestStatus.Found);
        if (FoundOrigin != ManualBookRequestFoundOrigin.Scan)
        {
            throw new InvalidOperationException("Only a copy held at scan can be reverted by a session correction.");
        }

        ClearFound();
        Status = ManualBookRequestStatus.Searching;
        Touch(at);
    }

    private void MarkFound(
        Isbn13 foundIsbn13,
        ManualBookRequestFoundOrigin origin,
        ScanSessionId? scanSessionId,
        AssoEventsId? pickedFromFairId,
        BookMovementId movementId,
        UserId author,
        DateTime at)
    {
        EnsureStatus(ManualBookRequestStatus.Searching);
        ValidateAuthor(author);
        if (string.IsNullOrWhiteSpace(foundIsbn13.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(foundIsbn13));
        }

        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        Status = ManualBookRequestStatus.Found;
        FoundOrigin = origin;
        FoundIsbn13 = foundIsbn13;
        FoundAt = utcAt;
        FoundBy = author;
        FoundScanSessionId = scanSessionId;
        PickedFromFairId = pickedFromFairId;
        HoldMovementId = movementId;
        Touch(utcAt);
    }

    private void Close(ManualBookRequestStatus status, UserId author, BookMovementId? releaseMovementId, DateTime at)
    {
        ValidateAuthor(author);
        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        Status = status;
        ClosedAt = utcAt;
        ClosedBy = author;
        ReleaseMovementId = releaseMovementId;
        Touch(utcAt);
    }

    private void ClearFound()
    {
        FoundOrigin = null;
        FoundIsbn13 = null;
        FoundAt = null;
        FoundBy = null;
        FoundScanSessionId = null;
        PickedFromFairId = null;
        HoldMovementId = null;
    }

    private void Touch(DateTime at)
    {
        UpdatedAt = DomainTime.RequireUtc(at, nameof(at));
        Version++;
    }

    private void EnsureStatus(ManualBookRequestStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"The request must be {expected} but is {Status}.");
        }
    }

    private static void ValidateAuthor(UserId? author)
    {
        if (author is null || author.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid author identifier is required.", nameof(author));
        }
    }

    private static string? NormalizeText(string? value, string parameterName)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is { Length: > TextMaxLength })
        {
            throw new ArgumentException($"The value cannot exceed {TextMaxLength} characters.", parameterName);
        }

        return normalized;
    }
}
```

`BookMovementType.cs` : ajouter **en dernier** (valeur stockée en `byte`, ne pas
renuméroter) :

```csharp
    Withdrawal,
    ManualRequestHold
```

Un mouvement `ManualRequestHold` de quantité `+1` trace l'exemplaire mis de côté au tri
(il ne change ni `Book.QuantityAvailable` ni les annonces) ; un `ManualRequestHold` de
quantité `-1` avec `ReversalOfMovementId` l'annule lors d'une correction de session.

`Errors.ManualRequest.cs` (même forme que `Errors.NotFoundReport.cs`) :

| Méthode | Type | Code |
|---|---|---|
| `RequesterNotFound(Guid id)` | NotFound | `ManualRequest.RequesterNotFound` |
| `RequestNotFound(Guid id)` | NotFound | `ManualRequest.RequestNotFound` |
| `ConsentRequired()` | Validation | `ManualRequest.ConsentRequired` |
| `InvalidName()` | Validation | `ManualRequest.InvalidName` |
| `InvalidPhone()` | Validation | `ManualRequest.InvalidPhone` |
| `InvalidTarget()` | Validation | `ManualRequest.InvalidTarget` |
| `DuplicateRequest()` | Conflict | `ManualRequest.DuplicateRequest` |
| `LimitReached(int limit)` | Conflict | `ManualRequest.LimitReached` |
| `InvalidTransition()` | Conflict | `ManualRequest.InvalidTransition` |
| `NothingInStock()` | Conflict | `ManualRequest.NothingInStock` |
| `RequesterErased()` | Conflict | `ManualRequest.RequesterErased` |
| `InvalidAuthor()` | Validation | `ManualRequest.InvalidAuthor` |

- [ ] **Étape 4 :** relancer la commande de l'étape 2 → 24 tests verts ; puis tout
  `Domain.tests`.
- [ ] **Étape 5 :** `rtk git commit -m "feat(manual-requests): add manual requester and request domain"`

---

### Tâche 2 : persistance et migration

**Files :**
- Create : `Infrastructure/Persistence/Configurations/ManualRequesterConfiguration.cs`,
  `ManualBookRequestConfiguration.cs`, migration `AddManualBookRequests`
- Modify : `Application/Common/Interfaces/Persistence/IProjectDbContext.cs`,
  `Infrastructure/Persistence/ProjectDbContext.cs`
- Test : `Infrastructure.tests/ManualRequest/ManualRequestConfigurationTests.cs`

**Interfaces produites :** `IProjectDbContext.ManualRequesters`,
`IProjectDbContext.ManualBookRequests` (implémentation par défaut
`=> throw new NotSupportedException();`, comme `BookNotFoundReports`, pour ne pas casser
les contextes de test existants).

- [ ] **Étape 1 : tests d'abord**, sur le modèle de
  `Infrastructure.tests/NotFoundReport/BookNotFoundReportConfigurationTests.cs`
  (SQLite en mémoire + `ProjectDbContext`) :
  - `Configuration_MapsTables` : tables `ManualRequesters` et `ManualBookRequests` ;
    `LastName`/`FirstName` `nvarchar(100)`, `Phone` `varchar(16)`, `Isbn13` et
    `FoundIsbn13` `char(13)`, `WorkId` 64, `Title`/`Authors`/`Publisher` 500.
  - `Version_IsAConcurrencyToken` :
    `entityType.FindProperty(nameof(ManualBookRequest.Version))!.IsConcurrencyToken.Should().BeTrue()`.
  - `Configuration_DeclaresIndexes` : index `IX_ManualBookRequests_Status_Isbn13`,
    `IX_ManualBookRequests_Status_WorkId`, `IX_ManualBookRequests_RequesterId`,
    `IX_ManualBookRequests_HoldMovementId`.
  - `RoundTrip_PersistsAFoundRequest` : enregistrer un demandeur + une demande trouvée
    au scan, relire dans un nouveau contexte, vérifier `FoundIsbn13`,
    `FoundScanSessionId`, `HoldMovementId`, `Version == 1`.
  - `ConcurrentClaim_SecondSaveFails` : deux `ProjectDbContext` sur la même connexion
    chargent la même demande `Searching` ; le premier `MarkFoundByScan` + `SaveChanges`
    passe, le second lève `DbUpdateConcurrencyException`.
- [ ] **Étape 2 :** `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter FullyQualifiedName~ManualRequest` → échec.
- [ ] **Étape 3 :** configurations, en recopiant les conversions de
  `BookNotFoundReportConfiguration.cs` (`UserId`, `Isbn13` via
  `BookPersistenceConversions.SerializeNullableIsbn13`/`ParseNullableIsbn13`, dates via
  `UtcDateTimeConverter`/`NullableUtcDateTimeConverter`, enums en `tinyint`) ; ajouter
  `ScanSessionId`, `BookMovementId` et `AssoEventsId` nullables en `Guid?`.
  `builder.Property(request => request.Version).IsConcurrencyToken();`.
  Aucune clé étrangère vers `Books` (une demande peut viser un livre jamais reçu) ;
  `RequesterId` est une FK vers `ManualRequesters` avec `DeleteBehavior.Restrict`.
  Ajouter les deux `DbSet` à `ProjectDbContext` et les membres par défaut à
  `IProjectDbContext`.
- [ ] **Étape 4 :** migration :

```bash
dotnet ef migrations add AddManualBookRequests \
  --project src/Backend/Vole_Papillon_Damour.Infrastructure \
  --startup-project src/Backend/Vole_Papillon_Damour.Api
```

  Relire : deux tables, quatre index, **aucune** modification de données existantes.
  La nouvelle valeur `BookMovementType.ManualRequestHold` (7) ne demande pas de migration
  (colonne `tinyint`).
- [ ] **Étape 5 :** tests verts, puis `Api.tests` (`DatabaseMigrationPolicyTests`).
- [ ] **Étape 6 :** `rtk git commit -m "feat(manual-requests): persist manual requesters and requests"`

---

### Tâche 3 : outbox — alertes immédiates après une remise en stock

**Files :**
- Modify : `Application/Common/Interfaces/Persistence/IBookAlertOutbox.cs`,
  `Infrastructure/Services/BookAlerts/BookAlertOutbox.cs`
- Test : `Infrastructure.tests/Books/BookAlertOutboxTests.cs`

**Interfaces produites :**

```csharp
/// RG-84 : alertes des membres pour un exemplaire remis en stock hors session,
/// dues immédiatement. Retourne le nombre de membres mis en file.
Task<int> QueueForReleaseAsync(BookMovementId releaseMovementId, DateTime releasedAt, CancellationToken cancellationToken);
```

- [ ] **Étape 1 : tests d'abord** (dans `BookAlertOutboxTests`, avec ses helpers) :
  - `QueueForRelease_DirectEntry_QueuesDueNowForWatchers` : un membre suit l'œuvre, un
    autre l'ISBN, un troisième une autre œuvre ; mouvement `DirectEntry` +1 sans
    session → 2 messages, `DueAt == releasedAt`, `ScanSessionId == null`, retour `2`,
    item `Mode == AvailableNow`.
  - `QueueForRelease_AnnouncementEntry_UsesFairOpening` : `AnnouncementEntry` rattaché à
    une bourse → `FairOpeningAt` de cette bourse dans le payload.
  - `QueueForRelease_RespectsCooldownAndSuspendedWatchlists` : historique < 30 jours ou
    liste suspendue → aucun message, retour `0`.
  - `QueueForRelease_OtherMovementType_QueuesNothing` (ex. `ManualRequestHold`).
  - `QueueForSession_IgnoresManualRequestHold` : une session qui ne contient qu'un
    `ManualRequestHold` ne met aucune alerte en file (`RG-81`).
- [ ] **Étape 2 :** `dotnet test … --filter FullyQualifiedName~BookAlertOutboxTests` → échec.
- [ ] **Étape 3 :** extraire de `QueueForSessionAsync` la partie « watchlists actives →
  correspondances → anti-répétition → messages » dans
  `private async Task<int> QueueCandidatesAsync(IReadOnlyList<AlertCandidate> candidates, DateTime referenceAt, DateTime dueAt, Guid? scanSessionId, CancellationToken)`
  sans changer son comportement pour les sessions (les tests existants doivent rester
  verts), puis écrire `QueueForReleaseAsync` : charger le mouvement, ne traiter que
  `DirectEntry`/`AnnouncementEntry` de quantité positive, construire un `AlertCandidate`
  (mode `AvailableNow` pour `DirectEntry`, `NextFair` sinon ; ouverture de la bourse
  comme dans `QueueForSessionAsync`), appeler `QueueCandidatesAsync(…, dueAt: releasedAt, scanSessionId: null)`.
  Le filtre `movement.Type == movementType` de `QueueForSessionAsync` exclut déjà
  `ManualRequestHold` : le test le fige.
- [ ] **Étape 4 :** tous les tests `BookAlertOutboxTests` verts.
- [ ] **Étape 5 :** `rtk git commit -m "feat(alerts): queue immediate alerts for released copies"`

---

### Tâche 4 : services communs — correspondance, stock et observateurs

**Files :**
- Create : `Application/ManualRequests/Common/ManualRequestMatcher.cs`,
  `ManualRequestStock.cs`, `ManualRequestWatchers.cs`
- Test : `Application.tests/ManualRequests/ManualRequestFixture.cs`,
  `Application.tests/ManualRequests/Common/ManualRequestCommonTests.cs`

**Interfaces produites :**

```csharp
internal static class ManualRequestMatcher
{
    // Demande Searching la plus ancienne (RequestedAt puis Id) qui vise cet ISBN ou cette œuvre.
    public static Task<ManualBookRequest?> FindOldestSearchingAsync(IProjectDbContext db, Isbn13 isbn13, string? workId, CancellationToken ct);
    // Vrai s'il existe une demande Searching de portée Work (pour décider de résoudre le WorkId).
    public static Task<bool> AnySearchingWorkRequestAsync(IProjectDbContext db, CancellationToken ct);
    // WorkId de la fiche, sinon celui du référentiel (resolver), sinon null.
    public static Task<string?> ResolveWorkIdAsync(IProjectDbContext db, IBibliographicMetadataResolver? resolver, Isbn13 isbn13, CancellationToken ct);
}

public sealed record ManualRequestStockSnapshot(int QuantityAvailable, int QuantityAnnounced, DateTimeOffset? NextFairStart);

internal static class ManualRequestStock
{
    // Somme sur la cible : l'ISBN (édition) ou toutes les fiches de l'œuvre (Book.WorkId), fiches redirigées exclues.
    // QuantityAnnounced ne compte que les annonces Announced rattachées à une bourse (AssoEventsId non nul).
    public static Task<ManualRequestStockSnapshot> GetAsync(IProjectDbContext db, ManualBookRequestScope scope, string? workId, Isbn13? isbn13, CancellationToken ct);
}

internal static class ManualRequestWatchers
{
    // Nombre de membres à watchlist active dont un item vise cet ISBN ou cette œuvre (affiché en E5 avant confirmation).
    public static Task<int> CountAsync(IProjectDbContext db, Isbn13 isbn13, string? workId, CancellationToken ct);
}
```

- [ ] **Étape 1 : fixture.** `ManualRequestFixture` sur le modèle de
  `Application.tests/MemberSelection/MemberSelectionFixture.cs` : SQLite en mémoire, un
  `ManualRequestTestDbContext : DbContext, IProjectDbContext` qui expose `Books`,
  `BookAnnouncements`, `BookMovements`, `ScanSessions`, `AssociationSettings`,
  `AssoEvents`, `Watchlists`, `WatchlistItems`, `UserAlertHistories`, `Users`,
  `ManualRequesters`, `ManualBookRequests` et applique les configurations de production
  (`ManualRequesterConfiguration`, `ManualBookRequestConfiguration`, etc.). Helpers :
  `AddBookAsync(isbn, quantityAvailable, workId)`, `AddAnnouncementAsync(isbn, fairId, quantity)`,
  `AddFairAsync(start)`, `AddSessionAsync(mode, fairId)`, `AddRequesterAsync(lastName, firstName, phone)`,
  `AddWorkRequestAsync(requesterId, workId, requestedAt)`, `AddEditionRequestAsync(requesterId, isbn, requestedAt)`,
  `AddWatcherAsync(scope, workId, isbn)`, `CreateClock(now)`. Les tâches 5 à 13 la réutilisent.
- [ ] **Étape 2 : tests d'abord** :
  - `FindOldestSearching_PrefersOldestAndIgnoresFound` (deux demandes d'œuvre, la plus
    ancienne est déjà `Found` → la suivante est retournée).
  - `FindOldestSearching_MatchesEditionOrWork`.
  - `ResolveWorkId_UsesBookThenResolver` (fiche sans `WorkId` → resolver substitué
    retourne `OL123W`).
  - `Stock_SumsEditionsOfTheWorkAndIgnoresUndatedAnnouncements`.
  - `Watchers_CountsActiveMembersOnly`.
- [ ] **Étape 3 :** `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter FullyQualifiedName~ManualRequests` → échec.
- [ ] **Étape 4 :** implémentation (requêtes `AsNoTracking`, sauf `FindOldestSearchingAsync`
  qui doit **suivre** l'entité pour la modifier ensuite).
- [ ] **Étape 5 :** vert. `rtk git commit -m "feat(manual-requests): add matching, stock and watcher helpers"`

---

### Tâche 5 : remise en stock d'un exemplaire (refus, annulation)

**Files :**
- Create : `Application/ManualRequests/Common/ManualRequestRelease.cs`
- Test : `Application.tests/ManualRequests/Common/ManualRequestReleaseTests.cs`

**Interfaces produites :**

```csharp
public enum ManualRequestReleaseMode { AvailableNow, NextFair }

public sealed record ManualRequestReleasePlan(ManualRequestReleaseMode Mode, AssoEventsId? FairId, DateTimeOffset? FairStart);

internal static class ManualRequestRelease
{
    // RG-84 : où ira l'exemplaire trouvé si on le refuse maintenant (affiché en E5).
    public static Task<ManualRequestReleasePlan> PlanAsync(IProjectDbContext db, ManualBookRequest request, DateTime now, CancellationToken ct);

    // Remet l'exemplaire en stock selon le plan, crée le mouvement (et l'annonce), retourne le mouvement.
    // N'appelle PAS SaveChanges : l'appelant enregistre puis appelle IBookAlertOutbox.QueueForReleaseAsync.
    public static Task<ErrorOr<BookMovementId>> ReleaseAsync(IProjectDbContext db, ManualBookRequest request, UserId by, DateTime now, CancellationToken ct);
}
```

Règles de `PlanAsync` (tableau `RG-84`) :

| Origine | Plan |
|---|---|
| `Scan`, session `AvailableNow` | `AvailableNow` |
| `Scan`, session `NextFair` avec bourse non annulée et `DateEnd ?? DateStart` future | `NextFair` sur la bourse **actuelle** de la session |
| `Scan`, autre cas (bourse passée, annulée, absente) | `AvailableNow` |
| `StockAvailable` | `AvailableNow` |
| `StockAnnounced`, bourse `PickedFromFairId` non annulée et future | `NextFair` sur cette bourse |
| `StockAnnounced`, autre cas | `AvailableNow` |

`ReleaseAsync` : charger la fiche canonique de `FoundIsbn13` (`RedirectedToIsbn13`
suivi) ; selon le plan :

- `AvailableNow` → `book.RecordAvailableEntry(now)` + mouvement `DirectEntry` +1 ;
- `NextFair` → `book.RecordAnnouncementEntry(now)` +
  `BookAnnouncement.Create(BookAnnouncementId.CreateUnique(), isbn, plan.FairId, 1, now, request.FoundScanSessionId!, clientGestureId: null)`
  + mouvement `AnnouncementEntry` +1. `FoundScanSessionId` est la session du scan
  (origine `Scan`) ou la session de l'annonce prélevée (origine `StockAnnounced`,
  tâche 7) : il est toujours renseigné quand le plan vaut `NextFair`.

Mouvement : `ScanSessionId = null`, `VolunteerId = by`, `AssoEventsId = plan.FairId`,
note `"ManualRequest.Release"`, `clientGestureId: null`. Sans `ScanSessionId`, ce
mouvement n'est jamais touché par les corrections de session (`RG-45`).

- [ ] **Étape 1 : tests d'abord** (un par ligne du tableau), plus
  `Release_AvailableNow_IncrementsStockAndCreatesDirectEntry`,
  `Release_NextFair_CreatesAnnouncementOnTheFair`,
  `Plan_ScanSessionReassignedToAvailableNow_UsesCurrentSessionMode` (`RG-85`).
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation. **Étape 4 :** vert.
- [ ] **Étape 5 :** `rtk git commit -m "feat(manual-requests): release held copies into stock"`

---

### Tâche 6 : commandes demandeur (créer, modifier, supprimer)

**Files :**
- Create : `Application/ManualRequests/Commands/CreateRequester/{CreateManualRequesterCommand, …Handler, …Validator}.cs`,
  `UpdateRequester/…`, `DeleteRequester/…`, `Application/ManualRequests/Common/ManualRequestResults.cs`
- Test : `Application.tests/ManualRequests/Commands/RequesterCommandHandlerTests.cs`

**Interfaces produites :**

```csharp
public sealed record CreateManualRequesterCommand(string LastName, string FirstName, string Phone, bool ConsentGiven, UserId By) : IRequest<ErrorOr<Guid>>;
public sealed record UpdateManualRequesterCommand(Guid RequesterId, string LastName, string FirstName, string Phone, UserId By) : IRequest<ErrorOr<Success>>;
public sealed record DeleteManualRequesterCommand(Guid RequesterId, UserId By) : IRequest<ErrorOr<ManualRequesterDeletionResult>>;
public sealed record ManualRequesterDeletionResult(int CancelledRequestCount, int NotifiedMemberCount);
```

- [ ] **Étape 1 : tests d'abord** :
  - `Create_WithoutConsent_ReturnsConsentRequired` (`RG-87`) — rien n'est enregistré.
  - `Create_WithInvalidPhone_ReturnsInvalidPhone` ; `Create_Valid_PersistsNormalizedPhone`.
  - `Update_ErasedRequester_ReturnsRequesterErased`.
  - `Delete_CancelsOpenRequestsReleasesFoundCopyAndErases` (`RG-88`) : une demande
    `Searching`, une `Found` au scan en `AvailableNow`, une `Given` → retour
    `CancelledRequestCount == 2`, stock +1, `outbox.QueueForReleaseAsync` appelé une fois,
    demandeur effacé, la demande `Given` inchangée.
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation. Le validateur FluentValidation
  vérifie la présence des champs ; les exceptions `ArgumentException` du domaine sont
  traduites en `InvalidName`/`InvalidPhone` (appeler `ManualRequester.NormalizePhone`
  dans un `try` avant de créer). La suppression se fait dans une transaction :
  `ReleaseAsync` pour chaque demande trouvée, `Cancel` pour chaque demande ouverte,
  `Erase`, `SaveChanges`, puis `QueueForReleaseAsync` pour chaque mouvement de remise,
  `SaveChanges`, commit.
- [ ] **Étape 4 :** vert. **Étape 5 :** `rtk git commit -m "feat(manual-requests): manage requesters"`

---

### Tâche 7 : ajouter une demande, prélever dans le stock

**Files :**
- Create : `Application/ManualRequests/Commands/AddRequest/{AddManualBookRequestCommand, …Handler, …Validator}.cs`,
  `Application/ManualRequests/Commands/PickFromStock/{PickManualRequestFromStockCommand, …Handler}.cs`,
  `Application/ManualRequests/Common/ManualRequestStockPick.cs`
- Test : `Application.tests/ManualRequests/Commands/AddRequestCommandHandlerTests.cs`

**Interfaces produites :**

```csharp
public sealed record AddManualBookRequestCommand(
    Guid RequesterId, ManualBookRequestScope Scope, string? WorkId, string? Isbn13,
    string? Title, string? Authors, string? Publisher, int? PublicationYear,
    bool PickFromStock, UserId By) : IRequest<ErrorOr<ManualBookRequestCreatedResult>>;
public sealed record ManualBookRequestCreatedResult(Guid RequestId, ManualBookRequestStatus Status);
public sealed record PickManualRequestFromStockCommand(Guid RequestId, UserId By) : IRequest<ErrorOr<Success>>;

internal static class ManualRequestStockPick
{
    // RG-82 : un exemplaire disponible (édition la plus fournie), sinon annoncé sur une bourse datée.
    public static Task<ErrorOr<Success>> PickAsync(IProjectDbContext db, ManualBookRequest request, UserId by, DateTime now, CancellationToken ct);
}
```

`PickAsync` : disponible → réutiliser `BookWithdrawal.WithdrawAsync(db, isbn, 1, "ManualRequest.StockPick", by, now, ct)`
(mouvement `Withdrawal` −1) puis `request.MarkFoundFromStock(isbn, null, null, movementId, by, now)` ;
sinon annonce `Announced` la plus récente avec `AssoEventsId` non nul : `Quantity > 1` →
`ApplyQuantityCorrection(Quantity - 1)`, sinon `Cancel()` ; mouvement `Correction` −1,
note `"Announcement.Correction: ManualRequest.StockPick"`, `AssoEventsId` de l'annonce ;
`request.MarkFoundFromStock(isbn, announcement.AssoEventsId, announcement.ScanSessionId, movement.Id, by, now)`.
Rien → `Errors.ManualRequest.NothingInStock()`.

- [ ] **Étape 1 : tests d'abord** :
  - `Add_WorkScope_CreatesSearchingRequest` ; `Add_EditionScope_RequiresValidIsbn`.
  - `Add_SameOpenTarget_ReturnsDuplicate` (message E4 « … cherche déjà ce livre. »).
  - `Add_OverWatchlistMaxItems_ReturnsLimitReached` (`RG-27`, `AssociationSettings.WatchlistMaxItems`).
  - `Add_ToErasedRequester_ReturnsRequesterErased`.
  - `Add_WithPickFromStock_WithdrawsOneAvailableAndMarksFound` (stock 2 → 1, mouvement
    `Withdrawal` −1, demande `Found`, origine `StockAvailable`).
  - `Pick_FromDatedAnnouncementWhenNothingAvailable` ; `Pick_WithNothing_ReturnsNothingInStock`.
  - `Pick_OnFoundRequest_ReturnsInvalidTransition`.
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation (transaction `Serializable` comme
  `AddWatchlistItemCommandHandler` pour l'unicité et la limite). **Étape 4 :** vert.
- [ ] **Étape 5 :** `rtk git commit -m "feat(manual-requests): add requests and pick from stock"`

---

### Tâche 8 : donné, refusé, annulé

**Files :**
- Create : `Application/ManualRequests/Commands/MarkGiven/…`, `Refuse/…`, `Cancel/…`
- Test : `Application.tests/ManualRequests/Commands/DecisionCommandHandlerTests.cs`

**Interfaces produites :**

```csharp
public sealed record MarkManualRequestGivenCommand(Guid RequestId, UserId By) : IRequest<ErrorOr<Success>>;
public sealed record RefuseManualRequestCommand(Guid RequestId, bool KeepSearching, UserId By) : IRequest<ErrorOr<ManualRequestReleaseResult>>;
public sealed record CancelManualRequestCommand(Guid RequestId, UserId By) : IRequest<ErrorOr<ManualRequestReleaseResult>>;
public sealed record ManualRequestReleaseResult(int NotifiedMemberCount);
```

- [ ] **Étape 1 : tests d'abord** :
  - `Given_ClosesWithoutStockOrSale` (`RG-83`) : stock et `SalesCount` inchangés, aucun
    mouvement `Sale`, aucune alerte.
  - `Given_OnSearching_ReturnsInvalidTransition`.
  - `Refuse_Close_ReleasesAndQueuesImmediateAlerts` : stock +1, `QueueForReleaseAsync`
    appelé avec le mouvement de remise et `releasedAt == now`, statut `Refused`,
    `NotifiedMemberCount` = valeur retournée par l'outbox substitué.
  - `Refuse_KeepSearching_ReturnsToSearchingAndNextScanServesIt`.
  - `Cancel_Searching_NoRelease` ; `Cancel_Found_ReleasesLikeRefusal` (`RG-86`).
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation, transaction comme
  `WithdrawNotFoundTargetCommandHandler`. **Étape 4 :** vert.
- [ ] **Étape 5 :** `rtk git commit -m "feat(manual-requests): give, refuse and cancel requests"`

---

### Tâche 9 : mise de côté au tri (`ScanBook`) et coordonnées pour la scanette

**Files :**
- Modify : `Application/Books/Commands/ScanBook/ScanBookCommandHandler.cs`,
  `Application/Books/Common/ScanBookResult.cs`,
  `Contracts/Books/Responses/ScanBookResponse.cs`, `Api/Controllers/BookController.cs`,
  `Application/Books/Queries/Admin/GetAdminVolunteerStatisticsQueryHandler.cs`,
  `Application/Books/Queries/GetVolunteerStatistics/GetVolunteerStatisticsQueryHandler.cs`
- Create : `Application/ManualRequests/Queries/GetScanCandidate/{GetManualRequestScanCandidateQuery, …Handler, ManualRequestScanCandidateResult}.cs`,
  `Contracts/ManualRequests/ManualRequestScanCandidateResponse.cs`
- Test : `Application.tests/Books/Commands/ScanBook/ScanBookCommandHandlerTests.cs`
  (nouveaux cas), `Application.tests/ManualRequests/Queries/ScanCandidateQueryHandlerTests.cs`

**Interfaces produites :**

```csharp
// ScanBookResult : nouveau dernier paramètre
bool HeldForManualRequest
// ScanBookResponse : nouveau dernier paramètre
bool HeldForManualRequest
// constructeur ScanBookCommandHandler : nouveau dernier paramètre optionnel
IBibliographicMetadataResolver? metadataResolver = null
public sealed record GetManualRequestScanCandidateQuery(string Isbn13) : IRequest<ErrorOr<ManualRequestScanCandidateResult?>>;
public sealed record ManualRequestScanCandidateResult(Guid RequestId, string FirstName, string LastName, string Phone);
```

Dans le handler, **avant** `BeginTransactionAsync` : si `command.Kept` et
`await ManualRequestMatcher.AnySearchingWorkRequestAsync(…)`, résoudre
`workId = await ManualRequestMatcher.ResolveWorkIdAsync(dbContext, metadataResolver, isbn13, ct)`
(sinon `workId = book?.WorkId` lu dans la transaction). Dans la transaction, après la
résolution de la fiche canonique et **seulement si `command.Kept`** :

```csharp
var heldRequest = await ManualRequestMatcher.FindOldestSearchingAsync(dbContext, isbn13, workId ?? book.WorkId, cancellationToken);
if (heldRequest is not null)
{
    movementType = BookMovementType.ManualRequestHold;
    // pas de RecordAvailableEntry / RecordAnnouncementEntry / BookAnnouncement (RG-79, RG-81)
}
```

puis, après la création du mouvement (quantité +1, mêmes champs qu'un scan gardé) :
`heldRequest.MarkFoundByScan(isbn13, session.Id, movement.Id, session.VolunteerId, occurredAt);`.
`session.RecordScan(true, …)` est inchangé (le livre compte comme gardé). Le verdict
renvoyé reste celui de `CalculateVerdict`. `BuildExistingResultAsync` renvoie
`HeldForManualRequest: movement.Type == BookMovementType.ManualRequestHold`.
Les `IsKeptScan` des deux handlers de statistiques bénévoles incluent `ManualRequestHold`.

`GetManualRequestScanCandidateQuery` : valider l'ISBN, résoudre le `WorkId` (fiche puis
resolver), `FindOldestSearchingAsync` en lecture, joindre le demandeur ; `null` si aucune
demande ou demandeur effacé. Endpoint dans `BookController` :
`GET /scan/manual-requests/candidate?isbn13=…` → `200` + `ManualRequestScanCandidateResponse(RequestId, FirstName, LastName, Phone)`
ou `204`, `.RequireAuthorization("Tri")`. Ajouter `ManualRequestHold` au mapping
`ScanBookResponse` (`MovementType` en chaîne).

- [ ] **Étape 1 : tests d'abord** (dans `ScanBookCommandHandlerTests`, étendre
  `ScanBookTestDbContext` avec `ManualRequesters`/`ManualBookRequests` et ajouter
  `fixture.CreateHandler(IBibliographicMetadataResolver? resolver)`) :
  - `Handle_KeptWithSearchingEditionRequest_HoldsCopyWithoutStock` : stock et annonces
    inchangés, mouvement `ManualRequestHold` +1, `HeldForManualRequest == true`, demande
    `Found`, `session.KeptCount == 1`.
  - `Handle_KeptUnknownBookWithWorkRequest_ResolvesWorkIdAndHolds` (resolver substitué,
    fiche créée par le scan).
  - `Handle_TwoCopies_SecondServesNextRequestThirdGoesToStock` (critère 6).
  - `Handle_Rejected_DoesNotHold` (« Écarter », `RG-16`).
  - `Handle_ReplayOfHeldGesture_IsIdempotent` (même `ClientGestureId` → `AlreadyProcessed`, `HeldForManualRequest`).
  - `Handle_NextFairSession_HoldsWithoutAnnouncement`.
  - `ScanCandidate_ReturnsOldestRequesterContact` ; `ScanCandidate_ErasedRequester_ReturnsNull`.
  - `Api.tests` : `EndpointAuthorizationTests` — `candidate` refusé sans rôle `Tri`.
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation. **Étape 4 :** toute la suite
  `ScanBook*` et `VolunteerStatistics*` verte.
- [ ] **Étape 5 :** `rtk git commit -m "feat(scan): hold copies for manual requests"`

---

### Tâche 10 : drapeau `isManuallyRequested` dans le delta de la scanette

**Files :**
- Modify : `Application/Books/Common/ScanCatalogBookResult.cs`,
  `Contracts/Books/Responses/ScanCatalogBookResponse.cs`,
  `Application/Books/Queries/GetCatalogDelta/GetCatalogDeltaQueryHandler.cs`,
  `Api/Controllers/BookController.cs` (mapping ligne ~596)
- Test : `Application.tests/Books/Queries/GetCatalogDeltaQueryHandlerTests.cs`

**Interfaces produites :** `ScanCatalogBookResult.IsManuallyRequested` et
`ScanCatalogBookResponse.IsManuallyRequested` (`bool`, ajouté juste après `IsWanted`).
Aucune donnée personnelle (`RG-80`).

- [ ] **Étape 1 : tests d'abord** :
  - `Delta_FullSync_FlagsBooksMatchingSearchingRequests` (édition et œuvre) ; une demande
    `Found`/`Given` ne lève pas le drapeau.
  - `Delta_Incremental_ReprojectsBooksWhoseManualRequestChanged` : une demande créée,
    trouvée ou annulée après le filigrane re-projette les fiches de sa cible (même
    mécanique que `recentWatchlistItems`, sur `ManualBookRequest.UpdatedAt`).
  - `Delta_Response_HasNoRequesterData` : aucun champ nom/téléphone dans
    `ScanCatalogBookResponse` (test de contrat par réflexion).
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation (`ManualTargets` calqué sur
  `WantedTargets`, filtré sur `Status == Searching`). **Étape 4 :** vert.
- [ ] **Étape 5 :** `rtk git commit -m "feat(scan): flag manually requested books in catalog delta"`

---

### Tâche 11 : corrections de session (`RG-85`)

**Files :**
- Modify : `Application/Books/Commands/Admin/RemoveScanSessionMovementCommandHandler.cs`,
  `CancelScanSessionCommandHandler.cs`
- Test : `Application.tests/Books/Commands/Admin/ScanSessionAdministrationCommandHandlerTests.cs`

- [ ] **Étape 1 : tests d'abord** :
  - `RemoveMovement_ManualRequestHold_RevertsRequestToSearching` : la demande repasse
    `Searching`, un mouvement `ManualRequestHold` −1 avec `ReversalOfMovementId`, stock
    inchangé.
  - `RemoveMovement_HoldOfGivenRequest_ReturnsAlreadyConsumed` (demande donnée ou refusée
    : `Errors.Book.SessionMovementAlreadyConsumed`, rien ne change).
  - `CancelSession_RevertsFoundHoldsAndSkipsClosedOnes` (Point d'attention 4).
  - `ReassignMode_LeavesHoldsUntouched` (les mouvements `ManualRequestHold` restent,
    demande toujours `Found`).
- [ ] **Étape 2 :** échec. **Étape 3 :** élargir les filtres
  `DirectEntry || AnnouncementEntry` des deux handlers à `ManualRequestHold` ; pour ce
  type, charger la demande par `HoldMovementId`, appeler `RevertScanHold(correctedAt)` si
  elle est `Found`, sinon renvoyer l'erreur (Remove) ou ignorer (Cancel) ; ajouter le
  mouvement inverse sans toucher au stock. `ReassignSessionModeCommandHandler` n'est pas
  modifié (son filtre exclut déjà le nouveau type) : le test le fige.
- [ ] **Étape 4 :** vert. **Étape 5 :** `rtk git commit -m "feat(manual-requests): revert holds on session corrections"`

---

### Tâche 12 : requêtes d'administration

**Files :**
- Create : `Application/ManualRequests/Queries/GetQueue/…`, `GetRequester/…`,
  `GetSummary/…`, `GetStock/…`
- Test : `Application.tests/ManualRequests/Queries/ManualRequestQueriesTests.cs`

**Interfaces produites :**

```csharp
public enum ManualRequestTab { Found, Searching, Closed }
public sealed record GetManualRequestQueueQuery(ManualRequestTab Tab, string? Search, int Page, int PageSize) : IRequest<ErrorOr<ManualRequestQueueResult>>;
public sealed record ManualRequestQueueResult(IReadOnlyList<ManualRequestQueueItem> Items, int TotalCount, int FoundCount, int SearchingCount, int ClosedCount, int FoundOverdueCount, int Page, int PageSize);
public sealed record ManualRequestQueueItem(
    Guid RequestId, Guid RequesterId, string? RequesterLastName, string? RequesterFirstName, string? RequesterPhone, bool RequesterErased,
    ManualBookRequestScope Scope, string? WorkId, string? Isbn13, string? Title, string? Authors, string? Publisher, int? PublicationYear,
    DateTime RequestedAt, ManualBookRequestStatus Status,
    ManualBookRequestFoundOrigin? FoundOrigin, string? FoundIsbn13, string? FoundPublisher, int? FoundPublicationYear, DateTime? FoundAt, string? FoundByName,
    int QuantityAvailable, int QuantityAnnounced, int WatcherCount,
    ManualRequestReleaseMode? ReleaseMode, DateTimeOffset? ReleaseFairStart,
    DateTime? ClosedAt, string? ClosedByName);
public sealed record GetManualRequesterQuery(Guid RequesterId) : IRequest<ErrorOr<ManualRequesterDetailResult>>;
public sealed record ManualRequesterDetailResult(Guid RequesterId, string? LastName, string? FirstName, string? Phone, bool Erased, DateTime ConsentRecordedAt, string? ConsentRecordedByName, IReadOnlyList<ManualRequestQueueItem> Requests);
public sealed record GetManualRequestSummaryQuery : IRequest<ErrorOr<ManualRequestSummaryResult>>;
public sealed record ManualRequestSummaryResult(int FoundCount, int FoundOverdueCount);
public sealed record GetManualRequestStockQuery(ManualBookRequestScope Scope, string? WorkId, string? Isbn13) : IRequest<ErrorOr<ManualRequestStockSnapshot>>;
```

Tris : `Found` par `FoundAt` croissant ; `Searching` par `RequestedAt` croissant ;
`Closed` par `ClosedAt` décroissant. `FoundOverdueCount` : trouvées depuis plus de
**14 jours** (`QM-2`). `Search` : nom, prénom, téléphone (chiffres seuls) et titre,
insensible à la casse. Les noms d'auteurs de gestes viennent de `Users` (prénom), comme
les listes F-11. `QuantityAvailable`/`QuantityAnnounced` (onglet `Searching`) via
`ManualRequestStock`, `WatcherCount`/`ReleaseMode`/`ReleaseFairStart` (onglet `Found`)
via `ManualRequestWatchers` et `ManualRequestRelease.PlanAsync`.
Le nombre de membres prévenus n'est pas mémorisé : l'onglet Closes affiche « Refusé le
{date} · {prénom} » (le « · 2 membres prévenus » de la maquette `AdminEtats` est
illustratif) ; le nombre réel apparaît dans le message qui suit le refus (tâche 19).

- [ ] **Étape 1 : tests d'abord** : tri et filtres de chaque onglet, recherche par
  téléphone `06 12` → `0612…`, compteurs, demandeur effacé (`RequesterErased`, noms
  nuls), `FoundOverdueCount`, plan de remise exposé, détail demandeur.
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation. **Étape 4 :** vert.
- [ ] **Étape 5 :** `rtk git commit -m "feat(manual-requests): add administration queries"`

---

### Tâche 13 : API d'administration et contrats

**Files :**
- Create : `Contracts/ManualRequests/ManualRequestAdministrationRequests.cs`,
  `ManualRequestAdministrationResponses.cs`,
  `Api/Controllers/ManualRequestAdministrationController.cs`
- Modify : `Api/Program.cs` (`app.UseManualRequestAdministrationController();` après
  `UseNotFoundReportAdministrationController`)
- Test : `Api.tests/ManualRequests/ManualRequestAdministrationEndpointsTests.cs`,
  `Api.tests/EndpointAuthorizationTests.cs`

Endpoints (tous `.RequireAuthorization("Administration")`, utilisateur courant via un
`TryGetUserId(principal, …)` identique à celui de `NotFoundReportAdministrationController`) :

| Verbe | Route | Corps / query | Réponse |
|---|---|---|---|
| GET | `/books/admin/manual-requests` | `tab=found\|searching\|closed`, `q`, `page`, `pageSize` | queue |
| GET | `/books/admin/manual-requests/summary` | — | `{foundCount, foundOverdueCount}` |
| GET | `/books/admin/manual-requests/stock` | `scope`, `workId`, `isbn13` | `{quantityAvailable, quantityAnnounced, nextFairStart}` |
| POST | `/books/admin/manual-requests/{id}/pick` | — | 204 |
| POST | `/books/admin/manual-requests/{id}/given` | — | 204 |
| POST | `/books/admin/manual-requests/{id}/refuse` | `{keepSearching}` | `{notifiedMemberCount}` |
| POST | `/books/admin/manual-requests/{id}/cancel` | — | `{notifiedMemberCount}` |
| GET | `/books/admin/manual-requesters/{id}` | — | détail |
| POST | `/books/admin/manual-requesters` | `{lastName, firstName, phone, consentGiven}` | 201 `{requesterId}` |
| PUT | `/books/admin/manual-requesters/{id}` | `{lastName, firstName, phone}` | 204 |
| DELETE | `/books/admin/manual-requesters/{id}` | — | `{cancelledRequestCount, notifiedMemberCount}` |
| POST | `/books/admin/manual-requesters/{id}/requests` | `{scope, workId, isbn13, title, authors, publisher, publicationYear, pickFromStock}` | 201 `{requestId, status}` |

Après un `refuse`, un `cancel` ou un `DELETE` réussis qui ont notifié au moins un membre,
appeler `BookAlertDeliveryService.ProcessPendingAsync` dans un `try/catch` journalisé :
un échec n'invalide pas la réponse (le Sweep horaire enverra).

Enums sérialisés en chaînes (`Searching`, `Found`, `Given`, `Refused`, `Cancelled`,
`Scan`, `StockAvailable`, `StockAnnounced`, `AvailableNow`, `NextFair`, `Work`,
`Edition`), comme les autres contrats admin.

- [ ] **Étape 1 : tests d'abord** (sur le modèle de
  `Api.tests/NotFoundReports/NotFoundReportAdministrationEndpointsTests.cs`) : chaque
  route répond 401/403 sans rôle `Administration` ; `POST requesters` sans consentement
  → 400 avec code `ManualRequest.ConsentRequired` ; `refuse` renvoie
  `notifiedMemberCount` ; la livraison immédiate en échec ne change pas le 200.
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation. **Étape 4 :** vert.
- [ ] **Étape 5 :** `rtk git commit -m "feat(manual-requests): expose administration endpoints"`

---

### Tâche 14 : effacement automatique (Worker, `RG-89`)

**Files :**
- Create : `Application/ManualRequests/Commands/EraseExpired/{EraseExpiredManualRequestersCommand, …Handler}.cs`
- Modify : `Worker/BookSweepFunction.cs`
- Test : `Application.tests/ManualRequests/Commands/EraseExpiredHandlerTests.cs`

**Interfaces produites :**
`public sealed record EraseExpiredManualRequestersCommand : IRequest<int>;` (nombre
d'effacements). Constante `RetentionAfterLastClosure = TimeSpan.FromDays(90)`.

- [ ] **Étape 1 : tests d'abord** : demandeur dont toutes les demandes sont closes depuis
  91 jours → effacé ; depuis 89 jours → conservé ; une demande ouverte → conservé ; sans
  aucune demande depuis plus de 90 jours après `CreatedAt` → effacé ; déjà effacé →
  ignoré.
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation ; dans `BookSweepFunction`, envoyer
  la commande après `IAccountDeletionService` et ajouter `ErasedManualRequesters` au log.
  `WorkerTriggerScheduleTests` reste inchangé.
- [ ] **Étape 4 :** vert. **Étape 5 :** `rtk git commit -m "feat(manual-requests): erase expired requester contacts"`

---

### Tâche 15 : Catalog — modèles, service d'API, route de section

**Files :**
- Modify : `src/Catalog/src/app/core/catalog.models.ts`,
  `core/catalog-admin-api.service.ts`, `core/catalog-administration-route.ts`
- Create : `features/administration/manual-requests/manual-request-format.ts`
- Test : `core/catalog-admin-api.service.spec.ts`, `core/catalog-administration-route.spec.ts`,
  `features/administration/manual-requests/manual-request-format.spec.ts`

**Interfaces produites :** types `CatalogManualRequestQueue`, `CatalogManualRequestItem`,
`CatalogManualRequester`, `CatalogManualRequestSummary`, `CatalogManualRequestStock`,
`CatalogManualRequestTab = 'found' | 'searching' | 'closed'` ; méthodes du service
(`getManualRequests`, `getManualRequestSummary`, `getManualRequestStock`,
`getManualRequester`, `createManualRequester`, `updateManualRequester`,
`deleteManualRequester`, `addManualBookRequest`, `pickManualRequest`,
`markManualRequestGiven`, `refuseManualRequest`, `cancelManualRequest`), même forme que
`getNotFoundQueue(accessToken, params)` ; section `'manual-requests'` ajoutée à
`CATALOG_ADMIN_SECTIONS` juste après `'not-found'`.

`manual-request-format.ts` :

```ts
// Affichage par groupes de deux chiffres : « 06 12 34 56 78 », « +33 6 12 34 56 78 ».
export function formatManualRequesterPhone(phone: string | null): string;
// « Jeanne MARTIN » ; « Demandeur effacé » si effacé.
export function formatManualRequesterName(firstName: string | null, lastName: string | null, erased: boolean): string;
// Lien tel: sans espaces.
export function manualRequesterPhoneHref(phone: string): string;
```

- [ ] **Étape 1 : tests d'abord** (URL, en-têtes, corps de chaque méthode avec
  `HttpTestingController` ; section reconnue ; formats `0612345678` → `06 12 34 56 78`,
  `+33612345678` → `+33 6 12 34 56 78`, nom effacé).
- [ ] **Étape 2 :** `npm test -- --watch=false` dans `src/Catalog` → échec.
  **Étape 3 :** implémentation. **Étape 4 :** vert.
- [ ] **Étape 5 :** `rtk git commit -m "feat(catalog): add manual request admin API client"`

---

### Tâche 16 : Catalog — barre latérale et liste « Demandes manuelles » (E1, E2)

**Maquette :** preview/Main.html — zones E1-barre-laterale, E1-entree-demandes-manuelles, E1-badge-a-remettre, E2-bouton-nouveau-demandeur, E2-onglets, E2-onglet-trouves, E2-onglet-en-recherche, E2-onglet-closes, E2-recherche, E2-ligne-trouvee, E2-origine-trouvaille, E2-demandeur, E2-lien-demandeur, E2-alerte-anciennete, E2-bouton-donne, E2-bouton-refuse, E2-encart-confidentialite

**Maquette :** preview/AdminEnRecherche.html — zones E2-ligne-en-recherche, E2-mention-deja-en-stock, E2-bouton-prelever, E2-bouton-annuler-demande

**Maquette :** preview/AdminEtats.html — zones E2-etat-vide, E2-etat-chargement, E2-etat-erreur, E2-ligne-close, E2-demandeur-efface, E5-toast

**Maquette :** preview/AdminDemandesMobile.html — zones E1-menu-mobile, E2-lien-appeler

**Files :**
- Create : `features/administration/manual-requests/manual-requests-view.component.{ts,html,scss,spec.ts}`
- Modify : `features/administration/catalog-administration-page.component.{ts,html,scss}`,
  sa spec, `feature` module qui déclare `NotFoundReportsViewComponent` (déclarer le
  nouveau composant au même endroit)

Calquer `not-found-reports/not-found-reports-view.component.*` : `@Input() accessToken`,
`@Output() countsChanged = new EventEmitter<CatalogManualRequestSummary>()`, signaux
`activeTab`, `queue`, `loading`, `error`, `success`, `search`, `page`.

- Barre latérale : entrée `{id: 'manual-requests', label: 'Demandes manuelles', icon: 'phone'}`
  dans le groupe *Pendant la bourse*, après *Livres introuvables*, **uniquement si
  `isAdministrator()`** ; `navBadge('manual-requests')` = `foundCount` si > 0, avec la
  classe de badge orange de *Livres introuvables*. Le compteur du menu mobile inclut ce
  badge.
- En-tête : kicker « Pendant la bourse », titre « Demandes manuelles. », texte « Livres
  recherchés par des personnes sans internet. Quand un livre est trouvé, appelez la
  personne puis indiquez s'il a été donné ou refusé. », bouton « Nouveau demandeur ».
- Onglets « Trouvés à remettre », « En recherche », « Closes » avec compteurs ; champ
  « Rechercher » (placeholder « Nom, téléphone ou titre »), recherche déclenchée après
  300 ms.
- Ligne trouvée : couverture, titre — auteur, pastille d'origine « Trouvé au tri par
  {prénom} · {date} » (pétrole) ou « Prélevé en stock par {prénom} · {date} » (bleu),
  « Édition mise de côté : {éditeur} · {année} », ISBN, « Demande : toutes les éditions /
  cette édition », lien nom du demandeur (ouvre E3), téléphone formaté (lien `tel:` sur
  mobile), « demandé le {date} », « · trouvé il y a {n} jours » en rouge brique au-delà
  de 14 jours, boutons « Donné » (orange) et « Refusé… ».
- Ligne en recherche : tableau Livre / Portée / Pour / Demandé le ; mention « Déjà en
  stock · {n} disponibles » quand `quantityAvailable + quantityAnnounced > 0`, bouton
  « Prélever » ; bouton « Annuler ».
- Onglet Closes : statut « Donné le … · {prénom} », « Refusé le … », « Annulé le … » ;
  « Demandeur effacé » en italique.
- États : vide « Aucun livre à remettre pour le moment. » + « Les livres trouvés au tri
  apparaîtront ici. » ; chargement (squelettes) ; erreur « Impossible de charger les
  demandes manuelles. Vérifiez la connexion puis réessayez. » + « Réessayer ».
- Encart : « Ces livres ne sont ni disponibles ni annoncés : aucun membre n'a été prévenu.
  Un refus les remet en stock et prévient aussitôt les membres qui les suivent. Les
  coordonnées sont effacées trois mois après la dernière demande close de la personne. »
- Messages (`E5-toast`, rôle `status`) : « Livre marqué comme donné », « Refus enregistré
  — {n} membres prévenus », « Exemplaire prélevé — demande trouvée ».

- [ ] **Étape 1 : tests d'abord** (spec du composant et de la page) : entrée absente pour
  un rôle `LivresRares` seul ; badge masqué à 0 ; changement d'onglet appelle l'API avec
  `tab` ; libellés exacts ci-dessus ; état vide/erreur ; « Prélever » appelle
  `pickManualRequest` puis recharge.
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation (variables `--catalog-*`
  existantes ; nouvelles variables `--catalog-verdict-hold` = `#0d5c63` et
  `--catalog-verdict-hold-soft` = `#e3f1f2` déclarées avec les autres couleurs).
- [ ] **Étape 4 :** vert ; contrôle visuel 1440 et 390 px contre les maquettes.
- [ ] **Étape 5 :** `rtk git commit -m "feat(catalog): list manual requests in administration"`

---

### Tâche 17 : Catalog — fiche demandeur (E3)

**Maquette :** preview/AdminDemandeur.html — zones E3-retour-liste, E3-formulaire-coordonnees, E3-champ-nom, E3-champ-prenom, E3-champ-telephone, E3-case-accord, E3-bouton-enregistrer, E3-liste-demandes, E3-bouton-ajouter-livre, E3-demande-trouvee, E3-statut-demande, E3-demande-close, E3-supprimer-demandeur

**Maquette :** preview/AdminEtats.html — zones E3-erreur-accord, E3-dialogue-suppression

**Maquette :** preview/AdminDemandeurMobile.html — zones E3-champ-nom, E3-case-accord, E3-supprimer-demandeur

**Files :**
- Create : `features/administration/manual-requests/manual-requester-detail.component.{ts,html,scss,spec.ts}`
- Modify : `manual-requests-view.component.*` (ouvre la fiche via le paramètre de
  requête `demandeur=<id>` de `/administration/manual-requests`, « Nouveau demandeur »
  ouvre la fiche vide)

- Retour « Demandes manuelles » ; kicker « Demandeur » ; titre « {Prénom} {NOM} » ;
  ligne « Accord recueilli par {prénom} le {date} · {n} demandes, {n} trouvée(s) ».
- Formulaire « Coordonnées » : « Nom », « Prénom », « Téléphone » (aide « Obligatoire ·
  fixe, mobile ou international »), case « La personne a accepté que l'association
  conserve ses coordonnées pour la rappeler au sujet de ces livres. » + « Obligatoire »,
  bouton « Enregistrer ». En modification, la case est cochée et désactivée.
- Erreur accord : « Sans l'accord de la personne, ses coordonnées ne peuvent pas être
  enregistrées. » (bordure rouge, `aria-invalid`). Téléphone invalide : « Ce numéro de
  téléphone n'est pas valide. »
- « Livres recherchés » + « Ajouter un livre » ; une ligne par demande avec statut
  (« Trouvé · à remettre », « En recherche », « Donné le … · {prénom} », « Refusé le … »,
  « Annulé le … ») et actions selon le statut (Donné / Refusé…, Prélever, Annuler).
- À la création, la fiche enchaîne sur l'ajout d'un premier livre (tâche 18).
- « Supprimer ce demandeur et ses demandes… » + aide « À la demande de la personne. Sans
  action, ses coordonnées sont effacées trois mois après la clôture de sa dernière
  demande. » ; dialogue « Supprimer {Prénom} {NOM} ? » avec le texte de la maquette
  (nombre de demandes annulées, livre trouvé remis en stock) et boutons « Annuler » /
  « Supprimer ».

- [ ] **Étape 1 : tests d'abord** : création bloquée sans accord, message exact ;
  enregistrement appelle `createManualRequester` puis ouvre l'ajout ; suppression
  confirmée appelle `deleteManualRequester` et revient à la liste.
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation. **Étape 4 :** vert + contrôle
  visuel 1440 / 390 px.
- [ ] **Étape 5 :** `rtk git commit -m "feat(catalog): manage a manual requester"`

---

### Tâche 18 : Catalog — ajouter un livre (E4)

**Maquette :** preview/AdminAjoutLivre.html — zones E4-dialogue-ajout, E4-recherche-referentiel, E4-resultats, E4-portee, E4-encart-deja-en-stock, E4-bouton-prelever, E4-bouton-ajouter

**Maquette :** preview/AdminEtats.html — zones E4-erreur-referentiel, E4-erreur-doublon, E4-edition-seule

**Maquette :** preview/AdminAjoutLivreMobile.html — zones E4-portee, E4-bouton-ajouter

**Files :**
- Create : `features/administration/manual-requests/manual-request-add-dialog.component.{ts,html,scss,spec.ts}`

- Kicker « Nouvelle demande », titre « Ajouter un livre pour {Prénom} {NOM} » ; champ
  « Rechercher un livre » (placeholder « Titre, auteur ou ISBN ») + « Chercher », via
  `GET /catalog/reference/search` (déjà utilisé par l'onglet Inventaire : réutiliser sa
  méthode du service public) ; légende « Tous les livres publiés · {n} résultats ».
- « Que cherche-t-elle ? » : « Toutes les éditions de cette œuvre · recommandé » (défaut)
  / « Uniquement cette édition » + ISBN. Notice sans `workId` : seule l'édition est
  proposée, cochée et désactivée, avec « Ce livre ne peut être suivi que dans cette
  édition. »
- Après sélection, `getManualRequestStock` ; si `quantityAvailable + quantityAnnounced > 0` :
  encart « Déjà à la bourse » / « {n} exemplaires disponibles · {n} annoncé pour le {date} »
  + « Prélever un exemplaire » (envoie `pickFromStock: true`).
- « Annuler » / « Ajouter la demande ». Erreurs : référentiel indisponible « La recherche
  de livres est momentanément indisponible. Réessayez dans quelques minutes. » ; code
  `ManualRequest.DuplicateRequest` → « {Prénom} {Nom} cherche déjà ce livre. »
- Mobile : plein écran avec barre d'actions fixe en bas (maquette mobile).

- [ ] **Étape 1 : tests d'abord** : portée par défaut œuvre ; notice sans œuvre ; encart
  masqué si stock nul ; « Prélever un exemplaire » envoie `pickFromStock: true` ;
  messages d'erreur exacts.
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation. **Étape 4 :** vert + contrôle visuel.
- [ ] **Étape 5 :** `rtk git commit -m "feat(catalog): add a book to a manual requester"`

---

### Tâche 19 : Catalog — donné, refusé, annulé (E5)

**Maquette :** preview/AdminDonne.html — zones E5-dialogue-donne, E5-bouton-confirmer-donne

**Maquette :** preview/AdminRefus.html — zones E5-dialogue-refus, E5-avertissement-alertes, E5-mise-a-disposition, E5-choix-suite, E5-bouton-confirmer-refus

**Maquette :** preview/AdminEtats.html — zones E5-variante-annonce, E2-dialogue-annulation-trouvee, E5-toast

**Maquette :** preview/AdminRefusMobile.html — zones E5-choix-suite, E5-bouton-confirmer-refus

**Files :**
- Create : `features/administration/manual-requests/manual-request-decision-dialog.component.{ts,html,scss,spec.ts}`
  (modes `given`, `refuse`, `cancel-found`, sur le modèle de
  `not-found-report-close-dialog.component.*`, avec focus piégé et retour du focus)

- Donné : kicker « Remise », titre « {Titre} a été remis à {Prénom} {NOM} ? », pastille
  édition + trouvaille, texte « L'exemplaire sort définitivement du stock de la bourse.
  Il n'est pas compté comme une vente. La demande est close. », « Annuler » /
  « Confirmer le don ».
- Refusé : kicker « Refus », titre « {Prénom} {NOM} ne prend pas {Titre} », avertissement
  « Le livre rejoint le stock et {n} membres qui le suivent seront prévenus par e-mail dès
  maintenant. » (`watcherCount` ; à 0 : « Le livre rejoint le stock. Aucun membre ne le
  suit. »), « Mise à disposition » : pastille « Disponible maintenant » ou « Annoncé ·
  bourse du {date} » selon `releaseMode`, aide « Mode de la session de scan du {date}.
  Non modifiable. » ; « Et ensuite ? » : « Clore la demande » (défaut, « Elle ne cherche
  plus ce livre. ») / « Continuer à chercher ce livre pour elle » (« Autre édition,
  exemplaire abîmé… La demande garde sa date du {date}. ») ; « Annuler » / « Confirmer le
  refus ».
- Annulation d'une demande trouvée : « Annuler la demande de {Prénom} {NOM} ? », texte de
  la maquette, « Garder la demande » / « Annuler la demande ». Une demande en recherche
  s'annule sans dialogue riche (confirmation simple).
- Mobile : feuille en bas d'écran.

- [ ] **Étape 1 : tests d'abord** : textes exacts ; « Confirmer le refus » envoie
  `keepSearching` selon le choix ; message « Refus enregistré — {n} membres prévenus »
  avec la valeur renvoyée ; le compteur de la barre latérale est rafraîchi
  (`countsChanged`).
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation. **Étape 4 :** vert + contrôle visuel.
- [ ] **Étape 5 :** `rtk git commit -m "feat(catalog): give, refuse or cancel manual requests"`

---

### Tâche 20 : Catalog — tuile du tableau de bord (E7)

**Maquette :** preview/AdminTableauDeBord.html — zones E7-tuile-a-remettre, E7-mention-anciennete, E7-lien-liste

**Maquette :** preview/AdminTableauDeBordMobile.html — zones E7-tuile-a-remettre

**Files :**
- Modify : `features/administration/catalog-administration-page.component.{ts,html,scss,spec.ts}`

À côté de la tuile `not-found-dashboard-tile`, et seulement si `foundCount > 0` : kicker
« Demandes manuelles » (icône téléphone, pétrole), « {n} livres à remettre », mention
« dont {n} trouvé(s) il y a plus de 14 jours » si `foundOverdueCount > 0`, lien « Ouvrir
la liste → » vers la section. Le résumé est chargé avec celui des livres introuvables.

- [ ] **Étape 1 : tests d'abord** (tuile absente à 0, présente sinon, clic → section).
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation. **Étape 4 :** vert.
- [ ] **Étape 5 :** `rtk git commit -m "feat(catalog): show books to hand over on the dashboard"`

---

### Tâche 21 : Scan — verdict « À mettre de côté » (E6)

**Maquette :** preview/ScanMiseDeCote.html — zones E6-verdict-mise-de-cote, E6-encart-demandeur, E6-recherche-par, E6-bouton-ecarter, E6-bouton-mettre-de-cote

**Maquette :** preview/ScanMiseDeCoteHorsLigne.html — zones E6-encart-hors-ligne

**Maquette :** preview/ScanCoordonneesChargement.html — zones E6-encart-chargement, E6-signal-rare

**Files :**
- Create : `src/Scan/src/app/offline/scan-manual-request.service.ts` (+ spec)
- Modify : `offline/scan-offline.model.ts`, `offline/scan-api.service.ts`,
  `offline/scan-verdict.service.ts`, `offline/scan-sync.service.ts` (fusion du delta,
  ligne ~669), `offline/scan-local-store.service.ts` (valeur par défaut, ligne ~953),
  `scanner/scanner.component.{ts,html,scss}` et leurs specs

**Interfaces produites :**

```ts
// scan-offline.model.ts
export type LocalBookVerdict = 'ManualRequest' | 'Wanted' | 'Selling' | 'TooMany' | 'FirstCopy';
export interface ScanCatalogBook { /* … */ isManuallyRequested?: boolean; }
export interface ScanManualRequestCandidate { requestId: string; firstName: string; lastName: string; phone: string; }
export interface ScanBookResponse { /* … */ heldForManualRequest: boolean; }

// scan-api.service.ts
getManualRequestCandidate(isbn13: string): Observable<ScanManualRequestCandidate | null>; // 204 → null

// scan-manual-request.service.ts
export type ScanManualRequestLookup =
  | {state: 'none'} | {state: 'loading'} | {state: 'found'; candidate: ScanManualRequestCandidate} | {state: 'unavailable'};
lookup(isbn13: string, online: boolean): Promise<ScanManualRequestLookup>;
```

- `ScanVerdictService.calculate` : `book?.isManuallyRequested === true` → `'ManualRequest'`
  avant toute autre règle (`RG-79`) ; un ancien cache sans le champ vaut `false`.
- Au scan en tri : si en ligne, lancer `lookup` en parallèle de la notice ; un candidat
  trouvé passe aussi le verdict à `ManualRequest` (cas du livre absent du cache local).
  Le candidat n'est **jamais** écrit dans IndexedDB ni dans les exports de diagnostic
  (`RG-80`).
- `activeVerdictKey` : `'ManualRequest'` prime sur `'Rare'` ; quand `isRare`, afficher en
  plus la bande violette « Fiche livre rare liée » sous le verdict.
- Bloc de verdict pétrole, icône personne, titre « À mettre de côté », sous-titre
  « Demande manuelle ». Encart « Pour » : en ligne « {Prénom} {NOM} » + téléphone formaté
  (groupes de deux chiffres) ; chargement : squelettes + « Chargement des coordonnées… » ;
  hors ligne, introuvable ou erreur : « Coordonnées dans l'administration » /
  « Administration → Demandes manuelles ». La mise de côté n'est jamais bloquée.
- Le reste de l'écran est inchangé (« Recherché par » garde son calcul actuel).
- Pied : « Scanner le suivant vaut « mettre de côté » » ; boutons « Écarter » / « Mettre
  de côté » (même action que « Garder »).
- Variables CSS `--scan-verdict-hold: #0d5c63` et `--scan-verdict-hold-soft: #e3f1f2` à
  côté des autres couleurs de verdict.

- [ ] **Étape 1 : tests d'abord** :
  - `ScanVerdictService` : `isManuallyRequested` prime sur `isWanted` et sur les ventes ;
    champ absent → règles actuelles.
  - `ScanManualRequestService` : 200 → `found` ; 204 → `none` ; erreur réseau ou
    hors ligne → `unavailable`.
  - Composant : titre « À mettre de côté », encart en ligne / hors ligne / chargement,
    bouton « Mettre de côté », bande livre rare, aucun appel `candidate` en mode caisse.
  - `scan-local-store` : aucune clé contenant `phone`, `firstName` ou `lastName` après un
    scan avec candidat.
- [ ] **Étape 2 :** `npm test -- --watch=false` dans `src/Scan` → échec.
  **Étape 3 :** implémentation. **Étape 4 :** vert + contrôle visuel à 390 px.
- [ ] **Étape 5 :** `rtk git commit -m "feat(scan): show set-aside verdict for manual requests"`

---

### Tâche 22 : Scan — résultat de la mise de côté et fin de session (E6)

**Maquette :** preview/ScanFinSession.html — zones E6-recap-mis-de-cote, E6-recap-non-attribue

**Files :**
- Modify : `offline/scan-offline.model.ts` (`ScanOutboxEntry.manualRequestOutcome?: 'Held' | 'Missed' | null`,
  `ScanSessionCounts.heldForManualRequestCount: number`, `ScanSessionCounts.manualRequestMissedIsbns: string[]`),
  `offline/scan-sync.service.ts`, `offline/scan-local-store.service.ts`
  (`getSessionCounts`), `offline/scan-workflow.service.ts`, `scanner/scanner.component.{ts,html}`

À la réponse d'un `POST /scan/sessions/{id}/scans` : `heldForManualRequest` → `'Held'` ;
geste gardé dont le verdict local était `ManualRequest` mais non retenu → `'Missed'`.
Fin de session : « {n} livres mis de côté pour des demandes manuelles. La présidente les
retrouve dans l'administration. » (si n > 0) ; pour chaque ISBN manqué, « {Titre} n'est
plus attendu : la demande a été servie par un autre poste. Remettez-le avec les livres
gardés. » (titre du catalogue local, ISBN à défaut).

- [ ] **Étape 1 : tests d'abord** : sync qui marque `Held`/`Missed` ; compteurs ; textes
  exacts du récapitulatif ; un ancien enregistrement sans le champ compte pour 0.
- [ ] **Étape 2 :** échec. **Étape 3 :** implémentation. **Étape 4 :** vert + contrôle visuel 390 px.
- [ ] **Étape 5 :** `rtk git commit -m "feat(scan): summarise manual request holds at session end"`

---

### Tâche 23 : documentation fonctionnelle et RGPD

**Files :**
- Modify : `docs/bourse-aux-livres/06-regles-metier.md` (renvoi vers `RG-77`…`RG-89` de la
  spec, exception à `RG-42` notée sous `RG-42`), `docs/bourse-aux-livres/07-exigences-non-fonctionnelles.md`
  (`ENF-10` : téléphone collecté pour les seuls demandeurs manuels),
  `docs/bourse-aux-livres/09-rgpd-compte-et-droits.md` (nouveau traitement : finalité,
  base légale = consentement oral tracé, durée 3 mois, accès), la page légale
  « Données personnelles » du Catalog (`features/legal/…`, même paragraphe en clair)
  et sa spec, `NEXT.md` (état de la feature).

- [x] **Étape 1 :** test de la page légale (le paragraphe « Demandes saisies pour des
  personnes sans compte » est présent). **Étape 2 :** échec. **Étape 3 :** rédaction.
  **Étape 4 :** vert.
- [x] **Étape 5 :** `rtk git commit -m "docs(manual-requests): document rules and personal data"`

---

### Tâche 24 : validation complète et PR

- [ ] `dotnet build src/Backend/Vole_Papillon_Damour.slnx` ; `dotnet test` sur les cinq
  projets `*.tests`.
- [ ] `npm test -- --watch=false` et `npm run build` dans `src/Catalog` et `src/Scan`.
- [ ] Contrôle visuel E1–E5, E7 à 1440 et 390 px, E6 à 390 px, contre
  `maquettes/preview/` ; aucun défilement horizontal.
- [ ] `python .claude/skills/feature/scripts/check_zones.py docs/features/manual-book-requests`
- [ ] `graphify update .`
- [ ] `rtk git fetch origin main`, `rtk git rebase origin/main`, relancer la suite.
- [ ] Push et PR selon `implement-handoff` ; « Risks and follow-up » : arbitrages du plan,
  migration `AddManualBookRequests` non appliquée, échecs de la tâche 0, écarts.

---

## Zones non implémentées

Aucune : toutes les zones des maquettes sont couvertes. Précisions :

- `E6-recherche-par` : la ligne existe déjà ; elle n'est **pas** modifiée (le « 3 membres »
  de la maquette est illustratif, la valeur reste celle du calcul actuel).
- `E1-barre-laterale` : seule l'entrée `E1-entree-demandes-manuelles` et son badge sont
  ajoutés ; le reste de la barre est inchangé.
