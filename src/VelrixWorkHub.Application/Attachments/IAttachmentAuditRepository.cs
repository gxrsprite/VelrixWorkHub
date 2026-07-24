using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Attachments;

public interface IAttachmentAuditRepository
{
    IReadOnlyList<AttachmentAuditEntry> List(Guid? attachmentId = null, Guid? businessId = null);
    void Add(AttachmentAuditEntry item);
}
