using FreeSql;
using VelrixWorkHub.Application.Attachments;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Attachments;

public sealed class FreeSqlAttachmentRepository(IFreeSql fsql) : IAttachmentRepository
{
    public IReadOnlyList<BusinessAttachment> List(string? businessType = null, Guid? businessId = null, bool includeDeleted = false)
    {
        var query = fsql.Select<BusinessAttachmentRecord>();
        if (!string.IsNullOrWhiteSpace(businessType)) query = query.Where(x => x.BusinessType == businessType);
        if (businessId is not null) query = query.Where(x => x.BusinessId == businessId);
        if (!includeDeleted) query = query.Where(x => x.Status == BusinessAttachmentStatus.Active);
        return query.OrderByDescending(x => x.UploadedAt).ToList().Select(ToDomain).ToArray();
    }

    public void Add(BusinessAttachment item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(BusinessAttachment item) => fsql.Update<BusinessAttachmentRecord>().SetSource(ToRecord(item)).ExecuteAffrows();

    private static BusinessAttachment ToDomain(BusinessAttachmentRecord x)
    {
        var item = new BusinessAttachment(x.BusinessType, x.BusinessId, x.FileName, x.ContentType, x.SizeBytes, x.Sha256, x.StorageKey, x.VersionNumber, x.UploadedBy, x.UploadedAt, x.OtherInfo) { Id = x.Id };
        if (x.Status == BusinessAttachmentStatus.Deleted) item.Delete(x.DeletedReason, x.DeletedAt ?? x.UploadedAt);
        return item;
    }

    private static BusinessAttachmentRecord ToRecord(BusinessAttachment x) => new() { Id = x.Id, BusinessType = x.BusinessType, BusinessId = x.BusinessId, FileName = x.FileName, ContentType = x.ContentType, SizeBytes = x.SizeBytes, Sha256 = x.Sha256, StorageKey = x.StorageKey, VersionNumber = x.VersionNumber, UploadedBy = x.UploadedBy, UploadedAt = x.UploadedAt, OtherInfo = x.OtherInfo, Status = x.Status, DeletedReason = x.DeletedReason, DeletedAt = x.DeletedAt };
}
