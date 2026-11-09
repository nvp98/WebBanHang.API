using System.Security.Cryptography;
using System.Text;

namespace WebBanHang.Api.Common;

/// <summary>
/// The legacy site stores unsalted MD5 (32 hex chars). New hashes use PBKDF2-SHA256:
/// "pbkdf2$&lt;iterations&gt;$&lt;salt b64&gt;$&lt;hash b64&gt;" (~80 chars — needs Database/upgrade.sql to widen the column).
/// </summary>
public static class Passwords
{
    private const int Iterations = 100_000;
    private const string Prefix = "pbkdf2$";

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? stored) => stored switch
    {
        null or "" => false,
        _ when stored.StartsWith(Prefix, StringComparison.Ordinal) => VerifyPbkdf2(password, stored),
        _ when stored.Length == 32 => FixedTimeEquals(LegacyMd5(password), stored.ToUpperInvariant()),
        _ => false,
    };

    /// <summary>True when the stored hash is the weak legacy format and should be upgraded.</summary>
    public static bool IsLegacy(string? stored) => stored is { Length: 32 };

    private static bool VerifyPbkdf2(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string LegacyMd5(string password) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password)));

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
}
