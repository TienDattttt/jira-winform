using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JiraClone.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Infrastructure.Session;

[SupportedOSPlatform("windows")]
public sealed class DpapiSessionPersistenceService : ISessionPersistenceService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("JiraClone.Session.v1");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SessionPersistenceOptions _options;
    private readonly ILogger<DpapiSessionPersistenceService> _logger;

    public DpapiSessionPersistenceService(
        SessionPersistenceOptions options,
        ILogger<DpapiSessionPersistenceService>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.SessionFilePath))
        {
            throw new ArgumentException("Session file path is required.", nameof(options));
        }

        _logger = logger ?? NullLogger<DpapiSessionPersistenceService>.Instance;
    }

    public async Task SaveAsync(SessionData session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var directoryPath = Path.GetDirectoryName(_options.SessionFilePath)
            ?? throw new InvalidOperationException("The configured session file path is invalid.");

        Directory.CreateDirectory(directoryPath);

        var plaintextBytes = JsonSerializer.SerializeToUtf8Bytes(session, JsonOptions);
        var protectedBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.CurrentUser);
        var tempFilePath = _options.SessionFilePath + ".tmp";

        await File.WriteAllBytesAsync(tempFilePath, protectedBytes);

        if (File.Exists(_options.SessionFilePath))
        {
            File.Replace(tempFilePath, _options.SessionFilePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempFilePath, _options.SessionFilePath);
        }

        File.SetAttributes(_options.SessionFilePath, FileAttributes.Hidden);
        _logger.LogInformation("Persisted encrypted session file to {SessionFilePath}.", _options.SessionFilePath);
    }

    public async Task<SessionData?> LoadAsync()
    {
        if (!File.Exists(_options.SessionFilePath))
        {
            return null;
        }

        try
        {
            var protectedBytes = await File.ReadAllBytesAsync(_options.SessionFilePath);
            var plaintextBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            var session = JsonSerializer.Deserialize<SessionData>(plaintextBytes, JsonOptions);

            if (session is null || session.UserId <= 0 || string.IsNullOrWhiteSpace(session.RefreshToken))
            {
                _logger.LogWarning("The persisted session file at {SessionFilePath} is invalid.", _options.SessionFilePath);
                await ClearAsync();
                return null;
            }

            return session;
        }
        catch (Exception exception) when (exception is IOException or CryptographicException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Failed to load persisted session from {SessionFilePath}. Clearing file.", _options.SessionFilePath);
            await ClearAsync();
            return null;
        }
    }

    public Task ClearAsync()
    {
        try
        {
            if (File.Exists(_options.SessionFilePath))
            {
                File.SetAttributes(_options.SessionFilePath, FileAttributes.Normal);
                File.Delete(_options.SessionFilePath);
                _logger.LogInformation("Deleted persisted session file at {SessionFilePath}.", _options.SessionFilePath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Unable to delete persisted session file at {SessionFilePath}.", _options.SessionFilePath);
        }

        var tempFilePath = _options.SessionFilePath + ".tmp";
        if (File.Exists(tempFilePath))
        {
            try
            {
                File.Delete(tempFilePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Unable to delete temporary session file at {SessionTempFilePath}.", tempFilePath);
            }
        }

        return Task.CompletedTask;
    }
}
