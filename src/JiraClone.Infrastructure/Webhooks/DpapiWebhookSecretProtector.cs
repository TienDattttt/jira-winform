using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using JiraClone.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Infrastructure.Webhooks;

[SupportedOSPlatform("windows")]
public sealed class DpapiWebhookSecretProtector : IWebhookSecretProtector
{
    private const string Prefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("JiraClone.Webhooks.v1");
    private readonly ILogger<DpapiWebhookSecretProtector> _logger;

    public DpapiWebhookSecretProtector(ILogger<DpapiWebhookSecretProtector>? logger = null)
    {
        _logger = logger ?? NullLogger<DpapiWebhookSecretProtector>.Instance;
    }

    public string Protect(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            return string.Empty;
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(secret.Trim());
        var protectedBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.LocalMachine);
        return $"{Prefix}{Convert.ToBase64String(protectedBytes)}";
    }

    public string Unprotect(string protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret))
        {
            return string.Empty;
        }

        if (!protectedSecret.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return protectedSecret;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(protectedSecret[Prefix.Length..]);
            var plaintextBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            _logger.LogWarning(exception, "Unable to decrypt webhook secret.");
            return string.Empty;
        }
    }
}
