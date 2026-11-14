using System.Reflection;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Worker;
using Xunit;

namespace Vole_Papillon_Damour.Worker.Tests;

public sealed class FairDigestFunctionTests
{
    [Fact]
    public void Timer_uses_the_configured_fair_digest_schedule()
    {
        var method = typeof(FairDigestFunction).GetMethod(nameof(FairDigestFunction.Run))
            ?? throw new InvalidOperationException("The fair digest timer function was not found.");
        var timerTrigger = method.GetParameters()
            .SelectMany(parameter => parameter.GetCustomAttributes<TimerTriggerAttribute>())
            .Should().ContainSingle().Subject;

        timerTrigger.Schedule.Should().Be("%FairDigest:Schedule%");
    }

    [Fact]
    public async Task Run_DoesNotCreateACommandScopeWhenFairDigestIsDisabled()
    {
        var scopeFactory = new CountingScopeFactory();
        var function = new FairDigestFunction(
            scopeFactory,
            NullLogger<FairDigestFunction>.Instance,
            Options.Create(new FairDigestOptions { Enabled = false }));

        await function.Run(null!, CancellationToken.None);

        scopeFactory.CreateScopeCalls.Should().Be(0);
    }

    private sealed class CountingScopeFactory : IServiceScopeFactory
    {
        public int CreateScopeCalls { get; private set; }

        public IServiceScope CreateScope()
        {
            CreateScopeCalls++;
            throw new InvalidOperationException("A disabled timer must not open a dependency scope.");
        }
    }
}
