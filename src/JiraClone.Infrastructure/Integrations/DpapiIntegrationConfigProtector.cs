using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JiraClone.Application.Integrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Infrastructure.Integrations;

[SupportedOSPlatform("windows")]
public sealed class DpapiIntegrationConfigProtector : IIntegrationConfigProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("JiraClone.Integrations.v1");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<DpapiIntegrationConfigProtector> _logger;

    public DpapiIntegrationConfigProtector(ILogger<DpapiIntegrationConfigProtector>? logger = null)
    {
        _logger = logger ?? NullLogger<DpapiIntegrationConfigProtector>.Instance;
    }

    public string Protect<TConfig>(TConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var plaintextBytes = JsonSerializer.SerializeToUtf8Bytes(config, JsonOptions);
        var protectedBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public TConfig? Unprotect<TConfig>(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return default;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(protectedValue);
            var plaintextBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<TConfig>(plaintextBytes, JsonOptions);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or JsonException)
        {
            _logger.LogWarning(exception, "Unable to decrypt integration config into {ConfigType}.", typeof(TConfig).Name);
            return default;
        }
    }
}
