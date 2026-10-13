# Tableau de loto en direct — plan d'implémentation

> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : maquettes/preview/.

**But :** fiabiliser le direct SSE (plusieurs réplicas, heartbeat, révisions, 404), refaire
le tableau projeté du BackOffice pour la vidéoprojection (deux palettes basculables, régie
discrète, annonces plein écran avec QR code), refaire le tableau mobile du site, ajouter le
bandeau « Aujourd'hui », l'encart de la fiche loto et l'adresse courte `/loto`.

**Architecture :**
- **Révision du tableau.** Chaque mutation du tableau incrémente `AssoEvents.LiveRevision`.
  Ce compteur est aussi le jeton de concurrence EF : deux actions simultanées ne peuvent pas
  réussir toutes les deux, la seconde reçoit un 409.
- **Diffusion locale.** Côté API, un hub en mémoire (`ILiveTableauHub`) diffuse les états
  complets aux clients SSE de la réplica. Chaque client a un canal borné à un élément : un
  téléphone lent ne garde que l'état le plus récent et ne freine pas les autres.
- **Diffusion entre réplicas.** Une tâche de fond (`LiveTableauSyncService`) interroge la
  base chaque seconde, uniquement pour les événements regardés sur cette réplica. Elle
  republie un état dès que sa révision a bougé ailleurs.
- **Flux SSE.** Il passe par `TypedResults.ServerSentEvents` (.NET 10) avec `id:` = révision,
  `retry: 3000`, un événement `ping` après 15 s de silence et un 404 si l'événement est
  inconnu.
- **Logique d'affichage partagée.** La déduction de l'affichage (phase, dernier numéro,
  voisins, compteur « 1ʳᵉ / 2ᵉ fois ») est une fonction pure de `@vpd/ui`, utilisée par la
  projection et par le tableau mobile.

**Stack :**
- .NET 10 : minimal API, EF Core, MediatR, Mapster ; tests xUnit, FluentAssertions,
  NSubstitute, SQLite.
- Angular 21 : BackOffice et Website, Tailwind ; tests Karma et Jasmine.

**Spec :** [`01-fonctionnel.md`](01-fonctionnel.md), règles `RG-01` à `RG-24`.

**Code déjà validé.** Le code de ce plan a été écrit, compilé et testé sur une copie de la
branche (`origin/main` + conception) :
- **Backend** : 927 tests verts.
- **BackOffice** : 32 specs vertes, build de production OK.
- **Website** : 105 specs vertes, build de production OK, 25 routes pré-rendues.
- **Rendus comparés aux maquettes** contre une API simulée : projection A et B, « Avant le
  début » B, tableau mobile 390 et 1440 px dans tous ses états, bandeau 390 et 1440 px,
  encart de fiche.

Reprendre le code tel quel. Les diffs sont relatifs à `origin/main` au moment de la
conception (`09be2578`).

## Contraintes globales

- **Un seul écran** au BackOffice, recopié sur le vidéoprojecteur. Aucune nouvelle
  ressource Azure, aucun changement d'infrastructure ni de `maxReplicas`.
- **Projection.** Scène de référence 1920 × 1080, mise à l'échelle de la fenêtre. Grille
  1448 px (75 %). Chiffres de la grille 76 px, dernier numéro 300–320 px, annonce 780 px.
  Police Libre Franklin 800/900 en `tabular-nums` ; plus aucune police manuscrite.
- **Palettes.** Variables CSS `pal-a` / `pal-b`, valeurs du tableau de
  `maquettes/README.md`, mémorisées dans `localStorage` sous la clé `vpd.loto.palette`
  (`dark` | `light`).
- **Adresse courte.** `https://volepapillondamour.fr/loto`. Le QR code est un SVG statique
  généré une fois (tâche 7) : pas de dépendance npm de QR code.
- **Délais.** Annonce du numéro = `environment.time_numero_modal` (2000 ms en production).
  Contrôles masqués après 3000 ms sans souris. Toast d'erreur 4000 ms.
- **Direct.** Heartbeat 15 s, `retry` 3 s, synchronisation entre réplicas 1 s, options
  `LiveTableau:*`. Côté client, reconnexion entre 500 ms et 5000 ms, « Reconnexion… » après
  5000 ms.
- **Libellés mot pour mot**, tels qu'en maquettes :
  - **Projection :**
    - « Partie {n} / {total} », « Dernier numéro », « Précédents », « Lots de la ligne »,
      « Bingo », « Gagné ! », « Voisins : 41 et 43 ».
    - Lignes : « 1 ligne », « 2 lignes », « Carton plein ». Compteur : « 1ʳᵉ fois »,
      « 2ᵉ fois ». Types : « Américaine », « +1 / −1 ».
  - **Annonces :**
    - « Numéro », « Pause », « On reprend dans quelques minutes. », « Numéros du bingo »,
      « Ensuite ».
    - « Le loto va commencer », « Première partie », « Loto terminé », « Merci à tous ! »,
      « Les bénéfices de cet après-midi soutiennent Maxence et sa famille. ».
    - QR code : « Suivez le tableau sur votre téléphone ».
  - **Régie :**
    - Boutons : « Annuler le dernier », « Ligne gagnée », « Bingo gagné » / « Annuler le
      bingo », « Pause » / « Reprendre », « Palette claire » / « Palette sombre ».
    - Aide : « Clic sur une case ou chiffres + Entrée », « Saisie ».
    - Erreurs : « Le numéro {n} est déjà sorti. ».
  - **Mobile :**
    - Pastilles : « En direct », « Connecté », « Terminé », « Reconnexion… », « Hors ligne ».
    - « Connexion perdue. Le tableau se remettra à jour tout seul dès le retour du réseau. »
    - « Ce tableau n'existe pas ou n'est plus disponible. », « Voir les événements ».
    - « Gardez cette page ouverte : les numéros s'afficheront ici dès le premier tirage. »
    - « Tous les numéros », « {n} numéros sortis », « Mis à jour automatiquement, pas
      besoin de recharger la page. ».
  - **Bandeau :**
    - « Aujourd'hui », « En direct », « Dernier numéro », « Suivre le tableau ».
    - « Voir l'événement », « Le loto est terminé, merci à tous ! », « Voir le tableau
      final », « Ouverture des portes 13 h 30 · {ville} », « Masquer le bandeau ».
  - **Fiche :** « Tableau en direct », « Le loto a commencé · dernier numéro {n} »,
    « Le tableau s'animera au premier numéro tiré. ».
- **Règles de jeu côté serveur** (handlers, `Partie`, `LinePartie`) inchangées : on ajoute
  seulement `MarkLiveChange()`.

## Points de vigilance (revue)

1. **Changement fait sur une autre réplica.** Un numéro saisi sur la réplica A doit
   atteindre un téléphone connecté à la réplica B (RG-10). Test
   `SyncOnce_BringsANumberDrawnOnAnotherReplicaToThisReplicaClients`, tâche 4.
2. **Mutation entre l'abonnement et la lecture de l'état initial.** Elle ne doit être ni
   perdue ni rejouée : abonnement d'abord, puis l'état, et le flux ignore les révisions déjà
   incluses. Test `ReadAsync_SendsNewerSnapshotsAndSkipsOnesAlreadyInTheInitialState`,
   tâche 4.
3. **Événement stocké à minuit, heure de Paris** (la veille à 23 h UTC) et consulté à
   0 h 30 : il est bien « aujourd'hui » (RG-16). Tests `IsHappeningOn_ComparesParisCalendarDays`
   (tâche 1) et `Handle_JustAfterParisMidnight_UsesTheParisDay` (tâche 3).
4. **Message SSE plus ancien que l'état affiché** (reconnexion sur une réplica en retard) :
   il est ignoré. Test « ignore un état plus ancien que celui affiché », tâche 8.
5. **Page pré-rendue au build** (pages légales, présentation…) : elle ne doit ni bloquer
   le build si l'API est injoignable, ni garder un bandeau périmé. L'appel serveur est borné
   à 3 s et le navigateur relit l'état aussitôt, tâche 10 (contrôle : build de production,
   « Prerendered 25 static routes »).

---

## Tâche 0 — État initial

- [ ] `dotnet build src/Backend/Vole_Papillon_Damour.slnx` puis
  `dotnet test src/Backend/Vole_Papillon_Damour.slnx --no-build`. Attendu à la conception :
  0 erreur, **890 tests verts** (Domain 171, Worker 4, Application 456, Api 64,
  Infrastructure 195).
- [ ] Dans `src/BackOffice` puis `src/Website` : `npm ci` si besoin, puis
  `npm test -- --watch=false --browsers=ChromeHeadless` et `npm run build`. Les
  avertissements de budget existants (bundle initial, `product.component.scss`) sont
  connus.
- [ ] Noter tout échec préexistant sans le corriger.

## Tâche 1 — Domaine : révision, statut du direct, jour de l'événement

RG-10, RG-16, RG-18. `LiveRevision` ordonne les états diffusés. `GetLiveStatus()` et
`GetLastDrawnNumero()` alimentent le bandeau. `IsHappeningOn()` compare des jours
calendaires à Paris.

**Fichiers :**
- Créer : `src/Backend/Vole_Papillon_Damour.Domain/AssoEventsAggregate/ValueObjects/LiveTableauStatus.cs`
- Modifier : `src/Backend/Vole_Papillon_Damour.Domain/AssoEventsAggregate/AssoEvents.cs`
- Test : `src/Backend/Vole_Papillon_Damour.Domain.tests/AssoEventsAggregateTests/AssoEventsLiveTableauTests.cs`

**Interfaces produites :** `int AssoEvents.LiveRevision { get; private set; }`,
`void MarkLiveChange()`, `LiveTableauStatus GetLiveStatus()`, `int? GetLastDrawnNumero()`,
`bool IsHappeningOn(DateOnly day, TimeZoneInfo zone)`,
`enum LiveTableauStatus { NotStarted, Live, Finished }`.

- [ ] **Test d'abord** (échoue à la compilation) :

Créer `src/Backend/Vole_Papillon_Damour.Domain.tests/AssoEventsAggregateTests/AssoEventsLiveTableauTests.cs` :

```csharp
using FluentAssertions;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.AssoEventsAggregateTests;

public class AssoEventsLiveTableauTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Paris");

    [Fact]
    public void MarkLiveChange_IncrementsTheRevision()
    {
        var assoEvent = CreateEvent(DateStart, null, CreatePartie(0));

        assoEvent.MarkLiveChange();
        assoEvent.MarkLiveChange();

        assoEvent.LiveRevision.Should().Be(2);
    }

    [Fact]
    public void GetLiveStatus_WithoutParties_IsNotStarted()
    {
        CreateEvent(DateStart, null).GetLiveStatus().Should().Be(LiveTableauStatus.NotStarted);
    }

    [Fact]
    public void GetLiveStatus_BeforeTheFirstNumber_IsNotStarted()
    {
        var assoEvent = CreateEvent(DateStart, null, CreatePartie(0), CreatePartie(1));

        assoEvent.GetLiveStatus().Should().Be(LiveTableauStatus.NotStarted);
        assoEvent.GetLastDrawnNumero().Should().BeNull();
    }

    [Fact]
    public void GetLiveStatus_AfterTheFirstNumber_IsLive()
    {
        var first = CreatePartie(0);
        var assoEvent = CreateEvent(DateStart, null, first, CreatePartie(1));
        first.AddLiveNumero(42);

        assoEvent.GetLiveStatus().Should().Be(LiveTableauStatus.Live);
        assoEvent.GetLastDrawnNumero().Should().Be(42);
    }

    [Fact]
    public void GetLastDrawnNumero_WhenTheCurrentPartieHasNoNumberYet_ReturnsThePreviousPartieLastNumber()
    {
        var first = CreatePartie(0);
        var assoEvent = CreateEvent(DateStart, null, first, CreatePartie(1));
        first.AddLiveNumero(12);
        first.AddLiveNumero(75);
        assoEvent.CurrentPartieIndex = 1;

        assoEvent.GetLiveStatus().Should().Be(LiveTableauStatus.Live);
        assoEvent.GetLastDrawnNumero().Should().Be(75);
    }

    [Fact]
    public void GetLiveStatus_AfterTheLastPartie_IsFinished()
    {
        var first = CreatePartie(0);
        var assoEvent = CreateEvent(DateStart, null, first);
        first.AddLiveNumero(7);
        assoEvent.CurrentPartieIndex = 1;

        assoEvent.GetLiveStatus().Should().Be(LiveTableauStatus.Finished);
        assoEvent.GetLastDrawnNumero().Should().Be(7);
    }

    [Theory]
    // Stocké à minuit UTC (date seule) : c'est bien le 15 novembre à Paris.
    [InlineData("2026-11-15T00:00:00+00:00", null, "2026-11-15", true)]
    // Stocké à minuit heure de Paris (23:00 UTC la veille).
    [InlineData("2026-11-14T23:00:00+00:00", null, "2026-11-15", true)]
    [InlineData("2026-11-15T13:00:00+00:00", null, "2026-11-14", false)]
    [InlineData("2026-11-15T13:00:00+00:00", null, "2026-11-16", false)]
    // Événement sur deux jours : le second jour compte aussi.
    [InlineData("2026-11-14T08:00:00+00:00", "2026-11-15T17:00:00+00:00", "2026-11-15", true)]
    // Fin antérieure au début (saisie erronée) : seul le jour de début compte.
    [InlineData("2026-11-15T08:00:00+00:00", "2026-11-10T17:00:00+00:00", "2026-11-15", true)]
    public void IsHappeningOn_ComparesParisCalendarDays(string start, string? end, string day, bool expected)
    {
        var assoEvent = CreateEvent(
            DateTimeOffset.Parse(start),
            end is null ? null : DateTimeOffset.Parse(end));

        assoEvent.IsHappeningOn(DateOnly.Parse(day), Paris).Should().Be(expected);
    }

    [Fact]
    public void IsHappeningOn_WhenCancelled_IsFalse()
    {
        var assoEvent = CreateEvent(DateTimeOffset.Parse("2026-11-15T13:00:00+00:00"), null);
        assoEvent.Cancel();

        assoEvent.IsHappeningOn(new DateOnly(2026, 11, 15), Paris).Should().BeFalse();
    }

    private static readonly DateTimeOffset DateStart = new(2026, 11, 15, 13, 0, 0, TimeSpan.Zero);

    private static AssoEvents CreateEvent(DateTimeOffset dateStart, DateTimeOffset? dateEnd, params Partie[] parties)
    {
        return AssoEvents.Create(
            "Grand loto",
            new Uri("https://example.com/loto.jpg"),
            new EventsType(EventsType.EventsTypeEnum.Bingo),
            dateStart,
            dateEnd,
            null,
            null,
            null,
            new Adresse(12, "Arras", "Rue du loto", 62000),
            null,
            parties,
            "Loto");
    }

    private static Partie CreatePartie(int index)
    {
        return Partie.Create(
            $"Partie {index}",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            index,
            false,
            [LinePartie.Create([Lot.Create("Lot", "lot.jpg", 0)], new NumberLine(NumberLine.NumberLineEnum.OneLine), 0)]);
    }
}
```

- [ ] `dotnet test src/Backend/Vole_Papillon_Damour.Domain.tests --filter AssoEventsLiveTableauTests` → échec attendu.
- [ ] Implémentation :

Créer `src/Backend/Vole_Papillon_Damour.Domain/AssoEventsAggregate/ValueObjects/LiveTableauStatus.cs` :

```csharp
namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

/// <summary>
/// Where a loto stands for its public audience: before the first number,
/// while numbers are drawn, or after the last partie has been won.
/// </summary>
public enum LiveTableauStatus
{
    NotStarted,
    Live,
    Finished
}
```

Modifier `src/Backend/Vole_Papillon_Damour.Domain/AssoEventsAggregate/AssoEvents.cs` :

```diff
@@ -29,6 +29,12 @@ public sealed class AssoEvents : AggregateRoot<AssoEventsId>
     public decimal? BookRevenue { get; private set; }
     
     public int CurrentPartieIndex { get; set; } = 0;
+
+    /// <summary>
+    /// Incremented by every live tableau change. It orders the snapshots sent to
+    /// the public tableau and lets each API replica detect changes made on another one.
+    /// </summary>
+    public int LiveRevision { get; private set; }
     
     private IList<Partie>? _parties = new List<Partie>();
     public IReadOnlyList<Partie>? Parties => _parties?.AsReadOnly();
@@ -148,6 +154,65 @@ public sealed class AssoEvents : AggregateRoot<AssoEventsId>
         UrlRegistration = urlRegistration;
     }
 
+    public void MarkLiveChange()
+    {
+        LiveRevision++;
+    }
+
+    public LiveTableauStatus GetLiveStatus()
+    {
+        if (_parties is null || _parties.Count == 0)
+        {
+            return LiveTableauStatus.NotStarted;
+        }
+
+        if (CurrentPartieIndex >= _parties.Count)
+        {
+            return LiveTableauStatus.Finished;
+        }
+
+        return _parties.Any(partie => partie.LastNumeros.Count > 0)
+            ? LiveTableauStatus.Live
+            : LiveTableauStatus.NotStarted;
+    }
+
+    /// <summary>
+    /// The last number drawn in the current partie, or in the closest previous
+    /// partie when the current one has not drawn any number yet.
+    /// </summary>
+    public int? GetLastDrawnNumero()
+    {
+        if (_parties is null)
+        {
+            return null;
+        }
+
+        return _parties
+            .Where(partie => partie.Index <= CurrentPartieIndex && partie.LastNumeros.Count > 0)
+            .OrderByDescending(partie => partie.Index)
+            .Select(partie => (int?)partie.LastNumeros[^1])
+            .FirstOrDefault();
+    }
+
+    /// <summary>
+    /// True when <paramref name="day"/>, a calendar day in <paramref name="zone"/>,
+    /// falls between the event's start and end days. A cancelled event never happens.
+    /// </summary>
+    public bool IsHappeningOn(DateOnly day, TimeZoneInfo zone)
+    {
+        if (IsCancelled)
+        {
+            return false;
+        }
+
+        var firstDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateStart, zone).DateTime);
+        var lastDay = DateEnd is null
+            ? firstDay
+            : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateEnd.Value, zone).DateTime);
+
+        return firstDay <= day && day <= (lastDay < firstDay ? firstDay : lastDay);
+    }
+
     public bool AddBingoNumero(int numero)
     {
         if (BingoHasBeenWon || _bingoNumeros.Contains(numero))
```

- [ ] Relancer : 13 tests verts.
- [ ] Commit : `feat(loto): add live tableau revision and status to events`

## Tâche 2 — Persistance : jeton de concurrence, révisions, migration, 409

RG-05 côté serveur, RG-10. `LiveRevision` devient jeton de concurrence. Le dépôt sait lire
les révisions d'une liste d'événements et les événements non annulés à partir d'une date.
`DbUpdateConcurrencyException` devient un 409.

**Fichiers :**
- Modifier : `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/Configurations/AssoEventConfiguration.cs`
- Modifier : `src/Backend/Vole_Papillon_Damour.Application/Common/Interfaces/Persistence/IEventRepository.cs`
- Modifier : `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/Repositories/EventRepository.cs`
- Créer (généré) : migration `AddAssoEventLiveRevision` + `ProjectDbContextModelSnapshot.cs`
- Modifier : `src/Backend/Vole_Papillon_Damour.Api/Errors/ErrorHandling.cs`
- Test : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Persistence/EventRepositoryLiveTableauTests.cs`
- Test : `src/Backend/Vole_Papillon_Damour.Api.tests/ErrorHandlingTests.cs`

**Interfaces produites :**
- `Task<List<AssoEvents>> GetNotCancelledFromAsync(DateTimeOffset fromUtc, CancellationToken)` ;
- `Task<IReadOnlyDictionary<AssoEventsId, int>> GetLiveRevisionsAsync(IReadOnlyCollection<AssoEventsId>, CancellationToken)`.

- [ ] **Tests d'abord** :

Créer `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Persistence/EventRepositoryLiveTableauTests.cs` :

```csharp
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;
using Vole_Papillon_Damour.Infrastructure.Persistence.Repositories;

namespace Vole_Papillon_Damour.Infrastructure.tests.Persistence;

public sealed class EventRepositoryLiveTableauTests
{
    [Fact]
    public async Task GetLiveRevisions_ReturnsTheRevisionOfEachExistingRequestedEvent()
    {
        await using var connection = await OpenConnectionAsync();
        await using var context = await CreateContextAsync(connection);
        var loto = CreateEvent();
        var other = CreateEvent();
        loto.MarkLiveChange();
        loto.MarkLiveChange();
        context.AssoEvents.AddRange(loto, other);
        await context.SaveChangesAsync();
        var repository = new EventRepository(context);

        var revisions = await repository.GetLiveRevisionsAsync(
            [loto.Id, AssoEventsId.CreateUnique()],
            CancellationToken.None);

        revisions.Should().ContainSingle();
        revisions[loto.Id].Should().Be(2);
    }

    [Fact]
    public async Task Update_WhenAnotherRequestChangedTheTableauMeanwhile_IsRejected()
    {
        await using var connection = await OpenConnectionAsync();
        var loto = CreateEvent();
        await using (var seed = await CreateContextAsync(connection))
        {
            seed.AssoEvents.Add(loto);
            await seed.SaveChangesAsync();
        }

        await using var firstRequest = await CreateContextAsync(connection);
        await using var secondRequest = await CreateContextAsync(connection);
        var firstCopy = (await firstRequest.AssoEvents.FindAsync(loto.Id))!;
        var secondCopy = (await secondRequest.AssoEvents.FindAsync(loto.Id))!;

        firstCopy.MarkLiveChange();
        await new EventRepository(firstRequest).UpdateAsync(firstCopy);
        secondCopy.MarkLiveChange();
        var act = () => new EventRepository(secondRequest).UpdateAsync(secondCopy);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static AssoEvents CreateEvent() =>
        AssoEvents.Create(
            "Grand loto",
            new Uri("https://example.com/loto.jpg"),
            new EventsType(EventsType.EventsTypeEnum.Bingo),
            new DateTimeOffset(2026, 11, 15, 13, 0, 0, TimeSpan.Zero),
            null,
            null,
            null,
            null,
            new Adresse(12, "Arras", "Rue du loto", 62000),
            null,
            [],
            "Loto");

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateCollation(
            "Latin1_General_100_CI_AI",
            (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase));
        return connection;
    }

    private static async Task<ProjectDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ProjectDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new TestDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private sealed class TestDbContext(DbContextOptions<ProjectDbContext> options)
        : ProjectDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Vole_Papillon_Damour.Domain.BookAggregate.Book>()
                .Property(book => book.RowVersion)
                .ValueGeneratedNever()
                .IsConcurrencyToken(false);

            foreach (var property in modelBuilder.Model
                         .GetEntityTypes()
                         .SelectMany(entityType => entityType.GetProperties())
                         .Where(property => property.GetColumnType() == "nvarchar(max)"))
            {
                property.SetColumnType("TEXT");
            }
        }
    }
}
```

Modifier `src/Backend/Vole_Papillon_Damour.Api.tests/ErrorHandlingTests.cs` :

```diff
@@ -4,6 +4,7 @@ using ErrorOr;
 using FluentAssertions;
 using Microsoft.AspNetCore.Builder;
 using Microsoft.AspNetCore.Http;
+using Microsoft.EntityFrameworkCore;
 using Microsoft.Extensions.DependencyInjection;
 using Vole_Papillon_Damour.Api.Errors;
 
@@ -43,4 +44,28 @@ public sealed class ErrorHandlingTests
 
         context.Response.StatusCode.Should().Be((int)HttpStatusCode.ServiceUnavailable);
     }
+
+    [Fact]
+    public async Task UseErrorHandling_WhenTwoUpdatesCollide_ReturnsConflict()
+    {
+        var services = new ServiceCollection()
+            .AddLogging()
+            .AddMetrics()
+            .AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("Vpd.Api.Tests"))
+            .BuildServiceProvider();
+        var application = new ApplicationBuilder(services);
+        application.UseErrorHandling();
+        application.Run(_ => throw new DbUpdateConcurrencyException("live tableau changed meanwhile"));
+        var pipeline = application.Build();
+        var context = new DefaultHttpContext
+        {
+            RequestServices = services
+        };
+        await using var responseBody = new MemoryStream();
+        context.Response.Body = responseBody;
+
+        await pipeline(context);
+
+        context.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
+    }
 }
```

- [ ] Implémentation :

Modifier `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/Configurations/AssoEventConfiguration.cs` :

```diff
@@ -40,6 +40,12 @@ public class AssoEventConfiguration : IEntityTypeConfiguration<AssoEvents>
                 value => new EventsType((EventsType.EventsTypeEnum)value)
             );
 
+        // Every live tableau mutation increments it: concurrent mutations of the same
+        // event cannot both succeed, and each revision describes one board state.
+        builder.Property(events => events.LiveRevision)
+            .IsConcurrencyToken()
+            .HasDefaultValue(0);
+
         builder.Property(events => events.IsCancelled)
             .HasDefaultValue(false);
```

Modifier `src/Backend/Vole_Papillon_Damour.Application/Common/Interfaces/Persistence/IEventRepository.cs` :

```diff
@@ -10,4 +10,12 @@ public interface IEventRepository: IRepository<AssoEvents>
     Task<AssoEvents?> GetNextBooksAsync();
     Task<List<AssoEvents>> GetNextOtherAsync();
     Task<List<AssoEvents>> GetNextEventsAsync();
+
+    /// <summary>Events not cancelled that start or end at or after <paramref name="fromUtc"/>.</summary>
+    Task<List<AssoEvents>> GetNotCancelledFromAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken);
+
+    /// <summary>Current <c>LiveRevision</c> of each existing event among <paramref name="ids"/>.</summary>
+    Task<IReadOnlyDictionary<AssoEventsId, int>> GetLiveRevisionsAsync(
+        IReadOnlyCollection<AssoEventsId> ids,
+        CancellationToken cancellationToken);
 }
\ No newline at end of file
```

Modifier `src/Backend/Vole_Papillon_Damour.Infrastructure/Persistence/Repositories/EventRepository.cs` :

```diff
@@ -72,4 +72,33 @@ public class EventRepository: BaseRepository<AssoEvents, ProjectDbContext>, IEve
             .OrderBy(x => x.DateStart)
             .ToListAsync();
     }
+
+    public async Task<List<AssoEvents>> GetNotCancelledFromAsync(
+        DateTimeOffset fromUtc,
+        CancellationToken cancellationToken)
+    {
+        return await Context.AssoEvents
+            .Where(x => !x.IsCancelled && (x.DateStart >= fromUtc || x.DateEnd >= fromUtc))
+            .OrderBy(x => x.DateStart)
+            .ToListAsync(cancellationToken);
+    }
+
+    public async Task<IReadOnlyDictionary<AssoEventsId, int>> GetLiveRevisionsAsync(
+        IReadOnlyCollection<AssoEventsId> ids,
+        CancellationToken cancellationToken)
+    {
+        if (ids.Count == 0)
+        {
+            return new Dictionary<AssoEventsId, int>();
+        }
+
+        var requestedIds = ids.ToList();
+        var revisions = await Context.AssoEvents
+            .AsNoTracking()
+            .Where(x => requestedIds.Contains(x.Id))
+            .Select(x => new { x.Id, x.LiveRevision })
+            .ToListAsync(cancellationToken);
+
+        return revisions.ToDictionary(x => x.Id, x => x.LiveRevision);
+    }
 }
```

Modifier `src/Backend/Vole_Papillon_Damour.Api/Errors/ErrorHandling.cs` :

```diff
@@ -1,4 +1,5 @@
 using Microsoft.AspNetCore.Diagnostics;
+using Microsoft.EntityFrameworkCore;
 
 namespace Vole_Papillon_Damour.Api.Errors;
 
@@ -18,6 +19,11 @@ public static class ErrorHandling
                         HttpRequestException => (
                             StatusCodes.Status503ServiceUnavailable,
                             "The bibliographic metadata providers are temporarily unavailable."),
+                        // Two live tableau actions on the same event at the same time:
+                        // the second one is rejected and can be retried.
+                        DbUpdateConcurrencyException => (
+                            StatusCodes.Status409Conflict,
+                            "The resource was modified at the same time. Please retry."),
                         _ => (StatusCodes.Status500InternalServerError, "An error occurred.")
                     };
                     await Results.Problem(
```

- [ ] Générer la migration (outil `dotnet-ef` 10) :
  `dotnet ef migrations add AddAssoEventLiveRevision --project src/Backend/Vole_Papillon_Damour.Infrastructure --startup-project src/Backend/Vole_Papillon_Damour.Api`.
  Contenu attendu de `Up` / `Down`, et rien d'autre dans le snapshot que la propriété
  `LiveRevision` (`int`, défaut 0, `IsConcurrencyToken`) :

```csharp
migrationBuilder.AddColumn<int>(
    name: "LiveRevision",
    table: "AssoEvents",
    type: "int",
    nullable: false,
    defaultValue: 0);
// Down
migrationBuilder.DropColumn(name: "LiveRevision", table: "AssoEvents");
```

- [ ] Tests : `EventRepositoryLiveTableauTests` (2) et `ErrorHandlingTests` (3) verts.
- [ ] Commit : `feat(loto): persist the live revision as a concurrency token`

## Tâche 3 — Application : révision des mutations, révisions, événement du jour

RG-10, RG-16, RG-17, RG-18. Les quatre handlers du direct appellent `MarkLiveChange()`
avant `UpdateAsync`. La révision est exposée dans `AssoEventResult` et `EventResponse`.
Deux requêtes s'ajoutent : `GetLiveRevisionsQuery` et `GetTodayEventQuery`, cette dernière
plaçant le loto en premier.

**Fichiers :**
- Modifier : les handlers `AddNumeroToEvent`, `RemoveLastNumero`, `AddWinPartie`,
  `AddBingoWin` (`src/Backend/Vole_Papillon_Damour.Application/Events/Commands/...`)
- Modifier : `src/Backend/Vole_Papillon_Damour.Application/Events/Common/AssoEventResult.cs`,
  `src/Backend/Vole_Papillon_Damour.Contracts/Events/Responses/EventResponse.cs`
- Créer : `Application/Common/ParisCalendar.cs`, `Application/Events/Common/TodayEventResult.cs`,
  `Application/Events/Queries/GetTodayEvent/*`, `Application/Events/Queries/GetLiveRevisions/*`
- Test : `src/Backend/Vole_Papillon_Damour.Application.tests/Events/Commands/LiveTableauRevisionTests.cs`,
  `src/Backend/Vole_Papillon_Damour.Application.tests/Events/Queries/GetTodayEventQueryHandlerTests.cs`

**Interfaces :**
- Consomme : la tâche 1 et la tâche 2.
- Produit : `GetTodayEventQuery : IRequest<TodayEventResult?>`,
  `GetLiveRevisionsQuery(IReadOnlyCollection<AssoEventsId>) : IRequest<IReadOnlyDictionary<AssoEventsId,int>>`,
  `TodayEventResult(Id, Name, EventsType, DateStart, DateEnd, HourOpenDoors, HourCloseDoors, City, LiveTableauStatus? LiveStatus, int? LastNumero)`,
  `ParisCalendar.Zone / Today(DateTime utcNow) / StartOfDayUtc(DateOnly)`,
  `int LiveRevision` en fin de `AssoEventResult` et dans `EventResponse`.

- [ ] **Tests d'abord** :

Créer `src/Backend/Vole_Papillon_Damour.Application.tests/Events/Commands/LiveTableauRevisionTests.cs` :

```csharp
using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Commands.Events.AddBingoWin;
using Vole_Papillon_Damour.Application.Events.Commands.Events.AddWinPartie;
using Vole_Papillon_Damour.Application.Events.Commands.Numeros.AddNumeroToEvent;
using Vole_Papillon_Damour.Application.Events.Commands.RemoveLastNumero;
using Vole_Papillon_Damour.Application.tests.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;

namespace Vole_Papillon_Damour.Application.tests.Events.Commands;

/// <summary>Every persisted live tableau change moves the revision sent to the public tableau.</summary>
public class LiveTableauRevisionTests
{
    [Fact]
    public async Task AddNumero_IncrementsTheRevisionReturnedAndPersisted()
    {
        var (repository, assoEvent) = CreateRepositoryWithLoto();

        var result = await new AddNumeroToEventCommandHandler(repository, TestMapperFactory.Create())
            .Handle(new AddNumeroToEventCommand(assoEvent.Id, 42), CancellationToken.None);

        result.Value.LiveRevision.Should().Be(1);
        assoEvent.LiveRevision.Should().Be(1);
    }

    [Fact]
    public async Task AddNumero_WhenRejected_KeepsTheRevision()
    {
        var (repository, assoEvent) = CreateRepositoryWithLoto();
        assoEvent.Parties![0].AddLiveNumero(42);

        var result = await new AddNumeroToEventCommandHandler(repository, TestMapperFactory.Create())
            .Handle(new AddNumeroToEventCommand(assoEvent.Id, 42), CancellationToken.None);

        result.IsError.Should().BeTrue();
        assoEvent.LiveRevision.Should().Be(0);
    }

    [Fact]
    public async Task RemoveLastNumero_IncrementsTheRevision()
    {
        var (repository, assoEvent) = CreateRepositoryWithLoto();
        assoEvent.Parties![0].AddLiveNumero(42);

        var result = await new RemoveLastNumeroCommandHandler(repository, TestMapperFactory.Create())
            .Handle(new RemoveLastNumeroCommand(assoEvent.Id), CancellationToken.None);

        result.Value.LiveRevision.Should().Be(1);
    }

    [Fact]
    public async Task AddWinPartie_IncrementsTheRevision()
    {
        var (repository, assoEvent) = CreateRepositoryWithLoto();
        assoEvent.Parties![0].AddLiveNumero(42);

        var result = await new AddWinPartieCommandHandler(repository, TestMapperFactory.Create())
            .Handle(new AddWinPartieCommand(assoEvent.Id), CancellationToken.None);

        result.Value.LiveRevision.Should().Be(1);
    }

    [Fact]
    public async Task AddBingoWin_IncrementsTheRevision()
    {
        var (repository, assoEvent) = CreateRepositoryWithLoto();

        var result = await new AddBingoWinCommandHandler(repository, TestMapperFactory.Create())
            .Handle(new AddBingoWinCommand(assoEvent.Id, true), CancellationToken.None);

        result.Value.LiveRevision.Should().Be(1);
    }

    private static (IEventRepository Repository, AssoEvents AssoEvent) CreateRepositoryWithLoto()
    {
        var assoEvent = LiveTableauTestData.CreateEvent(LiveTableauTestData.CreatePartie(0));
        var repository = Substitute.For<IEventRepository>();
        repository.GetByIdAsync(assoEvent.Id).Returns(Task.FromResult<AssoEvents?>(assoEvent));
        repository.UpdateAsync(Arg.Any<AssoEvents>()).Returns(callInfo => Task.FromResult(callInfo.Arg<AssoEvents>()));
        return (repository, assoEvent);
    }
}
```

Créer `src/Backend/Vole_Papillon_Damour.Application.tests/Events/Queries/GetTodayEventQueryHandlerTests.cs` :

```csharp
using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Queries.GetTodayEvent;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Events.Queries;

public class GetTodayEventQueryHandlerTests
{
    // 15 novembre 2026, 10 h 00 à Paris.
    private static readonly DateTime Now = new(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WithoutEventToday_ReturnsNull()
    {
        var result = await Handle(Now, CreateEvent("Bourse", EventsType.EventsTypeEnum.Books, "2026-11-16T08:00:00Z"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PrefersTheLotoWhenSeveralEventsHappenToday()
    {
        var bourse = CreateEvent("Bourse", EventsType.EventsTypeEnum.Books, "2026-11-15T07:00:00Z");
        var loto = CreateEvent("Grand loto", EventsType.EventsTypeEnum.Bingo, "2026-11-15T13:00:00Z");

        var result = await Handle(Now, bourse, loto);

        result!.Id.Should().Be(loto.Id);
        result.LiveStatus.Should().Be(LiveTableauStatus.NotStarted);
        result.LastNumero.Should().BeNull();
        result.City.Should().Be("Arras");
    }

    [Fact]
    public async Task Handle_ForALiveLoto_ReturnsItsLastNumber()
    {
        var loto = CreateEvent("Grand loto", EventsType.EventsTypeEnum.Bingo, "2026-11-15T13:00:00Z");
        loto.Parties![0].AddLiveNumero(42);

        var result = await Handle(Now, loto);

        result!.LiveStatus.Should().Be(LiveTableauStatus.Live);
        result.LastNumero.Should().Be(42);
    }

    [Fact]
    public async Task Handle_ForAnotherEventType_HasNoLiveStatus()
    {
        var result = await Handle(Now, CreateEvent("Bourse", EventsType.EventsTypeEnum.Books, "2026-11-15T07:00:00Z"));

        result!.Name.Should().Be("Bourse");
        result.LiveStatus.Should().BeNull();
        result.LastNumero.Should().BeNull();
    }

    [Fact]
    public async Task Handle_JustAfterParisMidnight_UsesTheParisDay()
    {
        // 23 h 30 UTC le 14 = 0 h 30 à Paris le 15 ; loto stocké à minuit heure de Paris.
        var loto = CreateEvent("Grand loto", EventsType.EventsTypeEnum.Bingo, "2026-11-14T23:00:00Z");

        var result = await Handle(new DateTime(2026, 11, 14, 23, 30, 0, DateTimeKind.Utc), loto);

        result!.Id.Should().Be(loto.Id);
    }

    [Fact]
    public async Task Handle_IgnoresACancelledEvent()
    {
        var loto = CreateEvent("Grand loto", EventsType.EventsTypeEnum.Bingo, "2026-11-15T13:00:00Z");
        loto.Cancel();

        var result = await Handle(Now, loto);

        result.Should().BeNull();
    }

    private static async Task<Vole_Papillon_Damour.Application.Events.Common.TodayEventResult?> Handle(
        DateTime utcNow,
        params AssoEvents[] events)
    {
        var repository = Substitute.For<IEventRepository>();
        repository.GetNotCancelledFromAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(events.ToList());
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(utcNow);

        return await new GetTodayEventQueryHandler(repository, clock)
            .Handle(new GetTodayEventQuery(), CancellationToken.None);
    }

    private static AssoEvents CreateEvent(string name, EventsType.EventsTypeEnum type, string dateStart)
    {
        var partie = Partie.Create(
            "Partie 1",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            [LinePartie.Create([Lot.Create("Lot", "lot.jpg", 0)], new NumberLine(NumberLine.NumberLineEnum.OneLine), 0)]);

        return AssoEvents.Create(
            name,
            new Uri("https://example.com/event.jpg"),
            new EventsType(type),
            DateTimeOffset.Parse(dateStart),
            null,
            null,
            null,
            null,
            new Adresse(12, "Arras", "Rue du loto", 62000),
            null,
            [partie],
            name);
    }
}
```

- [ ] Implémentation :

Modifier `src/Backend/Vole_Papillon_Damour.Application/Events/Commands/Numeros/AddNumeroToEvent/AddNumeroToEventCommandHandler.cs` :

```diff
@@ -41,6 +41,7 @@ public class AddNumeroToEventCommandHandler(IEventRepository eventRepository, IM
             }
         }
 
+        assoEvent.MarkLiveChange();
         assoEvent = await eventRepository.UpdateAsync(assoEvent);
         return mapper.Map<AssoEventResult>(assoEvent);
     }
```

Ajouter la même ligne `assoEvent.MarkLiveChange();` juste avant `assoEvent = await eventRepository.UpdateAsync(assoEvent);` dans :
`Events/Commands/Numeros/RemoveLastNumero/RemoveLastNumeroCommandHandler.cs`,
`Events/Commands/Events/AddWinPartie/AddWinPartieCommandHandler.cs`,
`Events/Commands/Events/AddBingoWin/AddBingoWinCommandHandler.cs`.

Modifier `src/Backend/Vole_Papillon_Damour.Application/Events/Common/AssoEventResult.cs` :

```diff
@@ -20,7 +20,8 @@ public record AssoEventResult(
     List<int> BingoNumeros,
     IReadOnlyList<PartieResult> Parties,
     int CurrentPartieIndex,
-    bool IsCancelled
+    bool IsCancelled,
+    int LiveRevision
     );
 
 public record PartieResult(
```

Modifier `src/Backend/Vole_Papillon_Damour.Contracts/Events/Responses/EventResponse.cs` :

```diff
@@ -28,6 +28,9 @@ public class EventResponse
     public List<int> BingoNumeros { get; set; }
     public bool BingoHasBeenWon { get; set; }
     public int CurrentPartieIndex { get; set; }
+
+    /// <summary>Revision of the live tableau, sent as the SSE event id.</summary>
+    public int LiveRevision { get; set; }
 }
 
 public class CreatePartiesResponse
```

Créer `src/Backend/Vole_Papillon_Damour.Application/Common/ParisCalendar.cs` :

```csharp
namespace Vole_Papillon_Damour.Application.Common;

/// <summary>
/// Calendar days as the association lives them: an event is "today" according to
/// the clock in Paris, whatever the server's time zone.
/// </summary>
public static class ParisCalendar
{
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Paris");

    public static DateOnly Today(DateTime utcNow)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, Zone));
    }

    public static DateTimeOffset StartOfDayUtc(DateOnly day)
    {
        var parisMidnight = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(parisMidnight, Zone), TimeSpan.Zero);
    }
}
```

Créer `src/Backend/Vole_Papillon_Damour.Application/Events/Common/TodayEventResult.cs` :

```csharp
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Common;

/// <summary>
/// The event shown in the site's "today" banner. <see cref="LiveStatus"/> and
/// <see cref="LastNumero"/> are only set for a loto.
/// </summary>
public record TodayEventResult(
    AssoEventsId Id,
    string Name,
    EventsType EventsType,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    DateTimeOffset? HourOpenDoors,
    DateTimeOffset? HourCloseDoors,
    string City,
    LiveTableauStatus? LiveStatus,
    int? LastNumero);
```

Créer `src/Backend/Vole_Papillon_Damour.Application/Events/Queries/GetTodayEvent/GetTodayEventQuery.cs` :

```csharp
using MediatR;
using Vole_Papillon_Damour.Application.Events.Common;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetTodayEvent;

/// <summary>The event happening today in Paris, a loto first; <c>null</c> when there is none.</summary>
public record GetTodayEventQuery : IRequest<TodayEventResult?>;
```

Créer `src/Backend/Vole_Papillon_Damour.Application/Events/Queries/GetTodayEvent/GetTodayEventQueryHandler.cs` :

```csharp
using MediatR;
using Vole_Papillon_Damour.Application.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetTodayEvent;

public class GetTodayEventQueryHandler(IEventRepository eventRepository, IDateTimeProvider clock)
    : IRequestHandler<GetTodayEventQuery, TodayEventResult?>
{
    public async Task<TodayEventResult?> Handle(GetTodayEventQuery query, CancellationToken cancellationToken)
    {
        var today = ParisCalendar.Today(clock.UtcNow);
        // One day of margin: an event stored at Paris midnight starts the previous UTC day.
        var candidates = await eventRepository.GetNotCancelledFromAsync(
            ParisCalendar.StartOfDayUtc(today).AddDays(-1),
            cancellationToken);

        var todayEvent = candidates
            .Where(assoEvent => assoEvent.IsHappeningOn(today, ParisCalendar.Zone))
            .OrderBy(assoEvent => IsLoto(assoEvent.EventsType) ? 0 : 1)
            .ThenBy(assoEvent => assoEvent.DateStart)
            .FirstOrDefault();

        if (todayEvent is null)
        {
            return null;
        }

        var isLoto = IsLoto(todayEvent.EventsType);
        return new TodayEventResult(
            todayEvent.Id,
            todayEvent.Name,
            todayEvent.EventsType,
            todayEvent.DateStart,
            todayEvent.DateEnd,
            todayEvent.HourOpenDoors,
            todayEvent.HourCloseDoors,
            todayEvent.Adresse.City,
            isLoto ? todayEvent.GetLiveStatus() : null,
            isLoto ? todayEvent.GetLastDrawnNumero() : null);
    }

    private static bool IsLoto(EventsType eventsType) =>
        eventsType.Value == EventsType.EventsTypeEnum.Bingo;
}
```

Créer `src/Backend/Vole_Papillon_Damour.Application/Events/Queries/GetLiveRevisions/GetLiveRevisionsQuery.cs` :

```csharp
using MediatR;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetLiveRevisions;

public record GetLiveRevisionsQuery(IReadOnlyCollection<AssoEventsId> AssoEventsIds)
    : IRequest<IReadOnlyDictionary<AssoEventsId, int>>;
```

Créer `src/Backend/Vole_Papillon_Damour.Application/Events/Queries/GetLiveRevisions/GetLiveRevisionsQueryHandler.cs` :

```csharp
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetLiveRevisions;

public class GetLiveRevisionsQueryHandler(IEventRepository eventRepository)
    : IRequestHandler<GetLiveRevisionsQuery, IReadOnlyDictionary<AssoEventsId, int>>
{
    public Task<IReadOnlyDictionary<AssoEventsId, int>> Handle(
        GetLiveRevisionsQuery query,
        CancellationToken cancellationToken) =>
        eventRepository.GetLiveRevisionsAsync(query.AssoEventsIds, cancellationToken);
}
```

- [ ] Tests : 11 nouveaux verts. Les tests existants des handlers restent verts.
- [ ] Commit : `feat(loto): expose live revisions and today's event`

## Tâche 4 — API : direct SSE propre et endpoint du jour

RG-10 à RG-15, RG-17, RG-18. Le hub en mémoire, le flux SSE, la synchronisation entre
réplicas et `GET /asso-events/today` (cache de sortie 10 s, 204 sans événement) remplacent
`ISSEClientManager`.

**Fichiers :**
- Créer : `src/Backend/Vole_Papillon_Damour.Api/LiveTableau/` (`LiveTableauSnapshot.cs`,
  `LiveTableauOptions.cs`, `ILiveTableauHub.cs`, `LiveTableauHub.cs`, `LiveTableauStream.cs`,
  `LiveTableauSyncService.cs`, `LiveTableauServiceCollectionExtensions.cs`)
- Créer : `src/Backend/Vole_Papillon_Damour.Contracts/Events/Responses/TodayEventResponse.cs`
- Modifier : `src/Backend/Vole_Papillon_Damour.Api/Controllers/AssoEventsController/EventsController.cs`,
  `src/Backend/Vole_Papillon_Damour.Api/Common/Mapping/EventMappingConfig.cs`,
  `src/Backend/Vole_Papillon_Damour.Api/DependencyInjection.cs`,
  `src/Backend/Vole_Papillon_Damour.Infrastructure/DependencyInjection.cs`
- Supprimer : `src/Backend/Vole_Papillon_Damour.Application/Common/Interfaces/Services/SSEClientManager.cs`,
  `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/SSEClientManager.cs`,
  `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Services/SSEClientManagerTests.cs`
- Modifier (doublure) : `Api.tests/EndpointAuthorizationTests.cs`,
  `Api.tests/MemberAccount/MemberCardEndpointsTests.cs`,
  `Api.tests/MemberAccount/MemberSelectionEndpointsTests.cs`,
  `Api.tests/MemberAccount/NotFoundReportEndpointsTests.cs`,
  `Api.tests/NotFoundReports/NotFoundReportAdministrationEndpointsTests.cs`
- Test : `src/Backend/Vole_Papillon_Damour.Api.tests/LiveTableau/` (`LiveTableauTestEvents.cs`,
  `LiveTableauHubTests.cs`, `LiveTableauStreamTests.cs`, `LiveTableauSyncServiceTests.cs`,
  `LiveTableauEndpointTests.cs`)

**Interfaces produites :**
- Flux : `GET /asso-events/{id}/tableau/sse`. Chaque élément porte `id: <révision>`. Le
  premier envoie `retry: 3000` et `data:` = `EventResponse` en camelCase avec
  `liveRevision`. Un `event: ping` est émis après 15 s de silence. Événement inconnu : 404.
- Endpoint du jour : `GET /asso-events/today` → 200 `TodayEventResponse`, ou 204.

- [ ] **Tests d'abord** :

Créer `src/Backend/Vole_Papillon_Damour.Api.tests/LiveTableau/LiveTableauTestEvents.cs` :

```csharp
using Mapster;
using MapsterMapper;
using Vole_Papillon_Damour.Api.Common.Mapping;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.LiveTableau;

internal static class LiveTableauTestEvents
{
    /// <summary>
    /// The API mapping, as in production: <see cref="EventMappingConfig"/> registers
    /// some of its conversions on the global settings.
    /// </summary>
    public static IMapper CreateMapper()
    {
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(typeof(EventMappingConfig).Assembly);
        return new Mapper(config);
    }

    public static AssoEvents CreateLoto()
    {
        var partie = Partie.Create(
            "Panier gourmand",
            new PartieType(PartieType.PartieTypeEnum.Standard),
            0,
            false,
            [LinePartie.Create([Lot.Create("Panier garni", "lot.jpg", 0)], new NumberLine(NumberLine.NumberLineEnum.OneLine), 0)]);

        return AssoEvents.Create(
            "Grand loto",
            new Uri("https://example.com/loto.jpg"),
            new EventsType(EventsType.EventsTypeEnum.Bingo),
            new DateTimeOffset(2026, 11, 15, 13, 0, 0, TimeSpan.Zero),
            null,
            null,
            null,
            null,
            new Adresse(12, "Arras", "Rue du loto", 62000),
            null,
            [partie],
            "Loto");
    }
}
```

Créer `src/Backend/Vole_Papillon_Damour.Api.tests/LiveTableau/LiveTableauHubTests.cs` :

```csharp
using FluentAssertions;
using Vole_Papillon_Damour.Api.LiveTableau;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.LiveTableau;

public sealed class LiveTableauHubTests
{
    [Fact]
    public void Publish_ReachesEveryClientOfTheEventAndOnlyThem()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        var otherEvent = AssoEventsId.CreateUnique();
        using var firstTab = hub.Subscribe(loto);
        using var secondTab = hub.Subscribe(loto);
        using var otherClient = hub.Subscribe(otherEvent);

        hub.Publish(loto, new LiveTableauSnapshot(1, "{}"));

        firstTab.Reader.TryRead(out var first).Should().BeTrue();
        secondTab.Reader.TryRead(out var second).Should().BeTrue();
        first!.Revision.Should().Be(1);
        second!.Revision.Should().Be(1);
        otherClient.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void Publish_KeepsOnlyTheNewestSnapshotForASlowClient()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        using var slowClient = hub.Subscribe(loto);

        hub.Publish(loto, new LiveTableauSnapshot(1, "{\"n\":1}"));
        hub.Publish(loto, new LiveTableauSnapshot(2, "{\"n\":2}"));
        hub.Publish(loto, new LiveTableauSnapshot(3, "{\"n\":3}"));

        slowClient.Reader.TryRead(out var snapshot).Should().BeTrue();
        snapshot!.Revision.Should().Be(3);
        slowClient.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void Dispose_RemovesTheClientAndForgetsTheRevisionOfAnUnwatchedEvent()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        var firstTab = hub.Subscribe(loto);
        var secondTab = hub.Subscribe(loto);
        hub.Publish(loto, new LiveTableauSnapshot(4, "{}"));

        firstTab.Dispose();
        hub.GetWatchedEvents().Should().ContainSingle().Which.Should().Be(loto);
        hub.GetKnownRevision(loto).Should().Be(4);

        secondTab.Dispose();
        secondTab.Dispose();
        hub.GetWatchedEvents().Should().BeEmpty();
        hub.GetKnownRevision(loto).Should().BeNull();
        secondTab.Reader.TryRead(out _).Should().BeTrue();
        secondTab.Reader.Completion.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void ObserveRevision_NeverGoesBack()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        using var client = hub.Subscribe(loto);

        hub.ObserveRevision(loto, 7);
        hub.ObserveRevision(loto, 5);

        hub.GetKnownRevision(loto).Should().Be(7);
    }
}
```

Créer `src/Backend/Vole_Papillon_Damour.Api.tests/LiveTableau/LiveTableauStreamTests.cs` :

```csharp
using System.Net.ServerSentEvents;
using FluentAssertions;
using Vole_Papillon_Damour.Api.LiveTableau;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.LiveTableau;

public sealed class LiveTableauStreamTests
{
    private static readonly TimeSpan LongHeartbeat = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Reconnection = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task ReadAsync_StartsWithTheInitialStateItsRevisionAndTheReconnectionDelay()
    {
        var hub = new LiveTableauHub();
        var subscription = hub.Subscribe(AssoEventsId.CreateUnique());
        using var cancellation = new CancellationTokenSource();
        await using var stream = LiveTableauStream
            .ReadAsync(new LiveTableauSnapshot(3, "{\"state\":3}"), subscription, LongHeartbeat, Reconnection, cancellation.Token)
            .GetAsyncEnumerator();

        (await stream.MoveNextAsync()).Should().BeTrue();

        stream.Current.Data.Should().Be("{\"state\":3}");
        stream.Current.EventId.Should().Be("3");
        stream.Current.ReconnectionInterval.Should().Be(Reconnection);
        stream.Current.EventType.Should().Be("message");
    }

    [Fact]
    public async Task ReadAsync_SendsNewerSnapshotsAndSkipsOnesAlreadyInTheInitialState()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        var subscription = hub.Subscribe(loto);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream = LiveTableauStream
            .ReadAsync(new LiveTableauSnapshot(5, "{}"), subscription, LongHeartbeat, Reconnection, cancellation.Token)
            .GetAsyncEnumerator();
        await stream.MoveNextAsync();

        hub.Publish(loto, new LiveTableauSnapshot(5, "{\"stale\":true}"));
        var next = stream.MoveNextAsync();
        hub.Publish(loto, new LiveTableauSnapshot(6, "{\"state\":6}"));

        (await next).Should().BeTrue();
        stream.Current.EventId.Should().Be("6");
        stream.Current.Data.Should().Be("{\"state\":6}");
        stream.Current.ReconnectionInterval.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_SendsAPingAfterAHeartbeatOfSilence()
    {
        var hub = new LiveTableauHub();
        var subscription = hub.Subscribe(AssoEventsId.CreateUnique());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream = LiveTableauStream
            .ReadAsync(new LiveTableauSnapshot(1, "{}"), subscription, TimeSpan.FromMilliseconds(50), Reconnection, cancellation.Token)
            .GetAsyncEnumerator();
        await stream.MoveNextAsync();

        (await stream.MoveNextAsync()).Should().BeTrue();

        stream.Current.EventType.Should().Be(LiveTableauStream.PingEventType);
        stream.Current.EventId.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_EndsAndUnsubscribesWhenTheClientLeaves()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        var subscription = hub.Subscribe(loto);
        using var cancellation = new CancellationTokenSource();
        var items = new List<SseItem<string>>();

        var reading = Task.Run(async () =>
        {
            await foreach (var item in LiveTableauStream.ReadAsync(
                               new LiveTableauSnapshot(1, "{}"), subscription, LongHeartbeat, Reconnection, cancellation.Token))
            {
                items.Add(item);
            }
        });
        await Task.Delay(100);
        cancellation.Cancel();
        await reading.WaitAsync(TimeSpan.FromSeconds(5));

        items.Should().ContainSingle();
        hub.GetWatchedEvents().Should().BeEmpty();
    }
}
```

Créer `src/Backend/Vole_Papillon_Damour.Api.tests/LiveTableau/LiveTableauSyncServiceTests.cs` :

```csharp
using ErrorOr;
using FluentAssertions;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Api.LiveTableau;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Application.Events.Queries.GetAssoEventById;
using Vole_Papillon_Damour.Application.Events.Queries.GetLiveRevisions;
using Vole_Papillon_Damour.Contracts.Events.Responses;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.LiveTableau;

public sealed class LiveTableauSyncServiceTests
{
    [Fact]
    public async Task SyncOnce_BringsANumberDrawnOnAnotherReplicaToThisReplicaClients()
    {
        // The database, shared by both replicas.
        var loto = LiveTableauTestEvents.CreateLoto();
        loto.MarkLiveChange();
        var mapper = LiveTableauTestEvents.CreateMapper();
        var sender = SenderReading(loto, mapper);

        // A phone is connected to replica B and knows revision 1.
        var replicaB = new LiveTableauHub();
        using var phone = replicaB.Subscribe(loto.Id);
        replicaB.ObserveRevision(loto.Id, loto.LiveRevision);

        // The operator draws 42 through replica A.
        var replicaA = new LiveTableauHub();
        loto.Parties![0].AddLiveNumero(42);
        loto.MarkLiveChange();
        replicaA.Publish(loto.Id, LiveTableauSnapshot.From(mapper.Map<EventResponse>(mapper.Map<AssoEventResult>(loto))));
        phone.Reader.TryRead(out _).Should().BeFalse();

        await CreateService(replicaB, sender, mapper).SyncOnceAsync(CancellationToken.None);

        phone.Reader.TryRead(out var snapshot).Should().BeTrue();
        snapshot!.Revision.Should().Be(2);
        snapshot.Json.Should().Contain("\"liveNumeros\":[42]");
    }

    [Fact]
    public async Task SyncOnce_WithoutConnectedClient_DoesNotQueryTheDatabase()
    {
        var sender = Substitute.For<ISender>();

        await CreateService(new LiveTableauHub(), sender, LiveTableauTestEvents.CreateMapper())
            .SyncOnceAsync(CancellationToken.None);

        await sender.DidNotReceiveWithAnyArgs().Send(Arg.Any<GetLiveRevisionsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncOnce_WhenTheRevisionDidNotMove_DoesNotReloadTheEvent()
    {
        var loto = LiveTableauTestEvents.CreateLoto();
        loto.MarkLiveChange();
        var mapper = LiveTableauTestEvents.CreateMapper();
        var sender = SenderReading(loto, mapper);
        var hub = new LiveTableauHub();
        using var phone = hub.Subscribe(loto.Id);
        hub.ObserveRevision(loto.Id, loto.LiveRevision);

        await CreateService(hub, sender, mapper).SyncOnceAsync(CancellationToken.None);

        await sender.DidNotReceive().Send(Arg.Any<GetAssoEventByIdQuery>(), Arg.Any<CancellationToken>());
        phone.Reader.TryRead(out _).Should().BeFalse();
    }

    private static ISender SenderReading(AssoEvents loto, IMapper mapper)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetLiveRevisionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyDictionary<AssoEventsId, int>)new Dictionary<AssoEventsId, int>
            {
                [loto.Id] = loto.LiveRevision
            });
        sender.Send(Arg.Any<GetAssoEventByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => (ErrorOr<AssoEventResult>)mapper.Map<AssoEventResult>(loto));
        return sender;
    }

    private static LiveTableauSyncService CreateService(ILiveTableauHub hub, ISender sender, IMapper mapper)
    {
        var services = new ServiceCollection()
            .AddSingleton(sender)
            .AddSingleton(mapper)
            .BuildServiceProvider();

        return new LiveTableauSyncService(
            hub,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new LiveTableauOptions()),
            NullLogger<LiveTableauSyncService>.Instance);
    }
}
```

Créer `src/Backend/Vole_Papillon_Damour.Api.tests/LiveTableau/LiveTableauEndpointTests.cs` :

```csharp
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ErrorOr;
using FluentAssertions;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Controllers.AssoEventsController;
using Vole_Papillon_Damour.Api.LiveTableau;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Application.Events.Queries.GetAssoEventById;
using Vole_Papillon_Damour.Application.Events.Queries.GetTodayEvent;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using DomainErrors = Vole_Papillon_Damour.Domain.Common.Errors.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.LiveTableau;

public sealed class LiveTableauEndpointTests
{
    [Fact]
    public async Task Sse_ForAnUnknownEvent_Returns404InsteadOfAnOpenStream()
    {
        var unknownId = AssoEventsId.CreateUnique();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetAssoEventByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns((ErrorOr<AssoEventResult>)DomainErrors.AssoEvent.AssoEventNotFound(unknownId));
        var hub = new LiveTableauHub();
        await using var application = CreateApplication(mediator, hub);

        var context = await InvokeAsync(application, "/asso-events/{id}/tableau/sse", unknownId.Value, TimeSpan.FromSeconds(5));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
        context.Response.ContentType.Should().NotBe("text/event-stream");
        hub.GetWatchedEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task Sse_ForAKnownEvent_StreamsTheStateWithItsRevisionWithoutBuffering()
    {
        var loto = LiveTableauTestEvents.CreateLoto();
        loto.MarkLiveChange();
        loto.MarkLiveChange();
        loto.MarkLiveChange();
        var mapper = LiveTableauTestEvents.CreateMapper();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetAssoEventByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns((ErrorOr<AssoEventResult>)mapper.Map<AssoEventResult>(loto));
        var hub = new LiveTableauHub();
        await using var application = CreateApplication(mediator, hub, mapper);

        // The client leaves after 300 ms; the stream must then end on its own.
        var context = await InvokeAsync(application, "/asso-events/{id}/tableau/sse", loto.Id.Value, TimeSpan.FromMilliseconds(300));
        var body = await ReadBodyAsync(context);

        context.Response.ContentType.Should().StartWith("text/event-stream");
        context.Response.Headers["X-Accel-Buffering"].ToString().Should().Be("no");
        body.Should().Contain("id: 3\n");
        body.Should().Contain("retry: 3000\n");
        body.Should().Contain("data: {\"id\":\"" + loto.Id.Value);
        body.Should().Contain("\"liveRevision\":3");
        hub.GetWatchedEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task Today_WithoutEvent_Returns204()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetTodayEventQuery>(), Arg.Any<CancellationToken>())
            .Returns((TodayEventResult?)null);
        await using var application = CreateApplication(mediator, new LiveTableauHub());

        var context = await InvokeAsync(application, "/asso-events/today", null, TimeSpan.FromSeconds(5));

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Today_WithALiveLoto_ReturnsItsStatusAndLastNumber()
    {
        var id = AssoEventsId.CreateUnique();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetTodayEventQuery>(), Arg.Any<CancellationToken>())
            .Returns(new TodayEventResult(
                id,
                "Grand loto",
                new EventsType(EventsType.EventsTypeEnum.Bingo),
                new DateTimeOffset(2026, 11, 15, 13, 0, 0, TimeSpan.Zero),
                null,
                new DateTimeOffset(2026, 11, 15, 12, 30, 0, TimeSpan.Zero),
                null,
                "Arras",
                LiveTableauStatus.Live,
                42));
        await using var application = CreateApplication(mediator, new LiveTableauHub(), LiveTableauTestEvents.CreateMapper());

        var context = await InvokeAsync(application, "/asso-events/today", null, TimeSpan.FromSeconds(5));
        var json = JsonNode.Parse(await ReadBodyAsync(context))!.AsObject();

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        json["id"]!.GetValue<Guid>().Should().Be(id.Value);
        json["eventType"]!.GetValue<string>().Should().Be("Bingo");
        json["liveStatus"]!.GetValue<string>().Should().Be("Live");
        json["lastNumero"]!.GetValue<int>().Should().Be(42);
        json["city"]!.GetValue<string>().Should().Be("Arras");
    }

    private static WebApplication CreateApplication(IMediator mediator, ILiveTableauHub hub, IMapper? mapper = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(mediator);
        builder.Services.AddSingleton(mapper ?? Substitute.For<IMapper>());
        builder.Services.AddSingleton(hub);
        builder.Services.AddOptions<LiveTableauOptions>();

        var application = builder.Build();
        application.UseRouting();
        application.UseEventsController();
        return application;
    }

    private static async Task<DefaultHttpContext> InvokeAsync(
        WebApplication application,
        string route,
        Guid? id,
        TimeSpan clientStaysFor)
    {
        var endpoint = application.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == route &&
                candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true);
        using var clientLeaves = new CancellationTokenSource(clientStaysFor);
        var context = new DefaultHttpContext
        {
            RequestServices = application.Services,
            RequestAborted = clientLeaves.Token
        };
        context.Request.Method = "GET";
        if (id is not null)
        {
            context.Request.RouteValues["id"] = id.Value.ToString();
        }

        context.Response.Body = new MemoryStream();
        context.SetEndpoint(endpoint);
        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
```

- [ ] Implémentation :

Créer `src/Backend/Vole_Papillon_Damour.Api/LiveTableau/LiveTableauSnapshot.cs` :

```csharp
using Vole_Papillon_Damour.Api.Common.Utils;
using Vole_Papillon_Damour.Contracts.Events.Responses;

namespace Vole_Papillon_Damour.Api.LiveTableau;

/// <summary>One full state of a live tableau, already serialized for the SSE stream.</summary>
public sealed record LiveTableauSnapshot(int Revision, string Json)
{
    public static LiveTableauSnapshot From(EventResponse response) =>
        new(response.LiveRevision, JsonSerializerHelper.Serialize(response));
}
```

Créer `src/Backend/Vole_Papillon_Damour.Api/LiveTableau/LiveTableauOptions.cs` :

```csharp
namespace Vole_Papillon_Damour.Api.LiveTableau;

public sealed class LiveTableauOptions
{
    public const string SectionName = "LiveTableau";

    /// <summary>How often a replica checks the database for changes made on another replica.</summary>
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum silence on a stream; proxies close idle connections.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Reconnection delay suggested to the browser EventSource.</summary>
    public TimeSpan ReconnectionInterval { get; set; } = TimeSpan.FromSeconds(3);
}
```

Créer `src/Backend/Vole_Papillon_Damour.Api/LiveTableau/ILiveTableauHub.cs` :

```csharp
using System.Threading.Channels;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.LiveTableau;

/// <summary>
/// In-memory fan-out of live tableau snapshots to the SSE clients connected to
/// this replica. Changes made on another replica arrive through
/// <see cref="LiveTableauSyncService"/>.
/// </summary>
public interface ILiveTableauHub
{
    /// <summary>
    /// Registers a client. Only the newest snapshot is kept for a slow client:
    /// each snapshot is a complete state, older ones are useless.
    /// </summary>
    ILiveTableauSubscription Subscribe(AssoEventsId assoEventsId);

    /// <summary>Sends <paramref name="snapshot"/> to every client of the event and records its revision.</summary>
    void Publish(AssoEventsId assoEventsId, LiveTableauSnapshot snapshot);

    /// <summary>Records a revision this replica already knows, without sending anything.</summary>
    void ObserveRevision(AssoEventsId assoEventsId, int revision);

    /// <summary>Highest revision published or observed on this replica, if any.</summary>
    int? GetKnownRevision(AssoEventsId assoEventsId);

    /// <summary>Events with at least one connected client.</summary>
    IReadOnlyCollection<AssoEventsId> GetWatchedEvents();
}

public interface ILiveTableauSubscription : IDisposable
{
    ChannelReader<LiveTableauSnapshot> Reader { get; }
}
```

Créer `src/Backend/Vole_Papillon_Damour.Api/LiveTableau/LiveTableauHub.cs` :

```csharp
using System.Collections.Concurrent;
using System.Threading.Channels;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.LiveTableau;

public sealed class LiveTableauHub : ILiveTableauHub
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<LiveTableauSnapshot>>> _subscribers = new();
    private readonly ConcurrentDictionary<Guid, int> _knownRevisions = new();

    public ILiveTableauSubscription Subscribe(AssoEventsId assoEventsId)
    {
        var channel = Channel.CreateBounded<LiveTableauSnapshot>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        var subscriptionId = Guid.NewGuid();
        var eventSubscribers = _subscribers.GetOrAdd(assoEventsId.Value, _ => new());
        eventSubscribers[subscriptionId] = channel;

        return new Subscription(channel, () => Unsubscribe(assoEventsId.Value, subscriptionId));
    }

    public void Publish(AssoEventsId assoEventsId, LiveTableauSnapshot snapshot)
    {
        ObserveRevision(assoEventsId, snapshot.Revision);
        if (!_subscribers.TryGetValue(assoEventsId.Value, out var eventSubscribers))
        {
            return;
        }

        foreach (var channel in eventSubscribers.Values)
        {
            channel.Writer.TryWrite(snapshot);
        }
    }

    public void ObserveRevision(AssoEventsId assoEventsId, int revision)
    {
        _knownRevisions.AddOrUpdate(
            assoEventsId.Value,
            revision,
            (_, known) => Math.Max(known, revision));
    }

    public int? GetKnownRevision(AssoEventsId assoEventsId) =>
        _knownRevisions.TryGetValue(assoEventsId.Value, out var revision) ? revision : null;

    public IReadOnlyCollection<AssoEventsId> GetWatchedEvents() =>
        _subscribers
            .Where(entry => !entry.Value.IsEmpty)
            .Select(entry => new AssoEventsId(entry.Key))
            .ToList();

    private void Unsubscribe(Guid eventId, Guid subscriptionId)
    {
        if (!_subscribers.TryGetValue(eventId, out var eventSubscribers) ||
            !eventSubscribers.TryRemove(subscriptionId, out var channel))
        {
            return;
        }

        channel.Writer.TryComplete();
        if (eventSubscribers.IsEmpty)
        {
            // Nobody watches this event any more: forget its revision too, so the
            // next first client triggers a fresh comparison with the database.
            _subscribers.TryRemove(KeyValuePair.Create(eventId, eventSubscribers));
            _knownRevisions.TryRemove(eventId, out _);
        }
    }

    private sealed class Subscription(Channel<LiveTableauSnapshot> channel, Action unsubscribe)
        : ILiveTableauSubscription
    {
        private int _disposed;

        public ChannelReader<LiveTableauSnapshot> Reader => channel.Reader;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                unsubscribe();
            }
        }
    }
}
```

Créer `src/Backend/Vole_Papillon_Damour.Api/LiveTableau/LiveTableauStream.cs` :

```csharp
using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

namespace Vole_Papillon_Damour.Api.LiveTableau;

/// <summary>
/// The SSE items sent to one client: the initial snapshot, then every newer
/// snapshot, and a <c>ping</c> event after each heartbeat interval of silence.
/// </summary>
public static class LiveTableauStream
{
    public const string PingEventType = "ping";

    public static async IAsyncEnumerable<SseItem<string>> ReadAsync(
        LiveTableauSnapshot initial,
        ILiveTableauSubscription subscription,
        TimeSpan heartbeat,
        TimeSpan reconnection,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (subscription)
        {
            yield return ToItem(initial, reconnection);
            var lastRevision = initial.Revision;

            while (!cancellationToken.IsCancellationRequested)
            {
                var next = await WaitForNextAsync(subscription, heartbeat, cancellationToken);
                if (next.IsClosed)
                {
                    yield break;
                }

                if (next.Snapshot is null)
                {
                    yield return new SseItem<string>(string.Empty, PingEventType);
                    continue;
                }

                // Published before this client read its initial state: already included.
                if (next.Snapshot.Revision <= lastRevision)
                {
                    continue;
                }

                lastRevision = next.Snapshot.Revision;
                yield return ToItem(next.Snapshot, null);
            }
        }
    }

    private static async Task<NextSnapshot> WaitForNextAsync(
        ILiveTableauSubscription subscription,
        TimeSpan heartbeat,
        CancellationToken cancellationToken)
    {
        using var heartbeatTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        heartbeatTimeout.CancelAfter(heartbeat);
        try
        {
            if (!await subscription.Reader.WaitToReadAsync(heartbeatTimeout.Token))
            {
                return new NextSnapshot(null, IsClosed: true);
            }

            return subscription.Reader.TryRead(out var snapshot)
                ? new NextSnapshot(snapshot, IsClosed: false)
                : new NextSnapshot(null, IsClosed: false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new NextSnapshot(null, IsClosed: false);
        }
        catch (OperationCanceledException)
        {
            return new NextSnapshot(null, IsClosed: true);
        }
    }

    private static SseItem<string> ToItem(LiveTableauSnapshot snapshot, TimeSpan? reconnection) =>
        new(snapshot.Json)
        {
            EventId = snapshot.Revision.ToString(CultureInfo.InvariantCulture),
            ReconnectionInterval = reconnection
        };

    private readonly record struct NextSnapshot(LiveTableauSnapshot? Snapshot, bool IsClosed);
}
```

Créer `src/Backend/Vole_Papillon_Damour.Api/LiveTableau/LiveTableauSyncService.cs` :

```csharp
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Events.Queries.GetAssoEventById;
using Vole_Papillon_Damour.Application.Events.Queries.GetLiveRevisions;
using Vole_Papillon_Damour.Contracts.Events.Responses;

namespace Vole_Papillon_Damour.Api.LiveTableau;

/// <summary>
/// Brings changes made on another API replica to the clients of this one. It
/// only queries the database for events that have a client here, and only
/// loads an event whose revision moved.
/// </summary>
public sealed class LiveTableauSyncService(
    ILiveTableauHub hub,
    IServiceScopeFactory scopeFactory,
    IOptions<LiveTableauOptions> options,
    ILogger<LiveTableauSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.SyncInterval);
        while (await WaitForNextTickAsync(timer, stoppingToken))
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Live tableau synchronisation failed; it will retry on the next tick.");
            }
        }
    }

    public async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        var watchedEvents = hub.GetWatchedEvents();
        if (watchedEvents.Count == 0)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
        var revisions = await sender.Send(new GetLiveRevisionsQuery(watchedEvents), cancellationToken);

        foreach (var (assoEventsId, revision) in revisions)
        {
            if (hub.GetKnownRevision(assoEventsId) >= revision)
            {
                continue;
            }

            var assoEvent = await sender.Send(new GetAssoEventByIdQuery(assoEventsId), cancellationToken);
            if (assoEvent.IsError)
            {
                continue;
            }

            hub.Publish(assoEventsId, LiveTableauSnapshot.From(mapper.Map<EventResponse>(assoEvent.Value)));
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
```

Créer `src/Backend/Vole_Papillon_Damour.Api/LiveTableau/LiveTableauServiceCollectionExtensions.cs` :

```csharp
namespace Vole_Papillon_Damour.Api.LiveTableau;

public static class LiveTableauServiceCollectionExtensions
{
    public static IServiceCollection AddLiveTableau(this IServiceCollection services)
    {
        services.AddOptions<LiveTableauOptions>().BindConfiguration(LiveTableauOptions.SectionName);
        services.AddSingleton<ILiveTableauHub, LiveTableauHub>();
        services.AddHostedService<LiveTableauSyncService>();
        return services;
    }
}
```

Créer `src/Backend/Vole_Papillon_Damour.Contracts/Events/Responses/TodayEventResponse.cs` :

```csharp
namespace Vole_Papillon_Damour.Contracts.Events.Responses;

/// <summary>The event shown in the site's "today" banner.</summary>
public class TodayEventResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset DateStart { get; set; }
    public DateTimeOffset? DateEnd { get; set; }
    public DateTimeOffset? HourOpenDoors { get; set; }
    public DateTimeOffset? HourCloseDoors { get; set; }
    public string City { get; set; } = string.Empty;

    /// <summary><c>NotStarted</c>, <c>Live</c> or <c>Finished</c> for a loto; <c>null</c> otherwise.</summary>
    public string? LiveStatus { get; set; }

    public int? LastNumero { get; set; }
}
```

Modifier `src/Backend/Vole_Papillon_Damour.Api/Controllers/AssoEventsController/EventsController.cs` :

```diff
@@ -1,10 +1,10 @@
 using MapsterMapper;
 using MediatR;
 using Microsoft.AspNetCore.Mvc;
-using Vole_Papillon_Damour.Api.Common.Utils;
+using Microsoft.Extensions.Options;
 using Vole_Papillon_Damour.Api.Errors;
+using Vole_Papillon_Damour.Api.LiveTableau;
 using Vole_Papillon_Damour.Application.Actuality.Commands.DeleteActuality;
-using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
 using Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;
 using Vole_Papillon_Damour.Application.Events.Commands.Events.AddBingoWin;
 using Vole_Papillon_Damour.Application.Events.Commands.Events.AddWinPartie;
@@ -16,10 +16,10 @@ using Vole_Papillon_Damour.Application.Events.Queries;
 using Vole_Papillon_Damour.Application.Events.Queries.GetAssoEventById;
 using Vole_Papillon_Damour.Application.Events.Queries.GetNextBingo;
 using Vole_Papillon_Damour.Application.Events.Queries.GetNextBooks;
+using Vole_Papillon_Damour.Application.Events.Queries.GetTodayEvent;
 using Vole_Papillon_Damour.Contracts.Events.Requests;
 using Vole_Papillon_Damour.Contracts.Events.Responses;
 using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
-using Vole_Papillon_Damour.Infrastructure.Services;
 
 namespace Vole_Papillon_Damour.Api.Controllers.AssoEventsController;
 
@@ -115,6 +115,17 @@ public static class EventsController
                     })
                 .WithName("Get next other event");
             
+            endpoints.MapGet("/asso-events/today",
+                    async (IMediator mediator, IMapper mapper, CancellationToken cancellationToken) =>
+                    {
+                        var todayEvent = await mediator.Send(new GetTodayEventQuery(), cancellationToken);
+                        return todayEvent is null
+                            ? Results.NoContent()
+                            : Results.Ok(mapper.Map<TodayEventResponse>(todayEvent));
+                    })
+                .WithName("Get today event")
+                .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(10)));
+
             endpoints.MapGet("/asso-events/{id}",
                     async (Guid id,
                         IMediator mediator, IMapper mapper) =>
@@ -183,7 +194,7 @@ public static class EventsController
             endpoints.MapPost("/asso-events/{id}/numeros",
                     async (AddNumeroToPartieRequest request, 
                         Guid id,
-                        IMediator mediator, IMapper mapper, ISSEClientManager sseClientManager) =>
+                        IMediator mediator, IMapper mapper, ILiveTableauHub liveTableauHub) =>
                     {
                         var command = new AddNumeroToEventCommand(
                             new AssoEventsId(id),
@@ -191,134 +202,113 @@ public static class EventsController
                         
                         var addNumeroResult = await mediator.Send(command);
 
-                        return await addNumeroResult.MatchAsync(
-                            async addNumeroResult =>
+                        return addNumeroResult.Match(
+                            addNumeroResult =>
                             {
                                 var eventResponse = mapper.Map<EventResponse>(addNumeroResult);
-                                
-                                var jsonMessage = JsonSerializerHelper.Serialize(eventResponse);
-                                await sseClientManager.SendToEvent(command.AssoEventsId, jsonMessage);
+                                liveTableauHub.Publish(command.AssoEventsId, LiveTableauSnapshot.From(eventResponse));
                                 
                                 return Results.Ok(eventResponse);
                             },
-                            error => Task.FromResult(error.Result()));
+                            error => error.Result());
                     })
                 .WithName("Add a number to a partie")
                 .RequireAuthorization(AdminPolicyName);
             
             endpoints.MapDelete("/asso-events/{id}/numeros",
                     async (Guid id,
-                        IMediator mediator, IMapper mapper, ISSEClientManager sseClientManager) =>
+                        IMediator mediator, IMapper mapper, ILiveTableauHub liveTableauHub) =>
                     {
                         var command = new RemoveLastNumeroCommand(
                             new AssoEventsId(id));
                         
                         var removeLastNumeroResult = await mediator.Send(command);
 
-                        return await removeLastNumeroResult.MatchAsync(
-                            async removeLastNumeroResult =>
+                        return removeLastNumeroResult.Match(
+                            removeLastNumeroResult =>
                             {
                                 var eventResponse = mapper.Map<EventResponse>(removeLastNumeroResult);
-                                
-                                var jsonMessage = JsonSerializerHelper.Serialize(eventResponse);
-                                await sseClientManager.SendToEvent(command.AssoEventsId, jsonMessage);
+                                liveTableauHub.Publish(command.AssoEventsId, LiveTableauSnapshot.From(eventResponse));
                                 
                                 return Results.Ok(eventResponse);
                             },
-                            error => Task.FromResult(error.Result()));
+                            error => error.Result());
                     })
                 .WithName("Remove last numero of a partie")
                 .RequireAuthorization(AdminPolicyName);
             
             endpoints.MapPost("/asso-events/{id}/win-partie",
                     async (Guid id,
-                        IMediator mediator, IMapper mapper, ISSEClientManager sseClientManager) =>
+                        IMediator mediator, IMapper mapper, ILiveTableauHub liveTableauHub) =>
                     {
                         var command = new AddWinPartieCommand(
                             new AssoEventsId(id));
                                     
                         var createPartieResult = await mediator.Send(command);
             
-                        return await createPartieResult.MatchAsync(
-                            async createPartieResult =>
+                        return createPartieResult.Match(
+                            createPartieResult =>
                             {
                                 var eventResponse = mapper.Map<EventResponse>(createPartieResult);
-                                
-                                var  jsonMessage = JsonSerializerHelper.Serialize(eventResponse);
-                                await sseClientManager.SendToEvent(command.AssoEventsId, jsonMessage);
+                                liveTableauHub.Publish(command.AssoEventsId, LiveTableauSnapshot.From(eventResponse));
                                 
                                 return Results.Ok(eventResponse);
                             },
-                            error => Task.FromResult(error.Result()));
+                            error => error.Result());
                     })
                 .WithName("Add win to a partie")
                 .RequireAuthorization(AdminPolicyName);
             
             endpoints.MapPut("/asso-events/{id}/bingo-win",
                     async (AddBingoWinRequest request, Guid id,
-                        IMediator mediator, IMapper mapper, ISSEClientManager sseClientManager) =>
+                        IMediator mediator, IMapper mapper, ILiveTableauHub liveTableauHub) =>
                     {
                         var command = new AddBingoWinCommand(
                             new AssoEventsId(id), request.HasBeenWon!.Value);
                                     
                         var createPartieResult = await mediator.Send(command);
             
-                        return await createPartieResult.MatchAsync(
-                            async createPartieResult =>
+                        return createPartieResult.Match(
+                            createPartieResult =>
                             {
                                 var eventResponse = mapper.Map<EventResponse>(createPartieResult);
-                                
-                                var jsonMessage = JsonSerializerHelper.Serialize(eventResponse);
-                                await sseClientManager.SendToEvent(command.AssoEventsId, jsonMessage);
+                                liveTableauHub.Publish(command.AssoEventsId, LiveTableauSnapshot.From(eventResponse));
                                 
                                 return Results.Ok(eventResponse);
                             },
-                            error => Task.FromResult(error.Result()));
+                            error => error.Result());
                     })
                 .WithName("Update Bingo win")
                 .RequireAuthorization(AdminPolicyName);
             
             endpoints.MapGet("/asso-events/{id}/tableau/sse",
-                    async (HttpContext ctx, Guid id, 
+                    async (HttpContext context, Guid id,
                         IMediator mediator, IMapper mapper,
-                        ISSEClientManager sseClientManager, CancellationToken ct) =>
+                        ILiveTableauHub liveTableauHub, IOptions<LiveTableauOptions> options,
+                        CancellationToken cancellationToken) =>
                     {
-                        ctx.Response.Headers.Append("Content-Type", "text/event-stream");
-                        ctx.Response.Headers.Append("Cache-Control", "no-cache");
-    
-                        var clientId = ctx.Connection.Id;
-                        var streamWriter = new StreamWriter(ctx.Response.Body);
-
-                        sseClientManager.AddClient(new AssoEventsId(id), clientId, streamWriter);
-                        
-                        var command = new GetAssoEventByIdQuery(
-                            new AssoEventsId(id));
-                        
-                        var assoEventResult = await mediator.Send(command, ct);
-
-                        await assoEventResult.MatchAsync(
-                            async assoEventResult =>
-                            {
-                                var eventResponse = mapper.Map<EventResponse>(assoEventResult);
-                                await sseClientManager.SendToClient(clientId, JsonSerializerHelper.Serialize(eventResponse));
-                                return Task.CompletedTask;
-                            },
-                            async error =>
-                            {
-                                return Task.CompletedTask;
-                            });
-
-                        var connectionClosed = new TaskCompletionSource();
-                        using var cancellationRegistration = ctx.RequestAborted.Register(() => connectionClosed.TrySetResult());
-
-                        try
-                        {
-                            await connectionClosed.Task;
-                        }
-                        finally
+                        var assoEventsId = new AssoEventsId(id);
+                        // Subscribe before reading the state: a change made in between is
+                        // then queued instead of lost.
+                        var subscription = liveTableauHub.Subscribe(assoEventsId);
+                        var assoEventResult = await mediator.Send(new GetAssoEventByIdQuery(assoEventsId), cancellationToken);
+                        if (assoEventResult.IsError)
                         {
-                            sseClientManager.RemoveClient(clientId);
+                            subscription.Dispose();
+                            return assoEventResult.FirstError.Result();
                         }
+
+                        var initial = LiveTableauSnapshot.From(mapper.Map<EventResponse>(assoEventResult.Value));
+                        liveTableauHub.ObserveRevision(assoEventsId, initial.Revision);
+                        // Reverse proxies (nginx, Render) must not buffer the stream.
+                        context.Response.Headers["X-Accel-Buffering"] = "no";
+
+                        return TypedResults.ServerSentEvents(LiveTableauStream.ReadAsync(
+                            initial,
+                            subscription,
+                            options.Value.HeartbeatInterval,
+                            options.Value.ReconnectionInterval,
+                            cancellationToken));
                     })
                 .WithName("Get SSE for a tableau");
         });
```

Modifier `src/Backend/Vole_Papillon_Damour.Api/Common/Mapping/EventMappingConfig.cs` :

```diff
@@ -64,6 +64,11 @@ public class EventMappingConfig : IRegister
             .Map(dest => dest.CityCode, src => src.Adresse.CityCode)
             .Map(dest => dest.RoadNumber, src => src.Adresse.RoadNumber);
 
+        config.NewConfig<TodayEventResult, TodayEventResponse>()
+            .Map(dest => dest.Id, src => src.Id.Value)
+            .Map(dest => dest.EventType, src => src.EventsType.Value.ToString())
+            .Map(dest => dest.LiveStatus, src => src.LiveStatus == null ? null : src.LiveStatus.Value.ToString());
+
         config.NewConfig<AddPartieToEventRequest, AddPartieToEventCommand>()
             .Map(dest => dest.PartiesCommand, src => src.Partie);
```

Modifier `src/Backend/Vole_Papillon_Damour.Api/DependencyInjection.cs` :

```diff
@@ -1,5 +1,7 @@
 using Vole_Papillon_Damour.Api.Common.Mapping;
 
+using Vole_Papillon_Damour.Api.LiveTableau;
+
 namespace Vole_Papillon_Damour.Api;
 
 public static class DependencyInjection
@@ -8,6 +10,7 @@ public static class DependencyInjection
     {
         services.AddMapping();
         services.AddAuthorization();
+        services.AddLiveTableau();
         return services;
     }
 }
\ No newline at end of file
```

Modifier `src/Backend/Vole_Papillon_Damour.Infrastructure/DependencyInjection.cs` :

```diff
@@ -79,7 +79,6 @@ public static class DependencyInjection
         }
         
         services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
-        services.AddSingleton<ISSEClientManager, SSEClientManager>();
         services.AddScoped<IUserDeletionRetentionPolicy, NoRetainedSalesMovementsPolicy>();
         services.AddScoped<IAccountDeletionStore, AccountDeletionStore>();
         services.AddScoped<IBookAlertOutbox, BookAlertOutbox>();
```

Dans les cinq fichiers de tests d'API listés, remplacer
`builder.Services.AddSingleton(Substitute.For<ISSEClientManager>());` par
`builder.Services.AddSingleton(Substitute.For<ILiveTableauHub>());`, ajouter
`using Vole_Papillon_Damour.Api.LiveTableau;` et retirer l'`using` devenu inutile. Exemple :

Modifier `src/Backend/Vole_Papillon_Damour.Api.tests/EndpointAuthorizationTests.cs` :

```diff
@@ -10,6 +10,7 @@ using NSubstitute;
 using Vole_Papillon_Damour.Api.Controllers;
 using Vole_Papillon_Damour.Api.Controllers.AssoEventsController;
 using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
+using Vole_Papillon_Damour.Api.LiveTableau;
 
 namespace Vole_Papillon_Damour.Api.tests;
 
@@ -248,7 +249,7 @@ public class EndpointAuthorizationTests
         // apparaît dans une signature, ce test le dira.
         builder.Services.AddSingleton(Substitute.For<IMediator>());
         builder.Services.AddSingleton(Substitute.For<IMapper>());
-        builder.Services.AddSingleton(Substitute.For<ISSEClientManager>());
+        builder.Services.AddSingleton(Substitute.For<ILiveTableauHub>());
         builder.Services.AddSingleton(Substitute.For<IAccountDeletionService>());
 
         var application = builder.Build();
```

- [ ] Supprimer les trois fichiers `SSEClientManager*` listés.
- [ ] `dotnet test src/Backend/Vole_Papillon_Damour.slnx` : attendu 927 tests verts
  (Domain 184, Worker 4, Application 467, Api 80, Infrastructure 192).
- [ ] Commit : `feat(loto): replace the SSE client manager with a replica-safe live hub`

## Tâche 5 — `@vpd/ui` : déduction de l'affichage du tableau

RG-01, RG-08, RG-09. Une fonction pure, utilisée par la projection (tâche 7) et le mobile
(tâche 9), calcule : la phase (`loading`, `before`, `playing`, `finished`), les 90 cases et
leur état (`idle`, `drawn`, `last`, `neighbour`), le dernier numéro et les cinq précédents,
les voisins du +1 / −1 (y compris de 90 à 1), « 1ʳᵉ fois » / « 2ᵉ fois », les lots, le
bingo, la première partie et la partie suivante.

**Fichiers :**
- Créer : `src/SharedUi/src/lib/loto/loto-tableau-view.ts`
- Modifier : `src/SharedUi/src/public-api.ts`
- Test : `src/Website/src/app/feature/tableau/loto-tableau-view.spec.ts` (SharedUi n'a pas
  de runner propre ; la spec vit dans le Website et importe `@vpd/ui`)

**Interfaces produites :**
- Fonctions : `buildLotoTableauView(event: LotoEventSnapshot | null): LotoTableauView`,
  `lotoLineLabel`, `lotoTypeLabel`, `LOTO_NUMBERS`.
- Types : `LotoCell`, `LotoPhase`, `LotoCellState`. Les `VpdEventModel` des deux
  applications sont assignables à `LotoEventSnapshot`.

- [ ] **Test d'abord** :

Créer `src/Website/src/app/feature/tableau/loto-tableau-view.spec.ts` :

```ts
import {buildLotoTableauView, LotoEventSnapshot, LotoPartieSnapshot} from '@vpd/ui';

function partie(index: number, overrides: Partial<LotoPartieSnapshot> = {}): LotoPartieSnapshot {
  return {
    id: `partie-${index}`,
    name: `Partie ${index + 1}`,
    partieType: 'Standard',
    index,
    pauseAfter: false,
    lastNumeros: [],
    liveNumeros: [],
    currentLineIndex: 0,
    lineParties: [
      {index: 0, numberLine: 'OneLine', lots: [
        {id: 'lot-a', name: 'Bon d’achat 30 €', isWon: null},
        {id: 'lot-b', name: 'Panier garni', isWon: null},
      ]},
      {index: 1, numberLine: 'TwoLine', lots: [{id: 'lot-c', name: 'Jambon', isWon: null}]},
    ],
    ...overrides,
  };
}

function loto(parties: LotoPartieSnapshot[], currentPartieIndex = 0): LotoEventSnapshot {
  return {id: 'loto', name: 'Grand loto', parties, currentPartieIndex, bingoNumeros: [14, 63], bingoHasBeenWon: false};
}

describe('buildLotoTableauView', () => {
  it('attend le chargement puis le premier numéro (RG-09)', () => {
    expect(buildLotoTableauView(null).phase).toBe('loading');
    expect(buildLotoTableauView(loto([])).phase).toBe('before');

    const view = buildLotoTableauView(loto([partie(0), partie(1)]));
    expect(view.phase).toBe('before');
    expect(view.firstPartie).toEqual({
      name: 'Partie 1',
      typeLabel: null,
      lines: [
        {label: '1 ligne', lots: ['Bon d’achat 30 €', 'Panier garni']},
        {label: '2 lignes', lots: ['Jambon']},
      ],
    });
  });

  it('décrit la partie en cours : dernier numéro, précédents, cases et lots', () => {
    const drawn = [3, 7, 12, 88, 31, 56, 75, 42];
    const view = buildLotoTableauView(loto([partie(0, {lastNumeros: drawn, liveNumeros: drawn}), partie(1)]));

    expect(view.phase).toBe('playing');
    expect(view.partieNumber).toBe(1);
    expect(view.partieCount).toBe(2);
    expect(view.lineLabel).toBe('1 ligne');
    expect(view.timesLabel).toBe('1ʳᵉ fois');
    expect(view.lastNumero).toBe(42);
    expect(view.previousNumeros).toEqual([75, 56, 31, 88, 12]);
    expect(view.cells.length).toBe(90);
    expect(view.cells[41]).toEqual({value: 42, state: 'last'});
    expect(view.cells[2]).toEqual({value: 3, state: 'drawn'});
    expect(view.cells[3]).toEqual({value: 4, state: 'idle'});
    expect(view.drawnCount).toBe(8);
    expect(view.nextPartie?.name).toBe('Partie 2');
    expect(view.showBingo).toBeTrue();
  });

  it('passe à « 2ᵉ fois » quand un lot de la ligne est gagné', () => {
    const current = partie(0, {lastNumeros: [5], liveNumeros: [5]});
    current.lineParties[0].lots[0].isWon = true;

    const view = buildLotoTableauView(loto([current]));

    expect(view.timesLabel).toBe('2ᵉ fois');
    expect(view.lots).toEqual([
      {id: 'lot-a', name: 'Bon d’achat 30 €', won: true},
      {id: 'lot-b', name: 'Panier garni', won: false},
    ]);
  });

  it('cercle les voisins du dernier numéro au +1 / −1, y compris de 90 à 1', () => {
    const current = partie(0, {
      partieType: 'PlusUnMoinsUn',
      lastNumeros: [18, 90],
      liveNumeros: [18, 17, 19, 90, 89, 1],
    });

    const view = buildLotoTableauView(loto([current]));

    expect(view.typeLabel).toBe('+1 / −1');
    expect(view.neighbours).toEqual([89, 1]);
    expect(view.cells[0].state).toBe('neighbour');
    expect(view.cells[88].state).toBe('neighbour');
    expect(view.cells[89].state).toBe('last');
    expect(view.cells[17].state).toBe('drawn');
  });

  it('montre la nouvelle partie vide après une ligne gagnée, puis la fin du loto', () => {
    const first = partie(0, {lastNumeros: [5, 9], liveNumeros: [5, 9]});

    const next = buildLotoTableauView(loto([first, partie(1)], 1));
    expect(next.phase).toBe('playing');
    expect(next.lastNumero).toBeNull();
    expect(next.previousNumeros).toEqual([]);

    expect(buildLotoTableauView(loto([first], 1)).phase).toBe('finished');
  });

  it('masque le rappel du bingo pendant la partie Bingo', () => {
    const view = buildLotoTableauView(loto([partie(0, {partieType: 'Bingo', lastNumeros: [4], liveNumeros: [4]})]));

    expect(view.showBingo).toBeFalse();
  });
});
```

- [ ] Implémentation :

Créer `src/SharedUi/src/lib/loto/loto-tableau-view.ts` :

```ts
/**
 * Ce que le tableau du loto affiche, déduit de l'état de l'événement renvoyé par l'API
 * (`EventResponse`). Partagé par la projection du BackOffice et le tableau mobile du
 * Website : les deux écrans lisent les mêmes règles (dernier numéro, voisins du
 * +1 / −1, compteur « 1ʳᵉ fois / 2ᵉ fois », états avant / pendant / après le jeu).
 *
 * Les interfaces ne reprennent que les champs utilisés : les modèles `VpdEventModel`
 * des deux applications y sont assignables tels quels.
 */

export interface LotoLotSnapshot {
  id: string;
  name: string;
  isWon: boolean | null;
}

export interface LotoLineSnapshot {
  index: number;
  numberLine: string;
  lots: LotoLotSnapshot[];
}

export interface LotoPartieSnapshot {
  id: string;
  name: string;
  partieType: string;
  index: number;
  pauseAfter: boolean;
  lastNumeros: number[];
  liveNumeros: number[];
  currentLineIndex: number;
  lineParties: LotoLineSnapshot[];
}

export interface LotoEventSnapshot {
  id: string;
  name: string;
  parties: LotoPartieSnapshot[] | null;
  currentPartieIndex: number;
  bingoNumeros: number[];
  bingoHasBeenWon: boolean;
}

export type LotoPhase = 'loading' | 'before' | 'playing' | 'finished';
export type LotoCellState = 'idle' | 'drawn' | 'last' | 'neighbour';

export interface LotoCell {
  value: number;
  state: LotoCellState;
}

export interface LotoLot {
  id: string;
  name: string;
  won: boolean;
}

export interface LotoPartieSummary {
  name: string;
  typeLabel: string | null;
  lines: { label: string; lots: string[] }[];
}

export interface LotoTableauView {
  phase: LotoPhase;
  eventName: string;
  partieNumber: number;
  partieCount: number;
  partieName: string;
  lineLabel: string | null;
  timesLabel: string | null;
  typeLabel: string | null;
  lastNumero: number | null;
  previousNumeros: number[];
  neighbours: number[];
  cells: LotoCell[];
  drawnCount: number;
  lots: LotoLot[];
  showBingo: boolean;
  bingoNumeros: number[];
  bingoWon: boolean;
  firstPartie: LotoPartieSummary | null;
  nextPartie: LotoPartieSummary | null;
}

const LINE_LABELS: Record<string, string> = {
  OneLine: '1 ligne',
  TwoLine: '2 lignes',
  CartonPlein: 'Carton plein',
};

const TYPE_LABELS: Record<string, string> = {
  Americaine: 'Américaine',
  PlusUnMoinsUn: '+1 / −1',
};

export const LOTO_NUMBERS: readonly number[] = Array.from({length: 90}, (_, index) => index + 1);

export function lotoLineLabel(numberLine: string): string {
  return LINE_LABELS[numberLine] ?? numberLine;
}

export function lotoTypeLabel(partieType: string): string | null {
  return TYPE_LABELS[partieType] ?? null;
}

export function buildLotoTableauView(event: LotoEventSnapshot | null): LotoTableauView {
  const parties = [...(event?.parties ?? [])].sort((a, b) => a.index - b.index);
  const current = event === null ? undefined : parties.find(partie => partie.index === event.currentPartieIndex);
  const phase = resolvePhase(event, parties, current);
  const line = current?.lineParties.find(candidate => candidate.index === current.currentLineIndex);
  const lastNumero = current?.lastNumeros.at(-1) ?? null;
  const neighbours = current ? neighboursOf(current) : [];
  const live = new Set(current?.liveNumeros ?? []);
  const neighbourSet = new Set(neighbours);
  const next = current === undefined ? undefined : parties.find(partie => partie.index === current.index + 1);

  return {
    phase,
    eventName: event?.name ?? '',
    partieNumber: (current?.index ?? 0) + 1,
    partieCount: parties.length,
    partieName: current?.name ?? '',
    lineLabel: line ? lotoLineLabel(line.numberLine) : null,
    timesLabel: line ? timesLabelOf(line) : null,
    typeLabel: current ? lotoTypeLabel(current.partieType) : null,
    lastNumero,
    previousNumeros: (current?.lastNumeros ?? []).slice(0, -1).reverse().slice(0, 5),
    neighbours,
    cells: LOTO_NUMBERS.map(value => ({
      value,
      state: value === lastNumero
        ? 'last'
        : neighbourSet.has(value)
          ? 'neighbour'
          : live.has(value) ? 'drawn' : 'idle',
    })),
    drawnCount: live.size,
    lots: (line?.lots ?? []).map(lot => ({id: lot.id, name: lot.name, won: lot.isWon !== null})),
    showBingo: current?.partieType !== 'Bingo',
    bingoNumeros: event?.bingoNumeros ?? [],
    bingoWon: event?.bingoHasBeenWon ?? false,
    firstPartie: parties[0] ? summaryOf(parties[0]) : null,
    nextPartie: next ? summaryOf(next) : null,
  };
}

function resolvePhase(
  event: LotoEventSnapshot | null,
  parties: LotoPartieSnapshot[],
  current: LotoPartieSnapshot | undefined,
): LotoPhase {
  if (event === null) {
    return 'loading';
  }
  if (parties.length === 0) {
    return 'before';
  }
  if (current === undefined) {
    return event.currentPartieIndex >= parties.length ? 'finished' : 'before';
  }
  const anyNumberDrawn = parties.some(partie => partie.lastNumeros.length > 0);
  return anyNumberDrawn ? 'playing' : 'before';
}

/**
 * Au +1 / −1, chaque tirage allume aussi ses deux voisins (90 et 1 se touchent). Les
 * voisins du dernier numéro sont les numéros allumés après lui dans `liveNumeros`.
 */
function neighboursOf(partie: LotoPartieSnapshot): number[] {
  const lastNumero = partie.lastNumeros.at(-1);
  if (partie.partieType !== 'PlusUnMoinsUn' || lastNumero === undefined) {
    return [];
  }
  const position = partie.liveNumeros.lastIndexOf(lastNumero);
  return position < 0 ? [] : partie.liveNumeros.slice(position + 1);
}

/** En « 1 ligne », deux lots se gagnent l'un après l'autre sur la même ligne. */
function timesLabelOf(line: LotoLineSnapshot): string | null {
  if (line.numberLine !== 'OneLine') {
    return null;
  }
  const stillWinnable = line.lots.filter(lot => lot.isWon === null).length;
  if (stillWinnable === 2) {
    return '1ʳᵉ fois';
  }
  return stillWinnable === 1 ? '2ᵉ fois' : null;
}

function summaryOf(partie: LotoPartieSnapshot): LotoPartieSummary {
  return {
    name: partie.name,
    typeLabel: lotoTypeLabel(partie.partieType),
    lines: [...partie.lineParties]
      .sort((a, b) => a.index - b.index)
      .map(line => ({label: lotoLineLabel(line.numberLine), lots: line.lots.map(lot => lot.name)})),
  };
}
```

Modifier `src/SharedUi/src/public-api.ts` :

```diff
@@ -25,3 +25,5 @@ export * from './lib/components/vpd-loader/vpd-loader.component';
 export * from './lib/pipes/vpd-price.pipe';
 export * from './lib/pipes/vpd-capitalize.pipe';
 export * from './lib/pipes/vpd-line-number-title.pipe';
+
+export * from './lib/loto/loto-tableau-view';
```

- [ ] `cd src/Website && npm test -- --watch=false --browsers=ChromeHeadless` : la spec passe.
- [ ] Commit : `feat(loto): share the loto tableau view model in @vpd/ui`

## Tâche 6 — BackOffice : clavier de régie, pause, messages, palette

RG-04 à RG-08, RG-22. Fonctions pures et service, testés avant le composant.

**Fichiers :**
- Créer : `src/BackOffice/src/app/feature/event-detail/components/tableau/loto-regie.ts`,
  `loto-palette.service.ts`
- Test : `loto-regie.spec.ts`, `loto-palette.service.spec.ts` (même dossier)

**Interfaces produites :**
- Types : `RegieAction` (`draw`, `undo`, `line`, `bingo`, `pause`, `palette`, `fullscreen`),
  `LotoPalette` (`'dark' | 'light'`).
- Régie : `reduceRegieKey(buffer, key): {buffer, action}`,
  `shouldPauseAfterWin(before, after): boolean`,
  `regieErrorMessage(action, error, alreadyDrawn): string`.
- Palette : `LotoPaletteService.palette` (signal), `toggle()`, `storageKey`.

- [ ] **Tests d'abord** :

Créer `src/BackOffice/src/app/feature/event-detail/components/tableau/loto-regie.spec.ts` :

```ts
import {reduceRegieKey, regieErrorMessage, shouldPauseAfterWin} from './loto-regie';
import {VpdEventModel, VpdEventPartieModel} from '../../../../shared/models/vpdEvent.model';
import {PartieTypeEnum} from '../../../../shared/enums/partieType.enum';

describe('reduceRegieKey', () => {
  it('tire le numéro tapé à l’appui sur Entrée', () => {
    let state = reduceRegieKey('', '4');
    state = reduceRegieKey(state.buffer, '2');
    expect(state).toEqual({buffer: '42', action: null});

    expect(reduceRegieKey(state.buffer, 'Enter')).toEqual({buffer: '', action: {type: 'draw', numero: 42}});
  });

  it('recommence la saisie après deux chiffres et refuse un numéro hors 1–90', () => {
    expect(reduceRegieKey('42', '7').buffer).toBe('7');
    expect(reduceRegieKey('95', 'Enter').action).toBeNull();
    expect(reduceRegieKey('0', 'Enter').action).toBeNull();
    expect(reduceRegieKey('', 'Enter').action).toBeNull();
  });

  it('efface un chiffre, ou annule le dernier numéro sans saisie en cours', () => {
    expect(reduceRegieKey('42', 'Backspace')).toEqual({buffer: '4', action: null});
    expect(reduceRegieKey('', 'Backspace')).toEqual({buffer: '', action: {type: 'undo'}});
  });

  it('associe les raccourcis de la barre de régie', () => {
    expect(reduceRegieKey('', 'l').action).toEqual({type: 'line'});
    expect(reduceRegieKey('', 'B').action).toEqual({type: 'bingo'});
    expect(reduceRegieKey('', ' ').action).toEqual({type: 'pause'});
    expect(reduceRegieKey('', 'p').action).toEqual({type: 'palette'});
    expect(reduceRegieKey('', 'F').action).toEqual({type: 'fullscreen'});
    expect(reduceRegieKey('4', 'Escape')).toEqual({buffer: '', action: null});
    expect(reduceRegieKey('4', 'x')).toEqual({buffer: '4', action: null});
  });
});

describe('shouldPauseAfterWin', () => {
  it('ouvre la pause quand la ligne gagnée termine une partie « pause après »', () => {
    const before = loto(0, [partie(0, true), partie(1, false)]);

    expect(shouldPauseAfterWin(before, loto(1, before.parties))).toBeTrue();
    expect(shouldPauseAfterWin(before, loto(0, before.parties))).toBeFalse();
  });

  it('n’ouvre pas la pause pour une partie sans « pause après »', () => {
    const before = loto(0, [partie(0, false), partie(1, true)]);

    expect(shouldPauseAfterWin(before, loto(1, before.parties))).toBeFalse();
  });
});

describe('regieErrorMessage', () => {
  it('signale un numéro déjà sorti (RG-06)', () => {
    expect(regieErrorMessage({type: 'draw', numero: 42}, {response: {status: 409}}, true))
      .toBe('Le numéro 42 est déjà sorti.');
  });

  it('distingue un conflit d’une coupure réseau', () => {
    expect(regieErrorMessage({type: 'line'}, {response: {status: 409}}, false))
      .toBe('Une autre action vient d’être enregistrée sur ce tableau. Réessayez.');
    expect(regieErrorMessage({type: 'undo'}, new Error('Network Error'), false))
      .toBe('L’action n’a pas pu être enregistrée. Vérifiez la connexion et réessayez.');
  });
});

function partie(index: number, pauseAfter: boolean): VpdEventPartieModel {
  return {
    id: `partie-${index}`,
    name: `Partie ${index + 1}`,
    partieType: PartieTypeEnum.STANDARD,
    index,
    pauseAfter,
    addedBingoNumber: null,
    lastNumeros: [],
    liveNumeros: [],
    lineParties: [],
    currentLineIndex: 0,
  };
}

function loto(currentPartieIndex: number, parties: VpdEventPartieModel[]): VpdEventModel {
  return {currentPartieIndex, parties} as unknown as VpdEventModel;
}
```

Créer `src/BackOffice/src/app/feature/event-detail/components/tableau/loto-palette.service.spec.ts` :

```ts
import {LotoPaletteService} from './loto-palette.service';

describe('LotoPaletteService', () => {
  afterEach(() => localStorage.removeItem(LotoPaletteService.storageKey));

  it('démarre en palette sombre et mémorise la bascule sur le poste (RG-22)', () => {
    localStorage.removeItem(LotoPaletteService.storageKey);
    const first = new LotoPaletteService();
    expect(first.palette()).toBe('dark');

    first.toggle();

    expect(first.palette()).toBe('light');
    expect(new LotoPaletteService().palette()).toBe('light');
  });
});
```

- [ ] Implémentation :

Créer `src/BackOffice/src/app/feature/event-detail/components/tableau/loto-regie.ts` :

```ts
import {VpdEventModel} from '../../../../shared/models/vpdEvent.model';

/** Actions de la régie, déclenchées au clavier ou depuis la barre (E3). */
export type RegieAction =
  | {type: 'draw'; numero: number}
  | {type: 'undo'}
  | {type: 'line'}
  | {type: 'bingo'}
  | {type: 'pause'}
  | {type: 'palette'}
  | {type: 'fullscreen'};

export interface RegieKeyResult {
  /** Chiffres tapés en attente de « Entrée », affichés dans le témoin de saisie. */
  buffer: string;
  action: RegieAction | null;
}

/**
 * Clavier de la régie : chiffres puis Entrée pour tirer, Retour arrière pour effacer un
 * chiffre ou, sans saisie en cours, annuler le dernier numéro ; L, B, Espace, P, F.
 */
export function reduceRegieKey(buffer: string, key: string): RegieKeyResult {
  if (/^[0-9]$/.test(key)) {
    return {buffer: buffer.length >= 2 ? key : buffer + key, action: null};
  }

  switch (key) {
    case 'Enter': {
      const numero = Number(buffer);
      return {
        buffer: '',
        action: buffer !== '' && numero >= 1 && numero <= 90 ? {type: 'draw', numero} : null,
      };
    }
    case 'Escape':
      return {buffer: '', action: null};
    case 'Backspace':
      return buffer === ''
        ? {buffer: '', action: {type: 'undo'}}
        : {buffer: buffer.slice(0, -1), action: null};
    case 'l':
    case 'L':
      return {buffer, action: {type: 'line'}};
    case 'b':
    case 'B':
      return {buffer, action: {type: 'bingo'}};
    case ' ':
      return {buffer, action: {type: 'pause'}};
    case 'p':
    case 'P':
      return {buffer, action: {type: 'palette'}};
    case 'f':
    case 'F':
      return {buffer, action: {type: 'fullscreen'}};
    default:
      return {buffer, action: null};
  }
}

/** RG-08 : la ligne gagnée qui termine une partie « pause après » ouvre l'écran Pause. */
export function shouldPauseAfterWin(before: VpdEventModel, after: VpdEventModel): boolean {
  if (after.currentPartieIndex <= before.currentPartieIndex) {
    return false;
  }
  const finished = before.parties.find(partie => partie.index === before.currentPartieIndex);
  return finished?.pauseAfter ?? false;
}

/** RG-06 et messages du toast d'erreur (E3). */
export function regieErrorMessage(action: RegieAction, error: unknown, alreadyDrawn: boolean): string {
  if (action.type === 'draw' && alreadyDrawn) {
    return `Le numéro ${action.numero} est déjà sorti.`;
  }
  const status = (error as {response?: {status?: number}} | null)?.response?.status;
  if (status === 409) {
    return 'Une autre action vient d’être enregistrée sur ce tableau. Réessayez.';
  }
  return 'L’action n’a pas pu être enregistrée. Vérifiez la connexion et réessayez.';
}
```

Créer `src/BackOffice/src/app/feature/event-detail/components/tableau/loto-palette.service.ts` :

```ts
import {Injectable, signal} from '@angular/core';

/** RG-22 : A « sombre » (tableau lumineux) ou B « claire », choisie sur place le jour J. */
export type LotoPalette = 'dark' | 'light';

@Injectable({
  providedIn: 'root'
})
export class LotoPaletteService {
  static readonly storageKey = 'vpd.loto.palette';

  readonly palette = signal<LotoPalette>(LotoPaletteService.read());

  toggle(): void {
    const next: LotoPalette = this.palette() === 'dark' ? 'light' : 'dark';
    this.palette.set(next);
    try {
      localStorage.setItem(LotoPaletteService.storageKey, next);
    } catch {
      // Stockage indisponible (navigation privée) : la palette vaut pour la session.
    }
  }

  private static read(): LotoPalette {
    try {
      return localStorage.getItem(LotoPaletteService.storageKey) === 'light' ? 'light' : 'dark';
    } catch {
      return 'dark';
    }
  }
}
```

- [ ] `cd src/BackOffice && npm test -- --watch=false --browsers=ChromeHeadless` : vert.
- [ ] Commit : `feat(backoffice): add loto regie keyboard and palette helpers`

## Tâche 7 — BackOffice : tableau projeté, annonces, régie discrète

**Maquette :** preview/Main.html — zones E1-tableau, E1-grille, E1-case-sortie, E1-case-non-sortie, E1-case-derniere, E1-partie, E1-ligne, E1-compteur-fois, E1-dernier-numero, E1-precedents, E1-lots, E1-bingo
**Maquette :** preview/E1-Claire.html — zones E1-case-voisin, E1-type-partie (palette B)
**Maquette :** preview/E3-Regie.html — zones E3-barre-regie, E3-bouton-annuler, E3-bouton-ligne, E3-bouton-bingo, E3-bouton-pause, E3-bouton-palette, E3-bouton-plein-ecran, E3-aide-raccourcis, E3-saisie-clavier, E3-toast-erreur
**Maquette :** preview/E2-Numero.html, preview/E2-NumeroClaire.html — zones E2-annonce-numero
**Maquette :** preview/E2-AvantDebut.html, preview/E2-AvantDebutClaire.html — zones E2-avant-debut, E2-premiere-partie, E2-qr-code, E2-adresse-courte
**Maquette :** preview/E2-Pause.html, preview/E2-PauseClaire.html — zones E2-pause, E2-bingo-pause, E2-partie-suivante
**Maquette :** preview/E2-Termine.html — zones E2-termine, E2-bilan-bingo

RG-01 à RG-09, RG-21 à RG-23.
- **Scène et palettes.** La scène 1920 × 1080 est mise à l'échelle de la fenêtre et centrée.
  Le fond `--bg` prolonge la toile sur les côtés.
- **Ordre des écrans.** Chargement, puis annonce du numéro, puis pause, puis « avant le
  début », puis « terminé », sinon le tableau.
- **Annonce du numéro (RG-07).** Elle suit chaque tirage, sans attendre la diffusion.
- **Pause (RG-08).** Elle s'ouvre si la ligne gagnée termine une partie « pause après », et
  au clavier (Espace). « Reprendre » la ferme.
- **Barre de régie (E3).** Elle apparaît quand la souris bouge et disparaît 3 s après, avec
  le curseur (RG-04).
- **Pendant un envoi (RG-05).** Toutes les actions de jeu sont désactivées. Un numéro déjà
  sorti est refusé localement avec le toast de RG-06.
- **Styles.** Ils vont dans une feuille globale préfixée `.loto-projection` (le budget de
  4 Ko par composant est trop petit). Le composant porte la classe sur son hôte.
- **QR code.** SVG statique de `https://volepapillondamour.fr/loto`, généré avec `segno`
  1.6.1 (version 3-M, bordure 2). La zone `E2-adresse-courte` correspond au texte
  `volepapillondamour.fr/loto` sous le QR code.

**Fichiers :**
- Modifier (réécrire) : `src/BackOffice/src/app/feature/event-detail/components/tableau/tableau.component.ts`,
  `tableau.component.html`
- Supprimer : `tableau.component.scss` et le dossier `components/` (`modal-tableau`,
  `number`, `tag`) du même composant
- Créer : `src/BackOffice/src/scss/_loto-projection.scss`, `src/BackOffice/public/images/loto-qr.svg`
- Modifier : `src/BackOffice/src/styles.scss`, `src/BackOffice/src/app/feature/feature.module.ts`,
  `src/BackOffice/src/app/shared/components/icon/icon.component.ts`, `icon.component.html`
  (icônes `contrast`, `fullscreen`, `pause`, `play`)

- [ ] Pas de nouveau test unitaire : la logique est couverte par les tâches 5 et 6. Le
  contrôle se fait au build et en recette visuelle (dernière tâche).
- [ ] Implémentation :

Remplacer tout le contenu de `src/BackOffice/src/app/feature/event-detail/components/tableau/tableau.component.ts` :

```ts
import {Component, computed, DestroyRef, HostListener, inject, OnInit, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {ActivatedRoute} from '@angular/router';
import {buildLotoTableauView, LotoCell} from '@vpd/ui';
import {VpdEventModel} from '../../../../shared/models/vpdEvent.model';
import {LotoFacadeService} from '../../../../shared/facades/loto.facade.service';
import {VpdEventsFacadeService} from '../../../../shared/facades/vpd-events.facade.service';
import {environment} from '../../../../../environments/environment';
import {LotoPaletteService} from './loto-palette.service';
import {reduceRegieKey, RegieAction, regieErrorMessage, shouldPauseAfterWin} from './loto-regie';

/** Format de référence de la toile : la scène est dessinée en 1920 × 1080 puis mise à l'échelle. */
const STAGE_WIDTH = 1920;
const STAGE_HEIGHT = 1080;
const CONTROLS_IDLE_MS = 3000;
const TOAST_MS = 4000;

type Overlay = 'none' | 'numero' | 'pause';

/**
 * Tableau du loto projeté en salle (E1), annonces plein écran (E2) et régie discrète
 * (E3) : un seul écran, recopié sur le vidéoprojecteur.
 */
@Component({
  selector: 'app-tableau',
  templateUrl: './tableau.component.html',
  // Styles : src/scss/_loto-projection.scss.
  host: {class: 'loto-projection'},
  standalone: false
})
export class TableauComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly eventFacade = inject(VpdEventsFacadeService);
  private readonly lotoFacade = inject(LotoFacadeService);
  protected readonly paletteService = inject(LotoPaletteService);

  protected readonly shortUrl = 'volepapillondamour.fr/loto';
  protected readonly assoEvent = signal<VpdEventModel | null>(null);
  protected readonly view = computed(() => buildLotoTableauView(this.assoEvent()));
  protected readonly overlay = signal<Overlay>('none');
  protected readonly announcedNumero = signal<number | null>(null);
  protected readonly busy = signal(false);
  protected readonly toast = signal<string | null>(null);
  protected readonly controlsVisible = signal(true);
  protected readonly keyBuffer = signal('');
  protected readonly stageScale = signal(1);
  protected readonly palette = this.paletteService.palette;

  /** Écran affiché : les annonces (E2) passent devant le tableau (E1). */
  protected readonly screen = computed(() => {
    const phase = this.view().phase;
    if (phase === 'loading') {
      return 'loading';
    }
    const overlay = this.overlay();
    if (overlay !== 'none') {
      return overlay;
    }
    return phase === 'playing' ? 'board' : phase;
  });

  private announceTimer: ReturnType<typeof setTimeout> | undefined;
  private controlsTimer: ReturnType<typeof setTimeout> | undefined;
  private toastTimer: ReturnType<typeof setTimeout> | undefined;

  ngOnInit(): void {
    this.updateStageScale();
    this.showControls();
    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const id = params.get('id');
        if (id !== null) {
          void this.eventFacade.getEventById$(id).then(event => this.assoEvent.set(event));
        }
      });
    this.destroyRef.onDestroy(() => {
      clearTimeout(this.announceTimer);
      clearTimeout(this.controlsTimer);
      clearTimeout(this.toastTimer);
    });
  }

  @HostListener('window:resize')
  updateStageScale(): void {
    this.stageScale.set(Math.min(window.innerWidth / STAGE_WIDTH, window.innerHeight / STAGE_HEIGHT));
  }

  /** RG-04 : la barre et le curseur n'apparaissent que tant que la souris bouge. */
  @HostListener('document:mousemove')
  showControls(): void {
    this.controlsVisible.set(true);
    clearTimeout(this.controlsTimer);
    this.controlsTimer = setTimeout(() => this.controlsVisible.set(false), CONTROLS_IDLE_MS);
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.ctrlKey || event.metaKey || event.altKey) {
      return;
    }
    const result = reduceRegieKey(this.keyBuffer(), event.key);
    if (result.action !== null || result.buffer !== this.keyBuffer()) {
      event.preventDefault();
    }
    this.keyBuffer.set(result.buffer);
    if (result.action !== null) {
      void this.run(result.action);
    }
  }

  protected onCellClicked(cell: LotoCell): void {
    void this.run({type: 'draw', numero: cell.value});
  }

  protected async run(action: RegieAction): Promise<void> {
    switch (action.type) {
      case 'palette':
        this.paletteService.toggle();
        return;
      case 'fullscreen':
        await this.toggleFullscreen();
        return;
      case 'pause':
        this.overlay.set(this.overlay() === 'pause' ? 'none' : 'pause');
        return;
    }

    const event = this.assoEvent();
    if (event === null || this.busy()) {
      return;
    }
    const alreadyDrawn = action.type === 'draw' && this.view().cells[action.numero - 1].state !== 'idle';
    if (alreadyDrawn) {
      this.showToast(regieErrorMessage(action, null, true));
      return;
    }

    this.busy.set(true);
    try {
      const updated = await this.send(event, action);
      this.assoEvent.set(updated);
      if (action.type === 'draw') {
        this.announce(action.numero);
      } else if (action.type === 'line' && shouldPauseAfterWin(event, updated)) {
        this.overlay.set('pause');
      }
    } catch (error) {
      this.showToast(regieErrorMessage(action, error, false));
    } finally {
      this.busy.set(false);
    }
  }

  private send(event: VpdEventModel, action: RegieAction): Promise<VpdEventModel> {
    switch (action.type) {
      case 'draw':
        return this.lotoFacade.postNumberToPartie$(event.id, action.numero);
      case 'undo':
        return this.lotoFacade.deleteRollBack$(event.id);
      case 'line':
        return this.lotoFacade.postWin$(event.id);
      default:
        return this.lotoFacade.postBingoWin$(event.id, !event.bingoHasBeenWon);
    }
  }

  /** RG-07 : le numéro occupe l'écran ~2 s ; une nouvelle saisie remplace l'annonce. */
  private announce(numero: number): void {
    clearTimeout(this.announceTimer);
    this.announcedNumero.set(numero);
    this.overlay.set('numero');
    this.announceTimer = setTimeout(() => {
      if (this.overlay() === 'numero') {
        this.overlay.set('none');
      }
    }, environment.time_numero_modal);
  }

  private showToast(message: string): void {
    clearTimeout(this.toastTimer);
    this.toast.set(message);
    this.toastTimer = setTimeout(() => this.toast.set(null), TOAST_MS);
  }

  private async toggleFullscreen(): Promise<void> {
    try {
      if (document.fullscreenElement) {
        await document.exitFullscreen();
      } else {
        await document.documentElement.requestFullscreen();
      }
    } catch {
      this.showToast('Le plein écran n’est pas disponible dans ce navigateur.');
    }
  }
}
```

Remplacer tout le contenu de `src/BackOffice/src/app/feature/event-detail/components/tableau/tableau.component.html` :

```html
<div class="viewport"
     [class.pal-a]="palette() === 'dark'"
     [class.pal-b]="palette() === 'light'"
     [class.cursor-hidden]="!controlsVisible()">
  <div class="stage" [style.transform]="'translate(-50%, -50%) scale(' + stageScale() + ')'">
    @switch (screen()) {
      @case ('loading') {
        <div class="screen screen-center" role="status" aria-label="Chargement du tableau">
          <span class="k">Chargement du tableau…</span>
        </div>
      }

      @case ('numero') {
        <!-- E2 · Numéro tiré -->
        <div class="screen screen-center" aria-live="assertive">
          <span class="k k-lg">Numéro</span>
          <span class="announce">{{ announcedNumero() }}</span>
          <span class="announce-caption">
            {{ view().partieName }}
            @if (view().lineLabel) { · {{ view().lineLabel }} }
            @if (view().timesLabel) { · {{ view().timesLabel }} }
            @if (view().neighbours.length > 0) { · voisins {{ view().neighbours.join(' et ') }} }
          </span>
        </div>
      }

      @case ('pause') {
        <!-- E2 · Pause -->
        <div class="screen screen-split">
          <div class="split-main">
            <span class="pause-title">Pause</span>
            <span class="lead">On reprend dans quelques minutes.</span>
            @if (view().showBingo || view().bingoWon) {
              <div class="bingo-panel bingo-panel-lg">
                <span class="k k-accent">Numéros du bingo</span>
                <span class="bingo-numbers-lg">{{ view().bingoWon ? 'Gagné !' : view().bingoNumeros.join(' · ') }}</span>
              </div>
            }
            @if (view().nextPartie; as next) {
              <div class="next-partie">
                <span class="k">Ensuite</span>
                <span class="next-partie-name">
                  Partie {{ view().partieNumber + 1 }} · {{ next.name }}
                  @if (next.typeLabel) { <span class="muted">· {{ next.typeLabel }}</span> }
                </span>
              </div>
            }
          </div>
          <ng-container [ngTemplateOutlet]="qrCode" [ngTemplateOutletContext]="{size: 420}"></ng-container>
        </div>
      }

      @case ('before') {
        <!-- E2 · Avant le début -->
        <div class="screen screen-split">
          <div class="split-main">
            <span class="k">Vole Papillon d'Amour · {{ view().eventName }}</span>
            <span class="before-title">Le loto va commencer</span>
            @if (view().firstPartie; as first) {
              <div class="first-partie">
                <span class="k">Première partie</span>
                <span class="first-partie-name">{{ first.name }}</span>
                <div class="chips">
                  @for (line of first.lines; track line.label) {
                    <span class="chip chip-lg">{{ line.label }} · {{ line.lots.join(', ') }}</span>
                  }
                </div>
              </div>
            }
          </div>
          <ng-container [ngTemplateOutlet]="qrCode" [ngTemplateOutletContext]="{size: 480}"></ng-container>
        </div>
      }

      @case ('finished') {
        <!-- E2 · Loto terminé -->
        <div class="screen screen-center finished">
          <span class="k">Vole Papillon d'Amour · {{ view().eventName }}</span>
          <span class="finished-title">Loto terminé</span>
          <span class="finished-thanks">Merci à tous !</span>
          <span class="bingo-pill"><span class="k k-accent">Bingo</span> {{ view().bingoWon ? 'Gagné !' : view().bingoNumeros.join(' · ') }}</span>
          <span class="lead">Les bénéfices de cet après-midi soutiennent Maxence et sa famille.</span>
        </div>
      }

      @default {
        <!-- E1 · Tableau en jeu -->
        <div class="board">
          <div class="grid" aria-label="Grille des numéros 1 à 90">
            @for (cell of view().cells; track cell.value) {
              <button type="button"
                      class="c"
                      [class.on]="cell.state === 'drawn'"
                      [class.last]="cell.state === 'last'"
                      [class.nb]="cell.state === 'neighbour'"
                      [disabled]="busy()"
                      [attr.aria-label]="'Tirer le ' + cell.value"
                      (click)="onCellClicked(cell)">{{ cell.value }}</button>
            }
          </div>

          <div class="side">
            <div class="side-block">
              <span class="k">Partie {{ view().partieNumber }} / {{ view().partieCount }}</span>
              <span class="partie-name">{{ view().partieName }}</span>
              <div class="chips">
                @if (view().lineLabel) { <span class="chip">{{ view().lineLabel }}</span> }
                @if (view().timesLabel) { <span class="chip chip-accent">{{ view().timesLabel }}</span> }
                @if (view().typeLabel) { <span class="chip chip-strong">{{ view().typeLabel }}</span> }
              </div>
            </div>

            <div class="side-block side-rule">
              <span class="k">Dernier numéro</span>
              <span class="last-big">{{ view().lastNumero ?? '—' }}</span>
              @if (view().neighbours.length > 0) {
                <span class="neighbours">Voisins : {{ view().neighbours.join(' et ') }}</span>
              }
            </div>

            @if (view().previousNumeros.length > 0) {
              <div class="side-block">
                <span class="k">Précédents</span>
                <div class="prev-row">
                  @for (numero of view().previousNumeros; track $index) {
                    <span class="prev">{{ numero }}</span>
                  }
                </div>
              </div>
            }

            <div class="side-block side-rule">
              <span class="k">Lots de la ligne</span>
              @for (lot of view().lots; track lot.id) {
                <span class="lot" [class.lot-won]="lot.won">{{ lot.name }}</span>
              }
            </div>

            @if (view().showBingo) {
              <div class="bingo-panel">
                <span class="k k-accent">Bingo</span>
                <span class="bingo-numbers">{{ view().bingoWon ? 'Gagné !' : view().bingoNumeros.join(' · ') }}</span>
              </div>
            }
          </div>
        </div>
      }
    }

    <!-- E3 · Régie discrète : masquée après 3 s sans souris (RG-04) -->
    @if (controlsVisible() && keyBuffer() !== '') {
      <div class="key-buffer" aria-live="polite">
        <span class="kbd-label">Saisie</span>
        <span class="key-buffer-value">{{ keyBuffer() }}_</span>
        <kbd>Entrée</kbd>
      </div>
    }

    @if (toast(); as message) {
      <div class="toast" role="alert">
        <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="9"></circle><path d="M12 7v6M12 16.5v.5"></path></svg>
        {{ message }}
      </div>
    }

    @if (controlsVisible()) {
      <div class="bar" role="toolbar" aria-label="Régie du loto">
        <button type="button" class="btn" [disabled]="busy()" (click)="run({type: 'undo'})">
          <app-icon name="undo" [size]="22"></app-icon> Annuler le dernier <kbd>⌫</kbd>
        </button>
        <button type="button" class="btn" [disabled]="busy()" [attr.aria-busy]="busy()" (click)="run({type: 'line'})">
          <app-icon name="check" [size]="22"></app-icon> Ligne gagnée <kbd>L</kbd>
        </button>
        <button type="button" class="btn" [disabled]="busy()" (click)="run({type: 'bingo'})">
          <app-icon name="trophy" [size]="22"></app-icon> {{ view().bingoWon ? 'Annuler le bingo' : 'Bingo gagné' }} <kbd>B</kbd>
        </button>
        <button type="button" class="btn" (click)="run({type: 'pause'})">
          <app-icon [name]="overlay() === 'pause' ? 'play' : 'pause'" [size]="22"></app-icon>
          {{ overlay() === 'pause' ? 'Reprendre' : 'Pause' }} <kbd>Espace</kbd>
        </button>
        <span class="sep" aria-hidden="true"></span>
        <button type="button" class="btn" (click)="run({type: 'palette'})"
                [attr.aria-label]="palette() === 'dark' ? 'Passer en palette claire' : 'Passer en palette sombre'">
          <app-icon name="contrast" [size]="22"></app-icon>
          {{ palette() === 'dark' ? 'Palette claire' : 'Palette sombre' }} <kbd>P</kbd>
        </button>
        <button type="button" class="btn" (click)="run({type: 'fullscreen'})" aria-label="Plein écran">
          <app-icon name="fullscreen" [size]="22"></app-icon> <kbd>F</kbd>
        </button>
        <span class="sep" aria-hidden="true"></span>
        <span class="hint">Clic sur une case ou chiffres + <kbd>Entrée</kbd></span>
      </div>
    }
  </div>
</div>

<ng-template #qrCode let-size="size">
  <div class="qr-card">
    <img src="images/loto-qr.svg" [width]="size" [height]="size" alt="QR code vers volepapillondamour.fr/loto">
    <span class="qr-title">Suivez le tableau<br>sur votre téléphone</span>
    <span class="qr-url">{{ shortUrl }}</span>
  </div>
</ng-template>
```

Créer `src/BackOffice/src/scss/_loto-projection.scss` :

```scss
// Tableau du loto projeté en salle (E1, E2, E3) — maquettes
// docs/features/loto-live-board/maquettes/preview/Main.html (palette A) et preview/E1-Claire.html (B).
// Feuille globale, préfixée par .loto-projection : le budget de style d'un composant
// (4 Ko) est trop petit pour une page plein écran à deux palettes. Les deux palettes
// partagent la mise en page ; seules les variables changent (RG-22). Les états d'une
// case se lisent par la luminosité (RG-01).
.loto-projection {
  .pal-a {
    --bg: #020e18;
    --panel: #071b2b;
    --line: #1b3c57;
    --cell: #0b2438;
    --cell-num: #5f8199;
    --on: #ffffff;
    --on-num: #041d30;
    --last: #f0801c;
    --last-num: #041d30;
    --last-big-bg: transparent;
    --last-big-color: #f0801c;
    --text: #ffffff;
    --muted: #9dc2da;
    --chip: #123552;
    --accent: #f9a93c;
    --won: #5f8199;
    --qr-border: transparent;
  }

  .pal-b {
    --bg: #ffffff;
    --panel: #f3f8fc;
    --line: #d9e9f4;
    --cell: #e6eff6;
    --cell-num: #5b7a92;
    --on: #072b45;
    --on-num: #ffffff;
    --last: #f0801c;
    --last-num: #041d30;
    // Orange sur blanc = 2,6:1 : le dernier numéro devient un pavé orange à chiffres encre.
    --last-big-bg: #f0801c;
    --last-big-color: #041d30;
    --text: #041d30;
    --muted: #33536e;
    --chip: #e9f4fb;
    --accent: #b4500e;
    --won: #6d8ba2;
    --qr-border: #072b45;
  }

  .viewport {
    position: fixed;
    inset: 0;
    overflow: hidden;
    background: var(--bg);
    color: var(--text);
    font-family: 'Libre Franklin', system-ui, sans-serif;
    font-variant-numeric: tabular-nums;
  }

  .cursor-hidden,
  .cursor-hidden * {
    cursor: none !important;
  }

  .stage {
    position: absolute;
    top: 50%;
    left: 50%;
    width: 1920px;
    height: 1080px;
    transform-origin: center;
  }

  .screen {
    width: 1920px;
    height: 1080px;
    box-sizing: border-box;
  }

  .screen-center {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 40px;
    padding: 96px;
    text-align: center;
  }

  .screen-split {
    display: flex;
    align-items: center;
    gap: 96px;
    padding: 80px 96px;
  }

  .split-main {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 32px;
  }

  .k {
    font-family: 'IBM Plex Mono', monospace;
    font-size: 18px;
    font-weight: 600;
    letter-spacing: .16em;
    text-transform: uppercase;
    color: var(--muted);
  }

  .k-lg {
    font-size: 30px;
    letter-spacing: .2em;
  }

  .k-accent {
    color: var(--accent);
  }

  .muted {
    color: var(--muted);
    font-weight: 600;
  }

  .lead {
    font-size: 44px;
    font-weight: 600;
    color: var(--muted);
  }

  // E1 ---------------------------------------------------------------------------
  .board {
    width: 1920px;
    height: 1080px;
    box-sizing: border-box;
    padding: 24px;
    display: flex;
    gap: 24px;
  }

  .grid {
    flex: 0 0 1448px; // RG-23 : 75 % de la largeur, toute la hauteur.
    display: grid;
    grid-template-columns: repeat(10, minmax(0, 1fr));
    grid-template-rows: repeat(9, minmax(0, 1fr));
    gap: 8px;
  }

  .c {
    display: flex;
    align-items: center;
    justify-content: center;
    border: 0;
    border-radius: 10px;
    padding: 0;
    background: var(--cell);
    color: var(--cell-num);
    font: 800 76px/1 'Libre Franklin', system-ui, sans-serif;
    font-variant-numeric: tabular-nums;
    letter-spacing: -.02em;
    cursor: pointer;

    &:disabled {
      cursor: progress;
    }

    &.on {
      background: var(--on);
      color: var(--on-num);
    }

    &.last {
      background: var(--last);
      color: var(--last-num);
      font-weight: 900;
    }

    &.nb {
      background: var(--cell);
      color: var(--text);
      box-shadow: inset 0 0 0 8px var(--text);
    }
  }

  .side {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 22px;
  }

  .side-block {
    display: flex;
    flex-direction: column;
    gap: 10px;
  }

  .side-rule {
    border-top: 2px solid var(--line);
    padding-top: 18px;
  }

  .partie-name {
    font-size: 44px;
    font-weight: 800;
    line-height: 1.05;
    letter-spacing: -.02em;
  }

  .chips {
    display: flex;
    flex-wrap: wrap;
    gap: 10px;
  }

  .chip {
    display: inline-flex;
    align-items: center;
    height: 48px;
    padding: 0 18px;
    border-radius: 999px;
    background: var(--chip);
    font-size: 26px;
    font-weight: 700;
  }

  .chip-lg {
    height: 52px;
    padding: 0 20px;
    font-size: 28px;
  }

  .chip-accent {
    background: var(--accent);
    color: #041d30;
  }

  .chip-strong {
    background: var(--on);
    color: var(--on-num);
  }

  .last-big {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 300px;
    border-radius: 20px;
    background: var(--last-big-bg);
    color: var(--last-big-color);
    font-size: 300px;
    font-weight: 900;
    line-height: 1;
    letter-spacing: -.06em;
  }

  .neighbours {
    font-size: 26px;
    font-weight: 700;
    color: var(--muted);
  }

  .prev-row {
    display: flex;
    gap: 10px;
  }

  .prev {
    flex: 1;
    height: 72px;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 10px;
    background: var(--on);
    color: var(--on-num);
    font-size: 44px;
    font-weight: 800;
  }

  .lot {
    font-size: 34px;
    font-weight: 800;
  }

  .lot-won {
    font-size: 30px;
    font-weight: 600;
    color: var(--won);
    text-decoration: line-through;
  }

  .bingo-panel {
    margin-top: auto;
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding: 18px 20px;
    border-radius: 14px;
    background: var(--panel);
    border: 2px solid var(--accent);
  }

  .bingo-numbers {
    font-size: 42px;
    font-weight: 800;
  }

  // E2 ---------------------------------------------------------------------------
  .announce {
    display: flex;
    align-items: center;
    justify-content: center;
    min-width: 1240px;
    height: 780px;
    border-radius: 48px;
    background: var(--last-big-bg);
    color: var(--last-big-color);
    font-size: 780px; // RG-03 : ≥ 60 % de la hauteur.
    font-weight: 900;
    line-height: 1;
    letter-spacing: -.06em;
  }

  .announce-caption {
    font-size: 34px;
    font-weight: 600;
    color: var(--muted);
  }

  .pause-title {
    font-size: 240px;
    font-weight: 900;
    line-height: .85;
    letter-spacing: -.05em;
  }

  .bingo-panel-lg {
    margin-top: 0;
    padding: 28px 32px;
    border-width: 3px;
    border-radius: 20px;
  }

  .bingo-numbers-lg {
    font-size: 96px;
    font-weight: 900;
    letter-spacing: -.02em;
  }

  .next-partie {
    display: flex;
    align-items: baseline;
    gap: 20px;
  }

  .next-partie-name {
    font-size: 48px;
    font-weight: 800;
  }

  .before-title {
    font-size: 132px;
    font-weight: 900;
    line-height: .95;
    letter-spacing: -.035em;
  }

  .first-partie {
    display: flex;
    flex-direction: column;
    gap: 16px;
    border-top: 3px solid var(--line);
    padding-top: 32px;
  }

  .first-partie-name {
    font-size: 60px;
    font-weight: 800;
    letter-spacing: -.02em;
  }

  .finished-title {
    font-size: 170px;
    font-weight: 900;
    line-height: .92;
    letter-spacing: -.04em;
  }

  .finished-thanks {
    font-size: 96px;
    font-weight: 800;
    color: var(--last);
    letter-spacing: -.02em;
  }

  .bingo-pill {
    display: inline-flex;
    align-items: center;
    gap: 16px;
    padding: 18px 30px;
    border-radius: 999px;
    background: var(--panel);
    border: 3px solid var(--accent);
    font-size: 40px;
    font-weight: 800;
  }

  .qr-card {
    flex: none;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 22px;
    padding: 36px;
    border-radius: 28px;
    background: #ffffff;
    border: 4px solid var(--qr-border);
    color: #041d30;

    img {
      image-rendering: pixelated;
    }
  }

  .qr-title {
    font-size: 32px;
    font-weight: 800;
    text-align: center;
    line-height: 1.15;
  }

  .qr-url {
    font-family: 'IBM Plex Mono', monospace;
    font-size: 28px;
    font-weight: 600;
    color: #0c6ea6;
  }

  // E3 ---------------------------------------------------------------------------
  kbd {
    font: 600 12px 'IBM Plex Mono', monospace;
    padding: 3px 7px;
    border-radius: 5px;
    border: 1px solid #33536e;
    color: #9dc2da;
    background: #041d30;
  }

  .bar {
    position: absolute;
    left: 50%;
    bottom: 20px;
    transform: translateX(-50%);
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 10px;
    border-radius: 16px;
    background: rgba(7, 27, 43, .94);
    border: 1px solid #1b3c57;
    box-shadow: 0 18px 40px -20px rgba(0, 0, 0, .8);
  }

  .btn {
    display: inline-flex;
    align-items: center;
    gap: 10px;
    height: 52px;
    padding: 0 16px;
    border-radius: 10px;
    border: 1px solid #1b3c57;
    background: #0b2438;
    color: #dcecf7;
    font: 600 17px 'Libre Franklin', sans-serif;
    cursor: pointer;

    &:disabled {
      opacity: .6;
      cursor: progress;
    }
  }

  .sep {
    width: 1px;
    height: 36px;
    margin: 0 4px;
    background: #1b3c57;
  }

  .hint {
    padding: 0 6px;
    font: 500 13px 'IBM Plex Mono', monospace;
    color: #9dc2da;
  }

  .key-buffer {
    position: absolute;
    top: 20px;
    left: 20px;
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 8px 14px;
    border-radius: 10px;
    background: rgba(7, 27, 43, .94);
    border: 1px solid #1b3c57;
  }

  .kbd-label {
    font: 600 12px 'IBM Plex Mono', monospace;
    letter-spacing: .14em;
    text-transform: uppercase;
    color: #9dc2da;
  }

  .key-buffer-value {
    font-size: 30px;
    font-weight: 800;
    color: #ffffff;
  }

  .toast {
    position: absolute;
    left: 50%;
    bottom: 104px;
    transform: translateX(-50%);
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px 18px;
    border-radius: 12px;
    background: #3a1406;
    border: 1px solid #dc6412;
    color: #ffd9bf;
    font-size: 18px;
    font-weight: 600;

    svg {
      color: #f9a93c;
    }
  }

  @media (prefers-reduced-motion: no-preference) {
    .bar,
    .toast,
    .key-buffer {
      transition: opacity .15s ease;
    }
  }
}
```

Modifier `src/BackOffice/src/styles.scss` :

```diff
@@ -6,6 +6,7 @@
 @use '@angular/material' as mat;
 @use '@dhutaryan/ngx-mat-timepicker' as mat-timepicker;
 @use "scss/main" as *;
+@use "scss/loto-projection";
 
 @include mat.elevation-classes();
 @include mat.app-background();
```

Créer `src/BackOffice/public/images/loto-qr.svg` :

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 33 33"><title>QR code vers volepapillondamour.fr/loto</title><path fill="#fff" d="M0 0h33v33h-33z"/><path stroke="#041d30" d="M2 2.5h7m1 0h1m4 0h3m2 0h1m3 0h7m-29 1h1m5 0h1m2 0h1m1 0h1m3 0h1m2 0h1m1 0h1m1 0h1m5 0h1m-29 1h1m1 0h3m1 0h1m1 0h6m3 0h1m1 0h2m1 0h1m1 0h3m1 0h1m-29 1h1m1 0h3m1 0h1m3 0h3m2 0h1m1 0h2m3 0h1m1 0h3m1 0h1m-29 1h1m1 0h3m1 0h1m3 0h1m4 0h2m1 0h1m3 0h1m1 0h3m1 0h1m-29 1h1m5 0h1m1 0h1m1 0h2m1 0h2m2 0h2m3 0h1m5 0h1m-29 1h7m1 0h1m1 0h1m1 0h1m1 0h1m1 0h1m1 0h1m1 0h1m1 0h7m-20 1h2m1 0h2m4 0h1m1 0h1m-21 1h1m1 0h1m3 0h2m3 0h1m1 0h1m1 0h2m2 0h2m2 0h1m2 0h1m1 0h1m-28 1h1m1 0h1m4 0h3m1 0h1m5 0h6m3 0h2m-29 1h1m1 0h1m3 0h2m4 0h4m1 0h1m1 0h4m1 0h3m1 0h1m-29 1h2m3 0h1m3 0h2m3 0h3m1 0h3m2 0h1m1 0h1m-25 1h3m2 0h2m1 0h3m1 0h2m1 0h1m3 0h1m1 0h2m4 0h1m-29 1h1m3 0h1m2 0h10m1 0h3m1 0h2m3 0h2m-28 1h1m2 0h3m3 0h1m4 0h2m1 0h2m1 0h4m3 0h1m-29 1h1m1 0h4m2 0h6m2 0h1m2 0h3m1 0h2m-24 1h2m3 0h2m1 0h2m2 0h1m1 0h1m2 0h1m1 0h1m1 0h2m4 0h1m-28 1h2m2 0h1m4 0h2m3 0h3m1 0h5m2 0h3m-29 1h2m2 0h5m2 0h1m1 0h3m3 0h4m2 0h1m2 0h1m-26 1h1m3 0h1m1 0h4m1 0h3m3 0h1m2 0h2m-25 1h3m2 0h2m1 0h2m2 0h3m1 0h1m3 0h6m1 0h1m-20 1h2m1 0h1m1 0h4m3 0h1m3 0h3m1 0h1m-29 1h7m1 0h4m3 0h2m3 0h1m1 0h1m1 0h1m3 0h1m-29 1h1m5 0h1m5 0h2m2 0h1m2 0h2m3 0h1m3 0h1m-29 1h1m1 0h3m1 0h1m3 0h1m2 0h1m2 0h1m2 0h7m1 0h2m-29 1h1m1 0h3m1 0h1m2 0h1m1 0h1m3 0h2m1 0h1m2 0h1m2 0h3m1 0h1m-29 1h1m1 0h3m1 0h1m1 0h2m3 0h1m1 0h1m2 0h1m2 0h1m2 0h1m2 0h2m-29 1h1m5 0h1m4 0h1m1 0h1m1 0h2m3 0h1m1 0h1m1 0h2m-26 1h7m1 0h3m2 0h1m2 0h1m1 0h1m1 0h1m1 0h1m1 0h1m3 0h1"/></svg>
```

Modifier `src/BackOffice/src/app/feature/feature.module.ts` :

```diff
@@ -47,9 +47,6 @@ import {
   CreationUpdateLotComponent
 } from './event-detail/components/bingo-event/components/dialogs/creation-update-line-partie/creation-update-lot.component';
 import {TableauComponent} from "./event-detail/components/tableau/tableau.component";
-import {ModalComponent} from "./event-detail/components/tableau/components/modal-tableau/modal.component";
-import {TagComponent} from "./event-detail/components/tableau/components/tag/tag.component";
-import {NumberComponent} from "./event-detail/components/tableau/components/number/number.component";
 import {
   CreateUpdateProductDialogComponent
 } from '../shared/components/dialogs/create-update-product-dialog/create-update-product-dialog.component';
@@ -88,9 +85,6 @@ export const MY_DATE_FORMATS: MatDateFormats = {
     CreationUpdatePartieComponent,
     CreationUpdateLotComponent,
     TableauComponent,
-    ModalComponent,
-    TagComponent,
-    NumberComponent,
     CreateUpdateProductDialogComponent,
     CatalogAdministrationComponent,
   ],
```

Modifier `src/BackOffice/src/app/shared/components/icon/icon.component.ts` :

```diff
@@ -7,10 +7,14 @@ export type IconName =
   | 'check'
   | 'chevron-right'
   | 'close'
+  | 'contrast'
   | 'delete'
   | 'download'
   | 'edit'
+  | 'fullscreen'
   | 'menu'
+  | 'pause'
+  | 'play'
   | 'trophy'
   | 'undo';
```

Modifier `src/BackOffice/src/app/shared/components/icon/icon.component.html` :

```diff
@@ -28,6 +28,10 @@
     @case ('close') {
       <path d="M6 6l12 12M18 6L6 18"/>
     }
+    @case ('contrast') {
+      <circle cx="12" cy="12" r="8"/>
+      <path d="M12 4a8 8 0 0 1 0 16Z" fill="currentColor"/>
+    }
     @case ('delete') {
       <path d="M5 7h14M9.5 7V5a1 1 0 0 1 1-1h3a1 1 0 0 1 1 1v2M7 7l1 12.5A1.5 1.5 0 0 0 9.5 21h5a1.5 1.5 0 0 0 1.5-1.5L17 7"/>
       <path d="M10.5 11v6M13.5 11v6"/>
@@ -39,9 +43,18 @@
     @case ('edit') {
       <path d="M15.7 4.7a1.9 1.9 0 0 1 2.7 2.7L8.5 17.3 4.5 18.4l1.1-4z"/>
     }
+    @case ('fullscreen') {
+      <path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5"/>
+    }
     @case ('menu') {
       <path d="M4 6.5h16M4 12h16M4 17.5h16"/>
     }
+    @case ('pause') {
+      <path d="M9 5v14M15 5v14"/>
+    }
+    @case ('play') {
+      <path d="M8 5.5v13l10-6.5Z"/>
+    }
     @case ('trophy') {
       <path d="M8 5.5h8v5a4 4 0 0 1-8 0Z"/>
       <path d="M8 6.5H5.5a2 2 0 0 0 0 4H7M16 6.5h2.5a2 2 0 0 1 0 4H17M11 15.5v2.3M8.5 20.5h7l-.9-2.2a1 1 0 0 0-.9-.5H10.3a1 1 0 0 0-.9.5Z"/>
```

- [ ] Supprimer `tableau.component.scss` et `components/` (9 fichiers).
- [ ] `cd src/BackOffice && npm run build` : aucune erreur de budget sur le tableau.
  `npm test -- --watch=false --browsers=ChromeHeadless` : vert (32 specs à la conception).
- [ ] Commit : `feat(backoffice): redesign the projected loto board for stage lighting`

## Tâche 8 — Website : client du direct (révisions, états de connexion, 404)

RG-12 à RG-15. Le service lit l'état complet par REST (404 → `not-found`) puis ouvre le flux.
- **Révisions.** Un état de révision inférieure à celle affichée est ignoré.
- **Reconnexion.** Elle se fait avec un délai croissant de 500 ms à 5 s, et tout de suite au
  retour du réseau (`online`) ou de la page (`visibilitychange`).
- **États de connexion.** « Reconnexion… » s'affiche après 5 s ; « Hors ligne » quand
  `navigator.onLine` est faux.
- **Portée.** Le service est fourni par la page, pas en racine : la connexion se ferme quand
  on quitte la page.

**Fichiers :**
- Modifier (réécrire) : `src/Website/src/app/shared/clients/sse-client.service.ts`,
  `sse-client.service.spec.ts`
- Modifier : `src/Website/src/app/shared/models/vpdEvent.model.ts` (`liveRevision?`)

**Interfaces produites :**
- `SseClientService.init(id): Promise<void>` ;
- les signaux `eventAsso` et `connection` (type
  `LiveTableauConnection = 'connecting' | 'live' | 'reconnecting' | 'offline' | 'not-found'`).

- [ ] **Test d'abord** :

Remplacer tout le contenu de `src/Website/src/app/shared/clients/sse-client.service.spec.ts` :

```ts
import {PLATFORM_ID} from '@angular/core';
import {TestBed} from '@angular/core/testing';

import {SseClientService} from './sse-client.service';
import {NumberLineEnum} from '../enums/numberLine.enum';
import {PartieTypeEnum} from '../enums/partieType.enum';
import {VpdEventEnum} from '../enums/vpdEvent.enum';
import {VpdEventModel} from '../models/vpdEvent.model';
import {VpdEventsFacadeService} from '../facades/vpd-events.facade.service';

class FakeEventSource {
  static readonly OPEN = 1;
  static readonly instances: FakeEventSource[] = [];

  readyState = 0;
  onmessage: ((event: MessageEvent<string>) => void) | null = null;
  onopen: (() => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  close = jasmine.createSpy('close');

  constructor(public readonly url: string) {
    FakeEventSource.instances.push(this);
  }

  emitOpen(): void {
    this.readyState = FakeEventSource.OPEN;
    this.onopen?.();
  }

  emitMessage(data: string): void {
    this.onmessage?.({data} as MessageEvent<string>);
  }

  emitError(): void {
    this.readyState = 2;
    this.onerror?.(new Event('error'));
  }
}

function liveEvent(liveRevision: number, liveNumeros: number[]): VpdEventModel {
  return {
    eventType: VpdEventEnum.Bingo,
    id: 'event-1',
    name: 'Live loto',
    description: 'Live event',
    dateStart: new Date('2026-05-19T20:00:00Z'),
    dateEnd: null,
    hourOpenDoors: null,
    hourCloseDoors: null,
    urlImageMap: null,
    urlRegistration: null,
    urlImage: 'https://example.com/loto.jpg',
    city: 'Arras',
    road: 'Rue du loto',
    cityCode: '62000',
    roadNumber: '12',
    currentPartieIndex: 0,
    bingoHasBeenWon: false,
    bingoNumeros: [],
    liveRevision,
    parties: [
      {
        id: 'partie-1',
        name: 'Partie 1',
        partieType: PartieTypeEnum.STANDARD,
        index: 0,
        pauseAfter: false,
        addedBingoNumber: null,
        lastNumeros: liveNumeros,
        liveNumeros,
        currentLineIndex: 0,
        lineParties: [
          {
            id: 'line-1',
            index: 0,
            numberLine: NumberLineEnum.ONELINE,
            lots: [{id: 'lot-1', name: 'Lot 1', urlImage: 'lot.jpg', index: 0, isWon: null}]
          }
        ]
      }
    ]
  };
}

describe('SseClientService', () => {
  const originalEventSource = globalThis.EventSource;
  let events: jasmine.SpyObj<VpdEventsFacadeService>;

  beforeEach(() => {
    FakeEventSource.instances.length = 0;
    spyOn(console, 'error');
    (globalThis as unknown as {EventSource: typeof FakeEventSource}).EventSource = FakeEventSource;
    events = jasmine.createSpyObj<VpdEventsFacadeService>('VpdEventsFacadeService', ['getEventById']);
    events.getEventById.and.resolveTo(liveEvent(1, [25]));
  });

  afterEach(() => {
    globalThis.EventSource = originalEventSource;
    TestBed.resetTestingModule();
  });

  it('affiche l’état lu à l’ouverture puis suit le flux de l’événement', async () => {
    const service = createService('browser', events);

    await service.init('event-1');
    expect(service.eventAsso()?.parties[0].liveNumeros).toEqual([25]);
    expect(FakeEventSource.instances[0].url).toContain('/asso-events/event-1/tableau/sse');

    FakeEventSource.instances[0].emitOpen();
    FakeEventSource.instances[0].emitMessage(JSON.stringify(liveEvent(2, [25, 42])));

    expect(service.connection()).toBe('live');
    expect(service.eventAsso()?.parties[0].liveNumeros).toEqual([25, 42]);
  });

  it('ignore un état plus ancien que celui affiché', async () => {
    events.getEventById.and.resolveTo(liveEvent(5, [25, 42, 7]));
    const service = createService('browser', events);

    await service.init('event-1');
    FakeEventSource.instances[0].emitMessage(JSON.stringify(liveEvent(4, [25, 42])));

    expect(service.eventAsso()?.liveRevision).toBe(5);
  });

  it('garde le dernier état sur un message illisible', async () => {
    const service = createService('browser', events);

    await service.init('event-1');
    FakeEventSource.instances[0].emitMessage('{malformed-json');

    expect(service.eventAsso()?.parties[0].lastNumeros).toEqual([25]);
    expect(console.error).toHaveBeenCalled();
  });

  it('signale un tableau introuvable sans ouvrir de flux (RG-15)', async () => {
    events.getEventById.and.rejectWith({response: {status: 404}});
    const service = createService('browser', events);

    await service.init('unknown');

    expect(service.connection()).toBe('not-found');
    expect(FakeEventSource.instances.length).toBe(0);
  });

  it('se reconnecte avec un délai croissant et affiche « Reconnexion… » après 5 s (RG-12, RG-13)', async () => {
    const service = createService('browser', events);
    await service.init('event-1');
    jasmine.clock().install();

    try {
      FakeEventSource.instances[0].emitOpen();
      FakeEventSource.instances[0].emitError();
      expect(FakeEventSource.instances[0].close).toHaveBeenCalled();
      expect(service.connection()).toBe('live');

      jasmine.clock().tick(499);
      expect(FakeEventSource.instances.length).toBe(1);
      jasmine.clock().tick(1);
      expect(FakeEventSource.instances.length).toBe(2);

      FakeEventSource.instances[1].emitError();
      jasmine.clock().tick(999);
      expect(FakeEventSource.instances.length).toBe(2);
      jasmine.clock().tick(1);
      expect(FakeEventSource.instances.length).toBe(3);

      jasmine.clock().tick(3500);
      expect(service.connection()).toBe('reconnecting');

      FakeEventSource.instances[2].emitOpen();
      FakeEventSource.instances[2].emitMessage(JSON.stringify(liveEvent(3, [25, 42, 7])));
      expect(service.connection()).toBe('live');
      expect(service.eventAsso()?.parties[0].liveNumeros).toEqual([25, 42, 7]);
    } finally {
      jasmine.clock().uninstall();
    }
  });

  it('ferme le flux précédent en changeant d’événement', async () => {
    const service = createService('browser', events);

    await service.init('event-1');
    const firstConnection = FakeEventSource.instances[0];
    await service.init('event-2');

    expect(firstConnection.close).toHaveBeenCalled();
    expect(FakeEventSource.instances[1].url).toContain('/asso-events/event-2/tableau/sse');
  });

  it('n’ouvre aucun flux pendant le rendu serveur', async () => {
    const service = createService('server', events);

    await service.init('event-1');

    expect(FakeEventSource.instances.length).toBe(0);
    expect(events.getEventById).not.toHaveBeenCalled();
  });
});

function createService(platformId: 'browser' | 'server', events: VpdEventsFacadeService): SseClientService {
  TestBed.configureTestingModule({
    providers: [
      SseClientService,
      {provide: PLATFORM_ID, useValue: platformId},
      {provide: VpdEventsFacadeService, useValue: events}
    ]
  });

  return TestBed.inject(SseClientService);
}
```

- [ ] Implémentation :

Remplacer tout le contenu de `src/Website/src/app/shared/clients/sse-client.service.ts` :

```ts
import {isPlatformBrowser} from '@angular/common';
import {Injectable, inject, OnDestroy, PLATFORM_ID, signal} from '@angular/core';
import {VpdEventModel} from "../models/vpdEvent.model";
import {VpdEventsFacadeService} from "../facades/vpd-events.facade.service";
import {environment} from "../../../environments/environment";

/** Pastille d'état du tableau en direct (E4, RG-13). */
export type LiveTableauConnection = 'connecting' | 'live' | 'reconnecting' | 'offline' | 'not-found';

/**
 * Tableau en direct d'un événement : état complet lu à l'ouverture, puis flux SSE.
 * Chaque message est un état complet portant sa révision : après une coupure, la
 * reconnexion renvoie l'état à jour (RG-12) et un message plus ancien est ignoré.
 *
 * Fourni par la page du tableau (pas en racine) : la connexion se ferme avec la page.
 */
@Injectable()
export class SseClientService implements OnDestroy {
  static readonly initialReconnectDelayMs = 500;
  static readonly maxReconnectDelayMs = 5000;
  static readonly reconnectingAfterMs = 5000;

  private readonly platformId = inject(PLATFORM_ID);
  private readonly events = inject(VpdEventsFacadeService);

  private eventSource: EventSource | undefined;
  private reconnectTimeoutId: ReturnType<typeof setTimeout> | undefined;
  private reconnectingTimeoutId: ReturnType<typeof setTimeout> | undefined;
  private reconnectDelayMs = SseClientService.initialReconnectDelayMs;
  private currentEventId: string | undefined;
  private listening = false;

  readonly eventAsso = signal<VpdEventModel | null>(null);
  readonly connection = signal<LiveTableauConnection>('connecting');

  async init(id: string): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.currentEventId = id;
    this.reconnectDelayMs = SseClientService.initialReconnectDelayMs;
    this.clearTimers();
    this.closeEventSource();
    this.eventAsso.set(null);
    this.connection.set('connecting');
    this.listenToBrowser();

    try {
      const snapshot = await this.events.getEventById(id);
      if (this.currentEventId === id) {
        this.accept(snapshot);
      }
    } catch (error) {
      if ((error as {response?: {status?: number}})?.response?.status === 404) {
        this.connection.set('not-found');
        return;
      }
      // Réseau indisponible : le flux SSE prendra le relais dès qu'il se connecte.
    }

    if (this.currentEventId === id) {
      this.openEventSource(id);
    }
  }

  private openEventSource(id: string): void {
    this.closeEventSource();

    const eventSource = new EventSource(`${environment.api_url}/asso-events/${id}/tableau/sse`);
    this.eventSource = eventSource;

    eventSource.onopen = () => {
      this.reconnectDelayMs = SseClientService.initialReconnectDelayMs;
      this.clearReconnectingTimeout();
      this.connection.set('live');
    };

    eventSource.onmessage = (event) => {
      try {
        this.accept(JSON.parse(event.data) as VpdEventModel);
        this.connection.set('live');
      } catch (error) {
        console.error('Invalid SSE event payload: ', error);
      }
    };

    eventSource.onerror = () => {
      this.closeEventSource();
      this.markDisconnected();
      this.scheduleReconnect(id);
    };
  }

  private accept(snapshot: VpdEventModel): void {
    const current = this.eventAsso();
    if (current !== null && current.id === snapshot.id &&
      (snapshot.liveRevision ?? 0) < (current.liveRevision ?? 0)) {
      return;
    }
    this.eventAsso.set(snapshot);
  }

  /** RG-13 : « Hors ligne » sans réseau, « Reconnexion… » après 5 s sans connexion. */
  private markDisconnected(): void {
    if (!navigator.onLine) {
      this.connection.set('offline');
      return;
    }
    if (this.reconnectingTimeoutId === undefined && this.connection() !== 'reconnecting') {
      this.reconnectingTimeoutId = setTimeout(() => {
        this.reconnectingTimeoutId = undefined;
        if (this.eventSource === undefined || this.eventSource.readyState !== EventSource.OPEN) {
          this.connection.set(navigator.onLine ? 'reconnecting' : 'offline');
        }
      }, SseClientService.reconnectingAfterMs);
    }
  }

  private scheduleReconnect(id: string): void {
    if (this.reconnectTimeoutId !== undefined) {
      return;
    }

    const reconnectDelayMs = this.reconnectDelayMs;
    this.reconnectDelayMs = Math.min(SseClientService.maxReconnectDelayMs, this.reconnectDelayMs * 2);

    this.reconnectTimeoutId = setTimeout(() => {
      this.reconnectTimeoutId = undefined;
      if (this.currentEventId === id) {
        this.openEventSource(id);
      }
    }, reconnectDelayMs);
  }

  private reconnectNow(): void {
    const id = this.currentEventId;
    if (id === undefined || this.connection() === 'not-found' || this.eventSource !== undefined) {
      return;
    }
    this.clearReconnectTimeout();
    this.reconnectDelayMs = SseClientService.initialReconnectDelayMs;
    this.openEventSource(id);
  }

  private readonly onOnline = () => this.reconnectNow();
  private readonly onOffline = () => {
    if (this.connection() !== 'not-found') {
      this.connection.set('offline');
    }
  };
  /** Un téléphone en veille coupe le flux : on le rouvre dès que la page revient. */
  private readonly onVisibilityChange = () => {
    if (document.visibilityState === 'visible') {
      this.reconnectNow();
    }
  };

  private listenToBrowser(): void {
    if (this.listening) {
      return;
    }
    this.listening = true;
    window.addEventListener('online', this.onOnline);
    window.addEventListener('offline', this.onOffline);
    document.addEventListener('visibilitychange', this.onVisibilityChange);
  }

  private closeEventSource(): void {
    this.eventSource?.close();
    this.eventSource = undefined;
  }

  private clearReconnectTimeout(): void {
    clearTimeout(this.reconnectTimeoutId);
    this.reconnectTimeoutId = undefined;
  }

  private clearReconnectingTimeout(): void {
    clearTimeout(this.reconnectingTimeoutId);
    this.reconnectingTimeoutId = undefined;
  }

  private clearTimers(): void {
    this.clearReconnectTimeout();
    this.clearReconnectingTimeout();
  }

  ngOnDestroy(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.currentEventId = undefined;
    this.clearTimers();
    this.closeEventSource();
    if (this.listening) {
      window.removeEventListener('online', this.onOnline);
      window.removeEventListener('offline', this.onOffline);
      document.removeEventListener('visibilitychange', this.onVisibilityChange);
    }
  }
}
```

Modifier `src/Website/src/app/shared/models/vpdEvent.model.ts` :

```diff
@@ -22,6 +22,8 @@ export interface VpdEventModel {
   currentPartieIndex: number,
   bingoHasBeenWon: boolean
   bingoNumeros: number[]
+  /** Révision du tableau en direct : un état plus ancien que celui affiché est ignoré. */
+  liveRevision?: number
 }
 
 export interface VpdEventPartieModel {
```

- [ ] Specs vertes.
- [ ] Commit : `feat(website): make the live tableau client revision-aware and resilient`

## Tâche 9 — Website : tableau en direct mobile

**Maquette :** preview/E4-Mobile.html — zones E4-etat-connexion, E4-dernier-numero, E4-precedents, E4-partie, E4-lots, E4-grille, E4-bingo
**Maquette :** preview/E4-Desktop.html — zones E4-etat-connexion, E4-dernier-numero, E4-grille
**Maquette :** preview/E4-Etats.html — zones E4-reconnexion, E4-avant-debut, E4-termine, E4-introuvable

RG-09 à RG-15. La page se lit sur une colonne à 390 px et sur deux colonnes à partir de `lg`
(panneau de 360 px, grille à droite). La pastille et le message de connexion suivent
`connection()`. Il n'y a pas d'écran Pause côté téléphone (décision du 13/10).

**Fichiers :**
- Modifier (réécrire) : `src/Website/src/app/feature/tableau/tableau.component.ts`,
  `tableau.component.html`, `tableau.component.scss`
- Supprimer : `src/Website/src/app/feature/tableau/components/` (`modal-tableau`, `number`, `tag`)
- Modifier : `src/Website/src/app/feature/feature.module.ts`

- [ ] Implémentation :

Remplacer tout le contenu de `src/Website/src/app/feature/tableau/tableau.component.ts` :

```ts
import {Component, computed, DestroyRef, inject, OnInit} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {ActivatedRoute} from "@angular/router";
import {buildLotoTableauView} from '@vpd/ui';
import {SseClientService} from "../../shared/clients/sse-client.service";

/** E4 · Tableau du loto en direct, pensé pour le téléphone des joueurs. */
@Component({
    selector: 'app-tableau',
    templateUrl: './tableau.component.html',
    styleUrl: './tableau.component.scss',
    providers: [SseClientService],
    standalone: false
})
export class TableauComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly live = inject(SseClientService);

  protected readonly view = computed(() => buildLotoTableauView(this.live.eventAsso()));
  protected readonly connection = this.live.connection;

  ngOnInit(): void {
    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const id = params.get('id');
        if (id !== null) {
          void this.live.init(id);
        }
      });
  }
}
```

Remplacer tout le contenu de `src/Website/src/app/feature/tableau/tableau.component.html` :

```html
<!-- E4 : maquettes preview/E4-Mobile.html (390 px), preview/E4-Desktop.html (1440 px), preview/E4-Etats.html -->
<main class="tableau min-h-screen bg-[#020e18] px-4 pb-10 pt-3 text-white lg:px-shell lg:pb-11 lg:pt-5">
  <div class="mx-auto flex w-full max-w-[1352px] flex-col gap-3.5 lg:gap-5">
    <div class="flex h-11 items-center justify-between lg:h-12">
      @if (connection() === 'not-found') {
        <a routerLink="/evenement" class="text-sm font-semibold text-cyan no-underline hover:text-white">← Évènements</a>
      } @else {
        <a [routerLink]="['/evenement', live.eventAsso()?.id]" class="min-w-0 truncate text-sm font-semibold text-cyan no-underline hover:text-white lg:text-[15px]">← {{ view().eventName || 'Grand loto' }}</a>
        <span role="status" class="inline-flex h-[30px] shrink-0 items-center gap-[7px] rounded-full border border-[#1b3c57] bg-[#0b2438] px-3 font-mono text-[10px] font-semibold uppercase tracking-[.14em]" [class]="connection() === 'live' ? 'text-orange-2' : 'text-mist-2'">
          @switch (connection()) {
            @case ('live') {
              @if (view().phase === 'finished') {
                Terminé
              } @else if (view().phase === 'before') {
                <span aria-hidden="true" class="h-2 w-2 rounded-full bg-cyan"></span> Connecté
              } @else {
                <span aria-hidden="true" class="h-2 w-2 rounded-full bg-orange shadow-[0_0_0_3px_rgba(240,128,28,.25)]"></span> En direct
              }
            }
            @case ('reconnecting') {
              <svg viewBox="0 0 16 16" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M13 8a5 5 0 1 1-1.5-3.5M13 3v2.5h-2.5"></path></svg>
              Reconnexion…
            }
            @case ('offline') { Hors ligne }
            @default { Connexion… }
          }
        </span>
      }
    </div>

    @if (connection() === 'reconnecting' || connection() === 'offline') {
      <p class="rounded-xl bg-[#123552] px-3.5 py-3 text-sm leading-snug text-mist-2">
        Connexion perdue. Le tableau se remettra à jour tout seul dès le retour du réseau.
      </p>
    }

    @if (connection() === 'not-found') {
      <!-- RG-15 -->
      <section class="flex max-w-[420px] flex-col gap-3.5 pt-16">
        <svg viewBox="0 0 48 48" width="56" height="56" fill="none" stroke="#9dc2da" stroke-width="2.5" stroke-linecap="round" aria-hidden="true"><rect x="7" y="7" width="34" height="34" rx="6"></rect><path d="M17 17l14 14M31 17L17 31"></path></svg>
        <h1 class="text-[30px] font-extrabold leading-tight">Ce tableau n'existe pas ou n'est plus disponible.</h1>
        <p class="text-[15px] leading-relaxed text-mist-2">Vérifiez le lien, ou retrouvez le loto du jour depuis la page des événements.</p>
        <a routerLink="/evenement" class="mt-2 inline-flex h-12 items-center justify-center rounded-full bg-white text-[15px] font-bold text-ink no-underline hover:bg-mist-2">Voir les événements</a>
      </section>
    } @else {
      @switch (view().phase) {
        @case ('loading') {
          <div class="flex flex-col gap-3.5" role="status" aria-label="Chargement du tableau de loto">
            <span class="skeleton skeleton-dark h-[220px] rounded-[18px]"></span>
            <span class="skeleton skeleton-dark h-12 rounded-lg"></span>
            <span class="skeleton skeleton-dark h-[320px] rounded-[14px]"></span>
          </div>
        }

        @case ('before') {
          <section class="flex max-w-[560px] flex-col gap-3.5">
            <div class="flex flex-col gap-3 px-1 pb-2 pt-7">
              <span class="k">{{ view().eventName }} · aujourd'hui</span>
              <h1 class="text-[44px] font-black leading-none tracking-[-.03em]">Le loto va commencer</h1>
              <p class="text-[15px] leading-relaxed text-mist-2">Gardez cette page ouverte : les numéros s'afficheront ici dès le premier tirage.</p>
            </div>
            @if (view().firstPartie; as first) {
              <div class="card">
                <span class="k">Première partie</span>
                <span class="text-[22px] font-extrabold">{{ first.name }}</span>
                @for (line of first.lines; track line.label) {
                  <span class="text-[15px]">{{ line.label }} · {{ line.lots.join(', ') }}</span>
                }
              </div>
            }
          </section>
        }

        @case ('finished') {
          <section class="flex max-w-[560px] flex-col gap-3.5">
            <div class="flex flex-col gap-3 px-1 pb-2 pt-7">
              <h1 class="text-[48px] font-black leading-none tracking-[-.03em]">Loto terminé</h1>
              <span class="text-[30px] font-extrabold text-orange">Merci à tous !</span>
              <p class="text-[15px] leading-relaxed text-mist-2">Les bénéfices de cet après-midi soutiennent Maxence et sa famille.</p>
            </div>
            <div class="card card-bingo">
              <span class="k k-accent">Bingo</span>
              <span class="text-[26px] font-extrabold">{{ view().bingoWon ? 'Gagné !' : view().bingoNumeros.join(' · ') }}</span>
            </div>
            <a routerLink="/evenement" class="mt-2 inline-flex h-12 items-center justify-center rounded-full bg-white text-[15px] font-bold text-ink no-underline hover:bg-mist-2">Voir les prochains événements</a>
          </section>
        }

        @default {
          <div class="flex flex-col gap-3.5 lg:flex-row lg:items-start lg:gap-7">
            <div class="flex flex-col gap-3.5 lg:w-[360px] lg:flex-none lg:gap-4">
              <div class="flex flex-col items-center gap-0.5 rounded-[18px] border border-[#1b3c57] bg-[#071b2b] px-4 pb-[22px] pt-[18px]" aria-live="polite">
                <span class="k">Dernier numéro</span>
                <span class="text-[184px] font-black leading-[.9] tracking-[-.06em] text-orange tabular-nums lg:text-[200px]">{{ view().lastNumero ?? '—' }}</span>
              </div>

              @if (view().previousNumeros.length > 0) {
                <div class="flex flex-col gap-2">
                  <span class="k">Précédents</span>
                  <div class="flex gap-2">
                    @for (numero of view().previousNumeros; track $index) {
                      <span class="prev">{{ numero }}</span>
                    }
                  </div>
                </div>
              }

              <div class="card">
                <span class="k">Partie {{ view().partieNumber }} / {{ view().partieCount }}</span>
                <span class="text-2xl font-extrabold tracking-[-.01em] lg:text-[26px]">{{ view().partieName }}</span>
                <div class="flex flex-wrap gap-2">
                  @if (view().lineLabel) { <span class="chip">{{ view().lineLabel }}</span> }
                  @if (view().timesLabel) { <span class="chip chip-accent">{{ view().timesLabel }}</span> }
                  @if (view().typeLabel) { <span class="chip chip-strong">{{ view().typeLabel }}</span> }
                </div>
                <div class="flex flex-col gap-1.5 border-t border-[#1b3c57] pt-2.5">
                  <span class="k">Lots de la ligne</span>
                  @for (lot of view().lots; track lot.id) {
                    <span [class]="lot.won ? 'text-[15px] text-[#6d8ba2] line-through' : 'text-[17px] font-bold lg:text-lg'">{{ lot.name }}</span>
                  }
                </div>
              </div>

              @if (view().showBingo) {
                <div class="card card-bingo max-lg:order-last">
                  <span class="k k-accent">Bingo</span>
                  <span class="text-[30px] font-extrabold tabular-nums lg:text-[32px]">{{ view().bingoWon ? 'Gagné !' : (view().bingoNumeros.join(' · ') || '—') }}</span>
                </div>
              }
            </div>

            <div class="flex flex-col gap-2 lg:min-w-0 lg:flex-1">
              <span class="k lg:hidden">Tous les numéros</span>
              <div class="grid grid-cols-10 gap-[3px] lg:gap-1.5" aria-label="Grille des numéros 1 à 90">
                @for (cell of view().cells; track cell.value) {
                  <span class="cell"
                        [class.cell-on]="cell.state === 'drawn'"
                        [class.cell-last]="cell.state === 'last'"
                        [class.cell-nb]="cell.state === 'neighbour'">{{ cell.value }}</span>
                }
              </div>
              <span class="text-xs text-mist">{{ view().drawnCount }} numéros sortis@if (view().neighbours.length > 0) { · voisins du +1 / −1 cerclés }</span>
            </div>
          </div>

          <p class="text-center text-xs text-mist">Mis à jour automatiquement, pas besoin de recharger la page.</p>
        }
      }
    }
  </div>
</main>
```

Remplacer tout le contenu de `src/Website/src/app/feature/tableau/tableau.component.scss` :

```scss
// E4 : sorti = case claire, dernier = orange, voisin = case cerclée (mêmes repères que la projection).
.k {
  @apply font-mono text-[10px] font-semibold uppercase tracking-[.16em] text-mist lg:text-[11px];
}

.card {
  @apply flex flex-col gap-2.5 rounded-[14px] border border-[#1b3c57] bg-[#071b2b] p-4 lg:p-[18px];
}

.card-bingo {
  @apply border-orange-2;
}

.k-accent {
  @apply text-orange-2;
}

.chip {
  @apply inline-flex h-[30px] items-center rounded-full bg-[#123552] px-3 text-[13px] font-bold lg:h-8 lg:px-3.5 lg:text-sm;
}

.chip-accent {
  @apply bg-orange-2 text-ink;
}

.chip-strong {
  @apply bg-white text-ink;
}

.prev {
  @apply flex h-12 flex-1 items-center justify-center rounded-lg bg-white text-2xl font-extrabold tabular-nums text-ink lg:h-14 lg:text-[28px];
}

.cell {
  @apply flex h-8 items-center justify-center rounded-[5px] bg-[#0b2438] text-sm font-bold tabular-nums text-[#6d8ba2] lg:h-[84px] lg:rounded-lg lg:text-4xl lg:font-extrabold;
}

.cell-on {
  @apply bg-white font-extrabold text-ink;
}

.cell-last {
  @apply bg-orange font-black text-ink;
}

.cell-nb {
  @apply bg-[#0b2438] text-white shadow-[inset_0_0_0_2px_#ffffff] lg:shadow-[inset_0_0_0_4px_#ffffff];
}
```

Modifier `src/Website/src/app/feature/feature.module.ts` :

```diff
@@ -59,9 +59,6 @@ import {CoreModule} from "../core/core.module";
 import { PartieCardComponent } from './event-detail/components/bingo-event/components/partie-card/partie-card.component';
 import { LotCardComponent } from './event-detail/components/bingo-event/components/lot-card/lot-card.component';
 import {TableauComponent} from "./tableau/tableau.component";
-import {ModalComponent} from "./tableau/components/modal-tableau/modal.component";
-import {TagComponent} from "./tableau/components/tag/tag.component";
-import {NumberComponent} from "./tableau/components/number/number.component";
 import {VpdAllEventsComponent} from "./vpd-all-events/vpd-all-events.component";
 import {MaladiesListComponent} from "./maxence/diseases/maladies-list/maladies-list.component";
 import {ActionsComponent} from "./actions/actions.component";
@@ -129,9 +126,6 @@ import {SouvenirsComponent} from './maxence/souvenirs/souvenirs.component';
     PartieCardComponent,
     LotCardComponent,
     TableauComponent,
-    ModalComponent,
-    TagComponent,
-    NumberComponent,
     VpdAllEventsComponent,
   ],
   imports: [
```

- [ ] Supprimer `components/` (9 fichiers).
- [ ] `npm run build` : le style du composant reste sous le seuil d'erreur de 4 Ko
  (2,9 Ko à la conception, avec un avertissement à 2 Ko).
- [ ] Commit : `feat(website): redesign the live loto tableau for phones`

## Tâche 10 — Website : bandeau « Aujourd'hui »

**Maquette :** preview/E5-Desktop.html — zones E5-bandeau, E5-statut-direct, E5-dernier-numero, E5-bouton-suivre, E5-fermer
**Maquette :** preview/E5-Mobile.html — zones E5-bandeau, E5-bouton-suivre, E5-fermer
**Maquette :** preview/E5-Etats.html — zones E5-variante-avant, E5-lien-evenement, E5-variante-termine, E5-variante-autre

RG-16 à RG-20.
- **Rendu serveur.** `TodayEventService` lit l'événement au rendu serveur, borné à 3 s, et
  le transmet par `TransferState` : pas de saut de mise en page.
- **Navigateur.** Il relit l'état tout de suite (une page pré-rendue est périmée), puis
  chaque minute, hors zone Angular, tant que l'onglet est visible.
- **Emplacement.** Le bandeau est inséré dans `navigation.component.html`, donc absent du
  tableau grâce au `@if` existant sur `/tableau`.
- **Fermeture.** Elle est mémorisée dans `localStorage` sous la clé
  `vpd.today-banner.dismissed`.

**Fichiers :**
- Créer : `src/Website/src/app/shared/models/today-event.model.ts`,
  `src/Website/src/app/shared/services/today-event.service.ts`,
  `src/Website/src/app/core/layouts/today-banner/today-banner-state.ts`,
  `today-banner.component.ts`, `today-banner.component.html`
- Modifier : `src/Website/src/app/shared/facades/vpd-events.facade.service.ts`,
  `src/Website/src/app/core/core.module.ts`,
  `src/Website/src/app/core/layouts/navigation/navigation.component.html`
- Test : `src/Website/src/app/core/layouts/today-banner/today-banner-state.spec.ts`

**Interfaces produites :**
- Service : `TodayEventService.today` (signal `TodayEventModel | null`), `start()`,
  `refresh()`.
- Façade : `VpdEventsFacadeService.getTodayEvent(): Promise<TodayEventModel | null>`.

- [ ] **Test d'abord** :

Créer `src/Website/src/app/core/layouts/today-banner/today-banner-state.spec.ts` :

```ts
import {
  isTodayBannerDismissed,
  parisDay,
  parisTime,
  todayBannerSchedule,
  todayBannerVariant,
} from './today-banner-state';
import {TodayEventModel} from '../../../shared/models/today-event.model';

const loto: TodayEventModel = {
  id: 'loto-1',
  name: 'Grand loto de Vole Papillon d’Amour',
  eventType: 'Bingo',
  dateStart: '2026-11-15T13:00:00Z',
  dateEnd: null,
  hourOpenDoors: '2026-11-15T12:30:00Z',
  hourCloseDoors: '2026-11-15T17:30:00Z',
  city: 'Arras',
  liveStatus: 'NotStarted',
  lastNumero: null,
};

describe('today banner state', () => {
  it('choisit la variante selon le type et l’avancement (RG-18)', () => {
    expect(todayBannerVariant(loto)).toBe('before');
    expect(todayBannerVariant({...loto, liveStatus: 'Live', lastNumero: 42})).toBe('live');
    expect(todayBannerVariant({...loto, liveStatus: 'Finished'})).toBe('finished');
    expect(todayBannerVariant({...loto, eventType: 'Books', liveStatus: null})).toBe('other');
  });

  it('reste fermé pour la journée, sauf au passage en direct (RG-19)', () => {
    const closedBefore = {eventId: 'loto-1', day: '2026-11-15', variant: 'before' as const};

    expect(isTodayBannerDismissed(loto, closedBefore, '2026-11-15')).toBeTrue();
    expect(isTodayBannerDismissed(loto, closedBefore, '2026-11-16')).toBeFalse();
    expect(isTodayBannerDismissed({...loto, liveStatus: 'Live'}, closedBefore, '2026-11-15')).toBeFalse();
    expect(isTodayBannerDismissed(
      {...loto, liveStatus: 'Live'},
      {...closedBefore, variant: 'live'},
      '2026-11-15')).toBeTrue();
    expect(isTodayBannerDismissed(loto, null, '2026-11-15')).toBeFalse();
  });

  it('compte les jours et les heures à Paris (RG-16)', () => {
    expect(parisDay(new Date('2026-11-14T23:30:00Z'))).toBe('2026-11-15');
    expect(parisTime('2026-11-15T12:30:00Z')).toBe('13 h 30');
  });

  it('annonce l’ouverture des portes avant le loto, les horaires sinon', () => {
    expect(todayBannerSchedule(loto)).toBe('Ouverture des portes 13 h 30 · Arras');
    expect(todayBannerSchedule({...loto, eventType: 'Books', liveStatus: null}))
      .toBe('13 h 30 – 18 h 30 · Arras');
  });
});
```

- [ ] Implémentation :

Créer `src/Website/src/app/shared/models/today-event.model.ts` :

```ts
/** Réponse de `GET /asso-events/today` : l'événement du jour, loto en priorité. */
export interface TodayEventModel {
  id: string;
  name: string;
  eventType: 'Bingo' | 'Books' | 'Other' | 'Unknown';
  dateStart: string;
  dateEnd: string | null;
  hourOpenDoors: string | null;
  hourCloseDoors: string | null;
  city: string;
  /** Pour un loto seulement. */
  liveStatus: 'NotStarted' | 'Live' | 'Finished' | null;
  lastNumero: number | null;
}
```

Modifier `src/Website/src/app/shared/facades/vpd-events.facade.service.ts` :

```diff
@@ -2,6 +2,7 @@ import {Injectable} from '@angular/core';
 import {AxiosService} from "../services/axios.service";
 import {MethodEnum} from "../enums/method.enum";
 import {VpdEventModel} from "../models/vpdEvent.model";
+import {TodayEventModel} from "../models/today-event.model";
 
 @Injectable({
   providedIn: 'root'
@@ -30,6 +31,12 @@ export class VpdEventsFacadeService {
     return this.axiosService.request(MethodEnum.GET, `/asso-events/${id}`, null);
   }
 
+  /** L'événement du jour (loto en priorité), ou `null` (204) s'il n'y en a pas. */
+  public async getTodayEvent(): Promise<TodayEventModel | null> {
+    const today: TodayEventModel | '' | null = await this.axiosService.request(MethodEnum.GET, `/asso-events/today`, null);
+    return today ? today : null;
+  }
+
   public getAllEvents$(): Promise<VpdEventModel[]> {
     return this.axiosService.request(MethodEnum.GET, `/asso-events`, null);
   }
```

Créer `src/Website/src/app/shared/services/today-event.service.ts` :

```ts
import {isPlatformServer} from '@angular/common';
import {inject, Injectable, makeStateKey, NgZone, PendingTasks, PLATFORM_ID, signal, TransferState} from '@angular/core';
import {VpdEventsFacadeService} from '../facades/vpd-events.facade.service';
import {TodayEventModel} from '../models/today-event.model';

const TODAY_EVENT_KEY = makeStateKey<TodayEventModel | null>('vpd-today-event');
const REFRESH_MS = 60_000;
const SERVER_TIMEOUT_MS = 3000;

/**
 * L'événement du jour pour le bandeau (E5) et la fiche (E6). Lu au rendu serveur et
 * transmis au navigateur (pas de saut de mise en page, RG-20), puis rafraîchi chaque
 * minute tant que l'onglet est visible pour passer à « En direct ».
 */
@Injectable({
  providedIn: 'root'
})
export class TodayEventService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly transferState = inject(TransferState);
  private readonly pendingTasks = inject(PendingTasks);
  private readonly zone = inject(NgZone);
  private readonly events = inject(VpdEventsFacadeService);
  private started = false;

  readonly today = signal<TodayEventModel | null>(null);

  async start(): Promise<void> {
    if (this.started) {
      return;
    }
    this.started = true;

    if (isPlatformServer(this.platformId)) {
      // Rendu serveur ou pré-rendu au build : l'API ne doit jamais bloquer la page.
      const done = this.pendingTasks.add();
      try {
        await Promise.race([this.refresh(), new Promise(resolve => setTimeout(resolve, SERVER_TIMEOUT_MS))]);
      } finally {
        done();
      }
      this.transferState.set(TODAY_EVENT_KEY, this.today());
      return;
    }

    if (this.transferState.hasKey(TODAY_EVENT_KEY)) {
      this.today.set(this.transferState.get(TODAY_EVENT_KEY, null));
      this.transferState.remove(TODAY_EVENT_KEY);
    }
    // Une page pré-rendue au build porte un état périmé : on le relit tout de suite.
    void this.refresh();

    // Hors zone : un intervalle permanent empêcherait l'application d'être « stable ».
    this.zone.runOutsideAngular(() => setInterval(() => {
      if (document.visibilityState === 'visible') {
        void this.refresh();
      }
    }, REFRESH_MS));
  }

  async refresh(): Promise<void> {
    try {
      this.today.set(await this.events.getTodayEvent());
    } catch {
      // API injoignable : on garde le dernier état connu.
    }
  }
}
```

Créer `src/Website/src/app/core/layouts/today-banner/today-banner-state.ts` :

```ts
import {TodayEventModel} from '../../../shared/models/today-event.model';

export type TodayBannerVariant = 'before' | 'live' | 'finished' | 'other';

/** Fermeture du bandeau mémorisée sur l'appareil (RG-19). */
export interface TodayBannerDismissal {
  eventId: string;
  day: string;
  variant: TodayBannerVariant;
}

export const TODAY_BANNER_STORAGE_KEY = 'vpd.today-banner.dismissed';

export function todayBannerVariant(today: TodayEventModel): TodayBannerVariant {
  if (today.eventType !== 'Bingo') {
    return 'other';
  }
  switch (today.liveStatus) {
    case 'Live':
      return 'live';
    case 'Finished':
      return 'finished';
    default:
      return 'before';
  }
}

/**
 * RG-19 : fermé pour l'événement et la journée ; le passage à « En direct » le fait
 * réapparaître une fois.
 */
export function isTodayBannerDismissed(
  today: TodayEventModel,
  dismissal: TodayBannerDismissal | null,
  day: string,
): boolean {
  if (dismissal === null || dismissal.eventId !== today.id || dismissal.day !== day) {
    return false;
  }
  return !(todayBannerVariant(today) === 'live' && dismissal.variant !== 'live');
}

/** Jour calendaire à Paris, au format `AAAA-MM-JJ`. */
export function parisDay(now: Date): string {
  return new Intl.DateTimeFormat('fr-CA', {
    timeZone: 'Europe/Paris',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(now);
}

/** « 13 h 30 » à l'heure de Paris. */
export function parisTime(iso: string): string {
  const parts = new Intl.DateTimeFormat('fr-FR', {
    timeZone: 'Europe/Paris',
    hour: '2-digit',
    minute: '2-digit',
  }).formatToParts(new Date(iso));
  const hour = parts.find(part => part.type === 'hour')?.value ?? '';
  const minute = parts.find(part => part.type === 'minute')?.value ?? '';
  return `${hour} h ${minute}`;
}

/** Ligne secondaire du bandeau : horaires et ville. */
export function todayBannerSchedule(today: TodayEventModel): string {
  const parts: string[] = [];
  if (todayBannerVariant(today) === 'before' && today.hourOpenDoors) {
    parts.push(`Ouverture des portes ${parisTime(today.hourOpenDoors)}`);
  } else if (today.hourOpenDoors && today.hourCloseDoors) {
    parts.push(`${parisTime(today.hourOpenDoors)} – ${parisTime(today.hourCloseDoors)}`);
  } else if (today.hourOpenDoors) {
    parts.push(parisTime(today.hourOpenDoors));
  }
  if (today.city) {
    parts.push(today.city);
  }
  return parts.join(' · ');
}
```

Créer `src/Website/src/app/core/layouts/today-banner/today-banner.component.ts` :

```ts
import {isPlatformBrowser} from '@angular/common';
import {Component, computed, inject, OnInit, PLATFORM_ID, signal} from '@angular/core';
import {TodayEventService} from '../../../shared/services/today-event.service';
import {
  isTodayBannerDismissed,
  parisDay,
  TODAY_BANNER_STORAGE_KEY,
  TodayBannerDismissal,
  todayBannerSchedule,
  todayBannerVariant,
} from './today-banner-state';

/** E5 · Bandeau « Aujourd'hui », au-dessus de l'en-tête, sur toutes les pages sauf le tableau. */
@Component({
  selector: 'app-today-banner',
  templateUrl: './today-banner.component.html',
  standalone: false
})
export class TodayBannerComponent implements OnInit {
  private readonly todayEvent = inject(TodayEventService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly dismissal = signal<TodayBannerDismissal | null>(null);

  protected readonly today = this.todayEvent.today;
  protected readonly variant = computed(() => {
    const today = this.today();
    return today === null ? null : todayBannerVariant(today);
  });
  protected readonly schedule = computed(() => {
    const today = this.today();
    return today === null ? '' : todayBannerSchedule(today);
  });
  protected readonly visible = computed(() => {
    const today = this.today();
    return today !== null && !isTodayBannerDismissed(today, this.dismissal(), parisDay(new Date()));
  });

  ngOnInit(): void {
    this.dismissal.set(this.readDismissal());
    void this.todayEvent.start();
  }

  protected close(): void {
    const today = this.today();
    const variant = this.variant();
    if (today === null || variant === null) {
      return;
    }
    const dismissal: TodayBannerDismissal = {eventId: today.id, day: parisDay(new Date()), variant};
    this.dismissal.set(dismissal);
    try {
      localStorage.setItem(TODAY_BANNER_STORAGE_KEY, JSON.stringify(dismissal));
    } catch {
      // Stockage indisponible : le bandeau reste fermé jusqu'au rechargement.
    }
  }

  private readDismissal(): TodayBannerDismissal | null {
    if (!isPlatformBrowser(this.platformId)) {
      return null;
    }
    try {
      const stored = localStorage.getItem(TODAY_BANNER_STORAGE_KEY);
      return stored === null ? null : JSON.parse(stored) as TodayBannerDismissal;
    } catch {
      return null;
    }
  }
}
```

Créer `src/Website/src/app/core/layouts/today-banner/today-banner.component.html` :

```html
@if (visible() && today(); as today) {
  <!-- E5 : maquettes preview/E5-Desktop.html, preview/E5-Mobile.html, preview/E5-Etats.html -->
  <div role="region" aria-label="Événement du jour"
       class="flex flex-col gap-2 bg-ink py-2.5 pl-4 pr-2 text-white lg:h-[52px] lg:flex-row lg:items-center lg:gap-5 lg:py-0 lg:pl-shell lg:pr-3">
    <div class="flex min-w-0 items-center gap-2.5 lg:gap-5">
      @if (variant() === 'live') {
        <span class="inline-flex shrink-0 items-center gap-2 font-mono text-[10px] font-semibold uppercase tracking-[.16em] text-orange-2 lg:text-[11px] lg:tracking-[.18em]">
          <span aria-hidden="true" class="h-2 w-2 rounded-full bg-orange shadow-[0_0_0_4px_rgba(240,128,28,.25)] motion-safe:animate-pulse"></span>
          En direct
        </span>
      } @else {
        <span class="shrink-0 font-mono text-[10px] font-semibold uppercase tracking-[.16em] text-cyan lg:text-[11px] lg:tracking-[.18em]">Aujourd'hui</span>
      }

      <span class="min-w-0 truncate text-sm font-semibold lg:text-[15px]">
        {{ variant() === 'finished' ? 'Le loto est terminé, merci à tous !' : today.name }}
      </span>

      <button type="button" (click)="close()" aria-label="Masquer le bandeau"
              class="ml-auto inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-mist lg:hidden">
        <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M4 4l8 8M12 4l-8 8"></path></svg>
      </button>
    </div>

    <div class="flex items-center gap-2.5 pr-2 lg:flex-1 lg:gap-5 lg:pr-0">
      @switch (variant()) {
        @case ('live') {
          @if (today.lastNumero !== null) {
            <span class="inline-flex items-center gap-2 text-[13px] text-mist-2 lg:text-sm">
              Dernier numéro
              <span class="inline-flex h-[30px] min-w-[34px] items-center justify-center rounded-lg bg-orange px-1.5 text-lg font-extrabold tabular-nums text-ink">{{ today.lastNumero }}</span>
            </span>
          }
          <a [routerLink]="['/evenement', today.id, 'tableau']"
             class="ml-auto inline-flex h-11 items-center gap-2 rounded-full bg-white px-4 text-sm font-bold text-ink transition hover:bg-mist-2 lg:h-9">
            Suivre le tableau
            <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 8h10M9 4l4 4-4 4"></path></svg>
          </a>
        }
        @case ('finished') {
          <a [routerLink]="['/evenement', today.id, 'tableau']"
             class="text-sm font-bold text-white underline underline-offset-[3px] hover:text-cyan lg:ml-auto">Voir le tableau final</a>
        }
        @default {
          @if (schedule()) {
            <span class="text-[13px] text-mist-2 lg:text-sm">{{ schedule() }}</span>
          }
          <a [routerLink]="['/evenement', today.id]"
             class="ml-auto shrink-0 text-sm font-bold text-white underline underline-offset-[3px] hover:text-cyan">Voir l'événement</a>
        }
      }

      <button type="button" (click)="close()" aria-label="Masquer le bandeau"
              class="hidden h-11 w-11 shrink-0 items-center justify-center rounded-full text-mist hover:text-white lg:inline-flex">
        <svg viewBox="0 0 16 16" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M4 4l8 8M12 4l-8 8"></path></svg>
      </button>
    </div>
  </div>
}
```

Modifier `src/Website/src/app/core/core.module.ts` :

```diff
@@ -6,6 +6,7 @@ import {RouterLink, RouterLinkActive} from "@angular/router";
 import { NavigationMobileComponent } from './layouts/navigation/navigation-mobile/navigation-mobile.component';
 import { CarouselComponent } from './layouts/footer/components/carousel/carousel.component';
 import {SharedModule} from "../shared/shared.module";
+import { TodayBannerComponent } from './layouts/today-banner/today-banner.component';
 
 
 
@@ -15,6 +16,7 @@ import {SharedModule} from "../shared/shared.module";
     NavigationComponent,
     NavigationMobileComponent,
     CarouselComponent,
+    TodayBannerComponent,
   ],
   exports: [
     FooterComponent,
```

Modifier `src/Website/src/app/core/layouts/navigation/navigation.component.html` :

```diff
@@ -1,4 +1,5 @@
 @if (!url().includes('/tableau')) {
+  <app-today-banner></app-today-banner>
   <header class="bg-paper font-sans text-ink-2">
     <div class="hidden items-center justify-between border-b border-line px-shell py-5 lg:flex">
       <a routerLink="/accueil" class="flex items-center gap-3" aria-label="Retour à l'accueil">
```

- [ ] Specs vertes. `npm run build` : « Prerendered 25 static routes », sans erreur, même
  API injoignable.
- [ ] Commit : `feat(website): announce today's event in a site-wide banner`

## Tâche 11 — Website : encart de la fiche loto et adresse courte `/loto`

**Maquette :** preview/E6-Desktop.html — zones E6-encart-direct, E6-bouton-suivre
**Maquette :** preview/E6-Mobile.html — zones E6-encart-direct, E6-bouton-suivre

RG-21, RG-24.
- **Encart.** Il apparaît sur la fiche quand l'événement affiché est le loto du jour. Son
  texte suit `liveStatus` : avant le jeu, en direct, terminé.
- **Adresse courte.** `/loto` est une route sans composant, en rendu serveur. Son garde
  redirige vers le tableau du loto du jour, sinon vers la fiche du prochain loto, sinon vers
  `/evenement`.

**Fichiers :**
- Modifier : `src/Website/src/app/feature/event-detail/event-detail.component.ts`,
  `event-detail.component.html`
- Créer : `src/Website/src/app/feature/tableau/loto-redirect.guard.ts`
- Modifier : `src/Website/src/app/app-routing.module.ts`, `src/Website/src/app/app.routes.server.ts`
- Test : `src/Website/src/app/feature/tableau/loto-redirect.guard.spec.ts`

- [ ] **Test d'abord** :

Créer `src/Website/src/app/feature/tableau/loto-redirect.guard.spec.ts` :

```ts
import {TestBed} from '@angular/core/testing';
import {ActivatedRouteSnapshot, provideRouter, RouterStateSnapshot, UrlTree} from '@angular/router';
import {lotoRedirectGuard} from './loto-redirect.guard';
import {VpdEventsFacadeService} from '../../shared/facades/vpd-events.facade.service';
import {TodayEventModel} from '../../shared/models/today-event.model';
import {VpdEventModel} from '../../shared/models/vpdEvent.model';

describe('lotoRedirectGuard', () => {
  let events: jasmine.SpyObj<VpdEventsFacadeService>;

  beforeEach(() => {
    events = jasmine.createSpyObj<VpdEventsFacadeService>('VpdEventsFacadeService', ['getTodayEvent', 'getLatestEventBingo']);
    TestBed.configureTestingModule({
      providers: [provideRouter([]), {provide: VpdEventsFacadeService, useValue: events}]
    });
  });

  it('ouvre le tableau du loto du jour', async () => {
    events.getTodayEvent.and.resolveTo({id: 'loto-1', eventType: 'Bingo'} as TodayEventModel);

    expect(await redirect()).toBe('/evenement/loto-1/tableau');
  });

  it('mène à la fiche du prochain loto les autres jours', async () => {
    events.getTodayEvent.and.resolveTo({id: 'bourse', eventType: 'Books'} as TodayEventModel);
    events.getLatestEventBingo.and.resolveTo({id: 'loto-2'} as VpdEventModel);

    expect(await redirect()).toBe('/evenement/loto-2');
  });

  it('mène aux événements quand aucun loto n’est prévu', async () => {
    events.getTodayEvent.and.resolveTo(null);
    events.getLatestEventBingo.and.rejectWith({response: {status: 404}});

    expect(await redirect()).toBe('/evenement');
  });

  async function redirect(): Promise<string> {
    const result = await TestBed.runInInjectionContext(() =>
      lotoRedirectGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)) as UrlTree;
    return result.toString();
  }
});
```

- [ ] Implémentation :

Créer `src/Website/src/app/feature/tableau/loto-redirect.guard.ts` :

```ts
import {inject} from '@angular/core';
import {CanActivateFn, Router, UrlTree} from '@angular/router';
import {VpdEventsFacadeService} from '../../shared/facades/vpd-events.facade.service';

/**
 * RG-24 : `/loto`, l'adresse courte affichée sous le QR code. Le jour du loto elle
 * ouvre son tableau en direct ; sinon la fiche du prochain loto, ou les événements.
 */
export const lotoRedirectGuard: CanActivateFn = async (): Promise<UrlTree> => {
  const router = inject(Router);
  const events = inject(VpdEventsFacadeService);

  try {
    const today = await events.getTodayEvent();
    if (today?.eventType === 'Bingo') {
      return router.createUrlTree(['/evenement', today.id, 'tableau']);
    }
    const nextLoto = await events.getLatestEventBingo();
    return router.createUrlTree(['/evenement', nextLoto.id]);
  } catch {
    return router.createUrlTree(['/evenement']);
  }
};
```

Modifier `src/Website/src/app/app-routing.module.ts` :

```diff
@@ -30,6 +30,7 @@ import {HospitalCareComponent} from "./feature/maxence/daily-life/hospital-care/
 import {SchoolComponent} from "./feature/maxence/daily-life/school/school.component";
 import {OrgansTransplantComponent} from "./feature/maxence/daily-life/organs-transplant/organs-transplant.component";
 import {TableauComponent} from "./feature/tableau/tableau.component";
+import {lotoRedirectGuard} from "./feature/tableau/loto-redirect.guard";
 import {VpdAllEventsComponent} from "./feature/vpd-all-events/vpd-all-events.component";
 import {LegalPageComponent} from "./feature/legal/legal-page.component";
 import {LEGAL_PAGE_PATHS} from "./feature/legal/legal-page-paths";
@@ -112,6 +113,11 @@ const routes: Routes = [
     path: 'evenement/:id/tableau',
     component: TableauComponent
   },
+  {
+    path: 'loto',
+    canActivate: [lotoRedirectGuard],
+    children: []
+  },
   {
     path: 'maxence',
     children: [
```

Modifier `src/Website/src/app/app.routes.server.ts` :

```diff
@@ -131,6 +131,10 @@ export const serverRoutes: ServerRoute[] = [
     path: 'evenement/:id/tableau',
     renderMode: RenderMode.Client,
   },
+  {
+    path: 'loto',
+    renderMode: RenderMode.Server,
+  },
   {
     path: 'evenement/:id',
     renderMode: RenderMode.Server,
```

Modifier `src/Website/src/app/feature/event-detail/event-detail.component.ts` :

```diff
@@ -2,6 +2,7 @@ import {Component, computed, inject, OnInit, signal} from '@angular/core';
 import {DomSanitizer, SafeResourceUrl} from '@angular/platform-browser';
 import {ActivatedRoute} from "@angular/router";
 import {VpdEventsFacadeService} from "../../shared/facades/vpd-events.facade.service";
+import {TodayEventService} from "../../shared/services/today-event.service";
 import {VpdEventModel} from "../../shared/models/vpdEvent.model";
 import {VpdEventEnum} from "../../shared/enums/vpdEvent.enum";
 import {eventMapsEmbedUrl, eventMapsUrl, formatEventAddress} from "../../shared/utils/event-address.util";
@@ -13,6 +14,16 @@ import {eventMapsEmbedUrl, eventMapsUrl, formatEventAddress} from "../../shared/
 })
 export class EventDetailComponent implements OnInit {
   private readonly sanitizer = inject(DomSanitizer);
+  private readonly todayEvent = inject(TodayEventService);
+
+  /** E6 : le jour du loto, l'encart « Tableau en direct » en tête de fiche. */
+  liveLoto = computed(() => {
+    const today = this.todayEvent.today();
+    const event = this.vpdEvent();
+    return today !== null && event !== null && today.id === event.id && today.eventType === 'Bingo'
+      ? today
+      : null;
+  });
 
   vpdEvent = signal<VpdEventModel | null>(null)
   isLoading = signal(true);
@@ -77,6 +88,7 @@ export class EventDetailComponent implements OnInit {
   }
 
   ngOnInit(): void {
+    void this.todayEvent.start();
     this.getEvents()
   }
```

Modifier `src/Website/src/app/feature/event-detail/event-detail.component.html` :

```diff
@@ -10,6 +10,33 @@
         ← Tous les évènements
       </a>
 
+      @if (liveLoto(); as loto) {
+        <!-- E6 : maquettes preview/E6-Desktop.html, preview/E6-Mobile.html -->
+        <div class="flex max-w-[650px] flex-col gap-3 rounded-xl border border-orange-2/45 bg-[#0b2438] p-4 sm:flex-row sm:items-center sm:gap-5 sm:px-5 sm:py-[18px]">
+          <div class="flex min-w-0 flex-1 flex-col gap-1.5">
+            <span class="inline-flex items-center gap-2 font-mono text-[10px] font-semibold uppercase tracking-[.18em] text-orange-2 sm:text-[11px]">
+              <span aria-hidden="true" class="h-2 w-2 rounded-full bg-orange shadow-[0_0_0_4px_rgba(240,128,28,.25)]"></span>Tableau en direct
+            </span>
+            <span class="text-base font-semibold text-white sm:text-[17px]">
+              @switch (loto.liveStatus) {
+                @case ('Live') {
+                  Le loto a commencé
+                  @if (loto.lastNumero !== null) {
+                    · dernier numéro <strong class="ml-1 inline-flex h-7 min-w-[32px] items-center justify-center rounded-[7px] bg-orange px-1 text-ink">{{ loto.lastNumero }}</strong>
+                  }
+                }
+                @case ('Finished') { Le loto est terminé, merci à tous ! }
+                @default { Le tableau s'animera au premier numéro tiré. }
+              }
+            </span>
+          </div>
+          <a [routerLink]="['/evenement', loto.id, 'tableau']"
+             class="inline-flex h-12 items-center justify-center whitespace-nowrap rounded-full bg-white px-5 text-[15px] font-bold text-ink transition hover:bg-mist-2 sm:h-11">
+            {{ loto.liveStatus === 'Finished' ? 'Voir le tableau final' : 'Suivre le tableau' }} →
+          </a>
+        </div>
+      }
+
       <div class="flex flex-wrap items-center gap-3">
         <span class="inline-flex items-center gap-2 font-mono text-[11px] font-semibold uppercase tracking-[.18em] text-orange-2">
           <span aria-hidden="true" class="h-1.5 w-1.5 rounded-full bg-orange-2"></span>
```

- [ ] `npm test -- --watch=false --browsers=ChromeHeadless` : 105 specs vertes à la
  conception. `npm run build` OK.
- [ ] Commit : `feat(website): link today's loto from its page and from /loto`

## Tâche 12 — Documentation, validation complète, recette, PR

- [ ] **Mémoire du repo.** Mettre à jour :
  - `.github/memory/10-api-endpoints.md` : `GET /asso-events/today`, et le nouveau
    comportement de `/asso-events/{id}/tableau/sse` (`id` = révision, `retry`, `ping`, 404,
    synchronisation entre réplicas) ;
  - `.github/memory/03-domain-model.md` : section « Live Loto Tableau Flow »
    (`LiveRevision`, jeton de concurrence, `ILiveTableauHub`, `LiveTableauSyncService`), en
    remplacement de la mention d'`ISSEClientManager.SendToEvent` ;
  - `.github/memory/changelog.md`.
- [ ] **Validation.**
  - `dotnet build src/Backend/Vole_Papillon_Damour.slnx` et
    `dotnet test src/Backend/Vole_Papillon_Damour.slnx` : 927 verts.
  - BackOffice et Website : `npm test -- --watch=false --browsers=ChromeHeadless` et
    `npm run build`.
  - `graphify update .`
- [ ] **Recette visuelle.** À 1920 × 1080 pour la projection, à 390 et 1440 px pour le site,
  page par page contre `maquettes/preview/`. API locale (Aspire, persona `Administration`
  pour le BackOffice).
  - **Projection :**
    - palette A puis touche **P** → palette B, puis rechargement : la palette B est gardée ;
    - « 4 », « 2 », Entrée → annonce plein écran, puis le 42 orange dans la grille ;
    - Retour arrière annule le 42 ;
    - la barre et le curseur disparaissent après 3 s ;
    - une partie « pause après » terminée ouvre la Pause avec le QR code ;
    - après la dernière partie, l'écran « Loto terminé » s'affiche.
  - **Direct :**
    - deux onglets du site sur le même tableau reçoivent chaque numéro ;
    - avec le réseau coupé 30 s pendant trois tirages, les trois numéros apparaissent au
      retour du réseau.
  - **Bandeau :** visible le jour J, absent sur `/evenement/{id}/tableau`, fermeture
    mémorisée pour la journée.
  - **Adresse courte :** `/loto` redirige vers le tableau du loto du jour.
- [ ] **PR** vers `main`, titre `feat(loto): reliable live board, stage projection and today banner`.

## Zones non implémentées

Aucune : chaque zone des maquettes est couverte par une tâche ci-dessus.
