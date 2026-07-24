using System.Security.Cryptography;
using System.Text;

namespace VelrixWorkHub.Application.Platform;

public static class Sha256Encrypt
{
    public static string Encrypt(string? value) => Encrypt(Encoding.UTF8.GetBytes(value ?? string.Empty));

    public static string Encrypt(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
