using System.Security.Cryptography;
using System.Text;

namespace DotNetMonoRepoTemplate.Logging;

public static class IpUtility
{
    private const string IPv4MappedPrefix = "::ffff:";

    public static string NormalizeIp(string ip) =>
        ip.StartsWith(IPv4MappedPrefix, StringComparison.Ordinal) ? ip[IPv4MappedPrefix.Length..] : ip;

    public static string HashIp(string ip, string pepper)
    {
        var bytes = Encoding.UTF8.GetBytes(NormalizeIp(ip) + pepper);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
