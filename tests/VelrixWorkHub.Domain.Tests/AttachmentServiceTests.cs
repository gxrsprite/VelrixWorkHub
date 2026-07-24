using VelrixWorkHub.Application.Attachments;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.Attachments;

namespace VelrixWorkHub.Domain.Tests;

public sealed class AttachmentServiceTests
{
    [Fact]
    public void Register_CreatesVersionsAndAuditsEveryLifecycleAction()
    {
        var attachmentRepository = new AttachmentRepository();
        var auditRepository = new AuditRepository();
        var service = new AttachmentService(attachmentRepository, auditRepository);
        var businessId = Guid.CreateVersion7();

        var first = service.Register("SalesContract", businessId, "contract.pdf", "application/pdf", "first"u8, "alice", new DateTime(2026, 7, 13, 9, 0, 0));
        var second = service.Register("SalesContract", businessId, "contract.pdf", "application/pdf", "second"u8, "alice", new DateTime(2026, 7, 13, 10, 0, 0));
        service.RecordDownload(second, "bob", new DateTime(2026, 7, 13, 11, 0, 0));
        service.Delete(second, "bob", "替换为新版合同", new DateTime(2026, 7, 13, 12, 0, 0));

        Assert.Equal(1, first.VersionNumber);
        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(6, second.SizeBytes);
        Assert.Equal("application/pdf", second.ContentType);
        Assert.Equal(BusinessAttachmentStatus.Deleted, second.Status);
        Assert.Equal(2, service.List("SalesContract", businessId, includeDeleted: true).Count);
        Assert.Equal(3, auditRepository.List(attachmentId: second.Id).Count);
        Assert.Equal(AttachmentAuditAction.Uploaded, auditRepository.List(attachmentId: first.Id).Single().Action);
        Assert.Equal(AttachmentAuditAction.Deleted, auditRepository.List(attachmentId: second.Id).Last().Action);
        Assert.Single(service.List("SalesContract", businessId, includeDeleted: false));
    }

    [Fact]
    public void Register_RejectsInvalidHashAndDownloadOfDeletedAttachment()
    {
        var attachmentRepository = new AttachmentRepository();
        var auditRepository = new AuditRepository();
        var service = new AttachmentService(attachmentRepository, auditRepository);
        var businessId = Guid.CreateVersion7();

        Assert.Throws<ArgumentException>(() => service.Register("PmpProject", businessId, "plan.txt", "text/plain", 1, "bad", "alice"));
        var item = service.Register("PmpProject", businessId, "plan.txt", "text/plain", "content"u8, "alice");
        service.Delete(item, "alice", "清理测试文件");

        Assert.Throws<InvalidOperationException>(() => service.RecordDownload(item, "bob"));
    }

    [Fact]
    public async Task UploadAsync_PersistsContentWithMetadataStorageKey()
    {
        var attachmentRepository = new AttachmentRepository();
        var auditRepository = new AuditRepository();
        var contentStore = new MemoryContentStore();
        var service = new AttachmentService(attachmentRepository, auditRepository);

        var item = await service.UploadAsync("PmpProject", Guid.CreateVersion7(), "plan.txt", "text/plain", new MemoryStream("project plan"u8.ToArray()), "alice", contentStore, otherInfo: "{\"source\":\"项目组\",\"category\":\"计划\"}");

        Assert.Equal(12, contentStore.ContentByKey[item.StorageKey].Length);
        Assert.Equal(12, item.SizeBytes);
        Assert.Equal("{\"source\":\"项目组\",\"category\":\"计划\"}", item.OtherInfo);
        Assert.Single(auditRepository.List(attachmentId: item.Id));

        var download = await service.DownloadAsync(item.Id, "bob", contentStore);
        using var reader = new StreamReader(download.Content);
        Assert.Equal("project plan", await reader.ReadToEndAsync());
        Assert.Equal(2, auditRepository.List(attachmentId: item.Id).Count);
    }

    [Fact]
    public void Register_PreservesValidatedOtherInfoObject()
    {
        var service = new AttachmentService(new AttachmentRepository(), new AuditRepository());

        var item = service.Register("LmsLicenseRequest", Guid.CreateVersion7(), "evidence.pdf", "application/pdf", 1, new string('a', 64), "alice", otherInfo: "{\"source\":\"客户提供\",\"category\":\"授权材料\"}");

        Assert.Equal("{\"source\":\"客户提供\",\"category\":\"授权材料\"}", item.OtherInfo);
        Assert.Throws<ArgumentException>(() => service.Register("LmsLicenseRequest", Guid.CreateVersion7(), "bad.pdf", "application/pdf", 1, new string('a', 64), "alice", otherInfo: "[]"));
    }

    [Fact]
    public async Task UploadAsync_WhenContentStoreFails_MarksMetadataDeleted()
    {
        var attachmentRepository = new AttachmentRepository();
        var auditRepository = new AuditRepository();
        var service = new AttachmentService(attachmentRepository, auditRepository);
        var businessId = Guid.CreateVersion7();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync("SalesOrder", businessId, "order.txt", "text/plain", new MemoryStream("order"u8.ToArray()), "alice", new FailingContentStore()));

        var history = service.List("SalesOrder", businessId, includeDeleted: true);
        Assert.Single(history);
        Assert.Equal(BusinessAttachmentStatus.Deleted, history[0].Status);
        Assert.Equal(2, auditRepository.List(attachmentId: history[0].Id).Count);
    }

    [Fact]
    public void AttachmentOperations_RejectMissingActorThroughAccessPolicy()
    {
        var service = new AttachmentService(new AttachmentRepository(), new AuditRepository());

        Assert.Throws<UnauthorizedAccessException>(() => service.Register("PmpProject", Guid.CreateVersion7(), "plan.txt", "text/plain", "content"u8, " "));
    }

    [Fact]
    public void AttachmentOperations_UseInjectedBusinessPolicy()
    {
        var policy = new DenyWritesPolicy();
        var service = new AttachmentService(new AttachmentRepository(), new AuditRepository(), policy);

        Assert.Throws<UnauthorizedAccessException>(() => service.Register("SalesContract", Guid.CreateVersion7(), "contract.pdf", "application/pdf", "content"u8, "alice"));
        Assert.Equal(1, policy.WriteChecks);
    }

    [Fact]
    public async Task Download_EnforcesReadPolicyBeforeOpeningContent()
    {
        var repository = new AttachmentRepository();
        var service = new AttachmentService(repository, new AuditRepository(), new DenyReadsPolicy());
        var item = service.Register("SalesOrder", Guid.CreateVersion7(), "order.txt", "text/plain", "content"u8, "alice");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DownloadAsync(item.Id, "bob", new MemoryContentStore()));
    }

    [Fact]
    public async Task LocalContentStore_RoundTripsAndRejectsPathTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"velrix-attachments-{Guid.CreateVersion7():N}");
        try
        {
            var store = new LocalAttachmentContentStore(root);
            await store.SaveAsync("SalesOrder/order.txt", new MemoryStream("order"u8.ToArray()));
            await using var content = await store.OpenReadAsync("SalesOrder/order.txt");
            using var reader = new StreamReader(content);
            Assert.Equal("order", await reader.ReadToEndAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.OpenReadAsync("../outside.txt"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Download_RejectsTamperedContentWithoutDownloadAudit()
    {
        var repository = new AttachmentRepository();
        var auditRepository = new AuditRepository();
        var contentStore = new MemoryContentStore();
        var service = new AttachmentService(repository, auditRepository);
        var item = await service.UploadAsync("PmpProject", Guid.CreateVersion7(), "plan.txt", "text/plain", new MemoryStream("original"u8.ToArray()), "alice", contentStore);
        contentStore.ContentByKey[item.StorageKey] = "tampered"u8.ToArray();

        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(item.Id, "bob", contentStore));
        Assert.Single(auditRepository.List(attachmentId: item.Id));
    }

    [Fact]
    public async Task UploadAsync_RejectsOversizeBeforeCreatingMetadata()
    {
        var repository = new AttachmentRepository();
        var service = new AttachmentService(repository, new AuditRepository());
        var content = new MemoryStream(new byte[checked((int)AttachmentService.MaxUploadBytes + 1)]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync("SalesOrder", Guid.CreateVersion7(), "large.bin", "application/octet-stream", content, "alice", new MemoryContentStore()));
        Assert.Empty(repository.List(includeDeleted: true));
    }

    [Fact]
    public async Task UploadAsync_CleansPartialContentWhenStoreFailsAfterWriting()
    {
        var repository = new AttachmentRepository();
        var store = new PartiallyFailingContentStore();
        var service = new AttachmentService(repository, new AuditRepository());

        await Assert.ThrowsAsync<IOException>(() => service.UploadAsync("SalesOrder", Guid.CreateVersion7(), "order.txt", "text/plain", new MemoryStream("order"u8.ToArray()), "alice", store));
        Assert.True(store.DeleteCalled);
        Assert.Empty(store.ContentByKey);
    }

    [Fact]
    public void Register_RejectsOversizeMetadataFromDirectCallers()
    {
        var repository = new AttachmentRepository();
        var service = new AttachmentService(repository, new AuditRepository());

        Assert.Throws<InvalidOperationException>(() => service.Register("SalesOrder", Guid.CreateVersion7(), "large.bin", "application/octet-stream", AttachmentService.MaxUploadBytes + 1, new string('a', 64), "alice"));
        Assert.Empty(repository.List(includeDeleted: true));
    }

    [Fact]
    public async Task Download_RejectsMetadataSizeMismatch()
    {
        var repository = new AttachmentRepository();
        var service = new AttachmentService(repository, new AuditRepository());
        var businessId = Guid.CreateVersion7();
        var content = "content"u8.ToArray();
        var item = new BusinessAttachment("SalesOrder", businessId, "order.txt", "text/plain", 1, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)), "salesorder/file", 1, "alice", DateTime.Now);
        repository.Add(item);
        var store = new MemoryContentStore();
        store.ContentByKey[item.StorageKey] = content;

        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(item.Id, "bob", store));
    }

    [Fact]
    public async Task Download_RejectsOversizeContentBeforeHashing()
    {
        var repository = new AttachmentRepository();
        var service = new AttachmentService(repository, new AuditRepository());
        var businessId = Guid.CreateVersion7();
        var item = new BusinessAttachment("SalesOrder", businessId, "large.bin", "application/octet-stream", AttachmentService.MaxUploadBytes, new string('a', 64), "salesorder/large", 1, "alice", DateTime.Now);
        repository.Add(item);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(item.Id, "bob", new MemoryContentStoreWithOversizePayload()));
    }

    private sealed class AttachmentRepository : IAttachmentRepository
    {
        private readonly List<BusinessAttachment> items = [];
        public IReadOnlyList<BusinessAttachment> List(string? businessType = null, Guid? businessId = null, bool includeDeleted = false) => items.Where(x => (businessType is null || x.BusinessType == businessType) && (businessId is null || x.BusinessId == businessId) && (includeDeleted || x.Status == BusinessAttachmentStatus.Active)).ToArray();
        public void Add(BusinessAttachment item) => items.Add(item);
        public void Update(BusinessAttachment item) { }
    }

    private sealed class AuditRepository : IAttachmentAuditRepository
    {
        private readonly List<AttachmentAuditEntry> items = [];
        public IReadOnlyList<AttachmentAuditEntry> List(Guid? attachmentId = null, Guid? businessId = null) => items.Where(x => (attachmentId is null || x.AttachmentId == attachmentId) && (businessId is null || x.BusinessId == businessId)).OrderBy(x => x.OccurredAt).ToArray();
        public void Add(AttachmentAuditEntry item) => items.Add(item);
    }

    private sealed class MemoryContentStore : IAttachmentContentStore
    {
        public Dictionary<string, byte[]> ContentByKey { get; } = [];
        public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            ContentByKey[storageKey] = buffer.ToArray();
        }
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream(ContentByKey[storageKey], writable: false));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) { ContentByKey.Remove(storageKey); return Task.CompletedTask; }
    }

    private sealed class MemoryContentStoreWithOversizePayload : IAttachmentContentStore
    {
        public Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream(new byte[checked((int)AttachmentService.MaxUploadBytes + 1)], writable: false));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FailingContentStore : IAttachmentContentStore
    {
        public Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default) => throw new InvalidOperationException("存储失败");
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new FileNotFoundException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PartiallyFailingContentStore : IAttachmentContentStore
    {
        public Dictionary<string, byte[]> ContentByKey { get; } = [];
        public bool DeleteCalled { get; private set; }
        public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            ContentByKey[storageKey] = buffer.ToArray();
            throw new IOException("写入后存储失败");
        }
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new FileNotFoundException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) { DeleteCalled = true; ContentByKey.Remove(storageKey); return Task.CompletedTask; }
    }

    private sealed class DenyWritesPolicy : IAttachmentAccessPolicy
    {
        public int WriteChecks { get; private set; }
        public void EnsureCanRead(string actor, string businessType, Guid businessId) { }
        public void EnsureCanWrite(string actor, string businessType, Guid businessId) { WriteChecks++; throw new UnauthorizedAccessException("测试策略拒绝写入"); }
    }

    private sealed class DenyReadsPolicy : IAttachmentAccessPolicy
    {
        public void EnsureCanRead(string actor, string businessType, Guid businessId) => throw new UnauthorizedAccessException("测试策略拒绝读取");
        public void EnsureCanWrite(string actor, string businessType, Guid businessId) { }
    }
}
