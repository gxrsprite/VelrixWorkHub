using VelrixWorkHub.Application.Attachments;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.SimpleForms;

public sealed class SimpleFormAttachmentService(
    ISimpleFormSubmissionRepository submissions,
    AttachmentService attachments)
{
    public IReadOnlyList<BusinessAttachment> List(Guid submissionId, Guid actorUserId) => attachments.List(nameof(SimpleFormSubmission), EnsureRead(submissionId, actorUserId).Id);

    public async Task<BusinessAttachment> UploadAsync(Guid submissionId, Guid actorUserId, string actor, string fileName, string? contentType, Stream content, IAttachmentContentStore contentStore, string? otherInfo = null, CancellationToken cancellationToken = default)
    {
        var submission = EnsureWrite(submissionId, actorUserId);
        return await attachments.UploadAsync(nameof(SimpleFormSubmission), submission.Id, fileName, contentType, content, actor, contentStore, otherInfo: otherInfo, cancellationToken: cancellationToken);
    }

    public void Delete(BusinessAttachment item, Guid actorUserId, string actor)
    {
        if (!item.BusinessType.Equals(nameof(SimpleFormSubmission), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("附件不属于简单表单申请。");
        EnsureWrite(item.BusinessId, actorUserId);
        attachments.Delete(item, actor, "申请人删除简单表单附件");
    }

    private SimpleFormSubmission EnsureRead(Guid submissionId, Guid actorUserId)
    {
        var submission = submissions.Get(submissionId) ?? throw new InvalidOperationException("表单申请不存在或已被删除。");
        if (actorUserId == Guid.Empty || submission.ApplicantUserId != actorUserId) throw new UnauthorizedAccessException("当前用户不能访问其他员工的表单附件。");
        return submission;
    }

    private SimpleFormSubmission EnsureWrite(Guid submissionId, Guid actorUserId)
    {
        var submission = EnsureRead(submissionId, actorUserId);
        if (submission.Status is not (SimpleFormSubmissionStatus.Draft or SimpleFormSubmissionStatus.Submitted or SimpleFormSubmissionStatus.Rejected)) throw new InvalidOperationException("当前表单状态不允许修改附件。");
        return submission;
    }
}
