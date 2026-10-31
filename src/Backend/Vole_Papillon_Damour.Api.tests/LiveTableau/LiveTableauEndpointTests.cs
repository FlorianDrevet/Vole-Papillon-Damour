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
using Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;
using Vole_Papillon_Damour.Application.Events.Commands.Events.UpdateEvent;
using Vole_Papillon_Damour.Application.Events.Queries.GetAssoEventById;
using Vole_Papillon_Damour.Application.Events.Queries.GetTodayEvent;
using Vole_Papillon_Damour.Contracts.Events.Requests;
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
        loto.SetLiveTheme(new LiveTheme(LiveTheme.LiveThemeEnum.Halloween));
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
        body.Should().Contain("id: 4\n");
        body.Should().Contain("retry: 3000\n");
        body.Should().Contain("data: {\"id\":\"" + loto.Id.Value);
        body.Should().Contain("\"liveRevision\":4");
        body.Should().Contain("\"liveTheme\":\"Halloween\"");
        hub.GetWatchedEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task GetEventById_ReturnsLiveTheme()
    {
        var loto = LiveTableauTestEvents.CreateLoto();
        loto.SetLiveTheme(new LiveTheme(LiveTheme.LiveThemeEnum.Halloween));
        var mapper = LiveTableauTestEvents.CreateMapper();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetAssoEventByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns((ErrorOr<AssoEventResult>)mapper.Map<AssoEventResult>(loto));
        await using var application = CreateApplication(mediator, new LiveTableauHub(), mapper);

        var context = await InvokeAsync(application, "/asso-events/{id}", loto.Id.Value, TimeSpan.FromSeconds(5));
        var json = JsonNode.Parse(await ReadBodyAsync(context))!.AsObject();

        json["liveTheme"]!.GetValue<string>().Should().Be("Halloween");
    }

    [Fact]
    public void UpdateRequest_MapsLiveThemeIntoCommand()
    {
        var mapper = LiveTableauTestEvents.CreateMapper();
        var request = new UpdateEventRequest { LiveTheme = "Halloween" };

        var command = mapper.Map<UpdateEventCommand>((request, Guid.Parse("9a25d34c-7a08-4b33-b761-ec532ad6950d")));

        command.LiveTheme.Should().Be(new LiveTheme(LiveTheme.LiveThemeEnum.Halloween));
    }

    [Fact]
    public void CreateRequest_MapsLiveThemeIntoCommand()
    {
        var mapper = LiveTableauTestEvents.CreateMapper();
        var request = new CreateEventRequest { EventType = "Bingo", LiveTheme = "Halloween" };

        var command = mapper.Map<CreateEventCommand>(request);

        command.LiveTheme.Should().Be(new LiveTheme(LiveTheme.LiveThemeEnum.Halloween));
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
                42,
                new LiveTheme(LiveTheme.LiveThemeEnum.Halloween)));
        await using var application = CreateApplication(mediator, new LiveTableauHub(), LiveTableauTestEvents.CreateMapper());

        var context = await InvokeAsync(application, "/asso-events/today", null, TimeSpan.FromSeconds(5));
        var json = JsonNode.Parse(await ReadBodyAsync(context))!.AsObject();

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
        json["id"]!.GetValue<Guid>().Should().Be(id.Value);
        json["eventType"]!.GetValue<string>().Should().Be("Bingo");
        json["liveStatus"]!.GetValue<string>().Should().Be("Live");
        json["lastNumero"]!.GetValue<int>().Should().Be(42);
        json["liveTheme"]!.GetValue<string>().Should().Be("Halloween");
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
