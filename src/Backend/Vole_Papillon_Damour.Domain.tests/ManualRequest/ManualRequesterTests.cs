using FluentAssertions;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.ManualRequest;

public sealed class ManualRequesterTests
{
    private static readonly UserId Admin = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
    private static readonly DateTime At = new(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public void Create_WithConsent_TrimsNamesAndNormalizesPhone()
    {
        var requester = ManualRequester.Create(Id, "  Martin ", " Jeanne", "06 12.34-56 78", consentGiven: true, Admin, At);

        requester.LastName.Should().Be("Martin");
        requester.FirstName.Should().Be("Jeanne");
        requester.Phone.Should().Be("0612345678");
        requester.ConsentRecordedAt.Should().Be(At);
        requester.ConsentRecordedBy.Should().Be(Admin);
        requester.IsErased.Should().BeFalse();
    }

    [Fact]
    public void Create_WithoutConsent_Throws()
    {
        var act = () => ManualRequester.Create(Id, "Martin", "Jeanne", "0612345678", consentGiven: false, Admin, At);

        act.Should().Throw<ArgumentException>().WithParameterName("consentGiven");
    }

    [Theory]
    [InlineData("+33 6 12 34 56 78", "+33612345678")]
    [InlineData("(02) 41 22 18 90", "0241221890")]
    public void NormalizePhone_AcceptsFrenchAndInternationalNumbers(string input, string expected)
    {
        ManualRequester.NormalizePhone(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("06 12 34 56 7")]
    [InlineData("06-12-34-56-78 poste 2")]
    [InlineData("+33 6 12 34 56 78 90 12 34")]
    public void NormalizePhone_RejectsInvalidNumbers(string input)
    {
        var act = () => ManualRequester.NormalizePhone(input);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Erase_ClearsContactAndBlocksUpdates()
    {
        var requester = ManualRequester.Create(Id, "Martin", "Jeanne", "0612345678", true, Admin, At);

        requester.Erase(At.AddDays(90));

        requester.IsErased.Should().BeTrue();
        requester.LastName.Should().BeNull();
        requester.FirstName.Should().BeNull();
        requester.Phone.Should().BeNull();
        requester.ErasedAt.Should().Be(At.AddDays(90));
        var act = () => requester.UpdateContact("Durand", "Paul", "0612345678", At.AddDays(91));
        act.Should().Throw<InvalidOperationException>();
    }
}
