using System.Security.Cryptography;
using System.Text;
using JiraClone.Application.Abstractions;

namespace JiraClone.Infrastructure.Security;

public class Sha256PasswordHasher : IPasswordHasher
{
    public (string Hash, string Salt) Hash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = ComputeHash(password, saltBytes);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool Verify(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hashBytes = ComputeHash(password, saltBytes);
        return Convert.ToBase64String(hashBytes) == hash;
    }

    private static byte[] ComputeHash(string password, byte[] saltBytes)
    {
        using var sha = SHA256.Create();
        var input = Encoding.UTF8.GetBytes(password).Concat(saltBytes).ToArray();
        return sha.ComputeHash(input);
    }
}
