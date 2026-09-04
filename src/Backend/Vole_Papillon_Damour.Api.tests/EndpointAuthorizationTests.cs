using FluentAssertions;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Vole_Papillon_Damour.Api.Controllers;
using Vole_Papillon_Damour.Api.Controllers.AssoEventsController;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Api.tests;

/// <summary>
/// Les points d'entrée sont déclarés un par un, et l'autorisation s'ajoute à la
/// main sur chacun : rien ne signale un oubli. `DELETE /asso-events/{id}` et
/// `DELETE /product/{productId}` sont ainsi restés ouverts à tous, alors que la
/// création et la modification voisines étaient bien protégées — n'importe qui
/// connaissant un identifiant pouvait supprimer un évènement ou un produit.
///
/// Ce test parcourt la table de routage réellement construite et refuse toute
/// écriture non protégée, plutôt que de vérifier les deux cas connus.
/// </summary>
public class EndpointAuthorizationTests
{
    private static readonly string[] MutatingMethods = ["POST", "PUT", "PATCH", "DELETE"];

    /// <summary>
    /// Écritures ouvertes, et pourquoi. Toute nouvelle entrée doit être justifiée
    /// ici : c'est le seul endroit où l'absence d'autorisation est un choix.
    /// </summary>
    private static readonly Dictionary<string, string> KnownPublicWrites = new()
    {
        // Délivre les jetons de l'authentification interne : il ne peut pas en exiger un.
        ["/auth/login"] = "point d'entrée de connexion",

        // Ouvert, et probablement à tort : `.RequireAuthorization("IsAdmin")` y est
        // commenté dans AuthenticationController. N'importe qui peut donc créer un
        // compte sur le mécanisme d'authentification interne. Laissé en l'état ici
        // faute de savoir ce qui en dépend encore (application de caisse, amorçage
        // du premier compte) ; à trancher avec le reste de la migration vers Entra.
        ["/auth/register"] = "création de compte héritée, à trancher",
    };

    [Fact]
    public void Every_mutating_endpoint_requires_authorization()
    {
        var unprotected = MutatingEndpoints()
            .Where(endpoint => !RequiresAuthorization(endpoint))
            .Where(endpoint => !KnownPublicWrites.ContainsKey(RouteOf(endpoint)))
            .Select(Describe)
            .ToList();

        unprotected.Should().BeEmpty(
            "toute écriture doit exiger une autorisation ; ajoutez `.RequireAuthorization(...)` "
            + "ou inscrivez le point d'entrée dans KnownPublicWrites en expliquant pourquoi");
    }

    [Fact]
    public void Deleting_an_event_or_a_product_requires_authorization()
    {
        var deletions = MutatingEndpoints()
            .Where(endpoint => RouteOf(endpoint) is "/asso-events/{id}" or "/product/{productId}")
            .Where(endpoint => HttpMethods(endpoint).Contains("DELETE"))
            .ToList();

        deletions.Should().HaveCount(2, "les deux suppressions doivent rester déclarées");
        deletions.Should().OnlyContain(endpoint => RequiresAuthorization(endpoint));
    }

    [Fact]
    public void Public_catalog_reads_are_anonymous()
    {
        var expectedRoutes = new[]
        {
            "/catalog/search",
            "/catalog/books/{isbn13}",
            "/catalog/fairs/next",
            "/catalog/works/{workId}",
            "/catalog/sitemap.xml",
        };

        var publicCatalogEndpoints = RegisteredEndpoints()
            .Where(endpoint => expectedRoutes.Contains(RouteOf(endpoint)))
            .ToList();

        publicCatalogEndpoints.Should().HaveSameCount(expectedRoutes);
        publicCatalogEndpoints.Should().OnlyContain(endpoint => !RequiresAuthorization(endpoint));
    }

    [Fact]
    public void Dead_stock_read_requires_administration()
    {
        var endpoint = RegisteredEndpoints()
            .Single(endpoint => RouteOf(endpoint) == "/books/admin/dead-stock");

        RequiresAuthorization(endpoint).Should().BeTrue();
        endpoint.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Should()
            .Contain(data => data.Policy == "Administration");
    }

    [Fact]
    public void Member_watchlist_endpoints_require_an_authenticated_member()
    {
        var expectedRoutes = new[]
        {
            "/catalog/me/watchlist",
            "/catalog/me/watchlist/{itemId:guid}",
        };

        var memberEndpoints = RegisteredEndpoints()
            .Where(endpoint => expectedRoutes.Contains(RouteOf(endpoint)))
            .ToList();

        memberEndpoints.Should().HaveCount(3);
        memberEndpoints.Should().OnlyContain(endpoint => RequiresAuthorization(endpoint));
        memberEndpoints
            .Where(endpoint => RouteOf(endpoint) == "/catalog/me/watchlist")
            .SelectMany(endpoint => HttpMethods(endpoint))
            .Should()
            .BeEquivalentTo("GET", "POST");
    }

    private static IReadOnlyList<RouteEndpoint> MutatingEndpoints() =>
        RegisteredEndpoints()
            .Where(endpoint => HttpMethods(endpoint).Intersect(MutatingMethods).Any())
            .ToList();

    /// <summary>
    /// Construit la table de routage sans démarrer l'application : les délégués ne
    /// sont pas exécutés, aucune base ni aucun service externe n'est nécessaire.
    /// </summary>
    private static IReadOnlyList<RouteEndpoint> RegisteredEndpoints()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();

        // Pour construire un point d'entrée, le routage doit savoir distinguer un
        // paramètre de service d'un paramètre de corps de requête. Des doublures
        // suffisent : aucun délégué n'est exécuté ici. Si une nouvelle dépendance
        // apparaît dans une signature, ce test le dira.
        builder.Services.AddSingleton(Substitute.For<IMediator>());
        builder.Services.AddSingleton(Substitute.For<IMapper>());
        builder.Services.AddSingleton(Substitute.For<ISSEClientManager>());
        builder.Services.AddSingleton(Substitute.For<IAccountDeletionService>());

        var application = builder.Build();
        application.UseRouting();

        application.UseAuthenticationController();
        application.UseAccountController();
        application.UseBookController();
        application.UseActualityController();
        application.UseProductController();
        application.UseOrdersController();
        application.UseEventsController();

        return application.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static IReadOnlyCollection<string> HttpMethods(Endpoint endpoint) =>
        endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];

    private static bool RequiresAuthorization(Endpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;

    private static string RouteOf(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText ?? string.Empty;

    private static string Describe(RouteEndpoint endpoint) =>
        $"{string.Join('/', HttpMethods(endpoint))} {RouteOf(endpoint)}";
}
