using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Infrastructure.Storage;

public class FileShareAttachmentService : IAttachmentService
{
    private readonly string _rootPath;
    private readonly long _maxAttachmentSizeBytes;
    private readonly ILogger<FileShareAttachmentService> _logger;

    public FileShareAttachmentService(string rootPath, long maxAttachmentSizeBytes, ILogger<FileShareAttachmentService>? logger = null)
    {
        _rootPath = rootPath;
        _maxAttachmentSizeBytes = Math.Max(1, maxAttachmentSizeBytes);
        _logger = logger ?? NullLogger<FileShareAttachmentService>.Instance;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<Attachment> SaveAsync(int issueId, int uploadedById, string sourceFilePath, CancellationToken cancellationToken = default)
    {
        var sourceInfo = new FileInfo(sourceFilePath);
        if (!sourceInfo.Exists)
        {
            throw new FileNotFoundException("Attachment source file was not found.", sourceFilePath);
        }

        if (sourceInfo.Length > _maxAttachmentSizeBytes)
        {
            _logger.LogWarning(
                "Attachment upload rejected because file size {FileSizeBytes} exceeded configured limit {MaxAttachmentSizeBytes} for issue {IssueId}.",
                sourceInfo.Length,
                _maxAttachmentSizeBytes,
                issueId);
            throw new ValidationException($"Attachment exceeds the configured limit of {Math.Ceiling(_maxAttachmentSizeBytes / 1024d / 1024d)} MB.");
        }

        var extension = Path.GetExtension(sourceFilePath);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var issueFolder = Path.Combine(_rootPath, $"issue-{issueId}");
        Directory.CreateDirectory(issueFolder);
        var destinationPath = Path.Combine(issueFolder, storedFileName);

        _logger.LogInformation("Saving attachment {FileName} for issue {IssueId}.", sourceInfo.Name, issueId);

        await using (var source = File.OpenRead(sourceFilePath))
        await using (var destination = File.Create(destinationPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        await using var fileStream = File.OpenRead(destinationPath);
        using var sha = SHA256.Create();
        var checksum = Convert.ToHexString(await sha.ComputeHashAsync(fileStream, cancellationToken));
        var info = new FileInfo(destinationPath);

        return new Attachment
        {
            IssueId = issueId,
            UploadedById = uploadedById,
            StoredFileName = storedFileName,
            OriginalFileName = Path.GetFileName(sourceFilePath),
            ContentType = ResolveContentType(extension),
            FileExtension = extension,
            FileSizeBytes = info.Length,
            StoragePath = destinationPath,
            UploadedAtUtc = DateTime.UtcNow,
            ChecksumSha256 = checksum
        };
    }

    public Task DeleteAsync(Attachment attachment, CancellationToken cancellationToken = default)
    {
        if (File.Exists(attachment.StoragePath))
        {
            _logger.LogInformation("Deleting attachment {AttachmentId} from {StoragePath}.", attachment.Id, attachment.StoragePath);
            File.Delete(attachment.StoragePath);
        }

        return Task.CompletedTask;
    }

    public Task<string> ResolvePathAsync(Attachment attachment, CancellationToken cancellationToken = default) =>
        Task.FromResult(attachment.StoragePath);

    private static string ResolveContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
}
