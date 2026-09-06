using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class SetBookFairRevenueCommandHandler(
    IProjectDbContext dbContext)
    : IRequestHandler<SetBookFairRevenueCommand, ErrorOr<AdminFairResult>>
{
    public async Task<ErrorOr<AdminFairResult>> Handle(
        SetBookFairRevenueCommand command,
        CancellationToken cancellationToken)
    {
        if (command.FairId is null || command.FairId.Value == Guid.Empty ||
            command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Errors.Book.FairNotFound(command.FairId?.Value ?? Guid.Empty);
        }

        if (command.Revenue is < 0 ||
            (command.Revenue is not null && decimal.Round(command.Revenue.Value, 2) != command.Revenue.Value))
        {
            return Errors.Book.InvalidRevenue();
        }

        var fair = await dbContext.AssoEvents.SingleOrDefaultAsync(
            candidate => candidate.Id == command.FairId,
            cancellationToken);
        if (fair is null)
        {
            return Errors.Book.FairNotFound(command.FairId.Value);
        }

        if (fair.EventsType?.Value != EventsType.EventsTypeEnum.Books)
        {
            return Errors.Book.TargetFairMustBeBooks();
        }

        fair.SetBookRevenue(command.Revenue);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AdminFairResult(
            fair.Id.Value,
            fair.Name,
            fair.DateStart,
            fair.DateEnd,
            fair.IsCancelled,
            fair.BookRevenue);
    }
}
