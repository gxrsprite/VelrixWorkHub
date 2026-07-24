using FreeSql;
using VelrixWorkHub.Application.Attachments;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Attachments;

public sealed class FreeSqlAttachmentAuditRepository(IFreeSql fsql) : IAttachmentAuditRepository
{
    public IReadOnlyList<AttachmentAuditEntry> List(Guid? attachmentId = null, Guid? businessId = null)
    {
        var query = fsql.Select<AttachmentAuditRecord>();
        if (attachmentId is not null) query = query.Where(x => x.AttachmentId == attachmentId);
        if (businessId is not null) query = query.Where(x => x.BusinessId == businessId);
        return query.OrderByDescending(x => x.OccurredAt).ToList().Select(ToDomain).ToArray();
    }

    public void Add(AttachmentAuditEntry item) => fsql.Insert(new AttachmentAuditRecord { Id = item.Id, AttachmentId = item.AttachmentId, BusinessType = item.BusinessType, BusinessId = item.BusinessId, Action = item.Action, Actor = item.Actor, OccurredAt = item.OccurredAt, Details = item.Details }).ExecuteAffrows();

    private static AttachmentAuditEntry ToDomain(AttachmentAuditRecord x) => new(x.AttachmentId, x.BusinessType, x.BusinessId, x.Action, x.Actor, x.OccurredAt, x.Details) { Id = x.Id };
}
