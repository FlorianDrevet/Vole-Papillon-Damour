using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Http;
using Vole_Papillon_Damour.Application.Events.Common;

namespace Vole_Papillon_Damour.Application.BingoCard.Commands.AnalyzeBingoCard;

public record AnalyzeBingoCardCommand(
    IFormFile? Image
) : IRequest<ErrorOr<List<BingoCardResult>>>;