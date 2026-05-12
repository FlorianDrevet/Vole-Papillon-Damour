using FluentValidation;
using Vole_Papillon_Damour.Application.Events.Commands.Lots.CreateLot;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.CreateEvent;

public class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.Description).NotEmpty()
            .WithMessage("Description should not be empty");
        RuleFor(x => x.Description).NotEmpty()
            .WithMessage("Description should not be empty");
        RuleFor(x => x.Description).NotEmpty()
            .WithMessage("Description should not be empty");
        
        //TODO Image not empty when type is OTHER
        
        RuleFor(x => x.Parties)
            .ForEach(p => p.SetValidator(new CreatePartieCommandValidator()));
        
        //TODO Validate Indexes of Parties
    }
}

public class CreatePartieCommandValidator : AbstractValidator<CreatePartiesCommand>
{
    public CreatePartieCommandValidator()
    {
        RuleFor(x => x.PartieType).NotEmpty()
            .WithMessage("PartieType should not be empty");
        
        RuleFor(x => x.Index)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Index should not be negative");
        RuleFor(x => x.LineParties)
            .ForEach(x => x.SetValidator(new CreateLinePartieCommandValidator()));
    }
}

public class CreateLinePartieCommandValidator : AbstractValidator<CreateLinePartiCommand>
{
    public CreateLinePartieCommandValidator()
    {
        RuleFor(x => x.Index)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Index should be greater or equal to 0");

        RuleFor(x => x.NumberLine)
            .NotEmpty()
            .WithMessage("NumberLine should not be empty");

        RuleFor(x => x.Lots)
            .ForEach(x => x.SetValidator(new CreateLotCommandValidator()));
    } 
}


public class CreateLotCommandValidator : AbstractValidator<CreateLotsCommand>
{
    public CreateLotCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty()
            .WithMessage("Name should not be empty");
        RuleFor(x => x.ImageName).NotEmpty()
            .WithMessage("Image should not be empty");
        RuleFor(x => x.ImageStream).NotEmpty()
            .WithMessage("Image Length should not be 0");
        RuleFor(x => x.Index)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Index should not be negative");

        //TODO Validate Indexes of Lots
        //TODO Validator NumberLine with PartieType
    }
}