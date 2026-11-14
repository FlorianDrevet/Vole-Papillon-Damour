using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.FairDigest.Queries.PreviewFairDigest;

namespace Vole_Papillon_Damour.Application.FairDigest.Commands.SendFairDigestTest;

public sealed class SendFairDigestTestCommandHandler(
    IProjectDbContext dbContext,
    IMediator mediator,
    IOptions<FairDigestOptions> options,
    IRecommendationSettings recommendationSettings,
    IFairDigestEmailSender emailSender)
    : IRequestHandler<SendFairDigestTestCommand, ErrorOr<FairDigestTestSendResult>>
{
    public async Task<ErrorOr<FairDigestTestSendResult>> Handle(
        SendFairDigestTestCommand command,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !recommendationSettings.Enabled || !emailSender.IsEnabled)
        {
            return Error.Validation("FairDigest.Disabled", "The fair digest email is disabled.");
        }

        var admin = await dbContext.Users
            .SingleOrDefaultAsync(user => user.Id == command.AdministratorId, cancellationToken);
        if (admin is null || string.IsNullOrWhiteSpace(admin.Email))
        {
            return Error.Validation("FairDigest.AdministratorEmailRequired", "A valid administrator email is required.");
        }

        var preview = await mediator.Send(
            new PreviewFairDigestQuery(command.AdministratorId),
            cancellationToken);
        if (preview.IsError)
        {
            return preview.Errors;
        }

        if (preview.Value.Content.SkippedNoContent)
        {
            return Error.Validation("FairDigest.NoPreviewContent", "There is no content to send in the preview.");
        }

        await emailSender.SendAsync(
            new FairDigestEmailMessage(
                preview.Value.CampaignId,
                admin.Id,
                admin.Email,
                admin.Name?.FirstName,
                preview.Value.Fair,
                preview.Value.Content),
            cancellationToken);

        return new FairDigestTestSendResult(admin.Email, preview.Value.IsExample);
    }
}
