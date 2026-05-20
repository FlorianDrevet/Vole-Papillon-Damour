using FluentAssertions;
using Vole_Papillon_Damour.Application.Events.Commands.Numeros.AddNumeroToEvent;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Events.Commands.Numeros;

public class AddNumeroToEventCommandValidatorTests
{
    private readonly AddNumeroToEventCommandValidator _validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(90)]
    public void Validate_WhenNumeroIsWithinBounds_IsValid(int numero)
    {
        var command = new AddNumeroToEventCommand(AssoEventsId.CreateUnique(), numero);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(91)]
    public void Validate_WhenNumeroIsOutsideBounds_IsInvalid(int numero)
    {
        var command = new AddNumeroToEventCommand(AssoEventsId.CreateUnique(), numero);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddNumeroToEventCommand.Numero));
    }

    [Fact]
    public void Validate_WhenAssoEventsIdIsNull_IsInvalid()
    {
        var command = new AddNumeroToEventCommand(null!, 25);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(AddNumeroToEventCommand.AssoEventsId));
    }
}