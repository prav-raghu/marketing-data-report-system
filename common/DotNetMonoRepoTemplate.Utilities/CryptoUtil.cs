using System.Security.Cryptography;
using System.Text;

namespace DotNetMonoRepoTemplate.Utilities;

public sealed class CryptoUtil
{
    private const int KeySizeBytes = 32;
    private const int Iterations = 100_000;
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("salt");

    private readonly byte[] _key;

    public CryptoUtil(string secret)
    {
        _key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(secret),
            Salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySizeBytes);
    }

    public string Encrypt(string text)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(text);
        var encrypted = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
        return $"{Convert.ToHexString(aes.IV).ToLowerInvariant()}:{Convert.ToHexString(encrypted).ToLowerInvariant()}";
    }

    public string Decrypt(string encryptedText)
    {
        var parts = encryptedText.Split(':', 2);
        var iv = Convert.FromHexString(parts[0]);
        var ciphertext = Convert.FromHexString(parts[1]);
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return Encoding.UTF8.GetString(decrypted);
    }
}
