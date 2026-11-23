using ONA.Identity.Infrastructure.Security;

namespace ONA.Identity.Infrastructure.Tests;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("secret123");

        Assert.True(_hasher.Verify(hash, "secret123"));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("secret123");

        Assert.False(_hasher.Verify(hash, "secret124"));
    }

    [Fact]
    public void Hash_SamePasswordTwice_UsesDifferentSalts()
    {
        Assert.NotEqual(_hasher.Hash("secret123"), _hasher.Hash("secret123"));
    }

    [Fact]
    public void Hash_HasVersionedFormatAndNeverContainsPlaintext()
    {
        var hash = _hasher.Hash("secret123");

        var parts = hash.Split('.');
        Assert.Equal(4, parts.Length);
        Assert.Equal("v1", parts[0]);
        Assert.True(int.Parse(parts[1]) >= 100_000);
        Assert.DoesNotContain("secret123", hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("v2.1.AA==.AA==")]
    [InlineData("v1.abc.AA==.AA==")]
    [InlineData("v1.1000.!!!.!!!")]
    public void Verify_MalformedHash_ReturnsFalse(string hash)
    {
        Assert.False(_hasher.Verify(hash, "secret123"));
    }
}
