using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Domain.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void Email_IsTrimmedAndLowerCased()
    {
        var email = Email.Create("  Ann@Example.COM ");

        Assert.Equal("ann@example.com", email.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("@example.com")]
    [InlineData("a@@example.com")]
    [InlineData("a@b@example.com")]
    [InlineData("a@nodot")]
    [InlineData("a@.com")]
    [InlineData("a@example.")]
    [InlineData("a b@example.com")]
    public void Email_RejectsInvalid(string? value)
    {
        var ex = Assert.Throws<InvalidEmailException>(() => Email.Create(value));

        Assert.Equal("user.invalid_email", ex.Code);
    }

    [Fact]
    public void Email_RejectsTooLong()
    {
        var value = new string('a', 250) + "@b.cc";

        Assert.Throws<InvalidEmailException>(() => Email.Create(value));
    }

    [Fact]
    public void Email_EqualityIsByNormalizedValue() =>
        Assert.Equal(Email.Create("A@b.cc"), Email.Create("a@B.cc"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DisplayName_RejectsBlank(string? value)
    {
        var ex = Assert.Throws<InvalidDisplayNameException>(() => new DisplayName(value));

        Assert.Equal("user.invalid_display_name", ex.Code);
    }

    [Fact]
    public void DisplayName_RejectsOver100Characters() =>
        Assert.Throws<InvalidDisplayNameException>(() => new DisplayName(new string('x', 101)));

    [Fact]
    public void DisplayName_Accepts100CharactersAndTrims()
    {
        Assert.Equal(100, new DisplayName(new string('x', 100)).Value.Length);
        Assert.Equal("Ann", new DisplayName("  Ann ").Value);
    }

    [Fact]
    public void UserId_NewIsParsableGuid()
    {
        var id = UserId.New();

        Assert.True(UserId.TryParse(id.Value, out var parsed));
        Assert.Equal(id, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void UserId_RejectsInvalid(string? value)
    {
        Assert.False(UserId.TryParse(value, out _));
        var ex = Assert.Throws<InvalidUserIdException>(() => UserId.From(value));
        Assert.Equal("user.invalid_id", ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    public void PasswordPolicy_RejectsWeak(string? password)
    {
        var ex = Assert.Throws<WeakPasswordException>(() => PasswordPolicy.EnsureStrong(password));

        Assert.Equal("user.weak_password", ex.Code);
    }

    [Fact]
    public void PasswordPolicy_AcceptsLetterAndDigitOfMinLength() =>
        PasswordPolicy.EnsureStrong("abcdefg1");
}
