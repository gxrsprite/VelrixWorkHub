using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Attachments;

public interface IAttachmentRepository
{
    IReadOnlyList<BusinessAttachment> List(string? businessType = null, Guid? businessId = null, bool includeDeleted = false);
    void Add(BusinessAttachment item);
    void Update(BusinessAttachment item);
}
