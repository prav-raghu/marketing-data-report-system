using System.Security.Cryptography;
using System.Text;

namespace DotNetMonoRepoTemplate.Utilities;

public sealed class WebhookSignatureService
{
    public string GenerateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifySignature(string payload, string signature, string secret)
    {
        var expectedSignature = GenerateSignature(payload, secret);
        var signatureBytes = Encoding.UTF8.GetBytes(signature);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        return signatureBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(signatureBytes, expectedBytes);
    }

    public string GenerateSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}
