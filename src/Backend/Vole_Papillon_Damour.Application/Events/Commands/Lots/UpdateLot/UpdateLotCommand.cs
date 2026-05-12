using System.Net.Mime;
using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Http;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.Lots.UpdateLot;

public record UpdateLotCommand(
    string Name,
    IFormFile? Image,
    Uri? ImageUri,
    AssoEventsId AssoEventsId,
    PartieId PartieId,
    LinePartieId LinePartieId,
    LotId LotId
) : IRequest<ErrorOr<AssoEventResult>>;