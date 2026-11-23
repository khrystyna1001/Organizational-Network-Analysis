using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using ONA.Identity.Application.Abstractions;

namespace ONA.Identity.Infrastructure.Security;

internal sealed class Sha256ResetTokenGenerator : IResetTokenGenerator
{
    public string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
