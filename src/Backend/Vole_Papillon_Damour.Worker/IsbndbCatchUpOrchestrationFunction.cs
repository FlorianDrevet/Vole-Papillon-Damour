using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Vole_Papillon_Damour.Application.Isbndb.Commands.CatchUpIsbndbBooks;

namespace Vole_Papillon_Damour.Worker;

public static class IsbndbCatchUpOrchestrationFunction
{
    public const string FunctionName = "IsbndbCatchUpOrchestration";

    [Function(FunctionName)]
    public static Task<CatchUpIsbndbBooksResult> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context) =>
        context.CallActivityAsync<CatchUpIsbndbBooksResult>(
            IsbndbCatchUpFunction.ManualActivityFunctionName,
            string.Empty);
}
