using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services;

namespace Vole_Papillon_Damour.Infrastructure.tests.Services;

public class SSEClientManagerTests
{
    [Fact]
    public async Task SendToEvent_WhenClientsWatchDifferentEvents_WritesOnlyToMatchingEventClients()
    {
        var manager = CreateManager();
        var firstEventId = AssoEventsId.CreateUnique();
        var secondEventId = AssoEventsId.CreateUnique();
        using var firstClientStream = new MemoryStream();
        using var secondClientStream = new MemoryStream();
        using var firstClientWriter = new StreamWriter(firstClientStream, Encoding.UTF8, leaveOpen: true);
        using var secondClientWriter = new StreamWriter(secondClientStream, Encoding.UTF8, leaveOpen: true);
        const string message = "{\"event\":\"first\"}";

        manager.AddClient(firstEventId, "first-client", firstClientWriter);
        manager.AddClient(secondEventId, "second-client", secondClientWriter);

        await manager.SendToEvent(firstEventId, message);

        ReadUtf8(firstClientStream).Should().Be($"data: {message}\n\n");
        ReadUtf8(secondClientStream).Should().BeEmpty();
    }

    [Fact]
    public async Task SendToClient_WhenClientExists_WritesOnlyThatClient()
    {
        var manager = CreateManager();
        var eventId = AssoEventsId.CreateUnique();
        using var firstClientStream = new MemoryStream();
        using var secondClientStream = new MemoryStream();
        using var firstClientWriter = new StreamWriter(firstClientStream, Encoding.UTF8, leaveOpen: true);
        using var secondClientWriter = new StreamWriter(secondClientStream, Encoding.UTF8, leaveOpen: true);
        const string message = "{\"direct\":true}";

        manager.AddClient(eventId, "first-client", firstClientWriter);
        manager.AddClient(eventId, "second-client", secondClientWriter);

        await manager.SendToClient("second-client", message);

        ReadUtf8(firstClientStream).Should().BeEmpty();
        ReadUtf8(secondClientStream).Should().Be($"data: {message}\n\n");
    }

    [Fact]
    public async Task AddClient_WhenSameClientMovesToAnotherEvent_RemovesPreviousEventRegistration()
    {
        var manager = CreateManager();
        var firstEventId = AssoEventsId.CreateUnique();
        var secondEventId = AssoEventsId.CreateUnique();
        using var firstClientStream = new MemoryStream();
        using var secondClientStream = new MemoryStream();
        using var firstClientWriter = new StreamWriter(firstClientStream, Encoding.UTF8, leaveOpen: true);
        using var secondClientWriter = new StreamWriter(secondClientStream, Encoding.UTF8, leaveOpen: true);

        manager.AddClient(firstEventId, "moving-client", firstClientWriter);
        manager.AddClient(secondEventId, "moving-client", secondClientWriter);

        await manager.SendToEvent(firstEventId, "first");
        await manager.SendToEvent(secondEventId, "second");

        ReadUtf8(firstClientStream).Should().BeEmpty();
        ReadUtf8(secondClientStream).Should().Be("data: second\n\n");
    }

    [Fact]
    public async Task SendToEvent_WhenClientWriteFails_RemovesFailingClientAndContinuesBroadcast()
    {
        var manager = CreateManager();
        var eventId = AssoEventsId.CreateUnique();
        using var failingStream = new ThrowingWriteStream();
        using var failingWriter = new StreamWriter(failingStream, Encoding.UTF8, leaveOpen: true);
        using var healthyClientStream = new MemoryStream();
        using var healthyWriter = new StreamWriter(healthyClientStream, Encoding.UTF8, leaveOpen: true);

        manager.AddClient(eventId, "failing-client", failingWriter);
        manager.AddClient(eventId, "healthy-client", healthyWriter);

        await manager.SendToEvent(eventId, "first");
        await manager.SendToEvent(eventId, "second");

        failingStream.WriteAttempts.Should().Be(1);
        ReadUtf8(healthyClientStream).Should().Be("data: first\n\ndata: second\n\n");
    }

    [Fact]
    public async Task RemoveClient_WhenClientWasRegistered_StopsFutureWrites()
    {
        var manager = CreateManager();
        var eventId = AssoEventsId.CreateUnique();
        using var clientStream = new MemoryStream();
        using var clientWriter = new StreamWriter(clientStream, Encoding.UTF8, leaveOpen: true);

        manager.AddClient(eventId, "client", clientWriter);
        manager.RemoveClient("client");

        await manager.SendToEvent(eventId, "message");
        await manager.SendToClient("client", "direct");

        ReadUtf8(clientStream).Should().BeEmpty();
    }

    private static SSEClientManager CreateManager()
    {
        return new SSEClientManager(Substitute.For<ILogger<SSEClientManager>>());
    }

    private static string ReadUtf8(MemoryStream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private sealed class ThrowingWriteStream : MemoryStream
    {
        public int WriteAttempts { get; private set; }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteAttempts++;
            throw new IOException("SSE client disconnected.");
        }
    }
}
