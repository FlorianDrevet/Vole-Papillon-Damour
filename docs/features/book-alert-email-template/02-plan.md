# Template HTML des e-mails d'alerte livres — plan d'implémentation

> Exécutant : Codex, skill implement-handoff. Spec : 01-fonctionnel.md. Maquettes : maquettes/preview/.

**But :** remplacer le HTML minimal des alertes de liste de recherche par le gabarit
maquetté (encart bourse avec date, horaires et adresse, cartes livres avec couverture,
non-réservation, bouton, pied de message), avec une version texte équivalente.

**Architecture :** les données manquantes sont figées à la mise en file (couverture,
chemin de la fiche publique, bourse d'ouverture) ou relues à l'envoi (horaires, adresse,
prénom). Le rendu est découpé en unités pures et testables : `BookAlertEmailFormat`
(libellés), `BookAlertEmailLayout` (groupes, accroche, aperçu), `BookAlertEmailText` et
`BookAlertEmailHtml` (rendus), assemblées par `BookAlertEmailContentBuilder`.

**Stack :** .NET 10, xUnit + FluentAssertions, EF Core (SQLite en mémoire pour les tests
d'outbox), Azure Communication Services (inchangé).

**Spec :** [`01-fonctionnel.md`](01-fonctionnel.md) — règles `RG-90` à `RG-99`.

Le code de ce plan a été compilé et testé sur une copie de la branche : la suite backend
complète passe (885 tests), et les rendus E2 (600 px) et E3 (390 px) ont été comparés
visuellement aux maquettes. Reprendre le code tel quel ; les diffs sont relatifs à
`origin/main` au moment de la conception.

## Contraintes globales

- Largeur du message : 600 px ; une colonne ; tableaux `role="presentation"` et styles en
  ligne ; aucune image de fond, aucun dégradé, aucun script, aucun formulaire.
- Palette (V2-CONVENTION) en valeurs littérales : papier `#ffffff` / `#f7fbfe` /
  `#e9f4fb`, encre `#041d30` / `#072b45`, bleus `#0c6ea6` / `#1497d6`, cyan `#7fd8f5`,
  orange `#f0801c`, lignes `#d9e9f4` / `#e2eef7`, fond client `#e6edf2`.
- Polices avec repli : `Newsreader,Georgia,'Times New Roman',serif` ·
  `'Libre Franklin',Arial,Helvetica,sans-serif` · `'IBM Plex Mono','Courier New',monospace`.
- Libellés mot pour mot : « Prochaine bourse », « Bourse suivante », « Disponible
  maintenant », « Disponible à la bourse du … », « Disponible dès maintenant »,
  « Prochaine ouverture : … », « Horaires », « Adresse », « Voir la fiche », « Livres rares
  suivis », « Ce livre n'est pas mis de côté. » / « Ces livres ne sont pas mis de côté. »,
  « Premier arrivé, premier servi. », « Voir ma liste de recherche », « Se désabonner »,
  « Vous recevez cet e-mail parce que ce livre figure / ces livres figurent dans votre
  liste de recherche. »
- Dates et heures à l'heure de Paris, en français : « samedi 7 novembre 2026 »,
  « 9 h 00 – 17 h 00 ».
- Objet des e-mails, règles d'envoi, en-têtes de désabonnement en un clic : inchangés.
- Seules les images en HTTPS sont affichées.
- Aucune modification d'infrastructure : `CatalogBaseUrl` et `WebsiteUrl` ont des valeurs
  par défaut de production dans `BookAlertEmailOptions`.

## Points de vigilance (revue)

1. **Charges utiles déjà en file** au déploiement, sans les nouveaux champs : elles
   doivent toujours se désérialiser et partir, sans couverture ni lien de fiche
   (assertion ajoutée en tâche 2 à `ClaimDue_ReturnsOnlyDuePendingAlertsAndLeasesThem`).
2. **Bourse d'ouverture annulée entre la mise en file et l'envoi** d'une alerte
   « disponible maintenant » : ne jamais afficher la date périmée (test
   `Layout_IgnoresTheQueuedOpeningOfAFairThatIsNoLongerActive`, tâche 4).
3. **Code postal commençant par 0** (stocké en entier) : « 01000 », pas « 1000 » (test
   `GetPendingDelivery_LoadsScheduleAndAddressOfTheOpeningFair`, tâche 3).
4. **Couverture non HTTPS** : bloc papillon, jamais d'`<img>` HTTP (test
   `Html_ShowsAnHttpsCoverAndFallsBackToTheButterflyBlock`, tâche 5).
5. **Texte du catalogue contenant du HTML ou `&`** : toujours encodé (test
   `Html_EncodesCatalogText`, tâche 5).

---

## Tâche 0 — État initial

- [ ] `dotnet build src/Backend/Vole_Papillon_Damour.slnx` puis
  `dotnet test src/Backend/Vole_Papillon_Damour.slnx --no-build`.
  Attendu : 0 erreur ; à la conception, 870 tests verts (Domain 171, Worker 4,
  Application 454, Api 64, Infrastructure 177). Noter tout échec préexistant sans le
  corriger.

## Tâche 1 — Chemin public d'une fiche livre partagé

Le lien « Voir la fiche » doit pointer vers la même URL que le sitemap et le front
(`/livres/{slug}-{isbn13}`). Le calcul, aujourd'hui privé au sitemap, devient un helper.

**Fichiers :**
- Créer : `src/Backend/Vole_Papillon_Damour.Application/Books/Common/PublicBookPath.cs`
- Modifier : `src/Backend/Vole_Papillon_Damour.Application/Books/Queries/GetPublicCatalogSitemap/GetPublicCatalogSitemapQueryHandler.cs`
- Test : `src/Backend/Vole_Papillon_Damour.Application.tests/Books/Common/PublicBookPathTests.cs`

**Interfaces produites :** `PublicBookPath.For(Isbn13 isbn13, string? title, string? authors) : string`.

- [ ] **Test d'abord** — créer `PublicBookPathTests.cs` :

```csharp
using FluentAssertions;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Common;

public sealed class PublicBookPathTests
{
    [Fact]
    public void For_BuildsTheCatalogSlugFromTitleAuthorsAndIsbn()
    {
        var path = PublicBookPath.For(
            ParseIsbn("9782070408504"),
            "Le Petit Prince",
            "Antoine de Saint-Exupéry");

        path.Should().Be("/livres/le-petit-prince-antoine-de-saint-exupery-9782070408504");
    }

    [Fact]
    public void For_WithoutTitleOrAuthorsFallsBackToLivre()
    {
        var path = PublicBookPath.For(ParseIsbn("9782070408504"), null, "  ");

        path.Should().Be("/livres/livre-9782070408504");
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
```

- [ ] `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter FullyQualifiedName~PublicBookPath` → échec de compilation (`PublicBookPath` inconnu).
- [ ] Créer `PublicBookPath.cs` :

```csharp
using System.Globalization;
using System.Text;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

/// <summary>
/// Chemin public d'une fiche livre du catalogue (« /livres/{slug}-{isbn13} »), identique
/// à celui que construit le front (`src/Catalog/src/app/shared/catalog-url.ts`).
/// </summary>
public static class PublicBookPath
{
    public static string For(Isbn13 isbn13, string? title, string? authors)
    {
        return $"/livres/{Slugify(title, authors)}-{isbn13.Value}";
    }

    private static string Slugify(string? title, string? authors)
    {
        var source = string.Join(
            ' ',
            new[] {title, authors}.Where(value => !string.IsNullOrWhiteSpace(value)));
        var decomposed = source.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSeparator = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.Length == 0 ? "livre" : builder.ToString();
    }
}
```

- [ ] Faire utiliser le helper par le sitemap et supprimer son `Slugify` privé :

```diff
--- a/src/Backend/Vole_Papillon_Damour.Application/Books/Queries/GetPublicCatalogSitemap/GetPublicCatalogSitemapQueryHandler.cs
+++ b/src/Backend/Vole_Papillon_Damour.Application/Books/Queries/GetPublicCatalogSitemap/GetPublicCatalogSitemapQueryHandler.cs
@@ -1,5 +1,3 @@
-using System.Globalization;
-using System.Text;
 using ErrorOr;
 using MediatR;
 using Microsoft.EntityFrameworkCore;
@@ -41,46 +39,11 @@ public sealed class GetPublicCatalogSitemapQueryHandler(

         return new PublicCatalogSitemapResult(
             books.Select(book => new PublicCatalogSitemapEntry(
-                    $"/livres/{Slugify(book.Title, book.Authors)}-{book.Id.Value}",
+                    PublicBookPath.For(book.Id, book.Title, book.Authors),
                     new DateTimeOffset(book.UpdatedAt, TimeSpan.Zero)))
                 .Concat(rareBooks.Select(book => new PublicCatalogSitemapEntry(
                     $"/livres-rares/{book.Id.Value:D}",
                     new DateTimeOffset(book.UpdatedAt, TimeSpan.Zero))))
                 .ToArray());
     }
-
-    private static string Slugify(string? title, string? authors)
-    {
-        var source = string.Join(
-            ' ',
-            new[] {title, authors}.Where(value => !string.IsNullOrWhiteSpace(value)));
-        var decomposed = source.Normalize(NormalizationForm.FormD);
-        var builder = new StringBuilder(decomposed.Length);
-        var pendingSeparator = false;
-
-        foreach (var character in decomposed)
-        {
-            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
-            {
-                continue;
-            }
-
-            if (char.IsLetterOrDigit(character))
-            {
-                if (pendingSeparator && builder.Length > 0)
-                {
-                    builder.Append('-');
-                }
-
-                builder.Append(char.ToLowerInvariant(character));
-                pendingSeparator = false;
-            }
-            else
-            {
-                pendingSeparator = true;
-            }
-        }
-
-        return builder.Length == 0 ? "livre" : builder.ToString();
-    }
 }
```

- [ ] `dotnet test src/Backend/Vole_Papillon_Damour.Application.tests --filter "FullyQualifiedName~PublicBookPath|FullyQualifiedName~PublicCatalogQueryHandler"` → vert (le test de sitemap existant `/livres/le-petit-prince-antoine-de-saint-exupery-9782070408504` reste vert).
- [ ] Commit : `refactor(books): share the public book path builder`

## Tâche 2 — Couverture, fiche et bourse d'ouverture figées à la mise en file

À la clôture d'une session, chaque élément d'alerte enregistre la couverture du livre, le
chemin de sa fiche publique (null si le livre est masqué ou redirigé) et la bourse dont
l'ouverture est annoncée : la bourse ciblée en mode `PROCHAINE BOURSE`, la prochaine bourse
active en mode `DISPONIBLE MAINTENANT`. Les modèles reçoivent aussi `BookAlertFair`, rempli
en tâche 3.

**Fichiers :**
- Modifier : `src/Backend/Vole_Papillon_Damour.Application/Common/Models/BookAlertOutboxWorkItem.cs`
- Modifier : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertOutbox.cs`
- Test : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Books/BookAlertOutboxTests.cs`

**Interfaces produites :**
- `BookAlertOutboxItem` gagne `string? CoverUrl = null, string? PublicPath = null, Guid? OpeningFairId = null` (en fin de liste, valeurs par défaut : les appels existants compilent).
- `BookAlertDelivery` gagne `IReadOnlyList<BookAlertFair>? Fairs = null`.
- `BookAlertFair(Guid AssoEventsId, DateTimeOffset OpeningAt, DateTimeOffset? DoorsOpenAt, DateTimeOffset? DoorsCloseAt, string? Address)`.

- [ ] **Tests d'abord** — dans `BookAlertOutboxTests.cs` : deux tests, une assertion de
  compatibilité sur les charges utiles anciennes, et la fixture qui accepte auteurs,
  couverture et un raccourci `QueueClaimAndLoadDeliveryAsync` :

```diff
--- a/src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Books/BookAlertOutboxTests.cs
+++ b/src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Books/BookAlertOutboxTests.cs
@@ -341,6 +341,48 @@ public sealed class BookAlertOutboxTests
         item.FairOpeningAt.Should().Be(fair.HourOpenDoors);
     }

+    [Fact]
+    public async Task QueueForSession_CarriesCoverPublicPathAndOpeningFairIntoDelivery()
+    {
+        await using var fixture = await BookAlertFixture.CreateAsync();
+        var member = await fixture.AddMemberAsync("member@example.org");
+        var book = await fixture.AddBookAsync(
+            "9782070363735",
+            "Le Petit Prince",
+            authors: "Antoine de Saint-Exupéry",
+            coverUrl: "https://covers.example.org/9782070363735.jpg");
+        var fair = await fixture.AddFairAsync();
+        var session = await fixture.AddSessionAsync(member.Id);
+        await fixture.AddDirectEntryAsync(session, book, member.Id);
+        await fixture.AddWatchlistAsync(member.Id, book);
+
+        var delivery = await fixture.QueueClaimAndLoadDeliveryAsync(session);
+
+        var item = delivery.Items.Should().ContainSingle().Subject;
+        item.CoverUrl.Should().Be("https://covers.example.org/9782070363735.jpg");
+        item.PublicPath.Should().Be(
+            "/livres/le-petit-prince-antoine-de-saint-exupery-9782070363735");
+        item.OpeningFairId.Should().Be(fair.Id.Value);
+        item.AssoEventsId.Should().BeNull();
+    }
+
+    [Fact]
+    public async Task QueueForSession_LeavesThePublicPathEmptyForAHiddenBook()
+    {
+        await using var fixture = await BookAlertFixture.CreateAsync();
+        var member = await fixture.AddMemberAsync("member@example.org");
+        var book = await fixture.AddBookAsync("9782070363735", "Titre");
+        book.UpdateCatalogVisibility(true, StartedAt);
+        await fixture.Context.SaveChangesAsync();
+        var session = await fixture.AddSessionAsync(member.Id);
+        await fixture.AddDirectEntryAsync(session, book, member.Id);
+        await fixture.AddWatchlistAsync(member.Id, book);
+
+        var delivery = await fixture.QueueClaimAndLoadDeliveryAsync(session);
+
+        delivery.Items.Should().ContainSingle().Which.PublicPath.Should().BeNull();
+    }
+
     [Fact]
     public async Task CancelPendingForSession_CancelsOnlyPendingAlertMessages()
     {
@@ -507,6 +549,8 @@ public sealed class BookAlertOutboxTests
         claimed.Should().HaveCount(1);
         claimed[0].MemberId.Should().Be(member.Id.Value);
         claimed[0].Items.Should().ContainSingle();
+        claimed[0].Items[0].CoverUrl.Should().BeNull();
+        claimed[0].Items[0].OpeningFairId.Should().BeNull();
         claimed[0].ClaimedUntil.Kind.Should().Be(DateTimeKind.Utc);
         var messages = await fixture.Context.OutboxMessages
             .OrderBy(message => message.DueAt)
@@ -898,10 +942,14 @@ public sealed class BookAlertOutboxTests
             string? workId = null,
             string? publisher = null,
             int? publicationYear = null,
-            string? physicalFormat = null)
+            string? physicalFormat = null,
+            string? authors = null,
+            string? coverUrl = null)
         {
             var book = Book.Create(ParseIsbn(isbn), StartedAt);
             var fields = new List<BookMetadataField> { BookMetadataField.Title };
+            if (authors is not null) fields.Add(BookMetadataField.Authors);
+            if (coverUrl is not null) fields.Add(BookMetadataField.CoverUrl);
             if (workId is not null) fields.Add(BookMetadataField.WorkId);
             if (publisher is not null) fields.Add(BookMetadataField.Publisher);
             if (publicationYear is not null) fields.Add(BookMetadataField.PublicationYear);
@@ -909,18 +957,20 @@ public sealed class BookAlertOutboxTests
             book.ApplyAutomaticMetadata(
                 new BookMetadataPatch(
                     title,
-                    null,
+                    authors,
                     publisher,
                     publicationYear,
                     physicalFormat,
                     null,
                     null,
-                    null,
+                    coverUrl,
                     fields,
                     workId),
                 BookMetadataSource.OpenLibrary,
                 StartedAt,
-                rawPayload: null);
+                rawPayload: null,
+                coverSource: coverUrl is null ? null : BookCoverSource.OpenLibrary,
+                coverCheckedAt: coverUrl is null ? null : StartedAt);
             Context.Books.Add(book);
             await Context.SaveChangesAsync();
             return book;
@@ -1025,6 +1075,27 @@ public sealed class BookAlertOutboxTests
             await Context.SaveChangesAsync();
         }

+        public async Task<BookAlertDelivery> QueueClaimAndLoadDeliveryAsync(ScanSession session)
+        {
+            var outbox = new BookAlertOutbox(Context);
+            await outbox.QueueForSessionAsync(session.Id, ClosedAt, CancellationToken.None);
+            await Context.SaveChangesAsync();
+            var message = await Context.OutboxMessages.SingleAsync();
+            message.DueAt = ClosedAt;
+            await Context.SaveChangesAsync();
+            var claimed = await outbox.ClaimDueAsync(
+                ClosedAt,
+                TimeSpan.FromMinutes(5),
+                50,
+                CancellationToken.None);
+            var delivery = await outbox.GetPendingDeliveryAsync(
+                claimed[0].MessageId,
+                claimed[0].ClaimedUntil,
+                ClosedAt,
+                CancellationToken.None);
+            return delivery ?? throw new InvalidOperationException("No delivery was produced.");
+        }
+
         public async Task AddSettingsAsync(UserId updatedBy, int alertDelayMinutes)
         {
             var settings = AssociationSettings.Create(updatedBy, StartedAt);
```

- [ ] `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter FullyQualifiedName~BookAlertOutboxTests` → échec de compilation (`CoverUrl`, `PublicPath`, `OpeningFairId` inconnus).
- [ ] Étendre les modèles :

```diff
--- a/src/Backend/Vole_Papillon_Damour.Application/Common/Models/BookAlertOutboxWorkItem.cs
+++ b/src/Backend/Vole_Papillon_Damour.Application/Common/Models/BookAlertOutboxWorkItem.cs
@@ -14,7 +14,10 @@ public sealed record BookAlertOutboxItem(
     string? Publisher = null,
     int? PublicationYear = null,
     string? PhysicalFormat = null,
-    DateTimeOffset? FairOpeningAt = null);
+    DateTimeOffset? FairOpeningAt = null,
+    string? CoverUrl = null,
+    string? PublicPath = null,
+    Guid? OpeningFairId = null);

 public sealed record RareBookAlertOutboxItem(
     Guid RareBookId,
@@ -36,4 +39,16 @@ public sealed record BookAlertDelivery(
     string Email,
     string? RecipientName,
     IReadOnlyList<BookAlertOutboxItem> Items,
-    IReadOnlyList<RareBookAlertOutboxItem>? RareItems = null);
+    IReadOnlyList<RareBookAlertOutboxItem>? RareItems = null,
+    IReadOnlyList<BookAlertFair>? Fairs = null);
+
+/// <summary>
+/// Bourse citée par une alerte, relue à l'envoi : date d'ouverture, horaires (null si non
+/// saisis) et adresse sur une ligne.
+/// </summary>
+public sealed record BookAlertFair(
+    Guid AssoEventsId,
+    DateTimeOffset OpeningAt,
+    DateTimeOffset? DoorsOpenAt,
+    DateTimeOffset? DoorsCloseAt,
+    string? Address);
```

- [ ] Figer les nouveaux champs dans `QueueForSessionAsync` et la charge utile :

```diff
--- a/src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertOutbox.cs
+++ b/src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertOutbox.cs
@@ -100,10 +100,10 @@ public sealed class BookAlertOutbox(ProjectDbContext dbContext) : IBookAlertOutb
             .ToDictionary(
                 assoEvent => assoEvent.Id.Value,
                 GetOpeningInstant);
-        var nextBookFairOpening = activeBookFairOpenings.Values
-            .Where(opening => opening > new DateTimeOffset(closedAt))
-            .OrderBy(opening => opening)
-            .Select(opening => (DateTimeOffset?)opening)
+        var nextBookFair = activeBookFairOpenings
+            .Where(pair => pair.Value > new DateTimeOffset(closedAt))
+            .OrderBy(pair => pair.Value)
+            .Select(pair => (KeyValuePair<Guid, DateTimeOffset>?)pair)
             .FirstOrDefault();

         var candidates = movements
@@ -112,6 +112,13 @@ public sealed class BookAlertOutbox(ProjectDbContext dbContext) : IBookAlertOutb
             {
                 var book = books.GetValueOrDefault(group.Key);
                 var firstMovement = group.First();
+                Guid? openingFairId =
+                    firstMovement.AssoEventsId is { } fairId &&
+                    activeBookFairOpenings.ContainsKey(fairId.Value)
+                        ? fairId.Value
+                        : session.Mode == ScanMode.AvailableNow
+                            ? nextBookFair?.Key
+                            : null;
                 return new AlertCandidate(
                     group.Key,
                     book?.WorkId,
@@ -123,12 +130,14 @@ public sealed class BookAlertOutbox(ProjectDbContext dbContext) : IBookAlertOutb
                     group.Sum(movement => movement.Quantity),
                     session.Mode,
                     firstMovement.AssoEventsId,
-                    firstMovement.AssoEventsId is { } fairId &&
-                    activeBookFairOpenings.TryGetValue(fairId.Value, out var fairOpening)
-                        ? fairOpening
-                        : session.Mode == ScanMode.AvailableNow
-                            ? nextBookFairOpening
-                            : null);
+                    openingFairId is { } openingId
+                        ? activeBookFairOpenings[openingId]
+                        : null,
+                    book?.CoverUrl,
+                    book is { IsHiddenFromCatalog: false, RedirectedToIsbn13: null }
+                        ? PublicBookPath.For(book.Id, book.Title, book.Authors)
+                        : null,
+                    openingFairId);
             })
             .ToArray();

@@ -203,7 +212,10 @@ public sealed class BookAlertOutbox(ProjectDbContext dbContext) : IBookAlertOutb
                     candidate.Quantity,
                     candidate.Mode,
                     candidate.AssoEventsId?.Value,
-                    candidate.FairOpeningAt))
+                    candidate.FairOpeningAt,
+                    candidate.CoverUrl,
+                    candidate.PublicPath,
+                    candidate.OpeningFairId))
                 .ToArray();
             dbContext.OutboxMessages.Add(new OutboxMessage
             {
@@ -1112,7 +1124,10 @@ public sealed class BookAlertOutbox(ProjectDbContext dbContext) : IBookAlertOutb
             item.Publisher,
             item.PublicationYear,
             item.PhysicalFormat,
-            item.FairOpeningAt);
+            item.FairOpeningAt,
+            item.CoverUrl,
+            item.PublicPath,
+            item.OpeningFairId);
     }

     private static RareBookAlertOutboxItem ToApplicationRareItem(RareAlertItemPayload item)
@@ -1195,7 +1210,10 @@ public sealed class BookAlertOutbox(ProjectDbContext dbContext) : IBookAlertOutb
         int Quantity,
         ScanMode Mode,
         Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects.AssoEventsId? AssoEventsId,
-        DateTimeOffset? FairOpeningAt);
+        DateTimeOffset? FairOpeningAt,
+        string? CoverUrl,
+        string? PublicPath,
+        Guid? OpeningFairId);

     private sealed record AlertEmailPayload(
         IReadOnlyList<AlertItemPayload>? Items,
@@ -1218,7 +1236,10 @@ public sealed class BookAlertOutbox(ProjectDbContext dbContext) : IBookAlertOutb
         int Quantity,
         ScanMode Mode,
         Guid? AssoEventsId,
-        DateTimeOffset? FairOpeningAt);
+        DateTimeOffset? FairOpeningAt,
+        string? CoverUrl = null,
+        string? PublicPath = null,
+        Guid? OpeningFairId = null);

     private static DateTimeOffset GetOpeningInstant(AssoEvents assoEvent)
     {
```

- [ ] `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter FullyQualifiedName~BookAlertOutboxTests` → vert (25 tests).
- [ ] Commit : `feat(book-alerts): keep cover, public path and opening fair in queued alerts`

## Tâche 3 — Bourse et prénom relus à l'envoi

`GetPendingDeliveryAsync` charge les bourses actives citées par les éléments éligibles
(horaires seulement s'ils sont saisis, adresse sur une ligne) et ne transmet plus que le
prénom du membre (« Bonjour Marie, »).

**Fichiers :**
- Modifier : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertOutbox.cs`
- Test : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Books/BookAlertOutboxTests.cs`

**Interfaces :** consomme `BookAlertFair` et `OpeningFairId` (tâche 2) ;
`BookAlertDelivery.RecipientName` contient désormais le prénom seul.

- [ ] **Tests d'abord** :

```diff
--- a/src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Books/BookAlertOutboxTests.cs
+++ b/src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Books/BookAlertOutboxTests.cs
@@ -366,6 +366,50 @@ public sealed class BookAlertOutboxTests
         item.AssoEventsId.Should().BeNull();
     }

+    [Fact]
+    public async Task GetPendingDelivery_LoadsScheduleAndAddressOfTheOpeningFair()
+    {
+        await using var fixture = await BookAlertFixture.CreateAsync();
+        var member = await fixture.AddMemberAsync("member@example.org");
+        var book = await fixture.AddBookAsync("9782070363735", "Titre");
+        var fair = await fixture.AddFairAsync(
+            DateTimeOffset.Parse("2026-09-04T07:00:00+00:00"),
+            DateTimeOffset.Parse("2026-09-04T15:00:00+00:00"),
+            new Adresse(12, "Saint-Just", "rue des Fleurs", 1000));
+        var session = await fixture.AddSessionAsync(member.Id, ScanMode.NextFair, fair.Id);
+        await fixture.AddAnnouncementEntryAsync(session, book, member.Id, fair.Id);
+        await fixture.AddWatchlistAsync(member.Id, book);
+
+        var delivery = await fixture.QueueClaimAndLoadDeliveryAsync(session);
+
+        delivery.RecipientName.Should().Be("Prénom");
+        var bookFair = delivery.Fairs.Should().ContainSingle().Subject;
+        bookFair.AssoEventsId.Should().Be(fair.Id.Value);
+        bookFair.OpeningAt.Should().Be(DateTimeOffset.Parse("2026-09-04T07:00:00+00:00"));
+        bookFair.DoorsOpenAt.Should().Be(DateTimeOffset.Parse("2026-09-04T07:00:00+00:00"));
+        bookFair.DoorsCloseAt.Should().Be(DateTimeOffset.Parse("2026-09-05T15:00:00+00:00"));
+        bookFair.Address.Should().Be("12 rue des Fleurs, 01000 Saint-Just");
+    }
+
+    [Fact]
+    public async Task GetPendingDelivery_WithoutDoorHoursLeavesTheScheduleEmpty()
+    {
+        await using var fixture = await BookAlertFixture.CreateAsync();
+        var member = await fixture.AddMemberAsync("member@example.org");
+        var book = await fixture.AddBookAsync("9782070363735", "Titre");
+        await fixture.AddFairAsync();
+        var session = await fixture.AddSessionAsync(member.Id);
+        await fixture.AddDirectEntryAsync(session, book, member.Id);
+        await fixture.AddWatchlistAsync(member.Id, book);
+
+        var delivery = await fixture.QueueClaimAndLoadDeliveryAsync(session);
+
+        var bookFair = delivery.Fairs.Should().ContainSingle().Subject;
+        bookFair.DoorsOpenAt.Should().BeNull();
+        bookFair.DoorsCloseAt.Should().BeNull();
+        bookFair.Address.Should().Be("Rue de test, 75000 Paris");
+    }
+
     [Fact]
     public async Task QueueForSession_LeavesThePublicPathEmptyForAHiddenBook()
     {
@@ -987,7 +1031,10 @@ public sealed class BookAlertOutboxTests
             return session;
         }

-        public async Task<AssoEvents> AddFairAsync(DateTimeOffset? hourOpenDoors = null)
+        public async Task<AssoEvents> AddFairAsync(
+            DateTimeOffset? hourOpenDoors = null,
+            DateTimeOffset? hourCloseDoors = null,
+            Adresse? address = null)
         {
             var fair = AssoEvents.Create(
                 "Test book fair",
@@ -996,9 +1043,9 @@ public sealed class BookAlertOutboxTests
                 DateTimeOffset.Parse("2026-09-04T18:00:00+00:00"),
                 DateTimeOffset.Parse("2026-09-05T18:00:00+00:00"),
                 hourOpenDoors,
+                hourCloseDoors,
                 null,
-                null,
-                new Adresse(null, "Paris", "Rue de test", 75000),
+                address ?? new Adresse(null, "Paris", "Rue de test", 75000),
                 null,
                 [],
                 string.Empty);
```

- [ ] `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter FullyQualifiedName~BookAlertOutboxTests` → 2 échecs (`Fairs` vide, `RecipientName` = « Prénom Nom »).
- [ ] Implémenter :

```diff
--- a/src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertOutbox.cs
+++ b/src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertOutbox.cs
@@ -1,5 +1,6 @@
 using System.Data;
 using System.Data.Common;
+using System.Globalization;
 using System.Text.Json;
 using System.Text.Json.Serialization;
 using Microsoft.EntityFrameworkCore;
@@ -8,6 +9,7 @@ using Vole_Papillon_Damour.Application.Common.Models;
 using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
 using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
 using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
+using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
 using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
 using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
 using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
@@ -586,16 +588,31 @@ public sealed class BookAlertOutbox(ProjectDbContext dbContext) : IBookAlertOutb
             return null;
         }

-        var recipientName = user.Name is null
-            ? null
-            : $"{user.Name.FirstName} {user.Name.LastName}".Trim();
+        var openingFairIds = eligibleItems
+            .Select(item => item.OpeningFairId)
+            .OfType<Guid>()
+            .Distinct()
+            .Select(AssoEventsId.Create)
+            .ToArray();
+        BookAlertFair[] fairs = openingFairIds.Length == 0
+            ? []
+            : (await dbContext.AssoEvents
+                    .AsNoTracking()
+                    .WhereActiveBookFair()
+                    .Where(fair => openingFairIds.Contains(fair.Id))
+                    .ToListAsync(cancellationToken))
+                .Select(ToBookAlertFair)
+                .ToArray();
+
+        var recipientFirstName = user.Name?.FirstName?.Trim();
         return new BookAlertDelivery(
             message.Id,
             memberId.Value,
             user.Email,
-            string.IsNullOrWhiteSpace(recipientName) ? null : recipientName,
+            string.IsNullOrWhiteSpace(recipientFirstName) ? null : recipientFirstName,
             eligibleItems,
-            eligibleRareItems);
+            eligibleRareItems,
+            fairs);
     }

     public async Task<int> CancelAsync(
@@ -1130,6 +1147,39 @@ public sealed class BookAlertOutbox(ProjectDbContext dbContext) : IBookAlertOutb
             item.OpeningFairId);
     }

+    private static BookAlertFair ToBookAlertFair(AssoEvents fair)
+    {
+        return new BookAlertFair(
+            fair.Id.Value,
+            BookFairSchedule.GetOpeningInstant(fair),
+            fair.HourOpenDoors is null ? null : BookFairSchedule.GetOpeningInstant(fair),
+            fair.HourCloseDoors is null ? null : BookFairSchedule.GetClosingInstant(fair),
+            FormatAddress(fair.Adresse));
+    }
+
+    private static string? FormatAddress(Adresse? address)
+    {
+        if (address is null)
+        {
+            return null;
+        }
+
+        var street = string.Join(
+            ' ',
+            new[] { address.RoadNumber?.ToString(CultureInfo.InvariantCulture), address.Road?.Trim() }
+                .Where(part => !string.IsNullOrWhiteSpace(part)));
+        var city = string.Join(
+            ' ',
+            new[]
+                {
+                    address.CityCode > 0 ? address.CityCode.ToString("D5", CultureInfo.InvariantCulture) : null,
+                    address.City?.Trim()
+                }
+                .Where(part => !string.IsNullOrWhiteSpace(part)));
+        var line = string.Join(", ", new[] { street, city }.Where(part => part.Length > 0));
+        return line.Length == 0 ? null : line;
+    }
+
     private static RareBookAlertOutboxItem ToApplicationRareItem(RareAlertItemPayload item)
     {
         return new RareBookAlertOutboxItem(
```

- [ ] Relancer → vert (27 tests).
- [ ] Commit : `feat(book-alerts): load fair schedule, address and first name at send time`

## Tâche 4 — Libellés et mise en page du message

Unités pures, sans HTML : formatage français (`BookAlertEmailFormat`), adresses citées
(`BookAlertEmailLinks`) et structure du message (`BookAlertEmailLayout`) — accroche
(RG-90), encarts et ordre des groupes (RG-91, RG-93), texte d'aperçu (RG-96).

**Maquette :** preview/Main.html — zones E1-accroche, E1-encart-bourse, E1-date-bourse, E1-horaires, E1-adresse, E1-texte-apercu
**Maquette :** preview/E2-Desktop.html — zones E2-accroche, E2-texte-apercu, E2-groupe-bourse, E2-encart-bourse, E2-groupe-bourse-suivant
**Maquette :** preview/E3-Desktop.html — zones E3-encart-disponible, E3-prochaine-ouverture
**Maquette :** preview/E4-Etats.html — zones E4-disponible-sans-ouverture, E4-sans-prenom, E4-accroche-rares

**Fichiers :**
- Créer : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailFormat.cs`
- Créer : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailLinks.cs`
- Créer : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailLayout.cs`
- Test : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Books/BookAlertEmailTestData.cs` (bourses, livres et liens d'exemple, réutilisés en tâche 5)
- Test : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Books/BookAlertEmailLayoutTests.cs`

**Interfaces produites :**
- `BookAlertEmailFormat.Date/Hours/Edition/Quantity/Title/Authors/NoReservation/NoReservationLead/Reason`, `BookAlertEmailFormat.French`.
- `BookAlertEmailLinks(string AssociationName, string? LogoUrl, string CatalogBaseUrl, string WebsiteUrl, string? UnsubscribeUrl)` avec `SearchListUrl`, `WebsiteLabel`, `HttpsLogoUrl`, `Unsubscribe`, `BookUrl(string?)`, `static HttpsOrNull(string?)`.
- `BookAlertEmailLayout.Build(BookAlertDelivery) : BookAlertEmailView` ; `BookAlertEmailView`, `BookAlertEmailHeadline`, `BookAlertEmailGroup`.

- [ ] **Tests d'abord** — créer `BookAlertEmailTestData.cs` :

```csharp
using FluentAssertions;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.tests.Books;

/// <summary>Bourses, livres et liens d'exemple partagés par les tests du gabarit d'alerte.</summary>
internal static class BookAlertEmailTestData
{
    public static readonly Guid NovemberFairId = Guid.Parse("4b0c9a51-5d0e-4a3c-9a43-0e0f1f6a7b01");
    public static readonly Guid DecemberFairId = Guid.Parse("4b0c9a51-5d0e-4a3c-9a43-0e0f1f6a7b02");

    public static readonly BookAlertFair NovemberFair = new(
        NovemberFairId,
        new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 11, 7, 16, 0, 0, TimeSpan.Zero),
        "12 rue des Fleurs, 42170 Saint-Just");

    public static readonly BookAlertFair DecemberFair = new(
        DecemberFairId,
        new DateTimeOffset(2026, 12, 5, 8, 0, 0, TimeSpan.Zero),
        null,
        null,
        null);

    public static readonly BookAlertEmailLinks Links = new(
        "Vole Papillon d'Amour",
        "https://volepapillondamour.fr/icons/vpd_icon.png",
        "https://livres.volepapillondamour.fr",
        "https://volepapillondamour.fr",
        "https://livres.volepapillondamour.fr/desinscription");

    public static BookAlertDelivery Delivery(
        IReadOnlyList<BookAlertOutboxItem> items,
        IReadOnlyList<RareBookAlertOutboxItem>? rareItems = null)
    {
        return new BookAlertDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "member@example.org",
            "Marie",
            items,
            rareItems ?? [],
            [NovemberFair, DecemberFair]);
    }

    public static BookAlertOutboxItem NextFairItem(string title, Guid fairId, string isbn = "9782070408504")
    {
        var fair = fairId == NovemberFairId ? NovemberFair : DecemberFair;
        return new BookAlertOutboxItem(
            ParseIsbn(isbn),
            null,
            title,
            "Antoine de Saint-Exupéry",
            1,
            ScanMode.NextFair,
            fairId,
            "Folio",
            1999,
            "Poche",
            fair.OpeningAt,
            PublicPath: $"/livres/le-petit-prince-{isbn}",
            OpeningFairId: fairId);
    }

    public static BookAlertOutboxItem AvailableNowItem(string title, Guid? openingFairId)
    {
        return new BookAlertOutboxItem(
            ParseIsbn("9782080703569"),
            null,
            title,
            "Pierre Loti",
            1,
            ScanMode.AvailableNow,
            null,
            "GF Flammarion",
            1990,
            "Poche",
            openingFairId is null ? null : NovemberFair.OpeningAt,
            PublicPath: "/livres/pecheur-d-islande-pierre-loti-9782080703569",
            OpeningFairId: openingFairId);
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
```

- [ ] puis `BookAlertEmailLayoutTests.cs` :

```csharp
using FluentAssertions;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;
using static Vole_Papillon_Damour.Infrastructure.tests.Books.BookAlertEmailTestData;

namespace Vole_Papillon_Damour.Infrastructure.tests.Books;

public sealed class BookAlertEmailLayoutTests
{
    [Fact]
    public void Format_UsesFrenchDatesAndParisDoorHours()
    {
        BookAlertEmailFormat.Date(NovemberFair.OpeningAt).Should().Be("samedi 7 novembre 2026");
        BookAlertEmailFormat.Hours(NovemberFair.DoorsOpenAt, NovemberFair.DoorsCloseAt)
            .Should().Be("9 h 00 – 17 h 00");
        BookAlertEmailFormat.Hours(NovemberFair.DoorsOpenAt, null).Should().Be("À partir de 9 h 00");
        BookAlertEmailFormat.Hours(null, null).Should().BeNull();
    }

    [Fact]
    public void Layout_PutsTheFairBeforeTheBooksForASingleTitle()
    {
        var view = BookAlertEmailLayout.Build(Delivery([NextFairItem("Le Petit Prince", NovemberFairId)]));

        view.FirstName.Should().Be("Marie");
        view.Headline.Should().Be(new BookAlertEmailHeadline("Le Petit Prince", " vous attend"));
        view.Preheader.Should().Be("À la bourse du samedi 7 novembre 2026 · 1 titre");
        var group = view.Groups.Should().ContainSingle().Subject;
        group.Label.Should().Be("Prochaine bourse");
        group.MainLinePrefix.Should().Be("Disponible à la bourse du ");
        group.MainLineEmphasis.Should().Be("samedi 7 novembre 2026");
        group.Hours.Should().Be("9 h 00 – 17 h 00");
        group.Address.Should().Be("12 rue des Fleurs, 42170 Saint-Just");
    }

    [Fact]
    public void Layout_GroupsBooksPerFairInChronologicalOrder()
    {
        var view = BookAlertEmailLayout.Build(Delivery(
        [
            NextFairItem("Les Misérables", DecemberFairId, "9782253096344"),
            NextFairItem("L'Étranger", NovemberFairId, "9782070360024"),
            NextFairItem("La Horde du Contrevent", NovemberFairId, "9782070342761")
        ]));

        view.Headline.Should().Be(new BookAlertEmailHeadline(null, "3 livres de votre liste vous attendent"));
        view.Preheader.Should().Be("À la bourse du samedi 7 novembre 2026 · 3 titres");
        view.Groups.Select(group => group.Label).Should().Equal(
            "Prochaine bourse · 2 livres",
            "Bourse suivante · 1 livre");
        view.Groups[1].MainLineEmphasis.Should().Be("samedi 5 décembre 2026");
        view.Groups[1].Hours.Should().BeNull();
        view.Groups[1].Address.Should().BeNull();
    }

    [Fact]
    public void Layout_AvailableNowWithoutKnownOpeningComesFirstAndShowsNoDetails()
    {
        var view = BookAlertEmailLayout.Build(Delivery(
        [
            NextFairItem("Le Petit Prince", NovemberFairId, "9782070408504"),
            AvailableNowItem("Pêcheur d'Islande", openingFairId: null)
        ]));

        var first = view.Groups[0];
        first.Label.Should().Be("Disponible maintenant · 1 livre");
        first.MainLineEmphasis.Should().Be("Disponible dès maintenant");
        first.NextOpening.Should().BeNull();
        first.Hours.Should().BeNull();
        first.Address.Should().BeNull();
    }

    [Fact]
    public void Layout_IgnoresTheQueuedOpeningOfAFairThatIsNoLongerActive()
    {
        var item = AvailableNowItem("Pêcheur d'Islande", openingFairId: Guid.NewGuid());

        var view = BookAlertEmailLayout.Build(Delivery([item]));

        view.Groups.Should().ContainSingle().Which.NextOpening.Should().BeNull();
    }
}
```

- [ ] `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter FullyQualifiedName~BookAlertEmailLayoutTests` → échec de compilation.
- [ ] Créer `BookAlertEmailFormat.cs` :

```csharp
using System.Globalization;
using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>
/// Libellés français des alertes : dates et heures à l'heure de Paris, édition, quantité.
/// </summary>
public static class BookAlertEmailFormat
{
    public static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly TimeZoneInfo ParisTimeZone = FindParisTimeZone();

    /// <summary>« samedi 7 novembre 2026 ».</summary>
    public static string Date(DateTimeOffset instant)
    {
        return ToParis(instant).ToString("dddd d MMMM yyyy", French);
    }

    /// <summary>« 9 h 00 – 17 h 00 », « À partir de 9 h 00 », « Jusqu'à 17 h 00 » ou null.</summary>
    public static string? Hours(DateTimeOffset? doorsOpenAt, DateTimeOffset? doorsCloseAt)
    {
        return (doorsOpenAt, doorsCloseAt) switch
        {
            ({ } open, { } close) => $"{Hour(open)} – {Hour(close)}",
            ({ } open, null) => $"À partir de {Hour(open)}",
            (null, { } close) => $"Jusqu'à {Hour(close)}",
            _ => null
        };
    }

    /// <summary>« Folio · 1999 · Poche », éléments connus seulement, ou null.</summary>
    public static string? Edition(BookAlertOutboxItem item)
    {
        var parts = new[]
            {
                item.Publisher?.Trim(),
                item.PublicationYear?.ToString(CultureInfo.InvariantCulture),
                item.PhysicalFormat?.Trim()
            }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        return parts.Length == 0 ? null : string.Join(" · ", parts);
    }

    public static string Quantity(int quantity)
    {
        return quantity > 1 ? $"{quantity} exemplaires" : "1 exemplaire";
    }

    public static string Title(BookAlertOutboxItem item)
    {
        return string.IsNullOrWhiteSpace(item.Title) ? "Titre non renseigné" : item.Title.Trim();
    }

    public static string? Authors(BookAlertOutboxItem item)
    {
        return string.IsNullOrWhiteSpace(item.Authors) ? null : item.Authors.Trim();
    }

    /// <summary>RG-94, sans le gras : « Ce livre n'est pas mis de côté. Premier arrivé, premier servi. »</summary>
    public static string NoReservation(int bookCount)
    {
        return $"{NoReservationLead(bookCount)} Premier arrivé, premier servi.";
    }

    public static string NoReservationLead(int bookCount)
    {
        return bookCount > 1
            ? "Ces livres ne sont pas mis de côté."
            : "Ce livre n'est pas mis de côté.";
    }

    /// <summary>RG-95 : raison de l'envoi, au singulier pour un seul livre cité.</summary>
    public static string Reason(int mentionedCount)
    {
        return mentionedCount > 1
            ? "Vous recevez cet e-mail parce que ces livres figurent dans votre liste de recherche."
            : "Vous recevez cet e-mail parce que ce livre figure dans votre liste de recherche.";
    }

    private static string Hour(DateTimeOffset instant)
    {
        var local = ToParis(instant);
        return $"{local.Hour} h {local.Minute:00}";
    }

    private static DateTimeOffset ToParis(DateTimeOffset instant)
    {
        return TimeZoneInfo.ConvertTime(instant, ParisTimeZone);
    }

    private static TimeZoneInfo FindParisTimeZone()
    {
        foreach (var identifier in new[] { "Europe/Paris", "Romance Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(identifier);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
```

- [ ] Créer `BookAlertEmailLinks.cs` :

```csharp
namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>Identité de l'association et adresses citées par une alerte.</summary>
public sealed record BookAlertEmailLinks(
    string AssociationName,
    string? LogoUrl,
    string CatalogBaseUrl,
    string WebsiteUrl,
    string? UnsubscribeUrl)
{
    public string SearchListUrl => $"{CatalogBaseUrl.TrimEnd('/')}/compte";

    public string WebsiteLabel =>
        Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var uri) ? uri.Host : WebsiteUrl;

    public string? HttpsLogoUrl => HttpsOrNull(LogoUrl);

    public string? Unsubscribe => string.IsNullOrWhiteSpace(UnsubscribeUrl) ? null : UnsubscribeUrl;

    public string? BookUrl(string? publicPath)
    {
        return string.IsNullOrWhiteSpace(publicPath)
            ? null
            : $"{CatalogBaseUrl.TrimEnd('/')}{publicPath}";
    }

    /// <summary>L'URL absolue si elle est en HTTPS, sinon null (images d'un e-mail).</summary>
    public static string? HttpsOrNull(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? uri.AbsoluteUri
            : null;
    }
}
```

- [ ] Créer `BookAlertEmailLayout.cs` :

```csharp
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>Contenu d'une alerte, ordonné comme le message (RG-90 à RG-97).</summary>
public sealed record BookAlertEmailView(
    string? FirstName,
    BookAlertEmailHeadline Headline,
    string Preheader,
    IReadOnlyList<BookAlertEmailGroup> Groups,
    IReadOnlyList<RareBookAlertOutboxItem> RareItems,
    int BookCount)
{
    public int MentionedCount => BookCount + RareItems.Count;
}

/// <summary>Accroche : titre mis en italique (ou null) suivi du texte.</summary>
public sealed record BookAlertEmailHeadline(string? EmphasizedTitle, string Text);

/// <summary>
/// Encart d'une bourse et ses cartes. La ligne principale est
/// <see cref="MainLinePrefix"/> suivi de <see cref="MainLineEmphasis"/> en gras.
/// </summary>
public sealed record BookAlertEmailGroup(
    ScanMode Mode,
    string Label,
    string MainLinePrefix,
    string MainLineEmphasis,
    string? NextOpening,
    string? Hours,
    string? Address,
    IReadOnlyList<BookAlertOutboxItem> Items);

public static class BookAlertEmailLayout
{
    public static BookAlertEmailView Build(BookAlertDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var fairs = (delivery.Fairs ?? []).ToDictionary(fair => fair.AssoEventsId);
        var rareItems = delivery.RareItems ?? [];

        var slots = delivery.Items
            .GroupBy(item => (item.Mode, Key: item.OpeningFairId ?? (object?)item.FairOpeningAt))
            .Select(group =>
            {
                var first = group.First();
                var fair = first.OpeningFairId is { } fairId ? fairs.GetValueOrDefault(fairId) : null;
                var openingAt = first.OpeningFairId is null ? first.FairOpeningAt : fair?.OpeningAt;
                return (Mode: group.Key.Mode, Fair: fair, OpeningAt: openingAt, Items: group.ToArray());
            })
            .OrderBy(slot => slot.Mode == ScanMode.AvailableNow && slot.OpeningAt is null ? 0 : 1)
            .ThenBy(slot => slot.OpeningAt ?? DateTimeOffset.MinValue)
            .ThenBy(slot => slot.Mode)
            .ToArray();

        var groups = new List<BookAlertEmailGroup>(slots.Length);
        var nextFairGroups = 0;
        foreach (var slot in slots)
        {
            var date = slot.OpeningAt is { } opening ? BookAlertEmailFormat.Date(opening) : null;
            var label = slot.Mode == ScanMode.AvailableNow
                ? "Disponible maintenant"
                : nextFairGroups++ == 0 ? "Prochaine bourse" : "Bourse suivante";
            if (slots.Length > 1)
            {
                label += $" · {(slot.Items.Length > 1 ? $"{slot.Items.Length} livres" : "1 livre")}";
            }

            var (prefix, emphasis) = slot.Mode == ScanMode.AvailableNow
                ? (string.Empty, "Disponible dès maintenant")
                : date is null
                    ? (string.Empty, "Disponible à une prochaine bourse")
                    : ("Disponible à la bourse du ", date);
            groups.Add(new BookAlertEmailGroup(
                slot.Mode,
                label,
                prefix,
                emphasis,
                slot.Mode == ScanMode.AvailableNow && date is not null
                    ? $"Prochaine ouverture : {date}"
                    : null,
                slot.Fair is null
                    ? null
                    : BookAlertEmailFormat.Hours(slot.Fair.DoorsOpenAt, slot.Fair.DoorsCloseAt),
                slot.Fair?.Address,
                slot.Items));
        }

        var bookCount = delivery.Items.Count;
        return new BookAlertEmailView(
            string.IsNullOrWhiteSpace(delivery.RecipientName) ? null : delivery.RecipientName.Trim(),
            BuildHeadline(delivery.Items),
            BuildPreheader(groups, bookCount),
            groups,
            rareItems,
            bookCount);
    }

    private static BookAlertEmailHeadline BuildHeadline(IReadOnlyList<BookAlertOutboxItem> items)
    {
        return items.Count switch
        {
            0 => new BookAlertEmailHeadline(null, "Des nouvelles de vos livres rares suivis"),
            1 when !string.IsNullOrWhiteSpace(items[0].Title) =>
                new BookAlertEmailHeadline(items[0].Title!.Trim(), " vous attend"),
            1 => new BookAlertEmailHeadline(null, "Un livre de votre liste vous attend"),
            _ => new BookAlertEmailHeadline(null, $"{items.Count} livres de votre liste vous attendent")
        };
    }

    private static string BuildPreheader(IReadOnlyList<BookAlertEmailGroup> groups, int bookCount)
    {
        if (groups.Count == 0)
        {
            return "Des nouvelles de vos livres rares suivis";
        }

        var first = groups[0];
        var mainLine = first.MainLinePrefix.Length == 0
            ? first.MainLineEmphasis
            : $"À la bourse du {first.MainLineEmphasis}";
        return $"{mainLine} · {(bookCount > 1 ? $"{bookCount} titres" : "1 titre")}";
    }
}
```

- [ ] Relancer → vert (5 tests).
- [ ] Commit : `feat(book-alerts): compute the alert email layout`

## Tâche 5 — Rendus texte et HTML

Les deux rendus et le builder qui les assemble. Le builder change de signature
(`Build(delivery, links)`) : l'ancien fichier de tests est remplacé, l'expéditeur et les
options sont adaptés dans la même tâche.

**Maquette :** preview/Main.html — zones E1-entete, E1-regle-marque, E1-carte-livre, E1-couverture, E1-titre-livre, E1-auteur, E1-edition, E1-quantite, E1-lien-fiche, E1-non-reservation, E1-bouton-liste, E1-pied, E1-lien-site, E1-raison-envoi, E1-desabonnement
**Maquette :** preview/E1-Mobile.html — zones E1-bouton-liste, E1-couverture, E1-horaires, E1-adresse (≤ 620 px : bouton pleine largeur, couverture 72×108, libellés au-dessus des valeurs)
**Maquette :** preview/E2-Desktop.html — zones E2-carte-livre, E2-non-reservation, E2-raison-envoi
**Maquette :** preview/E3-Desktop.html — zones E3-section-rares, E3-ligne-rare
**Maquette :** preview/E4-Etats.html — zones E4-sans-couverture, E4-couverture-remplacement, E4-images-bloquees, E4-mode-sombre, E4-rares-seuls, E4-version-texte

**Fichiers :**
- Créer : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailText.cs`
- Créer : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailHtml.cs`
- Remplacer : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailContentBuilder.cs`
- Modifier : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailOptions.cs`
- Modifier : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailSender.cs`
- Remplacer : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Books/BookAlertEmailContentBuilderTests.cs`

**Interfaces :** consomme la tâche 4 ; produit `BookAlertEmailText.Render(view, links)`,
`BookAlertEmailHtml.Render(view, links, subject)`,
`BookAlertEmailContentBuilder.Build(BookAlertDelivery, BookAlertEmailLinks) : BookAlertEmailContent`.

- [ ] **Tests d'abord** — remplacer le contenu de `BookAlertEmailContentBuilderTests.cs` :

```csharp
using System.Net;
using FluentAssertions;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;
using static Vole_Papillon_Damour.Infrastructure.tests.Books.BookAlertEmailTestData;

namespace Vole_Papillon_Damour.Infrastructure.tests.Books;

public sealed class BookAlertEmailContentBuilderTests
{
    [Fact]
    public void Text_CarriesTheSameContentAsTheHtml()
    {
        var content = BookAlertEmailContentBuilder.Build(
            Delivery([AvailableNowItem("Pêcheur d'Islande", NovemberFairId)]),
            Links);

        content.Subject.Should().Be("Un livre de votre liste de recherche est disponible");
        content.PlainText.Should().StartWith("Bonjour Marie,\n\nPêcheur d'Islande vous attend.\n");
        content.PlainText.Should().Contain("DISPONIBLE DÈS MAINTENANT\nProchaine ouverture : samedi 7 novembre 2026\n");
        content.PlainText.Should().Contain("Horaires : 9 h 00 – 17 h 00\nAdresse : 12 rue des Fleurs, 42170 Saint-Just\n");
        content.PlainText.Should().Contain("- Pêcheur d'Islande — Pierre Loti\n  Édition : GF Flammarion · 1990 · Poche — 1 exemplaire\n");
        content.PlainText.Should().Contain(
            "  Voir la fiche : https://livres.volepapillondamour.fr/livres/pecheur-d-islande-pierre-loti-9782080703569\n");
        content.PlainText.Should().Contain("Ce livre n'est pas mis de côté. Premier arrivé, premier servi.");
        content.PlainText.Should().Contain("Voir ma liste de recherche : https://livres.volepapillondamour.fr/compte");
        content.PlainText.Should().Contain("Vous recevez cet e-mail parce que ce livre figure dans votre liste de recherche.");
        content.PlainText.Should().EndWith("Se désabonner : https://livres.volepapillondamour.fr/desinscription\n");
    }

    [Fact]
    public void Html_IsAnEmailSafeSingleColumnLayout()
    {
        var content = BookAlertEmailContentBuilder.Build(
            Delivery([NextFairItem("Le Petit Prince", NovemberFairId)]),
            Links);

        content.Html.Should().StartWith("<!doctype html>");
        content.Html.Should().Contain("role=\"presentation\" width=\"600\"");
        content.Html.Should().Contain("mso-hide:all");
        content.Html.Should().Contain("@media (max-width:620px)");
        content.Html.Should().Contain("@media (prefers-color-scheme:dark)");
        content.Html.Should().NotContain("linear-gradient");
        content.Html.Should().NotContain("<script");
        content.Html.Should().Contain("src=\"https://volepapillondamour.fr/icons/vpd_icon.png\"");
        content.Html.Should().Contain("href=\"https://livres.volepapillondamour.fr/compte\"");
        var text = WebUtility.HtmlDecode(content.Html);
        text.Should().Contain("À la bourse du samedi 7 novembre 2026 · 1 titre");
        text.Should().Contain("<em>Le Petit Prince</em> vous attend");
        text.Should().Contain("Disponible à la bourse du <strong style=\"font-weight:600;\">samedi 7 novembre 2026</strong>");
        text.Should().Contain("Ce livre n'est pas mis de côté.");
        text.Should().Contain("Voir ma liste de recherche");
    }

    [Fact]
    public void Html_ShowsAnHttpsCoverAndFallsBackToTheButterflyBlock()
    {
        var withCover = NextFairItem("Le Petit Prince", NovemberFairId) with
        {
            CoverUrl = "https://covers.example.org/petit-prince.jpg"
        };
        var insecureCover = NextFairItem("Contes du chat perché", NovemberFairId, "9782070612758") with
        {
            CoverUrl = "http://covers.example.org/chat.jpg"
        };

        var content = BookAlertEmailContentBuilder.Build(Delivery([withCover, insecureCover]), Links);

        content.Html.Should().Contain(
            "<img src=\"https://covers.example.org/petit-prince.jpg\" alt=\"Le Petit Prince\" width=\"88\" height=\"132\"");
        content.Html.Should().NotContain("http://covers.example.org/chat.jpg");
        content.Html.Should().Contain("class=\"vpd-cover vpd-panel\"");
    }

    [Fact]
    public void Html_EncodesCatalogText()
    {
        var item = NextFairItem("<Titre à vérifier>", NovemberFairId) with { Publisher = "Éditions & Fils" };

        var content = BookAlertEmailContentBuilder.Build(Delivery([item]), Links);

        content.Html.Should().Contain("&lt;Titre");
        content.Html.Should().Contain("&amp; Fils");
        content.Html.Should().NotContain("<Titre");
    }

    [Fact]
    public void RareOnlyAlert_HasNoFairNoNoticeAndNoButton()
    {
        var rare = new RareBookAlertOutboxItem(
            Guid.Parse("b7ef1e5e-8f64-4be3-b8d7-82f2d9e8b5fd"),
            "Voyage au bout de la nuit",
            "Louis-Ferdinand Céline",
            "voyage-au-bout-de-la-nuit");

        var content = BookAlertEmailContentBuilder.Build(Delivery([], [rare]), Links);

        content.Subject.Should().Be("Un livre rare suivi a été vendu");
        var html = WebUtility.HtmlDecode(content.Html);
        html.Should().Contain("Des nouvelles de vos livres rares suivis");
        html.Should().Contain("Livres rares suivis");
        html.Should().Contain("<em>Voyage au bout de la nuit</em> — Louis-Ferdinand Céline : n'est plus disponible.");
        html.Should().NotContain("pas mis de côté");
        html.Should().NotContain("Voir ma liste de recherche");
        html.Should().NotContain("class=\"vpd-panel\"");
        content.PlainText.Should().Contain("- Voyage au bout de la nuit — Louis-Ferdinand Céline : n'est plus disponible.");
    }

    [Fact]
    public void MixedAlert_ListsRareBooksAfterTheCards()
    {
        var rare = new RareBookAlertOutboxItem(Guid.NewGuid(), "Les Fleurs du mal", null, "les-fleurs-du-mal");

        var content = BookAlertEmailContentBuilder.Build(
            Delivery([NextFairItem("Le Petit Prince", NovemberFairId)], [rare]),
            Links);

        var html = WebUtility.HtmlDecode(content.Html);
        html.IndexOf("Livres rares suivis", StringComparison.Ordinal)
            .Should().BeGreaterThan(html.IndexOf("Voir la fiche", StringComparison.Ordinal));
        html.IndexOf("Livres rares suivis", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("pas mis de côté", StringComparison.Ordinal));
        html.Should().Contain("ces livres figurent dans votre liste de recherche");
    }

    [Fact]
    public void WithoutFirstNameOrUnsubscribeUrl_TheMessageStaysComplete()
    {
        var delivery = Delivery([NextFairItem("Le Petit Prince", NovemberFairId)]) with { RecipientName = null };

        var content = BookAlertEmailContentBuilder.Build(delivery, Links with { UnsubscribeUrl = " " });

        content.PlainText.Should().StartWith("Bonjour,\n");
        content.Html.Should().Contain(">Bonjour,</p>");
        content.Html.Should().NotContain("Se désabonner");
        content.PlainText.Should().NotContain("Se désabonner");
    }
}
```

- [ ] `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter FullyQualifiedName~BookAlertEmailContentBuilderTests` → échec de compilation (`Build` à deux arguments inconnu).
- [ ] Créer `BookAlertEmailText.cs` :

```csharp
using System.Text;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>Version texte de l'alerte : même contenu et même ordre que le HTML (RG-99).</summary>
public static class BookAlertEmailText
{
    public static string Render(BookAlertEmailView view, BookAlertEmailLinks links)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(links);
        var text = new StringBuilder();
        text.Append(view.FirstName is null ? "Bonjour," : $"Bonjour {view.FirstName},").Append("\n\n");
        text.Append(view.Headline.EmphasizedTitle).Append(view.Headline.Text).Append(".\n");

        foreach (var group in view.Groups)
        {
            text.Append('\n')
                .Append((group.MainLinePrefix + group.MainLineEmphasis).ToUpper(BookAlertEmailFormat.French))
                .Append('\n');
            AppendLine(text, group.NextOpening);
            AppendLine(text, group.Hours is null ? null : $"Horaires : {group.Hours}");
            AppendLine(text, group.Address is null ? null : $"Adresse : {group.Address}");
            foreach (var item in group.Items)
            {
                var authors = BookAlertEmailFormat.Authors(item);
                var edition = BookAlertEmailFormat.Edition(item);
                var quantity = BookAlertEmailFormat.Quantity(item.Quantity);
                text.Append("\n- ").Append(BookAlertEmailFormat.Title(item));
                text.Append(authors is null ? string.Empty : $" — {authors}").Append('\n');
                text.Append("  ").Append(edition is null ? quantity : $"Édition : {edition} — {quantity}").Append('\n');
                AppendLine(text, links.BookUrl(item.PublicPath) is { } url ? $"  Voir la fiche : {url}" : null);
            }
        }

        if (view.RareItems.Count > 0)
        {
            text.Append("\nLIVRES RARES SUIVIS\n");
            foreach (var rare in view.RareItems)
            {
                var author = string.IsNullOrWhiteSpace(rare.AuthorMention) ? string.Empty : $" — {rare.AuthorMention.Trim()}";
                text.Append("- ").Append(rare.Title).Append(author).Append(" : n'est plus disponible.\n");
            }
        }

        if (view.BookCount > 0)
        {
            text.Append('\n').Append(BookAlertEmailFormat.NoReservation(view.BookCount)).Append('\n');
            text.Append("\nVoir ma liste de recherche : ").Append(links.SearchListUrl).Append('\n');
        }

        text.Append("\n--\n");
        text.Append(links.AssociationName).Append(" — Bourse aux livres\n");
        text.Append(links.WebsiteUrl).Append('\n');
        text.Append(BookAlertEmailFormat.Reason(view.MentionedCount)).Append('\n');
        AppendLine(text, links.Unsubscribe is null ? null : $"Se désabonner : {links.Unsubscribe}");
        return text.ToString();
    }

    private static void AppendLine(StringBuilder text, string? line)
    {
        if (line is not null)
        {
            text.Append(line).Append('\n');
        }
    }
}
```

- [ ] Créer `BookAlertEmailHtml.cs`. Les classes `vpd-*` ne servent qu'aux requêtes média
  (mobile ≤ 620 px et mode sombre) ; tout le style par défaut est en ligne :

```csharp
using System.Net;
using System.Text;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

/// <summary>
/// HTML de l'alerte, compatible clients mail : tableaux de présentation, styles en ligne,
/// 600 px, une colonne. Maquettes : docs/features/book-alert-email-template/maquettes/.
/// </summary>
public static class BookAlertEmailHtml
{
    private const string Serif = "Newsreader,Georgia,'Times New Roman',serif";
    private const string Sans = "'Libre Franklin',Arial,Helvetica,sans-serif";
    private const string Mono = "'IBM Plex Mono','Courier New',monospace";

    private const string Canvas = "#e6edf2";
    private const string Paper = "#ffffff";
    private const string PaperSoft = "#f7fbfe";
    private const string PaperBlue = "#e9f4fb";
    private const string Ink = "#041d30";
    private const string InkSoft = "#072b45";
    private const string Body = "#33546b";
    private const string Muted = "#4a6478";
    private const string Blue = "#0c6ea6";
    private const string BlueBright = "#1497d6";
    private const string Cyan = "#7fd8f5";
    private const string Orange = "#f0801c";
    private const string Line = "#d9e9f4";
    private const string LineSoft = "#e2eef7";
    private const string NoticeBackground = "#fff4e8";
    private const string NoticeBorder = "#f6d2ab";
    private const string NoticeInk = "#6b3305";

    private const string ResponsiveAndDarkStyles =
        "body{margin:0;padding:0;}" +
        "@media (max-width:620px){" +
        ".vpd-outer{padding:0!important;}" +
        ".vpd-container{width:100%!important;border:0!important;}" +
        ".vpd-pad{padding-left:20px!important;padding-right:20px!important;}" +
        ".vpd-h1{font-size:28px!important;}" +
        ".vpd-main{font-size:22px!important;}" +
        ".vpd-cover-cell{width:72px!important;}" +
        ".vpd-cover{width:72px!important;height:108px!important;}" +
        ".vpd-label,.vpd-value{display:block!important;width:auto!important;}" +
        ".vpd-button{display:block!important;text-align:center!important;}" +
        "}" +
        "@media (prefers-color-scheme:dark){" +
        ".vpd-bg{background:#081520!important;}" +
        ".vpd-container{background:#0b1b28!important;border-color:#23415a!important;}" +
        ".vpd-ink{color:#ffffff!important;}" +
        ".vpd-body{color:#c7d9e6!important;}" +
        ".vpd-accent{color:#7fd8f5!important;}" +
        ".vpd-panel{background:#12304a!important;border-color:#23415a!important;}" +
        ".vpd-card{border-color:#23415a!important;}" +
        ".vpd-chip{background:#12304a!important;border-color:#23415a!important;color:#e3eef6!important;}" +
        ".vpd-notice{background:#3a2410!important;border-color:#5a3a1c!important;color:#ffd7ad!important;}" +
        ".vpd-button{background:#7fd8f5!important;color:#041d30!important;}" +
        ".vpd-footer{background:#0f2333!important;border-color:#23415a!important;color:#c7d9e6!important;}" +
        ".vpd-logo{background:#ffffff!important;}" +
        "}";

    public static string Render(BookAlertEmailView view, BookAlertEmailLinks links, string subject)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(links);
        var html = new StringBuilder(16_384);
        html.Append("<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<meta name=\"color-scheme\" content=\"light dark\">")
            .Append("<meta name=\"supported-color-schemes\" content=\"light dark\">")
            .Append("<title>").Append(Encode(subject)).Append("</title>")
            .Append("<link href=\"https://fonts.googleapis.com/css2?family=Newsreader:ital,opsz,wght@0,6..72,400;0,6..72,600;1,6..72,400&amp;family=Libre+Franklin:wght@400;600&amp;family=IBM+Plex+Mono:wght@400;500&amp;display=swap\" rel=\"stylesheet\">")
            .Append("<style>").Append(ResponsiveAndDarkStyles).Append("</style></head>")
            .Append($"<body class=\"vpd-bg\" style=\"margin:0;padding:0;background:{Canvas};\">")
            .Append("<div style=\"display:none;max-height:0;overflow:hidden;mso-hide:all;font-size:1px;line-height:1px;color:")
            .Append(Canvas).Append(";\">").Append(Encode(view.Preheader)).Append("</div>")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-bg\" style=\"background:{Canvas};\">")
            .Append("<tr><td align=\"center\" class=\"vpd-outer\" style=\"padding:32px 12px;\">")
            .Append($"<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-container\" style=\"width:600px;max-width:600px;background:{Paper};border:1px solid {Line};\">");

        AppendHeader(html, links);
        AppendHeadline(html, view);
        for (var index = 0; index < view.Groups.Count; index++)
        {
            AppendGroup(html, view.Groups[index], links, index == 0);
        }

        AppendRareItems(html, view);
        if (view.BookCount > 0)
        {
            AppendNoReservation(html, view.BookCount);
            AppendSearchListButton(html, links);
        }
        else
        {
            html.Append("<tr><td style=\"padding:0 0 36px;font-size:0;line-height:0;\">&#160;</td></tr>");
        }

        AppendFooter(html, view, links);
        html.Append("</table></td></tr></table></body></html>");
        return html.ToString();
    }

    private static void AppendHeader(StringBuilder html, BookAlertEmailLinks links)
    {
        var association = Encode(links.AssociationName);
        html.Append("<tr><td class=\"vpd-pad\" style=\"padding:28px 40px 0;\">")
            .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>");
        if (links.HttpsLogoUrl is { } logoUrl)
        {
            html.Append("<td style=\"padding:0 14px 0 0;vertical-align:middle;\">")
                .Append($"<img src=\"{Encode(logoUrl)}\" alt=\"{association}\" width=\"52\" height=\"52\" class=\"vpd-logo\" ")
                .Append($"style=\"display:block;width:52px;height:52px;border:0;background:{Paper};\"></td>");
        }

        html.Append("<td style=\"vertical-align:middle;\">")
            .Append($"<div class=\"vpd-ink\" style=\"font-family:{Serif};font-size:21px;font-weight:600;line-height:1.2;color:{InkSoft};\">{association}</div>")
            .Append($"<div class=\"vpd-accent\" style=\"margin-top:3px;font-family:{Mono};font-size:11px;letter-spacing:0.14em;text-transform:uppercase;color:{Blue};\">Bourse aux livres</div>")
            .Append("</td></tr></table>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin-top:24px;\"><tr>");
        foreach (var color in new[] { Cyan, BlueBright, Orange })
        {
            html.Append($"<td height=\"4\" style=\"height:4px;background:{color};font-size:0;line-height:0;\">&#160;</td>");
        }

        html.Append("</tr></table></td></tr>");
    }

    private static void AppendHeadline(StringBuilder html, BookAlertEmailView view)
    {
        var greeting = view.FirstName is null ? "Bonjour," : $"Bonjour {view.FirstName},";
        html.Append("<tr><td class=\"vpd-pad\" style=\"padding:32px 40px 8px;\">")
            .Append($"<p class=\"vpd-ink\" style=\"margin:0 0 10px;font-family:{Sans};font-size:16px;line-height:1.5;color:{InkSoft};\">{Encode(greeting)}</p>")
            .Append($"<h1 class=\"vpd-h1 vpd-ink\" style=\"margin:0;font-family:{Serif};font-size:32px;font-weight:400;line-height:1.2;color:{Ink};\">");
        if (view.Headline.EmphasizedTitle is { } title)
        {
            html.Append("<em>").Append(Encode(title)).Append("</em>");
        }

        html.Append(Encode(view.Headline.Text)).Append("</h1></td></tr>");
    }

    private static void AppendGroup(
        StringBuilder html,
        BookAlertEmailGroup group,
        BookAlertEmailLinks links,
        bool isFirst)
    {
        var accent = group.Mode == ScanMode.AvailableNow ? BlueBright : Orange;
        html.Append($"<tr><td class=\"vpd-pad\" style=\"padding:{(isFirst ? 20 : 28)}px 40px 0;\">")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-panel\" style=\"background:{PaperBlue};border:1px solid {Line};border-top:4px solid {accent};\">")
            .Append("<tr><td style=\"padding:20px 24px 22px;\">")
            .Append($"<div class=\"vpd-accent\" style=\"font-family:{Mono};font-size:11px;letter-spacing:0.14em;text-transform:uppercase;color:{Blue};\">{Encode(group.Label)}</div>")
            .Append($"<div class=\"vpd-main vpd-ink\" style=\"margin-top:6px;font-family:{Serif};font-size:25px;line-height:1.25;color:{Ink};\">")
            .Append(Encode(group.MainLinePrefix))
            .Append($"<strong style=\"font-weight:600;\">{Encode(group.MainLineEmphasis)}</strong></div>");
        if (group.NextOpening is { } nextOpening)
        {
            html.Append($"<div class=\"vpd-ink\" style=\"margin-top:6px;font-family:{Sans};font-size:16px;line-height:1.4;color:{InkSoft};\">{Encode(nextOpening)}</div>");
        }

        if (group.Hours is not null || group.Address is not null)
        {
            html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin-top:16px;\">");
            AppendDetail(html, "Horaires", group.Hours);
            AppendDetail(html, "Adresse", group.Address);
            html.Append("</table>");
        }

        html.Append("</td></tr></table></td></tr>");
        for (var index = 0; index < group.Items.Count; index++)
        {
            AppendBookCard(html, group.Items[index], links, index == 0);
        }
    }

    private static void AppendDetail(StringBuilder html, string label, string? value)
    {
        if (value is null)
        {
            return;
        }

        html.Append("<tr>")
            .Append($"<td class=\"vpd-label vpd-body\" width=\"88\" valign=\"top\" style=\"width:88px;padding:4px 0;font-family:{Mono};font-size:11px;letter-spacing:0.1em;text-transform:uppercase;color:{Muted};\">{label}</td>")
            .Append($"<td class=\"vpd-value vpd-ink\" valign=\"top\" style=\"padding:2px 0 4px;font-family:{Sans};font-size:15px;line-height:1.4;color:{InkSoft};\">{Encode(value)}</td>")
            .Append("</tr>");
    }

    private static void AppendBookCard(
        StringBuilder html,
        BookAlertOutboxItem item,
        BookAlertEmailLinks links,
        bool isFirst)
    {
        var title = BookAlertEmailFormat.Title(item);
        html.Append($"<tr><td class=\"vpd-pad\" style=\"padding:{(isFirst ? 16 : 12)}px 40px 0;\">")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-card\" style=\"border:1px solid {Line};\"><tr>")
            .Append("<td class=\"vpd-cover-cell\" width=\"88\" valign=\"top\" style=\"width:88px;padding:18px 0 18px 18px;\">");
        AppendCover(html, item, title, links);
        html.Append("</td><td valign=\"top\" style=\"padding:18px 18px 18px 20px;\">")
            .Append($"<div class=\"vpd-ink\" style=\"font-family:{Serif};font-size:21px;line-height:1.25;color:{Ink};\">{Encode(title)}</div>");
        if (BookAlertEmailFormat.Authors(item) is { } authors)
        {
            html.Append($"<div class=\"vpd-body\" style=\"margin-top:4px;font-family:{Sans};font-size:15px;color:{Body};\">{Encode(authors)}</div>");
        }

        if (BookAlertEmailFormat.Edition(item) is { } edition)
        {
            html.Append($"<div class=\"vpd-body\" style=\"margin-top:10px;font-family:{Mono};font-size:12px;letter-spacing:0.04em;color:{Muted};\">{Encode(edition)}</div>");
        }

        html.Append($"<div style=\"margin-top:10px;\"><span class=\"vpd-chip\" style=\"display:inline-block;font-family:{Sans};font-size:13px;color:{InkSoft};background:{PaperSoft};border:1px solid {Line};padding:3px 10px;\">{BookAlertEmailFormat.Quantity(item.Quantity)}</span></div>");
        if (links.BookUrl(item.PublicPath) is { } bookUrl)
        {
            html.Append($"<div style=\"margin-top:14px;\"><a href=\"{Encode(bookUrl)}\" class=\"vpd-accent\" style=\"display:inline-block;padding:6px 0;font-family:{Sans};font-size:15px;font-weight:600;color:{Blue};\">Voir la fiche</a></div>");
        }

        html.Append("</td></tr></table></td></tr>");
    }

    private static void AppendCover(
        StringBuilder html,
        BookAlertOutboxItem item,
        string title,
        BookAlertEmailLinks links)
    {
        if (BookAlertEmailLinks.HttpsOrNull(item.CoverUrl) is { } coverUrl)
        {
            html.Append($"<img src=\"{Encode(coverUrl)}\" alt=\"{Encode(title)}\" width=\"88\" height=\"132\" class=\"vpd-cover\" ")
                .Append($"style=\"display:block;width:88px;height:132px;border:1px solid {Line};object-fit:cover;background:{PaperBlue};\">");
            return;
        }

        html.Append($"<table role=\"presentation\" width=\"88\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-cover vpd-panel\" style=\"width:88px;height:132px;background:{PaperBlue};border:1px solid {Line};\">")
            .Append("<tr><td align=\"center\" valign=\"middle\" style=\"padding:12px 8px;text-align:center;\">");
        if (links.HttpsLogoUrl is { } logoUrl)
        {
            html.Append($"<img src=\"{Encode(logoUrl)}\" alt=\"\" width=\"40\" height=\"40\" style=\"display:block;width:40px;height:40px;margin:0 auto 10px;border:0;\">");
        }

        html.Append($"<div class=\"vpd-ink\" style=\"font-family:{Serif};font-size:11px;line-height:1.2;color:{InkSoft};\">{Encode(title)}</div>")
            .Append("</td></tr></table>");
    }

    private static void AppendRareItems(StringBuilder html, BookAlertEmailView view)
    {
        if (view.RareItems.Count == 0)
        {
            return;
        }

        html.Append("<tr><td class=\"vpd-pad\" style=\"padding:28px 40px 0;\">")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr><td style=\"border-top:1px solid {Line};padding-top:20px;\">")
            .Append($"<div class=\"vpd-body\" style=\"font-family:{Mono};font-size:11px;letter-spacing:0.14em;text-transform:uppercase;color:{Muted};\">Livres rares suivis</div>")
            .Append($"<ul class=\"vpd-ink\" style=\"margin:10px 0 0;padding:0 0 0 18px;font-family:{Sans};font-size:15px;line-height:1.6;color:{InkSoft};\">");
        foreach (var rare in view.RareItems)
        {
            var author = string.IsNullOrWhiteSpace(rare.AuthorMention)
                ? string.Empty
                : $" — {Encode(rare.AuthorMention.Trim())}";
            html.Append($"<li><em>{Encode(rare.Title)}</em>{author} : n'est plus disponible.</li>");
        }

        html.Append("</ul></td></tr></table></td></tr>");
    }

    private static void AppendNoReservation(StringBuilder html, int bookCount)
    {
        html.Append("<tr><td class=\"vpd-pad\" style=\"padding:24px 40px 0;\">")
            .Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"vpd-notice\" style=\"background:{NoticeBackground};border:1px solid {NoticeBorder};\">")
            .Append($"<tr><td class=\"vpd-notice\" style=\"padding:14px 18px;font-family:{Sans};font-size:15px;line-height:1.5;color:{NoticeInk};\">")
            .Append($"<strong>{Encode(BookAlertEmailFormat.NoReservationLead(bookCount))}</strong> Premier arrivé, premier servi.")
            .Append("</td></tr></table></td></tr>");
    }

    private static void AppendSearchListButton(StringBuilder html, BookAlertEmailLinks links)
    {
        html.Append("<tr><td class=\"vpd-pad\" style=\"padding:28px 40px 36px;\">")
            .Append($"<a href=\"{Encode(links.SearchListUrl)}\" class=\"vpd-button\" style=\"display:inline-block;background:{Blue};color:#ffffff;font-family:{Sans};font-size:16px;font-weight:600;text-decoration:none;padding:14px 28px;border-radius:4px;\">Voir ma liste de recherche</a>")
            .Append("</td></tr>");
    }

    private static void AppendFooter(StringBuilder html, BookAlertEmailView view, BookAlertEmailLinks links)
    {
        html.Append($"<tr><td class=\"vpd-pad vpd-footer\" style=\"background:{PaperSoft};border-top:1px solid {LineSoft};padding:24px 40px 28px;font-family:{Sans};font-size:13px;line-height:1.6;color:{Muted};\">")
            .Append($"<div class=\"vpd-ink\" style=\"font-family:{Serif};font-size:16px;color:{InkSoft};\">{Encode(links.AssociationName)} — Bourse aux livres</div>")
            .Append($"<div style=\"margin-top:4px;\"><a href=\"{Encode(links.WebsiteUrl)}\" class=\"vpd-accent\" style=\"color:{Blue};\">{Encode(links.WebsiteLabel)}</a></div>")
            .Append($"<p style=\"margin:14px 0 0;\">{Encode(BookAlertEmailFormat.Reason(view.MentionedCount))}</p>");
        if (links.Unsubscribe is { } unsubscribe)
        {
            html.Append($"<p style=\"margin:6px 0 0;\"><a href=\"{Encode(unsubscribe)}\" style=\"display:inline-block;padding:6px 0;color:{Muted};\">Se désabonner</a></p>");
        }

        html.Append("</td></tr>");
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
```

- [ ] Remplacer `BookAlertEmailContentBuilder.cs` (l'objet `BuildSubject` est conservé à l'identique) :

```csharp
using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

public sealed record BookAlertEmailContent(
    string Subject,
    string PlainText,
    string Html);

public static class BookAlertEmailContentBuilder
{
    public static BookAlertEmailContent Build(BookAlertDelivery delivery, BookAlertEmailLinks links)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentException.ThrowIfNullOrWhiteSpace(links.AssociationName);

        var subject = BuildSubject(delivery.Items.Count, delivery.RareItems?.Count ?? 0);
        var view = BookAlertEmailLayout.Build(delivery);
        return new BookAlertEmailContent(
            subject,
            BookAlertEmailText.Render(view, links),
            BookAlertEmailHtml.Render(view, links, subject));
    }

    private static string BuildSubject(int ordinaryItemCount, int rareItemCount)
    {
        if (ordinaryItemCount == 0 && rareItemCount == 1)
        {
            return "Un livre rare suivi a été vendu";
        }

        if (ordinaryItemCount == 0 && rareItemCount > 1)
        {
            return $"{rareItemCount} livres rares suivis ont été vendus";
        }

        if (rareItemCount == 0)
        {
            return ordinaryItemCount == 1
                ? "Un livre de votre liste de recherche est disponible"
                : $"{ordinaryItemCount} livres de votre liste de recherche sont disponibles";
        }

        return "Des livres suivis ont changé de disponibilité";
    }
}
```

- [ ] Options et expéditeur :

```diff
--- a/src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailOptions.cs
+++ b/src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailOptions.cs
@@ -11,4 +11,6 @@ public sealed class BookAlertEmailOptions
     public string AssociationName { get; set; } = "Vole Papillon d'Amour";
     public string? LogoUrl { get; set; } = "https://volepapillondamour.fr/icons/vpd_icon.png";
     public string? UnsubscribeUrl { get; set; }
+    public string CatalogBaseUrl { get; set; } = "https://livres.volepapillondamour.fr";
+    public string WebsiteUrl { get; set; } = "https://volepapillondamour.fr";
 }
--- a/src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailSender.cs
+++ b/src/Backend/Vole_Papillon_Damour.Infrastructure/Services/BookAlerts/BookAlertEmailSender.cs
@@ -66,9 +66,12 @@ public sealed class BookAlertEmailSender : IBookAlertEmailSender

         var emailContent = BookAlertEmailContentBuilder.Build(
             delivery,
-            _options.AssociationName,
-            _options.UnsubscribeUrl,
-            _options.LogoUrl);
+            new BookAlertEmailLinks(
+                _options.AssociationName,
+                _options.LogoUrl,
+                _options.CatalogBaseUrl,
+                _options.WebsiteUrl,
+                _options.UnsubscribeUrl));
         var content = new EmailContent(emailContent.Subject)
         {
             PlainText = emailContent.PlainText,
```

- [ ] `dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests` → vert.
- [ ] Commit : `feat(book-alerts): render the branded alert email template`

## Tâche 6 — Échantillons et contrôle visuel

Un test écrit les rendus E1, E2, E3 et « rares seuls » quand `VPD_EMAIL_SAMPLES_DIR` est
défini, et ne fait rien sinon. Il sert à comparer le rendu réel aux maquettes.

**Maquette :** preview/Main.html — zones E1-texte-apercu, E1-carte-livre
**Maquette :** preview/E2-Mobile.html — zones E2-groupe-bourse, E2-carte-livre
**Maquette :** preview/E3-Mobile.html — zones E3-section-rares
**Maquette :** preview/E4-Etats.html — zones E4-rares-seuls

**Fichiers :**
- Créer : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Books/BookAlertEmailSamples.cs`

- [ ] Créer le test :

```csharp
using FluentAssertions;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;

namespace Vole_Papillon_Damour.Infrastructure.tests.Books;

/// <summary>
/// Écrit les rendus E1, E2, E3 et « rares seuls » dans le dossier désigné par
/// VPD_EMAIL_SAMPLES_DIR, pour les comparer aux maquettes. Sans la variable, ne fait rien.
/// </summary>
public sealed class BookAlertEmailSamples
{
    private static readonly Guid November = Guid.Parse("4b0c9a51-5d0e-4a3c-9a43-0e0f1f6a7b01");
    private static readonly Guid December = Guid.Parse("4b0c9a51-5d0e-4a3c-9a43-0e0f1f6a7b02");

    [Fact]
    public void WriteSamples()
    {
        var directory = Environment.GetEnvironmentVariable("VPD_EMAIL_SAMPLES_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var links = new BookAlertEmailLinks(
            "Vole Papillon d'Amour",
            "https://volepapillondamour.fr/icons/vpd_icon.png",
            "https://livres.volepapillondamour.fr",
            "https://volepapillondamour.fr",
            "https://livres.volepapillondamour.fr/desinscription");
        var rare = new RareBookAlertOutboxItem(
            Guid.NewGuid(),
            "Voyage au bout de la nuit",
            "Louis-Ferdinand Céline",
            "voyage-au-bout-de-la-nuit");
        var samples = new Dictionary<string, BookAlertDelivery>
        {
            ["E1-un-livre"] = Delivery([Item("9782070408504", "Le Petit Prince", "Antoine de Saint-Exupéry", "Folio", 1999, ScanMode.NextFair, November)]),
            ["E2-deux-bourses"] = Delivery(
            [
                Item("9782070360024", "L'Étranger", "Albert Camus", "Folio", 2005, ScanMode.NextFair, November, quantity: 2),
                Item("9782070342761", "La Horde du Contrevent", "Alain Damasio", "Folio SF", 2007, ScanMode.NextFair, November),
                Item("9782253096344", "Les Misérables, tome 1", "Victor Hugo", "Le Livre de Poche", 1998, ScanMode.NextFair, December)
            ]),
            ["E3-mixte"] = Delivery(
                [Item("9782080703569", "Pêcheur d'Islande", "Pierre Loti", "GF Flammarion", 1990, ScanMode.AvailableNow, November, cover: false)],
                [rare]),
            ["E4-rares-seuls"] = Delivery([], [rare])
        };

        foreach (var (name, delivery) in samples)
        {
            var content = BookAlertEmailContentBuilder.Build(delivery, links);
            File.WriteAllText(Path.Combine(directory, $"{name}.html"), content.Html);
            File.WriteAllText(Path.Combine(directory, $"{name}.txt"), content.PlainText);
        }

        Directory.GetFiles(directory, "*.html").Should().HaveCountGreaterThanOrEqualTo(4);
    }

    private static BookAlertDelivery Delivery(
        IReadOnlyList<BookAlertOutboxItem> items,
        IReadOnlyList<RareBookAlertOutboxItem>? rareItems = null)
    {
        return new BookAlertDelivery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "marie@example.org",
            "Marie",
            items,
            rareItems ?? [],
            [
                new BookAlertFair(
                    November,
                    new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 11, 7, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 11, 7, 16, 0, 0, TimeSpan.Zero),
                    "12 rue des Fleurs, 42170 Saint-Just"),
                new BookAlertFair(
                    December,
                    new DateTimeOffset(2026, 12, 5, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 12, 5, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 12, 5, 17, 0, 0, TimeSpan.Zero),
                    "12 rue des Fleurs, 42170 Saint-Just")
            ]);
    }

    private static BookAlertOutboxItem Item(
        string isbn,
        string title,
        string authors,
        string publisher,
        int year,
        ScanMode mode,
        Guid fairId,
        int quantity = 1,
        bool cover = true)
    {
        Isbn13.TryCreate(isbn, out var isbn13).Should().BeTrue();
        return new BookAlertOutboxItem(
            isbn13,
            null,
            title,
            authors,
            quantity,
            mode,
            mode == ScanMode.NextFair ? fairId : null,
            publisher,
            year,
            "Poche",
            null,
            cover ? $"https://covers.openlibrary.org/b/isbn/{isbn}-M.jpg" : null,
            $"/livres/exemple-{isbn}",
            fairId);
    }
}
```

- [ ] Générer les échantillons hors du dépôt :
  `VPD_EMAIL_SAMPLES_DIR=<dossier temporaire> dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter FullyQualifiedName~BookAlertEmailSamples`
- [ ] Capturer chaque `.html` à 680 px et à 390 px de large (Playwright avec Edge ou
  Chrome installés : `npx playwright@1.48.2 screenshot --channel msedge --full-page --viewport-size=390,900 file:///…`)
  et les comparer aux pages `maquettes/preview/` correspondantes (`preview/Main.html`,
  `preview/E1-Mobile.html`, `preview/E2-Desktop.html`, `preview/E2-Mobile.html`,
  `preview/E3-Desktop.html`, `preview/E3-Mobile.html`, `preview/E4-Etats.html`). Écart toléré :
  les maquettes utilisent des polices web que certains clients remplacent par Georgia ou
  Arial. Ne pas committer les échantillons ni les captures.
- [ ] Commit : `test(book-alerts): add opt-in rendered email samples`

## Tâche 7 — Documentation, validation complète et PR

**Fichiers :**
- Modifier : `docs/email-branding.md`
- Modifier : `docs/features/book-alert-email-template/README.md` (statut 5 = lien de la PR)

- [ ] Ajouter à `docs/email-branding.md`, après « Logo dans le contenu du message » :

```markdown
## Gabarit des alertes

Les alertes de liste de recherche utilisent le gabarit décrit dans
`docs/features/book-alert-email-template/` (spec `RG-90` à `RG-99`, maquettes). Le HTML
est produit par `BookAlertEmailHtml` (tableaux, styles en ligne, 600 px, variantes mobile
et mode sombre par requêtes média) et la version texte par `BookAlertEmailText`.

Liens cités par le message, avec leurs valeurs par défaut :

| Clé | Défaut | Usage |
|---|---|---|
| `BookAlerts:Email:CatalogBaseUrl` | `https://livres.volepapillondamour.fr` | fiches livres et bouton « Voir ma liste de recherche » (`/compte`) |
| `BookAlerts:Email:WebsiteUrl` | `https://volepapillondamour.fr` | lien du pied de message |

Pour comparer le rendu aux maquettes : définir `VPD_EMAIL_SAMPLES_DIR` et lancer le test
`BookAlertEmailSamples`.
```

- [ ] `dotnet build src/Backend/Vole_Papillon_Damour.slnx` → 0 erreur, aucun nouvel
  avertissement dans les fichiers touchés.
- [ ] `dotnet test src/Backend/Vole_Papillon_Damour.slnx --no-build` → tout vert (885 tests à la conception, + les échecs préexistants notés en tâche 0).
- [ ] Commit : `docs(book-alerts): document the alert email template`
- [ ] Ouvrir la PR selon la skill implement-handoff.

## Zones non implémentées

Aucune. La ligne « Boîte de réception » des maquettes (`E1-texte-apercu`,
`E2-texte-apercu`) illustre le texte d'aperçu caché (tâches 4 et 5), pas un élément
visible du corps.
