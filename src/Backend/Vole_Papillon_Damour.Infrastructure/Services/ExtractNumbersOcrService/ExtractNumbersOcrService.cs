using Azure.AI.Vision.ImageAnalysis;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Infrastructure.Services.ExtractNumbersOcrService;

public class ExtractNumbersOcrService: IExtractNumbersOcrService
{
    private readonly IOcrService _ocrService;
    
    public ExtractNumbersOcrService(IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public async Task<List<BingoCard>> ExtractBingoCards(Stream stream, CancellationToken cancellationToken)
    {
        var imageAnalysisResult = await _ocrService.ExtractTextFromImage(stream, cancellationToken);
        var casesCard = imageAnalysisResult.Read.Blocks.First().Lines
            .Select(line => new Case(line.Text, null, line.BoundingPolygon[0], line.BoundingPolygon[1],
                line.BoundingPolygon[2], line.BoundingPolygon[3]))
            .ToList();
        var casesWithNumbers = GetOnlyCasesWithNumbers(casesCard);
        var highestCase = casesWithNumbers.OrderByDescending(c => c.Height).First();
        var onlyHighestCase = casesWithNumbers.Where(c => c.Height >= (highestCase.Height * 3 / 5)).ToList();
        var multipleCases = SplitMultipleCases(onlyHighestCase);
        
        var bingoCards = new List<BingoCard>();
        while (multipleCases.Any())
        {
            var bingoCard = GetBingoCard(multipleCases);
            bingoCards.Add(bingoCard.Item1);
            multipleCases = bingoCard.Item2;
        }
        
        return bingoCards;
    }
    
    private bool CaseContainsNumber(Case caseCard)
    {
        return caseCard.Text.Any(char.IsDigit);
    }
    
    private IReadOnlyList<Case> GetOnlyCasesWithNumbers(IReadOnlyList<Case> cases)
    {
        return cases.Where(CaseContainsNumber).ToList();
    }

    private IReadOnlyList<Case> SplitMultipleCases(IReadOnlyList<Case> cases)
    {
        List<Case> res = new List<Case>();
        
        foreach (var caseBingo in cases)
        {
            var text = new string(caseBingo.Text.Select(c => char.IsDigit(c) ? c : ' ').ToArray());
            var numbers = text.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (numbers.Any(n => n.Length > 2))
            {
                var newNumbers = new List<string>();
                foreach (var n in numbers)
                {
                    if (n.Length > 2)
                    {
                        for (int i = 0; i < n.Length; i += 2)
                        {
                            newNumbers.Add(n.Substring(i, Math.Min(2, n.Length - i)));
                        }
                    }
                    else
                    {
                        newNumbers.Add(n);
                    }
                }

                numbers = newNumbers;
            }
            foreach (var t in numbers)
            {
                res.Add(new Case(t, int.Parse(t), caseBingo.BottomLeftCorner, caseBingo.BottomRightCorner,
                    caseBingo.TopRightCorner, caseBingo.TopLeftCorner));
            }
        }

        return res;
    }

    private Tuple<BingoCard, IReadOnlyList<Case>> GetBingoCard(IReadOnlyList<Case> cases)
    {
        int index = 5;
        var casesLeft = new List<Case>();
        var bingoCardCases = cases.Take(5).ToList();
        
        var positionX = bingoCardCases.Last().BottomRightCorner.X;

        while (index < cases.Count && bingoCardCases.Count < 15)
        {
            var caseBingo = cases[index];
            if (caseBingo.BottomLeftCorner.X > positionX)
            {
                casesLeft.Add(caseBingo);
                index++;
            }
            else
            {
                var nextLine = cases.Skip(index).Take(5).ToList();
                bingoCardCases.AddRange(nextLine);
                index += 5;
            }
        }
        
        casesLeft.AddRange(cases.Skip(index));
        
        var bingoCard = new BingoCard(
            bingoCardCases.Take(5).Select(c => c.Number).ToArray(),
            bingoCardCases.Skip(5).Take(5).Select(c => c.Number).ToArray(),
            bingoCardCases.Skip(10).Take(5).Select(c => c.Number).ToArray()
        );
        
        return new Tuple<BingoCard, IReadOnlyList<Case>>(bingoCard, casesLeft);
    }
    
    private class Case
    {
        public Case(string text, int? number, ImagePoint bottomLeftCorner, ImagePoint bottomRightCorner, ImagePoint topRightCorner, ImagePoint topLeftCorner)
        {
            Text = text;
            Number = number;
            BottomLeftCorner = bottomLeftCorner;
            BottomRightCorner = bottomRightCorner;
            TopRightCorner = topRightCorner;
            TopLeftCorner = topLeftCorner;
        }
        
        public float Height => Math.Abs(TopLeftCorner.Y - BottomLeftCorner.Y);

        public string Text { get; set; }
        public int? Number { get; set; }
        public ImagePoint BottomLeftCorner { get; set; }
        public ImagePoint BottomRightCorner { get; set; }
        public ImagePoint TopRightCorner { get; set; }
        public ImagePoint TopLeftCorner { get; set; }
    }
}