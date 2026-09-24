# Recherche BnF des éditions à ISBN-10 — plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal :** retrouver à la BnF les éditions antérieures à 2007, dont la notice porte un
ISBN-10, lorsqu'on les interroge avec l'ISBN-13 normalisé.

**Architecture :** les deux clients BnF (`BnfSruClient` pour la résolution d'une fiche,
`BnfSruSearchClient` pour la recherche de référence par ISBN) interrogent l'index SRU
`bib.isbn`. Cet index ne rapproche pas un ISBN-13 d'une notice enregistrée en ISBN-10.
L'index `bib.fuzzyISBN` le fait. Le correctif remplace l'index dans une seule méthode
partagée, `BnfSruClient.IsbnQuery`, appelée par les deux clients. C'est l'index que
prévoyait déjà `technique/07-integrations-externes.md` §2 : le code s'en était écarté.

**Tech Stack :** .NET 10, xUnit, FluentAssertions, `HttpMessageHandler` de test
(`StubHttpMessageHandler`), API SRU du catalogue général de la BnF.

**Spec :** pas de spécification séparée. Constat et mesure dans la branche
`docs/book-recommendations-design`, fichier
`docs/recommandations-livres/03-mesure-couverture-resumes.md` §5, résumés ici :

- échantillon de 138 éditions réelles de 46 livres connus, prises au catalogue BnF ;
- `bib.isbn all "<ISBN-13>"` (code actuel) : **0 édition d'avant 2007 trouvée sur 55**,
  soit 83 sur 138 au total ;
- `bib.fuzzyISBN all "<ISBN-13>"` : **138 sur 138**, et le premier enregistrement porte
  l'ISBN demandé dans 138 cas sur 138.

## Global Constraints

- Ne pas modifier `Isbn13` ni le contrat `BookMetadataResult`.
- La requête reste **une seule requête HTTP** par ISBN : ni repli séquentiel, ni appel
  supplémentaire (latence du scan, `ENF-01`, et politesse envers la BnF, T-07 §2).
- `maximumRecords=1` reste inchangé dans `BnfSruClient`. La pagination de
  `BnfSruSearchClient` reste inchangée.
- La recherche texte (`bib.anywhere`) n'est pas touchée.
- Tests d'abord (`AGENTS.md`, « Write tests before executable production changes »).
- Commandes git préfixées par `rtk`, comme l'exige
  `.github/skills/delivery-workflow/SKILL.md`.

## Review Focus

1. **Notice BnF dont la zone 010 porte un ISBN-10 à tirets** (`2-07-051842-6`), recherchée
   avec l'ISBN-13 `9782070518425` : la recherche de référence doit renvoyer l'ISBN-13
   demandé, pas une valeur brute. Test en tâche 2.
2. **ISBN en `979`**, sans équivalent ISBN-10 : la requête doit rester valide et inchangée
   sur le fond. Test en tâche 1.
3. **ISBN-10 saisi par un bénévole** dans la recherche de référence : il est converti en
   ISBN-13 avant la requête. Test en tâche 2.
4. **Plusieurs zones 010 dans une notice**, la première désignant une autre édition (un
   coffret, par exemple) : comportement existant, non modifié. La mesure n'a montré aucun
   cas sur 138. Pas de test, risque résiduel à signaler dans la PR.
5. **BnF indisponible ou sans résultat** : `null` (client de résolution) ou liste vide
   (recherche), comme aujourd'hui. Déjà couvert par
   `FindAsync_WhenSourceReturnsNoRecords_ReturnsNull` et
   `SearchAsync_WhenSourceReturnsAnError_ReturnsNoEdition`.

## Hors périmètre

- **Les fiches déjà en base ne sont pas relues.** Le traitement `enrich`
  (`EnrichPendingBooksCommandHandler`) reprend seul les fiches `Pending`, et les fiches
  `NotFound` à leurs 2ᵉ et 3ᵉ tentatives. Une fiche déjà `Resolved` par Open Library ou
  Google Books, ou `NotFound` après trois tentatives, garde ses métadonnées. Un rattrapage
  ponctuel relève d'une décision séparée, à signaler dans la PR.
- La lecture du résumé BnF (zone 330) : sujet des recommandations, pas de ce correctif.

## Fichiers

| Fichier | Rôle |
|---|---|
| `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/Bibliographic/BnfSruClient.cs` | Ajoute `internal static string IsbnQuery(Isbn13)`, l'utilise dans `BuildRequestUri` |
| `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/Bibliographic/BnfSruSearchClient.cs` | Utilise `BnfSruClient.IsbnQuery` pour une requête ISBN |
| `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Bibliographic/BnfSruClientTests.cs` | Tests de la requête de résolution |
| `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Bibliographic/BnfSruSearchClientTests.cs` | Tests de la recherche par ISBN |
| `docs/bourse-aux-livres/technique/07-integrations-externes.md` | Explique pourquoi `fuzzyISBN` |
| `docs/bourse-aux-livres/plan/README.md` | Référence ce lot `07` |

`BnfSruSearchClient` appelle déjà `BnfSruClient.Clean`, `FirstSubfield`, `ReadAuthors`
et `ParseYear` (méthodes `internal static`). `IsbnQuery` suit le même modèle.

---

### Tâche 1 : la résolution d'une fiche interroge `bib.fuzzyISBN`

**Files :**
- Modify : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/Bibliographic/BnfSruClient.cs:83-95`
- Test : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Bibliographic/BnfSruClientTests.cs`

**Interfaces :**
- Consumes : `Isbn13` (`Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects`), inchangé.
- Produces : `internal static string BnfSruClient.IsbnQuery(Isbn13 isbn13)`, qui renvoie
  exactement `bib.fuzzyISBN all "<isbn13.Value>"`. La tâche 2 l'utilise.

- [ ] **Étape 1 : adapter le test existant et ajouter le test `979`**

Dans `FindAsync_WithRecordedUnimarcNoticeWithCover_MapsBookMetadata`, remplacer les lignes

```csharp
        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.Query.Should().Contain("bib.isbn");
        capturedRequest.RequestUri.Query.Should().Contain("9782070363735");
        capturedRequest.RequestUri.Query.Should().Contain("recordSchema=unimarcXchange");
```

par

```csharp
        capturedRequest.Should().NotBeNull();
        var query = Uri.UnescapeDataString(capturedRequest!.RequestUri!.Query);
        query.Should().Contain("bib.fuzzyISBN all \"9782070363735\"");
        query.Should().NotContain("bib.isbn ");
        query.Should().Contain("recordSchema=unimarcXchange");
        query.Should().Contain("maximumRecords=1");
```

Puis ajouter ce test, avant `private const string RecordedNoticeWithCover` :

```csharp
    [Fact]
    public async Task FindAsync_With979Isbn_QueriesFuzzyIsbnIndexWithTheIsbn13()
    {
        Isbn13.TryCreate("9791023504637", out var isbn13).Should().BeTrue();
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri?.Host == "openapi.bnf.fr")
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<searchRetrieveResponse><numberOfRecords>0</numberOfRecords></searchRetrieveResponse>")
            };
        }));
        var client = new BnfSruClient(
            httpClient,
            Options.Create(new BibliographicOptions
            {
                BnfSruEndpoint = "https://bnf.example.test/api/SRU"
            }));

        await client.FindAsync(isbn13, CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        Uri.UnescapeDataString(capturedRequest!.RequestUri!.Query)
            .Should().Contain("bib.fuzzyISBN all \"9791023504637\"");
    }
```

- [ ] **Étape 2 : vérifier que les tests échouent**

```powershell
dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter "FullyQualifiedName~BnfSruClientTests"
```

Attendu : **2 échecs**, `FindAsync_WithRecordedUnimarcNoticeWithCover_MapsBookMetadata`
et `FindAsync_With979Isbn_QueriesFuzzyIsbnIndexWithTheIsbn13`, avec un message du type
`Expected query ... to contain "bib.fuzzyISBN all \"...\""`.

- [ ] **Étape 3 : implémenter**

Dans `BnfSruClient.cs`, remplacer `BuildRequestUri` par :

```csharp
    private Uri BuildRequestUri(Isbn13 isbn13)
    {
        var query = string.Join(
            "&",
            "version=1.2",
            "operation=searchRetrieve",
            $"query={Uri.EscapeDataString(IsbnQuery(isbn13))}",
            "recordSchema=unimarcXchange",
            "maximumRecords=1",
            "startRecord=1");

        return new Uri($"{_options.BnfSruEndpoint}?{query}", UriKind.Absolute);
    }

    // bib.isbn misses notices recorded with the ISBN-10 of pre-2007 editions when
    // queried with the normalized ISBN-13; bib.fuzzyISBN matches both forms.
    internal static string IsbnQuery(Isbn13 isbn13)
    {
        return $"bib.fuzzyISBN all \"{isbn13.Value}\"";
    }
```

- [ ] **Étape 4 : vérifier que les tests passent**

```powershell
dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter "FullyQualifiedName~BnfSruClientTests"
```

Attendu : **4 tests réussis, 0 échec.**

- [ ] **Étape 5 : commit**

```powershell
rtk git add src/Backend/Vole_Papillon_Damour.Infrastructure/Services/Bibliographic/BnfSruClient.cs src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Bibliographic/BnfSruClientTests.cs
rtk git commit -m "fix(catalog): resolve pre-2007 BnF notices through the fuzzy ISBN index"
```

---

### Tâche 2 : la recherche de référence par ISBN interroge `bib.fuzzyISBN`

**Files :**
- Modify : `src/Backend/Vole_Papillon_Damour.Infrastructure/Services/Bibliographic/BnfSruSearchClient.cs:109-124`
- Test : `src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Bibliographic/BnfSruSearchClientTests.cs`

**Interfaces :**
- Consumes : `BnfSruClient.IsbnQuery(Isbn13)` (tâche 1).
- Produces : rien de nouveau. `SearchAsync` garde sa signature.

- [ ] **Étape 1 : adapter le test existant et ajouter deux tests**

Dans `SearchAsync_WithIsbnQuery_SearchesIsbnIndexAndMapsEditions`, remplacer

```csharp
        query.Should().Contain("bib.isbn all \"9782070363735\"");
```

par

```csharp
        query.Should().Contain("bib.fuzzyISBN all \"9782070363735\"");
        query.Should().NotContain("bib.isbn ");
```

Puis ajouter ces deux tests, avant `private static HttpResponseMessage CoverResponse()` :

```csharp
    [Fact]
    public async Task SearchAsync_WithIsbn10Query_SearchesFuzzyIsbnIndexWithTheNormalizedIsbn13()
    {
        HttpRequestMessage? capturedRequest = null;
        var client = CreateClient(request =>
        {
            if (request.RequestUri?.Host == "openapi.bnf.fr")
            {
                return CoverResponse();
            }

            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(RecordedPre2007SearchResponse)
            };
        });

        await client.SearchAsync("2-07-051842-6", 1, 20, CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        Uri.UnescapeDataString(capturedRequest!.RequestUri!.Query)
            .Should().Contain("bib.fuzzyISBN all \"9782070518425\"");
    }

    [Fact]
    public async Task SearchAsync_WhenNoticeCarriesHyphenatedIsbn10_ReturnsTheIsbn13Edition()
    {
        var client = CreateClient(request =>
            request.RequestUri?.Host == "openapi.bnf.fr"
                ? CoverResponse()
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(RecordedPre2007SearchResponse)
                });

        var result = await client.SearchAsync("9782070518425", 1, 20, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Isbn13.Should().Be("9782070518425");
        result[0].Title.Should().Be("Harry Potter à l'école des sorciers");
        result[0].PublicationYear.Should().Be(1998);
    }
```

Et ajouter cette réponse enregistrée, après `RecordedSearchResponse`. C'est la forme réelle
d'une notice BnF d'avant 2007 : zone 010 en ISBN-10 à tirets.

```csharp
    private const string RecordedPre2007SearchResponse = """
        <?xml version="1.0" encoding="UTF-8"?>
        <srw:searchRetrieveResponse xmlns:srw="http://www.loc.gov/zing/srw/" xmlns:mxc="http://www.bnf.fr/namespaces/marcxchange/">
          <srw:numberOfRecords>1</srw:numberOfRecords>
          <srw:records>
            <srw:record>
              <srw:recordData>
                <mxc:record>
                  <mxc:datafield tag="010"><mxc:subfield code="a">2-07-051842-6</mxc:subfield></mxc:datafield>
                  <mxc:datafield tag="200"><mxc:subfield code="a">Harry Potter à l'école des sorciers</mxc:subfield></mxc:datafield>
                  <mxc:datafield tag="700">
                    <mxc:subfield code="a">Rowling</mxc:subfield>
                    <mxc:subfield code="b">J. K.</mxc:subfield>
                  </mxc:datafield>
                  <mxc:datafield tag="210">
                    <mxc:subfield code="c">Gallimard</mxc:subfield>
                    <mxc:subfield code="d">1998</mxc:subfield>
                  </mxc:datafield>
                </mxc:record>
              </srw:recordData>
            </srw:record>
          </srw:records>
        </srw:searchRetrieveResponse>
        """;
```

- [ ] **Étape 2 : vérifier les échecs**

```powershell
dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter "FullyQualifiedName~BnfSruSearchClientTests"
```

Attendu : **2 échecs**, `SearchAsync_WithIsbnQuery_SearchesIsbnIndexAndMapsEditions` et
`SearchAsync_WithIsbn10Query_SearchesFuzzyIsbnIndexWithTheNormalizedIsbn13`, sur
l'assertion `bib.fuzzyISBN`.

`SearchAsync_WhenNoticeCarriesHyphenatedIsbn10_ReturnsTheIsbn13Edition` **passe déjà** :
`ReadIsbn13` normalise la zone 010 via `Isbn13.TryCreate`. C'est un test de non-régression
qui fixe le point 1 de la Review Focus. S'il échoue, arrêter et comprendre avant d'aller
plus loin.

- [ ] **Étape 3 : implémenter**

Dans `BnfSruSearchClient.BuildRequestUri`, remplacer

```csharp
        var cql = Isbn13.TryCreate(query, out var isbn13)
            ? $"bib.isbn all \"{isbn13.Value}\""
            : $"bib.anywhere all \"{SanitizeCqlTerm(query)}\"";
```

par

```csharp
        var cql = Isbn13.TryCreate(query, out var isbn13)
            ? BnfSruClient.IsbnQuery(isbn13)
            : $"bib.anywhere all \"{SanitizeCqlTerm(query)}\"";
```

- [ ] **Étape 4 : vérifier**

```powershell
dotnet test src/Backend/Vole_Papillon_Damour.Infrastructure.tests --filter "FullyQualifiedName~Bibliographic"
```

Attendu : tous les tests du dossier `Bibliographic` réussissent, 0 échec.

- [ ] **Étape 5 : commit**

```powershell
rtk git add src/Backend/Vole_Papillon_Damour.Infrastructure/Services/Bibliographic/BnfSruSearchClient.cs src/Backend/Vole_Papillon_Damour.Infrastructure.tests/Bibliographic/BnfSruSearchClientTests.cs
rtk git commit -m "fix(catalog): find pre-2007 BnF editions in reference search by ISBN"
```

---

### Tâche 3 : vérification réelle, documentation, suite complète

**Files :**
- Modify : `docs/bourse-aux-livres/technique/07-integrations-externes.md` (§2, après la ligne `| Recherche | ... |`)
- Modify : `docs/bourse-aux-livres/plan/README.md` (table « Les lots »)

- [ ] **Étape 1 : vérifier contre la vraie BnF**

Quatre ISBN : deux éditions d'avant 2007, une en `979`, une récente.

```powershell
foreach ($isbn in '9782070518425','9782253171676','9791023504637','9782070612758') {
  $q = [uri]::EscapeDataString("bib.fuzzyISBN all `"$isbn`"")
  $x = [xml](Invoke-WebRequest -UseBasicParsing "https://catalogue.bnf.fr/api/SRU?version=1.2&operation=searchRetrieve&query=$q&recordSchema=unimarcXchange&maximumRecords=1").Content
  "$isbn -> $($x.searchRetrieveResponse.numberOfRecords) notice(s)"
}
```

Attendu : **au moins 1 notice pour chacun des quatre ISBN**. Avec l'ancien index
(`bib.isbn`), les deux premiers renvoient 0. Consigner la sortie dans la PR.

- [ ] **Étape 2 : documenter**

Dans `07-integrations-externes.md` §2, ajouter sous le tableau :

```markdown
**Pourquoi `bib.fuzzyISBN` et pas `bib.isbn`.** Les notices des éditions antérieures à
2007 portent un ISBN-10. Interrogé avec l'ISBN-13 normalisé (`RG-01`), l'index
`bib.isbn` ne les retrouve pas ; `bib.fuzzyISBN` rapproche les deux formes. Mesure du
24 septembre 2026 sur 138 éditions de livres connus : `bib.isbn` retrouvait 0 édition
d'avant 2007 sur 55, `bib.fuzzyISBN` retrouve les 138. Les deux clients
(`BnfSruClient`, `BnfSruSearchClient`) passent par `BnfSruClient.IsbnQuery`.
```

Dans `plan/README.md`, ajouter une ligne à la table « Les lots », après la ligne `06` :

```markdown
| [`07`](07-recherche-bnf-isbn10.md) | **Recherche BnF des éditions à ISBN-10** — index `bib.fuzzyISBN` | — | Fin |
```

- [ ] **Étape 3 : suite complète et graphe**

```powershell
dotnet build src/Backend/Vole_Papillon_Damour.slnx
dotnet test src/Backend/Vole_Papillon_Damour.slnx
graphify update .
```

Attendu : build sans erreur, tous les tests réussis. Si un test sans rapport échoue,
vérifier qu'il échoue aussi sur `origin/main` et le signaler dans la PR sans le corriger.

- [ ] **Étape 4 : commit**

```powershell
rtk git add docs/bourse-aux-livres/technique/07-integrations-externes.md docs/bourse-aux-livres/plan/README.md
rtk git diff --cached --check
rtk git commit -m "docs(catalog): explain the BnF fuzzy ISBN lookup"
```

Ne pas commiter les fichiers régénérés par `graphify` s'ils sont ignorés par git ;
sinon, les inclure dans ce commit.
