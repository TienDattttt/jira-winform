using System.Security.Cryptography;
using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;

namespace JiraClone.Infrastructure.Storage;

public class FileShareAttachmentService : IAttachmentService
{
    private readonly string _rootPath;

    public FileShareAttachmentService(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<Attachment> SaveAsync(int issueId, int uploadedById, string sourceFilePath, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(sourceFilePath);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var issueFolder = Path.Combine(_rootPath, $"issue-{issueId}");
        Directory.CreateDirectory(issueFolder);
        var destinationPath = Path.Combine(issueFolder, storedFileName);

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
