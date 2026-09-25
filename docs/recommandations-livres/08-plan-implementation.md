# Recommandations de livres — plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal :** proposer sous chaque fiche des livres proches (« Dans le même esprit »), et à un
membre connecté des suggestions tirées de ses achats associés (« Pour vous »),
désactivables.

**Architecture :** un traitement nocturne du worker lit un profil bibliographique par
édition (BnF + Open Library), compose un texte, le vectorise avec Azure OpenAI
(`text-embedding-3-small`, 512 dimensions), puis calcule en mémoire les 30 meilleurs voisins
de chaque livre (cosinus + bonus série/auteur/forme − pénalité de public). Il les écrit
dans une nouvelle génération de la table `BookNeighbors`. L'API ne fait que lire SQL : elle
filtre le stock, applique le seuil et projette les cartes existantes. Le frontend
`src/Catalog` ajoute trois surfaces et une préférence.

**Tech Stack :** .NET 10, EF Core 10 (SQL Server), MediatR, ErrorOr,
Microsoft.Extensions.AI (+ Azure.AI.OpenAI, `DefaultAzureCredential`),
System.Numerics.Tensors, Azure Functions (worker), xUnit + FluentAssertions + SQLite
(tests), Angular 21 + Karma/Jasmine, Bicep.

**Spec :** [`07-specification.md`](07-specification.md) — elle fait foi. Méthode et
chiffres : [`05-benchmark.md`](05-benchmark.md) §7.
**Maquettes :** [`maquettes/index.html`](maquettes/index.html) (export HTML du canvas
[Claude Design](https://claude.ai/artifact/GYWEeUku7qofkTt5pdKtoM)). Chaque tâche UI cite
une page `maquettes/html/<Écran>.html` et ses zones `data-zone="…"`. Ouvrir la page dans
un navigateur et chercher l'attribut dans le source.

## Global Constraints

- Couches du dépôt : `Domain` → `Application` (MediatR, ErrorOr) → `Infrastructure` → `Api` / `Worker`. Aucune nouvelle technologie de stockage (décision D2 : **pas de base graphe, pas de type SQL `VECTOR`**).
- Tests d'abord pour tout code exécutable (`AGENTS.md`, `.github/skills/tdd-workflow/SKILL.md`).
- Nouveaux `DbSet` dans `IProjectDbContext` : **avec une implémentation par défaut `=> throw new NotSupportedException();`**, comme `MemberSelectionItems`, pour ne casser aucun `DbContext` de test existant.
- **Aucune donnée personnelle ne part vers Azure OpenAI** : seul `BookSimilarityProfile.ProfileText` est vectorisé.
- Constantes du calcul, exactement : bonus même série `0.20f`, tome suivant `0.10f`, même auteur `0.08f`, même forme `0.03f`, pénalité publics opposés `0.10f`, 30 voisins par livre, 512 dimensions.
- Seuils d'affichage par défaut : `SimilarMinScore = 0.50`, `SimilarMaxCount = 5`, `SimilarMinCount = 2`, `PersonalMaxCount = 8`, `PersonalMinCount = 3`, 20 graines au plus.
- Tout est désactivé tant que `Recommendations:Enabled` vaut `false` : le worker ne fait rien, les endpoints renvoient une liste vide.
- Textes d'interface : ceux des maquettes, mot pour mot.
- Mobile 390 px : aucune barre de défilement horizontale de page ; seules les rangées de cartes défilent.
- Commandes git préfixées par `rtk` (`.github/skills/delivery-workflow/SKILL.md`).

## Review Focus

1. **Deux tomes d'une même série confondus avec une même œuvre** : la BnF donne parfois à tous les tomes l'identifiant `500$3` de la série. Attendu : le tome 2 reste un voisin (étiquette « Tome suivant »). Test en tâche 2 (`SameWork_WithDifferentTomes_IsFalse`).
2. **Traducteur ou préfacier pris pour l'auteur** : zone 702 avec `$4 = 730`. Attendu : ignoré. Une 702 **sans** `$4` est gardée : c'est le cas des notices récentes. Tests en tâche 3.
3. **Public mal déduit** (*Percy Jackson* en collection « Wiz » déduit adulte) : jamais d'exclusion, seulement une pénalité. Attendu : *Harry Potter* peut rester voisin. Test en tâche 2 (`OppositeAudience_IsPenalizedNotExcluded`).
4. **Voisin épuisé et non annoncé, ou masqué, ou fusionné** : jamais affiché. Tests en tâches 9 et 10.
5. **Préférence désactivée** : l'API ne calcule rien et renvoie `Disabled`, l'interface n'affiche rien. Tests en tâches 10 et 12.

## Fichiers

| Fichier | Rôle |
|---|---|
| `Domain/RecommendationAggregate/BookSimilarityProfile.cs` | Profil d'une édition : notice JSON, texte composé, empreinte, vecteur |
| `Domain/RecommendationAggregate/BookNeighbor.cs` | Un voisin d'une génération |
| `Domain/RecommendationAggregate/RecommendationGeneration.cs` | Ligne unique : génération courante |
| `Domain/RecommendationAggregate/MemberRecommendationPreference.cs` | Préférence d'un membre |
| `Domain/RecommendationAggregate/ValueObjects/NeighborReason.cs` | `NextTome`, `SameSeries`, `SameAuthor`, `Theme` |
| `Application/Recommendations/Similarity/SimilarityText.cs` | Normalisation, nettoyage des résumés |
| `Application/Recommendations/Similarity/SimilarityEdition.cs` | La notice lue aux sources (sérialisée dans le profil) |
| `Application/Recommendations/Similarity/SimilarityFeatures.cs` | Déductions : public, forme, série, œuvre, texte composé |
| `Application/Recommendations/Similarity/WorkGrouping.cs` | Regroupement par œuvre, résumé d'œuvre, public majoritaire |
| `Application/Recommendations/Similarity/NeighborComputer.cs` | Score et 30 meilleurs voisins |
| `Application/Common/Interfaces/Services/IBibliographicNoticeReader.cs`, `IBookEmbeddingService.cs`, `IBookNeighborWriter.cs`, `IRecommendationSettings.cs` | Ports |
| `Application/Recommendations/Commands/RefreshBookSimilarityProfiles/*` | Lecture des notices |
| `Application/Recommendations/Commands/RecomputeBookNeighbors/*` | Vecteurs et voisins |
| `Application/Recommendations/Commands/SetRecommendationPreference/*` | Préférence |
| `Application/Recommendations/Queries/GetSimilarBooks/*` | Fiche |
| `Application/Recommendations/Queries/GetMyRecommendations/*` | Pour vous et préférence |
| `Infrastructure/Services/Recommendations/*` | BnF/OL, Azure OpenAI, SqlBulkCopy, options |
| `Infrastructure/Persistence/Configurations/*Recommendation*.cs`, migration `AddBookRecommendations` | Tables |
| `Api/Controllers/BookController.cs`, `MemberAccountController.cs`, `Contracts/Recommendations/*` | Endpoints |
| `Worker/RecommendationsFunction.cs` | Traitement nocturne |
| `infra/modules/AiFoundry/aiFoundry.module.bicep`, `infra/main.bicep`, `infra/parameters/main.dev.bicepparam` | Déploiement d'embedding, variables |
| `src/Catalog/src/app/...` | Surfaces R1 à R8 |

Chemins backend relatifs à `src/Backend/Vole_Papillon_Damour.` ; les projets de test portent le suffixe `.tests`.

---

### Tâche 0 : préparer l'espace de travail

- [ ] **Étape 1 : vérifier le prérequis ISBN-10.** La lecture des notices utilise l'index `bib.fuzzyISBN` (correctif `fix/bnf-isbn10-lookup`, `docs/bourse-aux-livres/plan/07-recherche-bnf-isbn10.md`).

```powershell
rtk git fetch origin main
rtk git log origin/main --oneline | Select-String "fuzzy ISBN"
```

Si le correctif n'est pas encore dans `origin/main`, continuer quand même : la tâche 3 écrit la requête `bib.fuzzyISBN` elle-même, sans dépendre de `BnfSruClient.IsbnQuery`.

- [ ] **Étape 2 : constater l'état de départ.**

```powershell
dotnet build src/Backend/Vole_Papillon_Damour.slnx
dotnet test src/Backend/Vole_Papillon_Damour.slnx
cd src/Catalog; npm ci; npx ng test --watch=false --browsers=ChromeHeadless; cd ../..
```

Noter tout échec préexistant, sans le corriger.

---

### Tâche 1 : entités du domaine

**Files :**
- Create : `Domain/RecommendationAggregate/ValueObjects/NeighborReason.cs`, `BookSimilarityProfile.cs`, `BookNeighbor.cs`, `RecommendationGeneration.cs`, `MemberRecommendationPreference.cs`
- Test : `Domain.tests/RecommendationAggregateTests/BookSimilarityProfileTests.cs`, `MemberRecommendationPreferenceTests.cs`

**Interfaces :**
- Produces : `BookSimilarityProfile.Create(string isbn13)`, `.RecordNotice(string? noticeJson, bool found, DateTime fetchedAt)`, `.SetProfileText(string text, string textHash)`, `.RecordEmbedding(byte[] embedding, string textHash, DateTime embeddedAt)`, `bool NeedsEmbedding`; `BookNeighbor(Guid generationId, string isbn13, byte rank, string neighborIsbn13, float score, NeighborReason reason)`; `RecommendationGeneration.Singleton()`, `.Switch(Guid generationId, int bookCount, DateTime computedAt)`; `MemberRecommendationPreference.Create(UserId userId, bool enabled, DateTime at)`, `.Set(bool enabled, DateTime at)`.

- [ ] **Étape 1 : tests**

```csharp
// Domain.tests/RecommendationAggregateTests/BookSimilarityProfileTests.cs
using FluentAssertions;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Domain.tests.RecommendationAggregateTests;

public sealed class BookSimilarityProfileTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 3, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void NewProfile_NeedsNoEmbeddingUntilItHasText()
    {
        var profile = BookSimilarityProfile.Create("9782742793099");

        profile.NeedsEmbedding.Should().BeFalse();
        profile.SetProfileText("Les hommes qui n'aimaient pas les femmes. Auteur : Stieg Larsson", "hash-1");
        profile.NeedsEmbedding.Should().BeTrue();
    }

    [Fact]
    public void RecordEmbedding_ForCurrentText_ClearsNeedsEmbedding()
    {
        var profile = BookSimilarityProfile.Create("9782742793099");
        profile.SetProfileText("texte", "hash-1");

        profile.RecordEmbedding(new byte[2048], "hash-1", Now);

        profile.NeedsEmbedding.Should().BeFalse();
    }

    [Fact]
    public void ChangingText_AfterEmbedding_NeedsEmbeddingAgain()
    {
        var profile = BookSimilarityProfile.Create("9782742793099");
        profile.SetProfileText("texte", "hash-1");
        profile.RecordEmbedding(new byte[2048], "hash-1", Now);

        profile.SetProfileText("texte modifié", "hash-2");

        profile.NeedsEmbedding.Should().BeTrue();
    }

    [Fact]
    public void RecordEmbedding_WithWrongSize_Throws()
    {
        var profile = BookSimilarityProfile.Create("9782742793099");
        profile.SetProfileText("texte", "hash-1");

        var act = () => profile.RecordEmbedding(new byte[100], "hash-1", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithInvalidIsbn_Throws()
    {
        var act = () => BookSimilarityProfile.Create("123");

        act.Should().Throw<ArgumentException>();
    }
}
```

```csharp
// Domain.tests/RecommendationAggregateTests/MemberRecommendationPreferenceTests.cs
using FluentAssertions;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.RecommendationAggregateTests;

public sealed class MemberRecommendationPreferenceTests
{
    [Fact]
    public void Set_ChangesValueAndDate()
    {
        var first = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);
        var preference = MemberRecommendationPreference.Create(UserId.Create(Guid.NewGuid()), false, first);

        preference.Set(true, first.AddHours(1));

        preference.Enabled.Should().BeTrue();
        preference.UpdatedAt.Should().Be(first.AddHours(1));
    }
}
```

- [ ] **Étape 2 : vérifier l'échec** — `dotnet test src/Backend/Vole_Papillon_Damour.Domain.tests --filter "FullyQualifiedName~RecommendationAggregateTests"`. Attendu : échec de compilation, types absents.

- [ ] **Étape 3 : implémenter**

```csharp
// Domain/RecommendationAggregate/ValueObjects/NeighborReason.cs
namespace Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

public enum NeighborReason : byte
{
    NextTome = 1,
    SameSeries = 2,
    SameAuthor = 3,
    Theme = 4,
}
```

```csharp
// Domain/RecommendationAggregate/ValueObjects/Isbn13Check.cs
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

public static class Isbn13Check
{
    public static bool IsCanonical(string? value) =>
        value is { Length: 13 } && Isbn13.TryCreate(value, out var isbn) && isbn.Value == value;
}
```

```csharp
// Domain/RecommendationAggregate/BookSimilarityProfile.cs
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

/// <summary>Profil de similarité d'une édition : notice lue aux sources, texte vectorisé, vecteur.</summary>
public sealed class BookSimilarityProfile : Entity<string>
{
    public const int Dimensions = 512;
    public const int EmbeddingBytes = Dimensions * sizeof(float);

    public string Isbn13 => Id;
    public string? NoticeJson { get; private set; }
    public bool NoticeFound { get; private set; }
    public DateTime? NoticeFetchedAt { get; private set; }
    public string? ProfileText { get; private set; }
    public string? ProfileTextHash { get; private set; }
    public byte[]? Embedding { get; private set; }
    public string? EmbeddedTextHash { get; private set; }
    public DateTime? EmbeddedAt { get; private set; }

    public bool NeedsEmbedding => ProfileTextHash is not null && ProfileTextHash != EmbeddedTextHash;

    private BookSimilarityProfile(string isbn13) : base(isbn13)
    {
    }

    private BookSimilarityProfile()
    {
    }

    public static BookSimilarityProfile Create(string isbn13)
    {
        if (!Isbn13Check.IsCanonical(isbn13))
        {
            throw new ArgumentException("A canonical ISBN-13 is required.", nameof(isbn13));
        }

        return new BookSimilarityProfile(isbn13);
    }

    public void RecordNotice(string? noticeJson, bool found, DateTime fetchedAt)
    {
        NoticeJson = noticeJson;
        NoticeFound = found;
        NoticeFetchedAt = DomainTime.RequireUtc(fetchedAt, nameof(fetchedAt));
    }

    public void SetProfileText(string text, string textHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(textHash);
        ProfileText = text;
        ProfileTextHash = textHash;
    }

    public void RecordEmbedding(byte[] embedding, string textHash, DateTime embeddedAt)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Length != EmbeddingBytes)
        {
            throw new ArgumentException($"An embedding must hold {Dimensions} float32 values.", nameof(embedding));
        }

        Embedding = embedding;
        EmbeddedTextHash = textHash;
        EmbeddedAt = DomainTime.RequireUtc(embeddedAt, nameof(embeddedAt));
    }
}
```

```csharp
// Domain/RecommendationAggregate/BookNeighbor.cs
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

/// <summary>Un voisin d'un livre dans une génération de calcul. Modèle de lecture, écrit en masse.</summary>
public sealed class BookNeighbor
{
    public Guid GenerationId { get; private set; }
    public string Isbn13 { get; private set; } = string.Empty;
    public byte Rank { get; private set; }
    public string NeighborIsbn13 { get; private set; } = string.Empty;
    public float Score { get; private set; }
    public NeighborReason Reason { get; private set; }

    private BookNeighbor()
    {
    }

    public BookNeighbor(Guid generationId, string isbn13, byte rank, string neighborIsbn13, float score, NeighborReason reason)
    {
        GenerationId = generationId;
        Isbn13 = isbn13;
        Rank = rank;
        NeighborIsbn13 = neighborIsbn13;
        Score = score;
        Reason = reason;
    }
}
```

```csharp
// Domain/RecommendationAggregate/RecommendationGeneration.cs
using Vole_Papillon_Damour.Domain.Common;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

/// <summary>Ligne unique (Id = 1) : la génération de voisins que l'API lit.</summary>
public sealed class RecommendationGeneration
{
    public byte Id { get; private set; } = 1;
    public Guid? CurrentGenerationId { get; private set; }
    public DateTime? ComputedAt { get; private set; }
    public int BookCount { get; private set; }

    private RecommendationGeneration()
    {
    }

    public static RecommendationGeneration Singleton() => new();

    public void Switch(Guid generationId, int bookCount, DateTime computedAt)
    {
        CurrentGenerationId = generationId;
        BookCount = bookCount;
        ComputedAt = DomainTime.RequireUtc(computedAt, nameof(computedAt));
    }
}
```

```csharp
// Domain/RecommendationAggregate/MemberRecommendationPreference.cs
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

/// <summary>N'existe que si le membre a changé la valeur par défaut (activée).</summary>
public sealed class MemberRecommendationPreference
{
    public UserId UserId { get; private set; } = null!;
    public bool Enabled { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private MemberRecommendationPreference()
    {
    }

    public static MemberRecommendationPreference Create(UserId userId, bool enabled, DateTime at)
    {
        ArgumentNullException.ThrowIfNull(userId);
        return new MemberRecommendationPreference
        {
            UserId = userId,
            Enabled = enabled,
            UpdatedAt = DomainTime.RequireUtc(at, nameof(at)),
        };
    }

    public void Set(bool enabled, DateTime at)
    {
        Enabled = enabled;
        UpdatedAt = DomainTime.RequireUtc(at, nameof(at));
    }
}
```

- [ ] **Étape 4 : vérifier** — même commande. Attendu : 6 tests réussis.
- [ ] **Étape 5 : commit** — `rtk git commit -m "feat(recommendations): add similarity profile, neighbor and preference entities"`

---

### Tâche 2 : le cœur de calcul (pur, sans I/O)

C'est la traduction exacte de la méthode H6 du benchmark
(`tools/recommendation-benchmark/bench/features.py`, `methods.py`). **Lire ces deux fichiers
avant de commencer.**

**Files :**
- Create : `Application/Recommendations/Similarity/SimilarityText.cs`, `SimilarityEdition.cs`, `SimilarityFeatures.cs`, `WorkGrouping.cs`, `NeighborComputer.cs`
- Modify : `Directory.Packages.props` (ajouter `<PackageVersion Include="System.Numerics.Tensors" Version="10.0.0" />`, ou la dernière 10.0.x), `Application/Vole_Papillon_Damour.Application.csproj` (`<PackageReference Include="System.Numerics.Tensors" />`)
- Test : `Application.tests/Recommendations/Similarity/SimilarityTextTests.cs`, `SimilarityFeaturesTests.cs`, `WorkGroupingTests.cs`, `NeighborComputerTests.cs`

**Interfaces :**
- Produces : `SimilarityText.Normalize(string?)`, `SimilarityText.CleanSummary(string? raw, string? title)`, `record SimilarityEdition(...)` (ci-dessous), `SimilarityFeatureBuilder.Build(SimilarityEdition)`, `SimilarityFeatureBuilder.SameWork(SimilarityFeatures, SimilarityFeatures)`, `SimilarityFeatureBuilder.ComposeText(SimilarityEdition, string? workSummary)`, `WorkGrouping.Group(IReadOnlyList<SimilarityEdition>) -> IReadOnlyList<GroupedEdition>`, `NeighborComputer.Compute(IReadOnlyList<SimilarityFeatures>, IReadOnlyList<float[]>, int) -> IReadOnlyDictionary<string, IReadOnlyList<NeighborCandidate>>`.

- [ ] **Étape 1 : tests**

```csharp
// Application.tests/Recommendations/Similarity/SimilarityTextTests.cs
using FluentAssertions;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;

namespace Vole_Papillon_Damour.Application.tests.Recommendations.Similarity;

public sealed class SimilarityTextTests
{
    [Theory]
    [InlineData("L'Élégance du hérisson", "l elegance du herisson")]
    [InlineData("Œuvres  complètes", "oeuvres completes")]
    [InlineData(null, "")]
    public void Normalize_LowersStripsAccentsAndPunctuation(string? input, string expected) =>
        SimilarityText.Normalize(input).Should().Be(expected);

    [Fact]
    public void CleanSummary_RejectsShortTitleCopyExtractAndEditionText()
    {
        SimilarityText.CleanSummary("Le Comte de Monte-Cristo", "Le comte de Monte-Cristo").Should().BeNull();
        SimilarityText.CleanSummary("« — Monsieur le directeur, c'est justement parce que je suis un homme tranquille, auquel on n'a rien à reprocher »", "Germinal").Should().BeNull();
        SimilarityText.CleanSummary("Une édition abrégée qui transforme un roman du XIXe siècle. Les points forts de cette édition : des outils simples à chaque étape.", "Les misérables").Should().BeNull();
    }

    [Fact]
    public void CleanSummary_KeepsARealSummaryAndCapsItsLength()
    {
        var summary = "Jean Valjean sauve Marius de la barricade et l'emmène chez son grand-père avec l'aide de Javert. " + new string('x', 2000);

        var cleaned = SimilarityText.CleanSummary(summary, "Les misérables");

        cleaned.Should().StartWith("Jean Valjean sauve Marius");
        cleaned!.Length.Should().Be(1500);
    }
}
```

```csharp
// Application.tests/Recommendations/Similarity/SimilarityFeaturesTests.cs
using FluentAssertions;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;

namespace Vole_Papillon_Damour.Application.tests.Recommendations.Similarity;

public sealed class SimilarityFeaturesTests
{
    internal static SimilarityEdition Edition(
        string isbn,
        string title,
        string[]? surnames = null,
        string? bnfWorkId = null,
        string? olWorkKey = null,
        string? seriesTitle = null,
        int? seriesNumber = null,
        int? partNumber = null,
        string? collection = null,
        string? publisher = null,
        string? audience333 = null,
        bool cnlj = false,
        string[]? forms = null,
        string[]? subjects = null,
        string? dewey = null,
        string? summary = null,
        string? olDescription = null) =>
        new(isbn, title, null, partNumber, null,
            (surnames ?? ["Auteur"]).Select(s => $"Prénom {s}").ToArray(), surnames ?? ["Auteur"],
            publisher, collection, seriesTitle, seriesNumber, bnfWorkId, olWorkKey,
            forms ?? [], subjects ?? [], audience333, cnlj, dewey, summary, olDescription);

    [Fact]
    public void SameWork_WithSharedBnfWorkId_IsTrue()
    {
        var a = SimilarityFeatureBuilder.Build(Edition("9782070612758", "Le petit prince", bnfWorkId: "11958514"));
        var b = SimilarityFeatureBuilder.Build(Edition("9782075224055", "Le Petit Prince", bnfWorkId: "11958514"));

        SimilarityFeatureBuilder.SameWork(a, b).Should().BeTrue();
    }

    [Fact]
    public void SameWork_WithDifferentTomes_IsFalse()
    {
        var tome1 = SimilarityFeatureBuilder.Build(Edition("9782266149518", "Retour à l'état sauvage", bnfWorkId: "SERIE", seriesTitle: "La guerre des clans", seriesNumber: 1));
        var tome2 = SimilarityFeatureBuilder.Build(Edition("9782266156523", "À feu et à sang", bnfWorkId: "SERIE", seriesTitle: "La guerre des clans", seriesNumber: 2));

        SimilarityFeatureBuilder.SameWork(tome1, tome2).Should().BeFalse();
    }

    [Fact]
    public void SameWork_WithSameTitleAndOneSharedAuthorAmongTranslators_IsTrue()
    {
        var a = SimilarityFeatureBuilder.Build(Edition("9782070584628", "Harry Potter à l'école des sorciers", surnames: ["Rowling", "Ménard"]));
        var b = SimilarityFeatureBuilder.Build(Edition("9782070518425", "Harry Potter à l'école des sorciers", surnames: ["Rowling"]));

        SimilarityFeatureBuilder.SameWork(a, b).Should().BeTrue();
    }

    [Fact]
    public void SameWork_WithSameTitleButDifferentAuthors_IsFalse()
    {
        var novel = SimilarityFeatureBuilder.Build(Edition("9782070368228", "1984", surnames: ["Orwell"]));
        var comic = SimilarityFeatureBuilder.Build(Edition("9782380120318", "1984", surnames: ["Coste"]));

        SimilarityFeatureBuilder.SameWork(novel, comic).Should().BeFalse();
    }

    [Theory]
    [InlineData("À partir de 11 ans", false, null, null, SimilarityAudience.Youth)]
    [InlineData("À partir de 13 ans", false, null, null, SimilarityAudience.Teen)]
    [InlineData(null, true, null, null, SimilarityAudience.Youth)]
    [InlineData(null, false, "Folio junior", null, SimilarityAudience.Youth)]
    [InlineData(null, false, "Wiz", "Albin Michel", SimilarityAudience.Youth)]
    [InlineData(null, false, "Folio", "Gallimard", SimilarityAudience.Adult)]
    public void InferAudience(string? audience333, bool cnlj, string? collection, string? publisher, SimilarityAudience expected) =>
        SimilarityFeatureBuilder.InferAudience(Edition("9782070612758", "Titre", audience333: audience333, cnlj: cnlj, collection: collection, publisher: publisher))
            .Should().Be(expected);

    [Theory]
    [InlineData(null, "Kana", null, SimilarityForm.Illustrated)]
    [InlineData("Bandes dessinées", null, null, SimilarityForm.Illustrated)]
    [InlineData(null, "Dargaud", null, SimilarityForm.Illustrated)]
    [InlineData(null, "Albin Michel", "909", SimilarityForm.Essay)]
    [InlineData("Romans", "Gallimard", null, SimilarityForm.Text)]
    public void InferForm(string? form, string? publisher, string? dewey, SimilarityForm expected) =>
        SimilarityFeatureBuilder.InferForm(Edition("9782070612758", "Titre", forms: form is null ? [] : [form], publisher: publisher, dewey: dewey))
            .Should().Be(expected);

    [Fact]
    public void ComposeText_SkipsEmptySegmentsAndEndsWithWorkSummary()
    {
        var edition = Edition("9782742793099", "Les hommes qui n'aimaient pas les femmes", surnames: ["Larsson"], seriesTitle: "Millénium", seriesNumber: 1, collection: "Babel noir", forms: ["Romans"]);

        var text = SimilarityFeatureBuilder.ComposeText(edition, "Mikael Blomkvist enquête sur une disparition vieille de quarante ans.");

        text.Should().Be("Les hommes qui n'aimaient pas les femmes. Auteur : Prénom Larsson. Collection : Babel noir. Série : Millénium, tome 1. Genre : Romans. Mikael Blomkvist enquête sur une disparition vieille de quarante ans.");
    }
}
```

```csharp
// Application.tests/Recommendations/Similarity/WorkGroupingTests.cs
using FluentAssertions;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;
using static Vole_Papillon_Damour.Application.tests.Recommendations.Similarity.SimilarityFeaturesTests;

namespace Vole_Papillon_Damour.Application.tests.Recommendations.Similarity;

public sealed class WorkGroupingTests
{
    private const string Long = "Un aviateur tombé en panne dans le désert rencontre un petit garçon venu d'une autre planète, qui lui raconte son voyage.";

    [Fact]
    public void Group_SharesTheLongestCleanSummaryAcrossEditionsOfAWork()
    {
        var recent = Edition("9782070612758", "Le petit prince", bnfWorkId: "W1", summary: Long);
        var old = Edition("9782070331000", "Le petit prince", bnfWorkId: "W1");

        var grouped = WorkGrouping.Group([recent, old]);

        grouped.Single(g => g.Edition.Isbn13 == "9782070331000").WorkSummary.Should().Be(Long);
    }

    [Fact]
    public void Group_FallsBackToOpenLibraryDescription()
    {
        var edition = Edition("9782253171676", "Les rivières pourpres", olDescription: "Deux enquêtes parallèles mènent deux policiers vers une université isolée des Alpes françaises.");

        WorkGrouping.Group([edition]).Single().WorkSummary.Should().StartWith("Deux enquêtes");
    }

    [Fact]
    public void Group_UsesTheMajorityAudienceOfTheWork()
    {
        var youth = Edition("9782070612758", "Le petit prince", bnfWorkId: "W1", collection: "Folio junior");
        var adult1 = Edition("9782075224055", "Le petit prince", bnfWorkId: "W1", collection: "Folio");
        var adult2 = Edition("9782070408504", "Le petit prince", bnfWorkId: "W1", collection: "Folio");

        WorkGrouping.Group([youth, adult1, adult2])
            .Should().OnlyContain(g => g.Features.Audience == SimilarityAudience.Adult);
    }
}
```

```csharp
// Application.tests/Recommendations/Similarity/NeighborComputerTests.cs
using FluentAssertions;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;
using static Vole_Papillon_Damour.Application.tests.Recommendations.Similarity.SimilarityFeaturesTests;

namespace Vole_Papillon_Damour.Application.tests.Recommendations.Similarity;

public sealed class NeighborComputerTests
{
    private static float[] Vector(params float[] head)
    {
        var vector = new float[4];
        head.CopyTo(vector, 0);
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        return vector.Select(v => v / norm).ToArray();
    }

    [Fact]
    public void Compute_ExcludesSelfAndOtherEditionsOfTheSameWork()
    {
        var features = new[]
        {
            SimilarityFeatureBuilder.Build(Edition("9782266233200", "Dune", surnames: ["Herbert"], bnfWorkId: "W")),
            SimilarityFeatureBuilder.Build(Edition("9782221249420", "Dune", surnames: ["Herbert"], bnfWorkId: "W")),
            SimilarityFeatureBuilder.Build(Edition("9782266282000", "Hypérion", surnames: ["Simmons"])),
        };
        var vectors = new[] { Vector(1, 0), Vector(1, 0), Vector(0.9f, 0.1f) };

        var result = NeighborComputer.Compute(features, vectors, 30);

        result["9782266233200"].Select(n => n.NeighborIsbn13).Should().Equal("9782266282000");
    }

    [Fact]
    public void Compute_PutsTheNextTomeFirstWithReasonNextTome()
    {
        var features = new[]
        {
            SimilarityFeatureBuilder.Build(Edition("9782742793099", "Les hommes qui n'aimaient pas les femmes", surnames: ["Larsson"], seriesTitle: "Millénium", seriesNumber: 1)),
            SimilarityFeatureBuilder.Build(Edition("9782742765010", "La fille qui rêvait d'un bidon d'essence et d'une allumette", surnames: ["Larsson"], seriesTitle: "Millénium", seriesNumber: 2)),
            SimilarityFeatureBuilder.Build(Edition("9782253157533", "Miséricorde", surnames: ["Adler-Olsen"])),
        };
        var vectors = new[] { Vector(1, 0), Vector(0.5f, 0.5f), Vector(0.95f, 0.05f) };

        var first = NeighborComputer.Compute(features, vectors, 30)["9782742793099"][0];

        first.NeighborIsbn13.Should().Be("9782742765010");
        first.Reason.Should().Be(NeighborReason.NextTome);
    }

    [Fact]
    public void OppositeAudience_IsPenalizedNotExcluded()
    {
        var features = new[]
        {
            SimilarityFeatureBuilder.Build(Edition("9782226257017", "Percy Jackson", surnames: ["Riordan"], collection: "Wiz", publisher: "Albin Michel")),
            SimilarityFeatureBuilder.Build(Edition("9782070584628", "Harry Potter", surnames: ["Rowling"], collection: "Folio", publisher: "Gallimard")),
        };
        var vectors = new[] { Vector(1, 0), Vector(1, 0) };

        var neighbor = NeighborComputer.Compute(features, vectors, 30)["9782226257017"].Single();

        neighbor.NeighborIsbn13.Should().Be("9782070584628");
        neighbor.Score.Should().BeApproximately(1f + 0.03f - 0.10f, 0.0001f);
    }

    [Fact]
    public void Compute_KeepsAtMostTheRequestedNumberOfNeighbors()
    {
        var features = Enumerable.Range(0, 6)
            .Select(i => SimilarityFeatureBuilder.Build(Edition($"97820706127{i:D2}", $"Livre {i}", surnames: [$"Auteur{i}"])))
            .ToArray();
        var vectors = features.Select((_, i) => Vector(1, i)).ToArray();

        NeighborComputer.Compute(features, vectors, 3).Values.Should().OnlyContain(list => list.Count == 3);
    }

    [Fact]
    public void Reason_SameAuthorWhenNoSeries()
    {
        var q = SimilarityFeatureBuilder.Build(Edition("9782070360024", "L'étranger", surnames: ["Camus"]));
        var c = SimilarityFeatureBuilder.Build(Edition("9782070360420", "La peste", surnames: ["Camus"]));

        NeighborComputer.Adjust(q, c).Reason.Should().Be(NeighborReason.SameAuthor);
    }
}
```

Les ISBN des tests sont des chaînes à 13 chiffres ; les fonctions pures ne valident pas
la clé de contrôle (seul le domaine le fait).

- [ ] **Étape 2 : vérifier l'échec** — `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~Recommendations.Similarity"`. Attendu : compilation en échec.

- [ ] **Étape 3 : implémenter**

```csharp
// Application/Recommendations/Similarity/SimilarityText.cs
using System.Globalization;
using System.Text;

namespace Vole_Papillon_Damour.Application.Recommendations.Similarity;

public static class SimilarityText
{
    public const int MinSummaryLength = 80;
    public const int MaxSummaryLength = 1500;

    private static readonly string[] EditionMarkers =
    [
        "cette edition", "ce coffret", "cette nouvelle edition", "les points forts",
        "edition abregee", "edition collector", "a l occasion des",
    ];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value
            .Replace("œ", "oe", StringComparison.OrdinalIgnoreCase)
            .Replace("æ", "ae", StringComparison.OrdinalIgnoreCase)
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(lower);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    public static string? CleanSummary(string? raw, string? title)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = string.Join(' ', raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length < MinSummaryLength || text[0] is '«' or '"' or '“' or '—' or '-')
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(title) && Normalize(text) == Normalize(title))
        {
            return null;
        }

        var head = Normalize(text.Length > 200 ? text[..200] : text);
        if (EditionMarkers.Any(head.Contains))
        {
            return null;
        }

        return text.Length > MaxSummaryLength ? text[..MaxSummaryLength] : text;
    }
}
```

```csharp
// Application/Recommendations/Similarity/SimilarityEdition.cs
namespace Vole_Papillon_Damour.Application.Recommendations.Similarity;

/// <summary>Ce que les sources disent d'une édition. Sérialisé en JSON dans BookSimilarityProfile.NoticeJson.</summary>
public sealed record SimilarityEdition(
    string Isbn13,
    string Title,
    string? PartTitle,
    int? PartNumber,
    string? Subtitle,
    IReadOnlyList<string> Authors,
    IReadOnlyList<string> AuthorSurnames,
    string? Publisher,
    string? Collection,
    string? SeriesTitle,
    int? SeriesNumber,
    string? BnfWorkId,
    string? OpenLibraryWorkKey,
    IReadOnlyList<string> Forms,
    IReadOnlyList<string> Subjects,
    string? Audience333,
    bool CnljReviewed,
    string? Dewey,
    string? EditionSummary,
    string? OpenLibraryDescription);
```

```csharp
// Application/Recommendations/Similarity/SimilarityFeatures.cs
using System.Text.RegularExpressions;

namespace Vole_Papillon_Damour.Application.Recommendations.Similarity;

public enum SimilarityAudience { Adult = 0, Youth = 1, Teen = 2 }

public enum SimilarityForm { Text = 0, Illustrated = 1, Essay = 2 }

public sealed record SimilarityFeatures(
    string Isbn13,
    string TitleKey,
    IReadOnlySet<string> Surnames,
    string? BnfWorkId,
    string? OpenLibraryWorkKey,
    string? SeriesKey,
    int? Tome,
    SimilarityAudience Audience,
    SimilarityForm Form);

public static partial class SimilarityFeatureBuilder
{
    // Collections et éditeurs jeunesse, en forme normalisée. Liste à compléter au fil des erreurs observées.
    private static readonly string[] YouthMarkers =
    [
        "junior", "jeunesse", "cadet", "bibliotheque rose", "bibliotheque verte", "j aime lire", "castor",
        "witty", "wiz", "kids", "premiers romans", "mon premier", "heure des histoires", "albums", "fj poche",
    ];

    private static readonly string[] MangaPublishers = ["kana", "pika", "kurokawa", "ki oon", "kaze", "tonkam", "glenat manga"];

    private static readonly string[] ComicPublishers =
    [
        "dargaud", "dupuis", "casterman", "lombard", "delcourt", "soleil", "bamboo", "futuropolis",
        "l association", "rue de sevres", "blake et mortimer", "moulinsart", "fluide glacial", "vents d ouest",
    ];

    [GeneratedRegex(@"(\d+)\s*ans")]
    private static partial Regex AgeRegex();

    public static SimilarityAudience InferAudience(SimilarityEdition edition)
    {
        if (edition.Audience333 is { } audience && AgeRegex().Match(audience) is { Success: true } match)
        {
            return int.Parse(match.Groups[1].Value) <= 12 ? SimilarityAudience.Youth : SimilarityAudience.Teen;
        }

        var haystack = SimilarityText.Normalize($"{edition.Collection} {edition.Publisher}");
        if (edition.CnljReviewed || YouthMarkers.Any(marker => ContainsWord(haystack, marker)))
        {
            return SimilarityAudience.Youth;
        }

        return SimilarityAudience.Adult;
    }

    public static SimilarityForm InferForm(SimilarityEdition edition)
    {
        var forms = SimilarityText.Normalize(string.Join(' ', edition.Forms));
        var publisher = SimilarityText.Normalize(edition.Publisher);
        var collection = SimilarityText.Normalize(edition.Collection);
        if (collection.Contains("manga") || MangaPublishers.Any(publisher.Contains) ||
            forms.Contains("bandes dessinees") || forms.Contains("romans graphiques") || forms.Contains("manga") ||
            ComicPublishers.Any(publisher.Contains))
        {
            return SimilarityForm.Illustrated;
        }

        var dewey = edition.Dewey?.Trim();
        if (!string.IsNullOrEmpty(dewey) && !dewey.StartsWith('8'))
        {
            return SimilarityForm.Essay;
        }

        return edition.Subjects.Count > 0 && !forms.Contains("roman") ? SimilarityForm.Essay : SimilarityForm.Text;
    }

    public static (string? Key, int? Tome) InferSeries(SimilarityEdition edition)
    {
        if (!string.IsNullOrWhiteSpace(edition.SeriesTitle))
        {
            return (SimilarityText.Normalize(edition.SeriesTitle), edition.SeriesNumber ?? edition.PartNumber);
        }

        return edition.PartNumber is { } part ? (SimilarityText.Normalize(edition.Title), part) : (null, null);
    }

    public static SimilarityFeatures Build(SimilarityEdition edition, SimilarityAudience? audienceOverride = null)
    {
        var (seriesKey, tome) = InferSeries(edition);
        return new SimilarityFeatures(
            edition.Isbn13,
            SimilarityText.Normalize(edition.Title),
            edition.AuthorSurnames.Select(SimilarityText.Normalize).Where(s => s.Length > 0).ToHashSet(StringComparer.Ordinal),
            string.IsNullOrWhiteSpace(edition.BnfWorkId) ? null : edition.BnfWorkId,
            string.IsNullOrWhiteSpace(edition.OpenLibraryWorkKey) ? null : edition.OpenLibraryWorkKey,
            seriesKey,
            tome,
            audienceOverride ?? InferAudience(edition),
            InferForm(edition));
    }

    public static bool SameWork(SimilarityFeatures a, SimilarityFeatures b)
    {
        if (a.Tome is { } tomeA && b.Tome is { } tomeB && tomeA != tomeB)
        {
            return false; // la BnF donne parfois à tous les tomes l'identifiant de la série
        }

        if (a.BnfWorkId is not null && a.BnfWorkId == b.BnfWorkId)
        {
            return true;
        }

        if (a.OpenLibraryWorkKey is not null && a.OpenLibraryWorkKey == b.OpenLibraryWorkKey)
        {
            return true;
        }

        return a.TitleKey == b.TitleKey &&
               (a.Surnames.Count == 0 || b.Surnames.Count == 0 || a.Surnames.Overlaps(b.Surnames));
    }

    public static string ComposeText(SimilarityEdition edition, string? workSummary)
    {
        var parts = new List<string> { TitleLine(edition) };
        if (edition.Authors.Count > 0)
        {
            parts.Add("Auteur : " + string.Join(", ", edition.Authors));
        }

        if (!string.IsNullOrWhiteSpace(edition.Collection))
        {
            parts.Add("Collection : " + edition.Collection);
        }

        if (!string.IsNullOrWhiteSpace(edition.SeriesTitle))
        {
            parts.Add("Série : " + edition.SeriesTitle + (edition.SeriesNumber is { } n ? $", tome {n}" : string.Empty));
        }

        if (edition.Forms.Count > 0)
        {
            parts.Add("Genre : " + string.Join(", ", edition.Forms.Distinct()));
        }

        if (edition.Subjects.Count > 0)
        {
            parts.Add("Sujets : " + string.Join(", ", edition.Subjects.Distinct()));
        }

        if (!string.IsNullOrWhiteSpace(edition.Audience333))
        {
            parts.Add("Public : " + edition.Audience333);
        }

        if (!string.IsNullOrWhiteSpace(workSummary))
        {
            parts.Add(workSummary);
        }

        return string.Join(". ", parts);
    }

    private static string TitleLine(SimilarityEdition edition)
    {
        var title = edition.Title;
        if (!string.IsNullOrWhiteSpace(edition.PartTitle) &&
            SimilarityText.Normalize(edition.PartTitle) != SimilarityText.Normalize(title))
        {
            title += ", " + edition.PartTitle;
        }

        return string.IsNullOrWhiteSpace(edition.Subtitle) ? title : $"{title} : {edition.Subtitle}";
    }

    private static bool ContainsWord(string haystack, string marker) =>
        $" {haystack} ".Contains($" {marker} ", StringComparison.Ordinal);
}
```

```csharp
// Application/Recommendations/Similarity/WorkGrouping.cs
namespace Vole_Papillon_Damour.Application.Recommendations.Similarity;

public sealed record GroupedEdition(SimilarityEdition Edition, SimilarityFeatures Features, string? WorkSummary);

public static class WorkGrouping
{
    public static IReadOnlyList<GroupedEdition> Group(IReadOnlyList<SimilarityEdition> editions)
    {
        var raw = editions.Select(e => SimilarityFeatureBuilder.Build(e)).ToArray();
        var parent = Enumerable.Range(0, editions.Count).ToArray();

        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

        // Regroupement par clés exactes pour rester en O(n) : œuvre BnF, œuvre OL, titre normalisé.
        void UnionBy(Func<SimilarityFeatures, string?> key)
        {
            foreach (var bucket in raw.Select((f, i) => (Key: key(f), Index: i))
                         .Where(x => x.Key is not null)
                         .GroupBy(x => x.Key, StringComparer.Ordinal))
            {
                var members = bucket.Select(x => x.Index).ToArray();
                for (var a = 0; a < members.Length; a++)
                for (var b = a + 1; b < members.Length; b++)
                {
                    if (SimilarityFeatureBuilder.SameWork(raw[members[a]], raw[members[b]]))
                    {
                        parent[Find(members[a])] = Find(members[b]);
                    }
                }
            }
        }

        UnionBy(f => f.BnfWorkId);
        UnionBy(f => f.OpenLibraryWorkKey);
        UnionBy(f => f.TitleKey);

        var groups = Enumerable.Range(0, editions.Count).GroupBy(Find).ToArray();
        var result = new GroupedEdition[editions.Count];
        foreach (var group in groups)
        {
            var members = group.ToArray();
            var summary = members
                .Select(i => SimilarityText.CleanSummary(editions[i].EditionSummary, editions[i].Title))
                .Where(s => s is not null)
                .OrderByDescending(s => s!.Length)
                .FirstOrDefault()
                ?? members
                    .Select(i => SimilarityText.CleanSummary(editions[i].OpenLibraryDescription, editions[i].Title))
                    .FirstOrDefault(s => s is not null);
            var audience = members
                .GroupBy(i => raw[i].Audience)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .First()
                .Key;
            foreach (var i in members)
            {
                result[i] = new GroupedEdition(editions[i], raw[i] with { Audience = audience }, summary);
            }
        }

        return result;
    }
}
```

```csharp
// Application/Recommendations/Similarity/NeighborComputer.cs
using System.Numerics.Tensors;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Recommendations.Similarity;

public sealed record NeighborCandidate(string NeighborIsbn13, float Score, NeighborReason Reason);

public static class NeighborComputer
{
    public const float SameSeriesBonus = 0.20f;
    public const float NextTomeBonus = 0.10f;
    public const float SameAuthorBonus = 0.08f;
    public const float SameFormBonus = 0.03f;
    public const float OppositeAudiencePenalty = 0.10f;

    /// <summary>Vecteurs normalisés (norme 1) : le produit scalaire est le cosinus.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<NeighborCandidate>> Compute(
        IReadOnlyList<SimilarityFeatures> features,
        IReadOnlyList<float[]> vectors,
        int neighborsPerBook)
    {
        if (features.Count != vectors.Count)
        {
            throw new ArgumentException("One vector per book is required.", nameof(vectors));
        }

        var results = new IReadOnlyList<NeighborCandidate>[features.Count];
        Parallel.For(0, features.Count, i =>
        {
            var heap = new PriorityQueue<NeighborCandidate, float>(neighborsPerBook + 1);
            for (var j = 0; j < features.Count; j++)
            {
                if (i == j || SimilarityFeatureBuilder.SameWork(features[i], features[j]))
                {
                    continue;
                }

                var (bonus, reason) = Adjust(features[i], features[j]);
                var score = TensorPrimitives.Dot<float>(vectors[i], vectors[j]) + bonus;
                heap.Enqueue(new NeighborCandidate(features[j].Isbn13, score, reason), score);
                if (heap.Count > neighborsPerBook)
                {
                    heap.Dequeue(); // retire le plus faible
                }
            }

            var ordered = new List<NeighborCandidate>(heap.Count);
            while (heap.Count > 0)
            {
                ordered.Add(heap.Dequeue());
            }

            ordered.Reverse();
            results[i] = ordered;
        });

        return features.Select((f, i) => (f.Isbn13, results[i]))
            .ToDictionary(x => x.Isbn13, x => x.Item2, StringComparer.Ordinal);
    }

    public static (float Bonus, NeighborReason Reason) Adjust(SimilarityFeatures query, SimilarityFeatures candidate)
    {
        var bonus = 0f;
        NeighborReason? reason = null;
        if (query.SeriesKey is not null && query.SeriesKey == candidate.SeriesKey)
        {
            bonus += SameSeriesBonus;
            reason = NeighborReason.SameSeries;
            if (query.Tome is { } tome && candidate.Tome == tome + 1)
            {
                bonus += NextTomeBonus;
                reason = NeighborReason.NextTome;
            }
        }

        if (query.Surnames.Overlaps(candidate.Surnames))
        {
            bonus += SameAuthorBonus;
            reason ??= NeighborReason.SameAuthor;
        }

        if (query.Form == candidate.Form)
        {
            bonus += SameFormBonus;
        }

        if ((query.Audience, candidate.Audience) is (SimilarityAudience.Youth, SimilarityAudience.Adult)
            or (SimilarityAudience.Adult, SimilarityAudience.Youth))
        {
            bonus -= OppositeAudiencePenalty;
        }

        return (bonus, reason ?? NeighborReason.Theme);
    }
}
```

- [ ] **Étape 4 : vérifier** — même filtre. Attendu : tous les tests `Recommendations.Similarity` réussis.
- [ ] **Étape 5 : commit** — `rtk git commit -m "feat(recommendations): port the benchmarked similarity method"`

---

### Tâche 3 : lecture des notices BnF et Open Library

**Files :**
- Create : `Application/Common/Interfaces/Services/IBibliographicNoticeReader.cs`, `Infrastructure/Services/Recommendations/BibliographicNoticeReader.cs`, `Infrastructure/Services/Recommendations/UnimarcNoticeParser.cs`
- Modify : `Infrastructure/DependencyInjection.cs` (client HTTP typé, comme `IBnfSruClient`, lignes 101-106)
- Test : `Infrastructure.tests/Recommendations/UnimarcNoticeParserTests.cs`, `BibliographicNoticeReaderTests.cs`

**Interfaces :**
- Produces : `interface IBibliographicNoticeReader { Task<SimilarityEdition?> ReadAsync(string isbn13, CancellationToken ct); }` (`null` si ni la BnF ni Open Library ne connaissent l'ISBN) ; `UnimarcNoticeParser.Parse(XElement record, string isbn13) -> SimilarityEdition`.

- [ ] **Étape 1 : tests du parseur.** Construire une notice enregistrée (même forme que `BnfSruClientTests.RecordedNoticeWithCover`) avec les zones `010`, `200 ($a $e $h $i)`, `225$a`, `461 ($t $v)`, `500$3`, `608 ($a $2=CNLJ)`, `606 ($a $x)`, `333$a`, `101$a`, `330$a`, et trois responsabilités :
  - `700` Larsson/Stieg ;
  - `702` Ménard/Jean-François avec `$4=730` ;
  - `702` Collins/Suzanne **sans** `$4`.

  Asserter :
  - `Authors == ["Stieg Larsson", "Suzanne Collins"]` et `AuthorSurnames == ["Larsson", "Collins"]` (le traducteur est ignoré, la 702 sans rôle est gardée) ;
  - `SeriesTitle == "Millénium"`, `SeriesNumber == 1`, `BnfWorkId == "40221640"`, `CnljReviewed == true` ;
  - `Subjects` contient les `$a` et les `$x` de la 606 ;
  - `EditionSummary` vaut le `330$a`.

  Deuxième test : une notice sans zone 330 donne `EditionSummary == null`.

- [ ] **Étape 2 : tests du lecteur.** Avec `StubHttpMessageHandler` (copié de `BnfSruClientTests`) :
  1. la requête BnF contient `bib.fuzzyISBN all "9782742793099"` et `recordSchema=unimarcXchange` ;
  2. Open Library est appelé sur `https://openlibrary.org/isbn/9782742793099.json`. Si l'édition n'a pas de `description` mais un `works[0].key`, le lecteur appelle `https://openlibrary.org{key}.json` et remplit `OpenLibraryDescription` (format chaîne, et format `{ "value": "…" }`) et `OpenLibraryWorkKey` ;
  3. BnF sans notice **et** Open Library `404` : `ReadAsync` renvoie `null` ;
  4. BnF en erreur `503` : le lecteur renvoie les données Open Library seules (titre compris) et ne lève pas d'exception.

- [ ] **Étape 3 : vérifier l'échec**, puis **implémenter.**
  - `UnimarcNoticeParser` reprend les helpers `FirstSubfield` et `Subfield` de `BnfSruClient` (méthodes `internal static`), avec la règle 702 : garder si `$4` absent ou égal à `070`.
  - `BibliographicNoticeReader(HttpClient httpClient, IOptions<BibliographicOptions> options)` : un appel SRU `maximumRecords=1`, puis Open Library.
    - Quand la BnF n'a rien, construire l'édition depuis Open Library : `title` et, sur l'œuvre, `authors` inutilisables (clés) ; laisser les auteurs vides.
    - Toute `HttpRequestException` ou `TaskCanceledException` hors annulation est absorbée par source.
  - Enregistrement DI :

```csharp
services.AddHttpClient<IBibliographicNoticeReader, BibliographicNoticeReader>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<BibliographicOptions>>().Value;
    client.Timeout = TimeSpan.FromMilliseconds(Math.Max(options.BnfTimeoutMilliseconds, options.OpenLibraryTimeoutMilliseconds) * 2);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
});
```

- [ ] **Étape 4 : vérifier** — `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter "FullyQualifiedName~Recommendations"`.
- [ ] **Étape 5 : commit** — `rtk git commit -m "feat(recommendations): read BnF and Open Library notices for similarity"`

---

### Tâche 4 : persistance

**Files :**
- Modify : `Application/Common/Interfaces/Persistence/IProjectDbContext.cs`, `Infrastructure/Persistence/ProjectDbContext.cs`
- Create : `Infrastructure/Persistence/Configurations/BookSimilarityProfileConfiguration.cs`, `BookNeighborConfiguration.cs`, `RecommendationGenerationConfiguration.cs`, `MemberRecommendationPreferenceConfiguration.cs`
- Create (généré) : migration `AddBookRecommendations`

- [ ] **Étape 1 : `IProjectDbContext`**, à la suite de `CheckoutPassageLines` :

```csharp
DbSet<BookSimilarityProfile> BookSimilarityProfiles => throw new NotSupportedException();
DbSet<BookNeighbor> BookNeighbors => throw new NotSupportedException();
DbSet<RecommendationGeneration> RecommendationGenerations => throw new NotSupportedException();
DbSet<MemberRecommendationPreference> MemberRecommendationPreferences => throw new NotSupportedException();
```

et les `DbSet` réels correspondants dans `ProjectDbContext`.

- [ ] **Étape 2 : configurations.**

| Table | Colonnes et contraintes |
|---|---|
| `BookSimilarityProfiles` | PK `Isbn13 char(13)` (`IsUnicode(false)`) ; `NoticeJson nvarchar(max)` ; `NoticeFound bit` ; `NoticeFetchedAt datetime2` ; `ProfileText nvarchar(max)` ; `ProfileTextHash char(64)` ; `Embedding varbinary(2048)` ; `EmbeddedTextHash char(64)` ; `EmbeddedAt datetime2`. Index sur `NoticeFetchedAt` |
| `BookNeighbors` | PK composite `(GenerationId, Isbn13, Rank)` ; `NeighborIsbn13 char(13)` ; `Score real` ; `Reason tinyint` (conversion `byte`) |
| `RecommendationGenerations` | PK `Id tinyint`, `CHECK (Id = 1)` ; `CurrentGenerationId uniqueidentifier NULL` ; `ComputedAt datetime2 NULL` ; `BookCount int`. Données d'amorçage : une ligne `Id = 1` (`HasData`) |
| `MemberRecommendationPreferences` | PK `UserId` (conversion `UserId`, comme `MemberSelectionItemConfiguration`) ; FK vers `Users` **`OnDelete(DeleteBehavior.Cascade)`** ; `Enabled bit` ; `UpdatedAt datetime2` |

- [ ] **Étape 3 : migration**

```powershell
dotnet ef migrations add AddBookRecommendations --project src/Backend/Vole_Papillon_Damour.Infrastructure --startup-project src/Backend/Vole_Papillon_Damour.Api
```

Relire le fichier généré : quatre tables, la ligne d'amorçage, la cascade depuis `Users`,
**aucune** modification d'une table existante.

- [ ] **Étape 4 : vérifier** — `dotnet build` puis `dotnet test` (suite complète) : les `DbContext` de test ne doivent pas casser.
- [ ] **Étape 5 : commit** — `rtk git commit -m "feat(recommendations): store profiles, neighbor generations and preferences"`

---

### Tâche 5 : ports d'infrastructure (réglages, embeddings, écriture en masse)

**Files :**
- Create : `Application/Common/Interfaces/Services/IRecommendationSettings.cs`, `IBookEmbeddingService.cs`, `IBookNeighborWriter.cs`
- Create : `Infrastructure/Services/Recommendations/RecommendationOptions.cs`, `RecommendationSettings.cs`, `AzureOpenAiBookEmbeddingService.cs`, `NoOpBookEmbeddingService.cs`, `SqlBookNeighborWriter.cs`
- Modify : `Infrastructure/DependencyInjection.cs`
- Test : `Infrastructure.tests/Recommendations/AzureOpenAiBookEmbeddingServiceTests.cs`

**Interfaces :**

```csharp
public interface IRecommendationSettings
{
    bool Enabled { get; }
    int NeighborsPerBook { get; }       // 30
    int ProfileBatchSize { get; }       // 200
    float SimilarMinScore { get; }      // 0.50
    int SimilarMaxCount { get; }        // 5
    int SimilarMinCount { get; }        // 2
    int PersonalMaxCount { get; }       // 8
    int PersonalMinCount { get; }       // 3
}

public interface IBookEmbeddingService
{
    bool IsConfigured { get; }
    /// <returns>Un vecteur normalisé de 512 float par texte, dans l'ordre.</returns>
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}

public sealed record BookNeighborRow(string Isbn13, byte Rank, string NeighborIsbn13, float Score, NeighborReason Reason);

public interface IBookNeighborWriter
{
    /// <summary>Écrit une génération complète puis bascule RecommendationGenerations.Current, atomiquement.</summary>
    Task WriteGenerationAsync(Guid generationId, IReadOnlyCollection<BookNeighborRow> rows, int bookCount, DateTime computedAt, CancellationToken cancellationToken);
}
```

- [ ] **Étape 1 : test du service d'embedding.** Un faux `IEmbeddingGenerator<string, Embedding<float>>` (Microsoft.Extensions.AI) qui capture les options et renvoie des vecteurs non normalisés. Asserter :
  1. `EmbeddingGenerationOptions.Dimensions == 512` ;
  2. les textes sont envoyés par lots de **100** au plus ;
  3. chaque vecteur renvoyé a une norme de 1 (± 1e-5) ;
  4. une réponse de 1 536 valeurs lève `InvalidOperationException`.
- [ ] **Étape 2 : implémenter.**
  - `AzureOpenAiBookEmbeddingService(IEmbeddingGenerator<string, Embedding<float>> generator)` : découpe par 100, appelle `generator.GenerateAsync(batch, new EmbeddingGenerationOptions { Dimensions = 512 }, ct)`, contrôle la longueur, puis normalise avec `TensorPrimitives.Norm` et `TensorPrimitives.Divide`.
  - `SqlBookNeighborWriter(ProjectDbContext db)` :
    1. dans une transaction, `SqlBulkCopy` (`Microsoft.Data.SqlClient`, connexion et transaction du `DbContext`) des lignes vers `BookNeighbors`, par lots de 10 000 ;
    2. `UPDATE RecommendationGenerations SET CurrentGenerationId = @id, BookCount = @count, ComputedAt = @at WHERE Id = 1` ;
    3. commit ;
    4. **après** le commit, `DELETE TOP (50000) FROM BookNeighbors WHERE GenerationId <> @id` en boucle jusqu'à 0 ligne.
- [ ] **Étape 3 : DI.** Dans `AddInfrastructure`, section `Recommendations` :

```csharp
services.Configure<RecommendationOptions>(builderConfiguration.GetSection(RecommendationOptions.SectionName));
services.AddSingleton<IRecommendationSettings, RecommendationSettings>();
services.AddScoped<IBookNeighborWriter, SqlBookNeighborWriter>();
AddBookEmbeddings(services, builderConfiguration);
```

`AddBookEmbeddings` suit exactement `AddActualityTitleGeneration` (lignes 155-181).
Sans `Endpoint` ou `EmbeddingDeploymentName`, enregistrer `NoOpBookEmbeddingService` (`IsConfigured = false`). Sinon :

```csharp
services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<RecommendationOptions>>().Value;
    return new AzureOpenAIClient(new Uri(options.Endpoint!), new DefaultAzureCredential())
        .GetEmbeddingClient(options.EmbeddingDeploymentName)
        .AsIEmbeddingGenerator();
});
services.AddScoped<IBookEmbeddingService, AzureOpenAiBookEmbeddingService>();
```

`RecommendationOptions` (`SectionName = "Recommendations"`) porte les clés de la spec §6 avec leurs valeurs par défaut.

- [ ] **Étape 4 : vérifier**, **commit** — `rtk git commit -m "feat(recommendations): add embedding, settings and bulk neighbor writer"`

---

### Tâche 6 : commande de rafraîchissement des profils

**Files :**
- Create : `Application/Recommendations/Commands/RefreshBookSimilarityProfiles/RefreshBookSimilarityProfilesCommand.cs`, `…Handler.cs`
- Test : `Application.tests/Recommendations/RefreshBookSimilarityProfilesCommandHandlerTests.cs` (fixture SQLite sur le modèle de `CheckoutPassages/CheckoutPassageFixture.cs`, avec `Books` et `BookSimilarityProfiles` réels)

**Interfaces :**
- Consumes : `IBibliographicNoticeReader`, `IRecommendationSettings`, `IDateTimeProvider`.
- Produces : `record RefreshBookSimilarityProfilesCommand() : IRequest<RefreshBookSimilarityProfilesResult>` ; `record RefreshBookSimilarityProfilesResult(int Candidates, int Found, int NotFound)`.

Règles :
- **Candidats** : `Books` visibles et canoniques (`!IsHiddenFromCatalog && RedirectedToIsbn13 == null`) sans profil, ou dont le profil a `NoticeFetchedAt` antérieur à `now − 90 jours`, ou antérieur à `Book.UpdatedAt`. Les plus anciens d'abord, `ProfileBatchSize` au plus.
- Pour chacun, `ReadAsync` :
  - trouvé : `RecordNotice(JsonSerializer.Serialize(edition), true, now)` ;
  - `null` : `RecordNotice(null, false, now)`, pour ne pas le réessayer avant 90 jours.
- `Enabled == false` : ne rien faire, résultat `(0, 0, 0)`.
- Une notice est lue à la fois (politesse envers la BnF, T-07 §2).

- [ ] **Étape 1 : tests.**
  1. Deux livres visibles sans profil, un masqué, un redirigé : seuls les deux premiers sont lus.
  2. Un profil rafraîchi il y a 10 jours n'est pas relu ; il l'est à 91 jours.
  3. Lecteur renvoyant `null` : profil créé avec `NoticeFound == false`.
  4. `Enabled == false` : aucun appel au lecteur.
- [ ] **Étapes 2 à 5** : échec, implémentation, succès, commit `feat(recommendations): refresh similarity profiles from bibliographic sources`.

---

### Tâche 7 : commande de calcul des voisins

**Files :**
- Create : `Application/Recommendations/Commands/RecomputeBookNeighbors/RecomputeBookNeighborsCommand.cs`, `…Handler.cs`, `EmbeddingBytes.cs`
- Test : `Application.tests/Recommendations/RecomputeBookNeighborsCommandHandlerTests.cs`

**Interfaces :**
- Consumes : tâches 2, 5 ; `IDateTimeProvider`.
- Produces : `record RecomputeBookNeighborsCommand() : IRequest<RecomputeBookNeighborsResult>` ; `record RecomputeBookNeighborsResult(int Books, int Embedded, int Neighbors, Guid? GenerationId)`. `EmbeddingBytes.ToBytes(float[])` et `EmbeddingBytes.ToFloats(byte[])` (`MemoryMarshal.Cast`).

Déroulé :
1. Si `!Enabled` ou `!embeddings.IsConfigured` : résultat vide, sans rien écrire.
2. Charger les profils `NoticeFound` des livres visibles et canoniques. Désérialiser `NoticeJson` en `SimilarityEdition`.
3. `WorkGrouping.Group` sur toutes les éditions. Pour chaque édition : `text = ComposeText(edition, workSummary)` et `hash = SHA-256 hex (minuscules) du texte UTF-8`. Puis `profile.SetProfileText(text, hash)`.
4. Vectoriser **uniquement** les profils `NeedsEmbedding`, par l'appel groupé du service. `RecordEmbedding(EmbeddingBytes.ToBytes(v), hash, now)`. `SaveChangesAsync`.
5. Les éditions sans vecteur (échec partiel) sont exclues du calcul.
6. `NeighborComputer.Compute(features, vectors, NeighborsPerBook)` → lignes `BookNeighborRow(isbn, rank = index + 1, …)`.
7. `writer.WriteGenerationAsync(Guid.NewGuid(), rows, books, now, ct)`.

- [ ] **Étape 1 : tests**, avec un faux `IBookEmbeddingService` qui renvoie des vecteurs fixés par ISBN, et un faux `IBookNeighborWriter` qui capture les lignes.
  1. Trois livres dont deux tomes consécutifs d'une série : la ligne de rang 1 du tome 1 est le tome 2, avec `NextTome`.
  2. Relancer sans changement de notice : **aucun** appel au service d'embedding (empreintes égales).
  3. Modifier le résumé d'une édition : seules les éditions de **cette œuvre** sont revectorisées (le résumé d'œuvre est partagé).
  4. `IsConfigured == false` : ni embedding ni écriture.
  5. Aucun texte transmis au service ne contient autre chose que les champs de notice. Vérifier qu'il commence par le titre et ne contient aucun `Guid`.
- [ ] **Étapes 2 à 5** : échec, implémentation, succès, commit `feat(recommendations): compute nightly neighbor generations`.

---

### Tâche 8 : traitement nocturne du worker et infrastructure

**Files :**
- Create : `Worker/RecommendationsFunction.cs`
- Modify : `infra/modules/AiFoundry/aiFoundry.module.bicep`, `infra/main.bicep`, `infra/parameters/main.dev.bicepparam`
- Test : `Worker.tests/RecommendationsFunctionTests.cs` (sur le modèle des tests existants du worker)

- [ ] **Étape 1 : test** : la fonction envoie `RefreshBookSimilarityProfilesCommand` **puis** `RecomputeBookNeighborsCommand`, dans cet ordre, et journalise les compteurs.
- [ ] **Étape 2 : fonction**, sur le modèle exact de `BookEnrichmentFunction` :

```csharp
[Function("Recommendations")]
public async Task Run([TimerTrigger("0 30 3 * * *")] TimerInfo timer, CancellationToken cancellationToken)
```

- [ ] **Étape 3 : Bicep.**
  - Dans `aiFoundry.module.bicep`, ajouter les paramètres `embeddingDeploymentName string = ''` et `embeddingCapacity int = 50`, puis une ressource `embeddingDeployment` conditionnelle (`if (!empty(embeddingDeploymentName))`) :

```bicep
resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = if (!empty(embeddingDeploymentName)) {
  parent: account
  name: empty(embeddingDeploymentName) ? 'unused' : embeddingDeploymentName
  dependsOn: [deployment]
  sku: {
    name: 'GlobalStandard'
    capacity: embeddingCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-3-small'
      version: '1'
    }
  }
}
```

  - **Nom : `text-embedding-3-small`.** Ce déploiement existe déjà, créé à la main le 24 septembre (`06` §0) ; le Bicep doit reprendre **le même nom, le même SKU et la même capacité (50)** pour ne rien recréer. `dependsOn` évite deux opérations de déploiement concurrentes sur le compte.
  - Dans `main.bicep` : paramètres `recommendationsEnabled bool = false` et `recommendationsEmbeddingDeploymentName string = 'text-embedding-3-small'`. Les passer au module, puis ajouter aux paramètres d'application du **worker** et de l'**API** :
    - `Recommendations__Enabled` ;
    - pour le worker seulement : `Recommendations__Endpoint` (sortie `endpoint` du module) et `Recommendations__EmbeddingDeploymentName`.

    Suivre la construction existante des variables `TitleGeneration__*` (vers la ligne 1742 de `main.bicep`). Le rôle *Cognitive Services OpenAI User* du worker couvre déjà le compte.
  - Dans `main.dev.bicepparam` : `param recommendationsEnabled = readEnvironmentVariable('RECOMMENDATIONS_ENABLED', 'false') == 'true'`.
- [ ] **Étape 4 : vérifier** — `az bicep build --file infra/main.bicep` sans erreur, puis les tests du worker.
- [ ] **Étape 5 : commit** — `rtk git commit -m "feat(recommendations): schedule nightly computation and provision embeddings"`

---

### Tâche 9 : API — livres proches d'une fiche

**Files :**
- Create : `Application/Recommendations/Queries/GetSimilarBooks/GetSimilarBooksQuery.cs`, `…Handler.cs`, `SimilarBookResult.cs`
- Create : `Contracts/Recommendations/SimilarBooksResponse.cs`
- Modify : `Api/Controllers/BookController.cs` (endpoint après `/catalog/books/{isbn13}`, ligne 98)
- Test : `Application.tests/Recommendations/GetSimilarBooksQueryHandlerTests.cs`, `Api.tests/…` (sur le modèle des tests d'endpoints existants)

**Interfaces :**
- Produces : `record GetSimilarBooksQuery(string Isbn13) : IRequest<ErrorOr<IReadOnlyList<SimilarBookResult>>>` ; `record SimilarBookResult(PublicCatalogBookResult Book, NeighborReason Reason)`.
- HTTP : `GET /catalog/books/{isbn13}/similar` → `200 { "books": [ { …PublicCatalogBookResponse, "reason": "NextTome" } ] }` (`reason` est une `string` dans le contrat, `NeighborReason.ToString()`). Anonyme, `RequireRateLimiting(RateLimitingPolicies.PublicCatalog)`, en-tête `Cache-Control: public, max-age=300`. ISBN invalide : `400`, comme `GetPublicCatalogBook`.

Déroulé :
1. `!Enabled` ou aucune génération courante : liste vide.
2. Lire les voisins `(CurrentGenerationId, Isbn13)` par rang, gardés si `Score >= SimilarMinScore`.
3. Charger ces `Books`, visibles et canoniques, puis leurs annonces et bourses exactement comme `GetPublicBookQueryHandler` (lignes 43-49), et projeter par `PublicCatalogProjector.Project(books, announcements, fairs, rareIsbns: vide, now)`.
4. Garder `QuantityAvailable > 0 || QuantityAnnounced > 0`, dans l'ordre des rangs, `SimilarMaxCount` au plus.
5. Moins de `SimilarMinCount` : liste vide.

- [ ] **Étape 1 : tests.**
  1. L'ordre des rangs est conservé et la raison est portée.
  2. Un voisin épuisé et non annoncé est écarté ; un voisin **annoncé** est gardé.
  3. Un voisin masqué ou redirigé est écarté.
  4. Un voisin sous le seuil est écarté.
  5. Un seul voisin affichable donne une liste vide.
  6. Sans génération courante, la liste est vide.
  7. Seuls les voisins de la génération **courante** sont lus : une ancienne génération présente est ignorée.
- [ ] **Étapes 2 à 5** : échec, implémentation, succès (tests Application et API), commit `feat(recommendations): expose similar books for a catalog record`.

---

### Tâche 10 : API — Pour vous et préférence

**Files :**
- Create : `Application/Recommendations/Queries/GetMyRecommendations/*`, `Application/Recommendations/Commands/SetRecommendationPreference/*`, `Contracts/Recommendations/MyRecommendationsResponse.cs`, `RecommendationPreferenceRequest.cs`
- Modify : `Api/Controllers/MemberAccountController.cs` (après `/catalog/me/purchases`, ligne 108)
- Test : `Application.tests/Recommendations/GetMyRecommendationsQueryHandlerTests.cs`, `SetRecommendationPreferenceCommandHandlerTests.cs` (fixture de `CheckoutPassages`, étendue aux tables de recommandation)

**Interfaces :**
- `record GetMyRecommendationsQuery(string ExternalId, string? Email, string? FirstName, string? LastName, int Limit) : IRequest<ErrorOr<MyRecommendationsResult>>`
- `record MyRecommendationsResult(RecommendationStatus Status, IReadOnlyList<PersonalRecommendation> Items)`, avec `enum RecommendationStatus { Enabled, Disabled, NoPurchases }` et `record PersonalRecommendation(PublicCatalogBookResult Book, NeighborReason Reason, string SeedTitle)`.
- `record SetRecommendationPreferenceCommand(string ExternalId, string? Email, string? FirstName, string? LastName, bool Enabled) : IRequest<ErrorOr<Success>>`
- HTTP, tous `RequireAuthorization()`, en-tête `Cache-Control: no-store` :
  - `GET /catalog/me/recommendations?limit=4` → `{ "status": "Enabled"|"Disabled"|"NoPurchases", "items": [ { …PublicCatalogBookResponse, "reason": "Theme", "seedTitle": "Dune" } ] }` ;
  - `GET /catalog/me/recommendations/preference` → `{ "enabled": true }` ;
  - `PUT /catalog/me/recommendations/preference` avec `{ "enabled": false }` → `204`.
- `limit` borné à `[1, PersonalMaxCount]` par un validateur FluentValidation, sur le modèle de `GetMyPurchasesQueryValidator`.

Déroulé de `GetMyRecommendations` :
1. Identifier le membre par `MemberIdentityService.EnsureAsync`, comme `GetMyPurchasesQueryHandler`.
2. Préférence `Enabled == false` : `Disabled`, liste vide, **aucune autre lecture**.
3. **Graines** : lignes des passages `Associated` du membre, `VoidedAt == null`, `Isbn13 != null`, par `OccurredAt` décroissant, dédoublonnées par ISBN, 20 au plus. Aucune : `NoPurchases`. Poids de la graine d'indice `k` (0 = la plus récente) : `1.0 − 0.5 × k / max(1, n − 1)`.
4. Voisins de la génération courante pour ces graines. Score cumulé par candidat : `Σ score × poids`. Garder, pour chaque candidat, la graine qui contribue le plus, avec la raison de ce voisinage.
5. **Exclure** :
   - les ISBN des graines ;
   - toute édition d'une œuvre déjà achetée : la génération ne contient jamais la même œuvre, **mais** deux graines peuvent se proposer l'une l'autre. On exclut donc aussi les candidats qui sont eux-mêmes des graines ;
   - les ISBN de `MemberSelectionItems` du membre ;
   - les candidats dont **aucun** score individuel n'atteint `SimilarMinScore`.
6. Projeter et filtrer (visible, canonique, disponible ou annoncé) comme en tâche 9. Classer par score cumulé décroissant et garder `limit`.
7. `SeedTitle` = titre de la ligne d'achat de la graine retenue (instantané historique).

- [ ] **Étape 1 : tests.**
  1. Préférence désactivée : `Disabled`, sans lecture de voisins (un `DbSet` `BookNeighbors` qui lève une exception passe quand même).
  2. Sans achat associé : `NoPurchases`.
  3. Une ligne annulée ne sert pas de graine.
  4. Un livre déjà acheté n'est jamais proposé.
  5. Un livre présent dans Ma sélection n'est pas proposé.
  6. Deux graines ayant le même voisin : ce voisin passe devant un voisin d'une seule graine au score individuel égal.
  7. `SeedTitle` est le titre de la graine qui contribue le plus, et `Reason == NextTome` quand c'est la suite de cet achat.
  8. `SetRecommendationPreference` crée la ligne au premier changement, puis la met à jour ; la suppression de l'utilisateur supprime la préférence (cascade, testée en SQLite avec les clés étrangères actives).
- [ ] **Étapes 2 à 5** : échec, implémentation, succès, commit `feat(recommendations): personal recommendations and opt-out preference`.

---

### Tâche 11 : frontend — « Dans le même esprit » sous la fiche

**Maquettes :**
- `maquettes/html/Main.html` : zones `R1-section`, `R1-entete`, `R1-mention`, `R1-grille`, `R1-carte`, `R1-disponibilite`, `R1-raison`, `R1-legende` ;
- `maquettes/html/FicheMobile.html` : `R2-section`, `R2-defilement`, `R2-carte`, `R2-indicateur` ;
- `maquettes/html/FicheEtats.html` : `R3-chargement`, `R3-peu`, `R3-masquee`.

**Files :**
- Modify : `src/Catalog/src/app/core/catalog.models.ts`, `core/catalog-api.service.ts`, `shared/book-card/book-card.component.{ts,html,scss,spec.ts}`, `features/book-detail/catalog-book-detail-page.component.html`, `app.module.ts`
- Create : `src/Catalog/src/app/features/book-detail/similar-books/similar-books.component.{ts,html,scss,spec.ts}`

**Interfaces :**

```ts
// catalog.models.ts
export type CatalogNeighborReason = 'NextTome' | 'SameSeries' | 'SameAuthor' | 'Theme';
export interface CatalogSimilarBook extends CatalogBook { reason: CatalogNeighborReason; }
export interface CatalogSimilarBooksResponse { books: CatalogSimilarBook[]; }
export const neighborReasonLabels: Record<CatalogNeighborReason, string> = {
  NextTome: 'Tome suivant',
  SameSeries: 'Même série',
  SameAuthor: 'Même auteur',
  Theme: 'Proche par le thème',
};
```

```ts
// catalog-api.service.ts
getSimilarBooks(isbn13: string): Observable<CatalogSimilarBooksResponse> {
  return this.http.get<CatalogSimilarBooksResponse>(
    `${this.apiUrl}/catalog/books/${encodeURIComponent(isbn13)}/similar`,
  );
}
```

- [ ] **Étape 1 : tests.**
  - `book-card.component.spec.ts` : avec `reason = 'NextTome'` et la variante `home`, la carte affiche une étiquette `.book-card-reason` au texte « Tome suivant » et à la classe `reason-next-tome`. Sans `reason`, aucune étiquette.
  - `similar-books.component.spec.ts`, avec un `CatalogApiService` factice :
    1. pendant le chargement, `[data-zone="similar-loading"]` est présent et `aria-busy="true"` ;
    2. avec 5 livres, 5 `app-book-card` et le titre « Si ce livre vous a plu. » ;
    3. avec 1 livre, **rien** n'est rendu (la section n'existe pas) ;
    4. en erreur HTTP, rien n'est rendu et aucune erreur n'est affichée ;
    5. un changement d'`isbn13` relance l'appel.
- [ ] **Étape 2 : implémenter.**
  - `BookCardComponent` : ajouter `@Input() reason: CatalogNeighborReason | null = null`. Dans la variante `home`, au-dessus du titre, afficher `<span class="book-card-reason" [class]="'book-card-reason reason-' + kebab(reason)">{{ reasonLabel() }}</span>`. Styles de R1-legende :
    - `reason-next-tome` : fond `#072b45`, texte blanc ;
    - `reason-same-series` : fond `#e9f4fb`, bordure `#9dc2da`, texte `#072b45` ;
    - `reason-same-author` : bordure `#9dc2da`, texte `#0c6ea6` ;
    - `reason-theme` : bordure `#d9e9f4`, texte `#4e6c84`.

    Tous en `IBM Plex Mono`, 10 px, majuscules, `letter-spacing: .1em`.
  - `SimilarBooksComponent` (`selector: 'app-similar-books'`, `standalone: false`, `OnPush`) : `@Input({required: true}) isbn13!: string`. Signaux `state: 'loading' | 'ready' | 'hidden'` et `books`. Recharger dans `ngOnChanges`. `catchError` bascule sur `hidden`. `books.length < 2` bascule aussi sur `hidden`.
  - Gabarit : reprendre la structure de `R1-section` (règle dégradée 56×3 px, étiquette « Dans le même esprit » en `#dc6412`, `h2` Newsreader 34 px « Si ce livre vous a plu. », paragraphe, mention `R1-mention` à droite). Grille `repeat(5, minmax(0, 1fr))`, `gap: 20px`, `<app-book-card variant="home" [book]="book" [reason]="book.reason">`. Sous 720 px, rangée à défilement horizontal `R2-defilement` : `display: flex; overflow-x: auto; scroll-snap-type: x mandatory`, cartes de 156 px, `role="list"`. Le compteur `R2-indicateur` est facultatif, en CSS pur. Squelette : 5 blocs `#e9f4fb` (`R3-chargement`).
  - Fiche : insérer `<app-similar-books [isbn13]="item.isbn13"></app-similar-books>` **après** `</section>` de `.book-detail` (ligne 180 de `catalog-book-detail-page.component.html`), dans le bloc `@else if (book; as item)`.
  - Déclarer le composant dans `app.module.ts`.
- [ ] **Étape 3 : vérifier** — `npx ng test --watch=false --browsers=ChromeHeadless` et `npx ng build`. Vérifier à 390 px et 1440 px, en comparant avec `Main.html` et `FicheMobile.html`.
- [ ] **Étape 4 : commit** — `rtk git commit -m "feat(catalog): show similar books under each record"`

---

### Tâche 12 : frontend — « Pour vous » sur l'accueil

**Maquettes :** `maquettes/html/AccueilPourVous.html` (`R4-section`, `R4-entete`, `R4-pourquoi`, `R4-grille`, `R4-carte`, `R4-raison`), `AccueilPourVousMobile.html` (`R5-section`, `R5-defilement`), `PourVousEtats.html` (`R8-chargement`).

**Files :**
- Modify : `core/catalog.models.ts`, `core/catalog-member-api.service.ts`, `features/home/catalog-home-page.component.html`, `app.module.ts`
- Create : `features/home/for-you/for-you-section.component.{ts,html,scss,spec.ts}`

**Interfaces :**

```ts
export type CatalogRecommendationStatus = 'Enabled' | 'Disabled' | 'NoPurchases';
export interface CatalogPersonalRecommendation extends CatalogBook { reason: CatalogNeighborReason; seedTitle: string; }
export interface CatalogRecommendationsResponse { status: CatalogRecommendationStatus; items: CatalogPersonalRecommendation[]; }
```

```ts
// catalog-member-api.service.ts
getRecommendations(accessToken: string, limit = 4): Observable<CatalogRecommendationsResponse> {
  return this.http.get<CatalogRecommendationsResponse>(`${this.apiUrl}/catalog/me/recommendations`, {
    headers: this.authorizationHeaders(accessToken),
    params: new HttpParams().set('limit', limit),
  });
}
getRecommendationPreference(accessToken: string): Observable<{enabled: boolean}> { /* GET …/preference */ }
setRecommendationPreference(accessToken: string, enabled: boolean): Observable<void> { /* PUT …/preference, body {enabled} */ }
```

- [ ] **Étape 1 : tests** (`for-you-section.component.spec.ts`, `CatalogAuthService` et `CatalogMemberApiService` factices) :
  1. visiteur non connecté : aucun appel, rien de rendu ;
  2. connecté, statut `Enabled` et 4 éléments : section rendue, eyebrow « Pour vous, {prénom} » ;
  3. raison `NextTome` : « La suite de *X* » ; sinon « Parce que vous avez aimé *X* » (`R4-raison`) ;
  4. `Disabled`, `NoPurchases`, ou moins de 3 éléments : rien de rendu ;
  5. erreur : rien de rendu ;
  6. le lien « Pourquoi ces suggestions ? » pointe vers `/compte` avec `queryParams {tab: 'preferences'}`. Vérifier que la page compte lit ce paramètre ; sinon l'ajouter à `selectTab` dans `catalog-account-page.component.ts`, avec un test.
- [ ] **Étape 2 : implémenter.** La carte réutilise `app-book-card variant="home"` sans étiquette de raison. Sous la carte, la ligne `R4-raison` : bordure haute `#e2eef7`, 13 px `#33536e`, titre de la graine en `<em>` Newsreader. Placer `<app-for-you-section>` **avant** la section `selection-section` « Arrivés cette semaine » (ligne 266 de `catalog-home-page.component.html`). Grille de 4 colonnes ; défilement horizontal sous 720 px (`R5-defilement`, cartes de 170 px) ; squelette `R8-chargement`.
- [ ] **Étape 3 : vérifier** (tests, build, 390 et 1440 px), **commit** — `rtk git commit -m "feat(catalog): personal recommendations on the home page"`

---

### Tâche 13 : frontend — Mes achats et préférence

**Maquettes :** `maquettes/html/MesAchatsPourVous.html` (`R6-bandeau`, `R6-liste`, `R6-carte`), `ComptePreferences.html` (`R7-suggestions`, `R7-statut`, `R7-explication`, `R7-bouton`), `PourVousEtats.html` (`R8-sans-achat`, `R8-desactive`).

**Files :**
- Create : `features/account/recommendations-band/recommendations-band.component.{ts,html,scss,spec.ts}`
- Modify : `features/account/purchases/account-purchases.component.html` (bandeau en tête de `<section class="account-purchases">`), `features/account/catalog-account-page.component.{html,ts,spec.ts}` (panneau dans l'onglet `preferences`, après `alert-settings-panel`, ligne 509), `app.module.ts`

- [ ] **Étape 1 : tests.**
  - Bandeau :
    1. `Enabled` avec 4 éléments : 4 cartes compactes (`R6-carte`) et le lien « Gérer ces suggestions » ;
    2. `NoPurchases` : invitation `R8-sans-achat`, bouton « Afficher ma carte » vers l'onglet `card` ;
    3. `Disabled` : rien de rendu.
  - Panneau de préférence :
    1. statut « Activées » et bouton « Désactiver les suggestions » ;
    2. un clic appelle `setRecommendationPreference(token, false)` et affiche « Désactivées » avec le bouton « Réactiver les suggestions » (`R8-desactive`) ;
    3. le bouton est désactivé pendant l'appel ;
    4. en erreur, message `role="alert"` et retour à l'état précédent.
- [ ] **Étape 2 : implémenter** en réutilisant les classes du panneau d'alertes (`alert-settings-panel`, `settings-heading`, `account-button quiet|secondary`) et les textes exacts de R7 et R8.
- [ ] **Étape 3 : vérifier, commit** — `rtk git commit -m "feat(catalog): recommendations band in purchases and opt-out preference"`

---

### Tâche 14 : RGPD, documentation, livraison

**Files :**
- Modify : `docs/bourse-aux-livres/09-rgpd-compte-et-droits.md` (tableau des traitements : ligne « Suggestions personnalisées » — achats associés et préférence ; finalité : proposer des livres proches ; base : intérêt légitime, opposition par la préférence ; aucune donnée transmise au fournisseur d'IA)
- Modify : la page publique « Vos données et le RGPD » du catalogue (`src/Catalog/src/app/features/legal/…`), avec le même paragraphe
- Modify : `docs/recommandations-livres/README.md` et `00-journal-des-echanges.md` (statut : implémenté, PR #…)
- Modify : `NEXT.md` : ligne « Recommandations : activer `RECOMMENDATIONS_ENABLED`, calibrer `SimilarMinScore` sur 50 fiches »

- [ ] **Étape 1** : textes RGPD, puis relecture.
- [ ] **Étape 2** : suite complète :

```powershell
dotnet build src/Backend/Vole_Papillon_Damour.slnx
dotnet test src/Backend/Vole_Papillon_Damour.slnx
cd src/Catalog; npx ng test --watch=false --browsers=ChromeHeadless; npx ng build; cd ../..
az bicep build --file infra/main.bicep
graphify update .
```

- [ ] **Étape 3** : livraison selon `.github/skills/delivery-workflow/SKILL.md` : `rtk git fetch origin main`, `rtk git rebase origin/main`, relancer les tests, pousser, PR vers `main`. Dans « Risks and follow-up » :
  1. `Recommendations:Enabled` reste à `false` jusqu'au premier calcul nocturne réussi ;
  2. le seuil `SimilarMinScore` est à calibrer sur le vrai catalogue (relecture de 50 fiches) ;
  3. le déploiement d'embedding existe déjà dans Azure (créé à la main le 24 septembre, même nom que le Bicep) ;
  4. la liste des collections jeunesse est à compléter au fil des erreurs observées.

## Auto-relecture du plan

- **Couverture de la spec** : §2.1 (tâche 3), §2.2 et §2.3 (tâche 2), §2.4 (tâches 2 et 7), §3.1 (tâches 9 et 11), §3.2 (tâches 10, 12 et 13), §3.3 (tâches 10 et 13), §4 (tâches 7 et 14), §5 (tâches 4 à 8), §6 (tâche 5), §7 (tests des tâches 2, 9, 10, 11, 12 et 13 ; critère 10 en suivi de PR).
- **Cohérence des noms** : `NeighborReason` (domaine) ↔ `CatalogNeighborReason` (front), mêmes valeurs. **L'API n'a pas de `JsonStringEnumConverter` global** (vérifié) : les contrats de `Contracts/Recommendations` déclarent `Reason` et `Status` en `string`, remplis par `.ToString()` dans le mapping de réponse des contrôleurs.
- **Code vérifié** : les blocs de la tâche 2 (5 fichiers source et 4 fichiers de tests) ont été compilés et exécutés tels quels dans un projet .NET 10 isolé, avec `System.Numerics.Tensors` et FluentAssertions : **29 tests réussis**. Les autres tâches s'appuient sur le code existant du dépôt et n'ont pas été compilées hors contexte.
