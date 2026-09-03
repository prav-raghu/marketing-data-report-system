using System.Security.Cryptography;
using System.Text;

namespace DotNetMonoRepoTemplate.Utilities;

public sealed class CryptoUtil
{
    private const int KeySizeBytes = 32;
    private const int SaltSizeBytes = 16;
    private const int Iterations = 100_000;

    private readonly byte[] _secret;

    public CryptoUtil(string secret)
    {
        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public string Encrypt(string text)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var key = DeriveKey(salt);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(text);
        var encrypted = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
        return string.Join(
            ':',
            Convert.ToHexString(salt).ToLowerInvariant(),
            Convert.ToHexString(aes.IV).ToLowerInvariant(),
            Convert.ToHexString(encrypted).ToLowerInvariant());
    }

    public string Decrypt(string encryptedText)
    {
        var parts = encryptedText.Split(':', 3);
        var salt = Convert.FromHexString(parts[0]);
        var iv = Convert.FromHexString(parts[1]);
        var ciphertext = Convert.FromHexString(parts[2]);
        var key = DeriveKey(salt);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return Encoding.UTF8.GetString(decrypted);
    }

    private byte[] DeriveKey(byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(_secret, salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
}
