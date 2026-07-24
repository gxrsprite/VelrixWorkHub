using System.Security.Cryptography;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Attachments;

public sealed class AttachmentService(IAttachmentRepository repository, IAttachmentAuditRepository auditRepository, IAttachmentAccessPolicy? accessPolicy = null)
{
    public const long MaxUploadBytes = 20 * 1024 * 1024;
    private IAttachmentAccessPolicy AccessPolicy { get; } = accessPolicy ?? new DefaultAttachmentAccessPolicy();
    public IReadOnlyList<BusinessAttachment> List(string businessType, Guid businessId, bool includeDeleted = false) => repository.List(businessType, businessId, includeDeleted).OrderByDescending(x => x.VersionNumber).ThenByDescending(x => x.UploadedAt).ToArray();
    public IReadOnlyList<AttachmentAuditEntry> Audit(Guid businessId) => auditRepository.List(businessId: businessId).OrderByDescending(x => x.OccurredAt).ToArray();

    public BusinessAttachment Register(string businessType, Guid businessId, string fileName, string? contentType, long sizeBytes, string sha256, string uploadedBy, DateTime? uploadedAt = null, string? otherInfo = null)
    {
        AccessPolicy.EnsureCanWrite(uploadedBy, businessType, businessId);
        if (sizeBytes > MaxUploadBytes) throw new InvalidOperationException($"附件不能超过 {MaxUploadBytes / 1024 / 1024} MB。");
        var version = repository.List(businessType, businessId, true).Where(x => x.FileName.Equals(fileName.Trim(), StringComparison.OrdinalIgnoreCase)).Select(x => x.VersionNumber).DefaultIfEmpty(0).Max() + 1;
        var storageKey = $"{businessType.Trim().ToLowerInvariant()}/{businessId:N}/{Guid.CreateVersion7():N}-{Path.GetFileName(fileName.Trim())}";
        var item = new BusinessAttachment(businessType, businessId, fileName, contentType, sizeBytes, sha256, storageKey, version, uploadedBy, uploadedAt ?? DateTime.Now, otherInfo);
        repository.Add(item);
        auditRepository.Add(new AttachmentAuditEntry(item.Id, item.BusinessType, item.BusinessId, AttachmentAuditAction.Uploaded, uploadedBy, item.UploadedAt, $"版本 V{item.VersionNumber}，文件 {item.FileName}"));
        return item;
    }

    public BusinessAttachment Register(string businessType, Guid businessId, string fileName, string? contentType, ReadOnlySpan<byte> content, string uploadedBy, DateTime? uploadedAt = null, string? otherInfo = null)
    {
        return Register(businessType, businessId, fileName, contentType, content.Length, Convert.ToHexString(SHA256.HashData(content)), uploadedBy, uploadedAt, otherInfo);
    }

    public async Task<BusinessAttachment> UploadAsync(string businessType, Guid businessId, string fileName, string? contentType, Stream content, string uploadedBy, IAttachmentContentStore contentStore, DateTime? uploadedAt = null, string? otherInfo = null, CancellationToken cancellationToken = default)
    {
        await using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxUploadBytes) throw new InvalidOperationException($"附件不能超过 {MaxUploadBytes / 1024 / 1024} MB。");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        var bytes = buffer.ToArray();
        var item = Register(businessType, businessId, fileName, contentType, bytes, uploadedBy, uploadedAt, otherInfo);
        try
        {
            await contentStore.SaveAsync(item.StorageKey, new MemoryStream(bytes, writable: false), cancellationToken);
            return item;
        }
        catch
        {
            try { await contentStore.DeleteAsync(item.StorageKey, cancellationToken); } catch { }
            item.Delete("附件内容保存失败", DateTime.Now);
            repository.Update(item);
            auditRepository.Add(new AttachmentAuditEntry(item.Id, item.BusinessType, item.BusinessId, AttachmentAuditAction.Deleted, uploadedBy, item.DeletedAt!.Value, "附件内容保存失败，元数据已回滚为删除状态"));
            throw;
        }
    }

    public void RecordDownload(BusinessAttachment item, string actor, DateTime? occurredAt = null)
    {
        AccessPolicy.EnsureCanRead(actor, item.BusinessType, item.BusinessId);
        if (item.Status != BusinessAttachmentStatus.Active) throw new InvalidOperationException("已删除附件不能下载。");
        auditRepository.Add(new AttachmentAuditEntry(item.Id, item.BusinessType, item.BusinessId, AttachmentAuditAction.Downloaded, actor, occurredAt ?? DateTime.Now, $"下载版本 V{item.VersionNumber}"));
    }

    public async Task<(BusinessAttachment Item, Stream Content)> DownloadAsync(Guid attachmentId, string actor, IAttachmentContentStore contentStore, CancellationToken cancellationToken = default)
    {
        var item = repository.List(includeDeleted: false).FirstOrDefault(x => x.Id == attachmentId) ?? throw new FileNotFoundException("附件不存在或已删除。", attachmentId.ToString());
        AccessPolicy.EnsureCanRead(actor, item.BusinessType, item.BusinessId);
        await using var storedContent = await contentStore.OpenReadAsync(item.StorageKey, cancellationToken);
        var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = await storedContent.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxUploadBytes)
            {
                buffer.Dispose();
                throw new InvalidDataException($"附件内容超过允许的 {MaxUploadBytes / 1024 / 1024} MB 限制，拒绝下载。");
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        if (buffer.Length != item.SizeBytes)
        {
            buffer.Dispose();
            throw new InvalidDataException("附件内容大小与元数据不一致，拒绝下载。");
        }
        var actualHash = Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
        if (!actualHash.Equals(item.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            buffer.Dispose();
            throw new InvalidDataException("附件内容哈希校验失败，拒绝下载。");
        }
        buffer.Position = 0;
        RecordDownload(item, actor);
        return (item, buffer);
    }

    public void Delete(BusinessAttachment item, string actor, string reason, DateTime? deletedAt = null)
    {
        AccessPolicy.EnsureCanWrite(actor, item.BusinessType, item.BusinessId);
        item.Delete(reason, deletedAt ?? DateTime.Now);
        repository.Update(item);
        auditRepository.Add(new AttachmentAuditEntry(item.Id, item.BusinessType, item.BusinessId, AttachmentAuditAction.Deleted, actor, item.DeletedAt!.Value, reason));
    }
}
