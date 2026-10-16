using System.Security.Cryptography;

namespace Tools.NeuralNetGoal.Service.Auth;

/// <summary>PBKDF2-SHA256 hasher. Stored as "v1.&lt;iterations&gt;.&lt;salt base64&gt;.&lt;hash base64&gt;", so the iteration count can be
/// raised later and old hashes still verify (and can be upgraded at the next login).</summary>
public static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    public const int MinLength = 10;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        var p = stored.Split('.');
        if (p.Length != 4 || p[0] != "v1" || !int.TryParse(p[1], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(p[2]);
            var expected = Convert.FromBase64String(p[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }

    /// <summary>Returns a message when the password is not acceptable, otherwise null.</summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength) return $"The password must have at least {MinLength} characters.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "The password must contain letters and digits.";
        return null;
    }
}
