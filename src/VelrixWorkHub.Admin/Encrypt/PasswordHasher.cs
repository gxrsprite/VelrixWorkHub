using System.Security.Cryptography;

namespace AdminBlazor;

/// <summary>
/// PHC password hashing and legacy credential migration helper.
/// </summary>
public static class PasswordHasher
{
    private const string PhcAlgorithm = "pbkdf2-sha256";
    private const string Argon2idAlgorithm = "argon2id";
    private const string ScryptAlgorithm = "scrypt";
    private const string LegacyAlgorithm = "PBKDF2-SHA256";
    private const int Iterations = 120_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"${PhcAlgorithm}$i={Iterations}${ToPhcBase64(salt)}${ToPhcBase64(hash)}";
    }

    public static bool Verify(string? password, string? passwordHash, string? legacyPassword = null)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        if (!string.IsNullOrWhiteSpace(passwordHash))
            return VerifyHash(password, passwordHash);

        return !string.IsNullOrEmpty(legacyPassword)
            && CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(password),
                System.Text.Encoding.UTF8.GetBytes(legacyPassword));
    }

    /// <summary>
    /// Indicates that a successfully verified credential should be written in the current PHC format.
    /// </summary>
    public static bool RequiresFormatUpgrade(string? passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            return true;

        return !TryReadInfo(passwordHash, out var info) || info.RequiresUpgrade;
    }

    public static bool TryReadInfo(string? passwordHash, out PasswordHashInfo info)
    {
        info = default;
        if (string.IsNullOrWhiteSpace(passwordHash))
            return false;

        if (passwordHash.StartsWith('$'))
        {
            var parts = passwordHash.Split('$');
            if (parts.Length != 5 || parts[0].Length != 0)
                return false;

            var algorithm = parts[1];
            if (!IsKnownPhcAlgorithm(algorithm))
                return false;

            info = new PasswordHashInfo(algorithm, true, algorithm != PhcAlgorithm, algorithm != PhcAlgorithm);
            return true;
        }

        var legacyParts = passwordHash.Split('$');
        if (legacyParts.Length == 4 && legacyParts[0] == LegacyAlgorithm)
        {
            info = new PasswordHashInfo(LegacyAlgorithm, false, true, false);
            return true;
        }

        return false;
    }

    private static bool VerifyHash(string password, string passwordHash)
    {
        return passwordHash.StartsWith('$')
            ? VerifyPhcHash(password, passwordHash)
            : VerifyLegacyHash(password, passwordHash);
    }

    private static bool VerifyPhcHash(string password, string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 5 || parts[0].Length != 0)
            return false;

        return parts[1] switch
        {
            PhcAlgorithm => VerifyPbkdf2PhcHash(password, parts[2], parts[3], parts[4]),
            Argon2idAlgorithm or ScryptAlgorithm => false,
            _ => false
        };
    }

    private static bool VerifyPbkdf2PhcHash(string password, string parameters, string saltText, string hashText)
    {
        if (!TryGetIterations(parameters, out var iterations))
            return false;

        try
        {
            var salt = FromPhcBase64(saltText);
            var expectedHash = FromPhcBase64(hashText);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool VerifyLegacyHash(string password, string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || parts[0] != LegacyAlgorithm || !int.TryParse(parts[1], out var iterations) || iterations < 1)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryGetIterations(string parameters, out int iterations)
    {
        iterations = 0;
        foreach (var parameter in parameters.Split(','))
        {
            if (parameter.StartsWith("i=", StringComparison.Ordinal)
                && int.TryParse(parameter[2..], out iterations)
                && iterations > 0)
                return true;
        }

        return false;
    }

    private static bool IsKnownPhcAlgorithm(string algorithm)
    {
        return algorithm is PhcAlgorithm or Argon2idAlgorithm or ScryptAlgorithm;
    }

    private static string ToPhcBase64(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=');
    }

    private static byte[] FromPhcBase64(string value)
    {
        var padded = value.PadRight(value.Length + (4 - value.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}

public readonly record struct PasswordHashInfo(
    string Algorithm,
    bool IsPhc,
    bool RequiresUpgrade,
    bool RequiresExternalVerifier);
