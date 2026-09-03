using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using AssociationSettingsEntity = Vole_Papillon_Damour.Domain.AssociationSettingsAggregate.AssociationSettings;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetAssociationSettings;

public sealed class GetAssociationSettingsQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetAssociationSettingsQuery, ErrorOr<AssociationSettingsResult>>
{
    private static readonly DateTime DefaultUpdatedAt =
        new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task<ErrorOr<AssociationSettingsResult>> Handle(
        GetAssociationSettingsQuery query,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.AssociationSettings
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettingsEntity.SingletonId,
                cancellationToken);

        if (settings is null)
        {
            settings = AssociationSettingsEntity.Create(
                UserId.Create(Guid.Empty),
                DefaultUpdatedAt);
        }

        return AssociationSettingsResult.From(settings);
    }
}
