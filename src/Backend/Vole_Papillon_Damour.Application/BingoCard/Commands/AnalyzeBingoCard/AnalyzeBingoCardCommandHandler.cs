using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.BingoCard.Commands.AnalyzeBingoCard;

public class AnalyzeBingoCardCommandHandler(IExtractNumbersOcrService extractNumbersOcrService, IMapper mapper)
    : IRequestHandler<AnalyzeBingoCardCommand, ErrorOr<List<BingoCardResult>>>
{
    public async Task<ErrorOr<List<BingoCardResult>>> Handle(AnalyzeBingoCardCommand command, CancellationToken cancellationToken)
    {
        await using var stream = command.Image?.OpenReadStream();
        
        if (stream is null)
        {
            return Errors.BingoCard.CannotOpenImage();
        }
        
        var bingoCards = await extractNumbersOcrService.ExtractBingoCards(stream, cancellationToken);
        return mapper.Map<List<BingoCardResult>>(bingoCards);
    }
}