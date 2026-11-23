using ONA.Identity.Infrastructure.Security;

namespace ONA.Identity.Infrastructure.Tests;

public sealed class ResetTokenGeneratorTests
{
    private readonly Sha256ResetTokenGenerator _generator = new();

    [Fact]
    public void Generate_ReturnsDistinctUrlSafeTokensOf32Bytes()
    {
        var a = _generator.Generate();
        var b = _generator.Generate();

        Assert.NotEqual(a, b);
        Assert.Equal(43, a.Length); // 32 bytes, unpadded base64url
        Assert.DoesNotContain('+', a);
        Assert.DoesNotContain('/', a);
        Assert.DoesNotContain('=', a);
    }

    [Fact]
    public void Hash_IsStableHexSha256_AndDoesNotContainTheToken()
    {
        var token = _generator.Generate();

        var hash = _generator.Hash(token);

        Assert.Equal(hash, _generator.Hash(token));
        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain(token, hash);
        Assert.NotEqual(hash, _generator.Hash(token + "x"));
    }
}
