using ErrorOr;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.Common.Errors;
public static partial class Errors
{
    public static class AssoEvent
    {
        public static Error AssoEventNotFound(AssoEventsId id) => Error.NotFound(
            code: "AssoEvent.NotFound",
            description: "AssoEvent not found with id: " + id.Value
        );
        
        public static Error AssoEventNextBingoNotFound() => Error.NotFound(
            code: "AssoEvent.NotFound",
            description: "AssoEvent not found for next bingo"
        );
        
        public static Error AssoEventNextBooksNotFound() => Error.NotFound(
            code: "AssoEvent.NotFound",
            description: "AssoEvent not found for next books"
        );
        
        public static Error AssoEventNextOtherEventNotFound() => Error.NotFound(
            code: "AssoEvent.NotFound",
            description: "AssoEvent not found for next other event"
        );
        
        public static Error CantRemoveBingoNumero(AssoEventsId id, int numero) => Error.NotFound(
            code: "AssoEvent.BingoNumero",
            description: "AssoEvent: " + id.Value + " can not remove numero bingo: " + numero
        );
        
        public static class Partie
        {
            public static Error PartieNotFound(AssoEventsId assoEventsId, PartieId id) => Error.NotFound(
                code: "AssoEvent.Partie.NotFound",
                description: "Partie not found with id: " + id.Value + " in assoEvent " + assoEventsId.Value
            );

            public static Error NumeroAlreadyExists(AssoEventsId commandAssoEventsId, PartieId commandPartieId, int commandNumero)
            {
                return Error.Conflict(
                    code: "AssoEvent.Partie.NumeroAlreadyExists",
                    description: "Numero " + commandNumero + " already exists in partie " + commandPartieId.Value +
                                 " in assoEvent " + commandAssoEventsId.Value
                );
            }
            
            public static Error NoLastNumeros(AssoEventsId assoEventsId, PartieId id) => Error.NotFound(
                code: "AssoEvent.Partie.LastNumero",
                description: "Partie with id: " + id.Value + " in assoEvent " + assoEventsId.Value + " does not have last numero."
            );

            public static Error PartieWithIndexNotFound(AssoEventsId assoEventsId, int index) => Error.NotFound(
                code: "AssoEvent.Partie.WithIndexNotFound",
                description: "Partie with index: " + index + " in assoEvent " + assoEventsId.Value + " does not exist."
            );

            public static class LinePartie
            {
                public static Error PartieLineNotFound(AssoEventsId assoEventsId, PartieId partieId, LinePartieId id) => Error.NotFound(
                    code: "AssoEvent.Partie.NotFound",
                    description: "LinePartie not found with id: " + id.Value + 
                                 " in partie " + partieId.Value +
                                 " in assoEvent " + assoEventsId.Value
                );
                
                public static class Lot
                {
                    public static Error LotNotFound(AssoEventsId assoEventsId, PartieId partieId, LinePartieId linePartieId, LotId id) => Error.NotFound(
                        code: "AssoEvent.Partie.NotFound",
                        description: "Lot not found with id: " + id.Value + 
                                     " in partie " + partieId.Value +
                                     " in linePartie " + linePartieId.Value +
                                     " in assoEvent " + assoEventsId.Value
                    );
                }
            }
        }
    }
}
