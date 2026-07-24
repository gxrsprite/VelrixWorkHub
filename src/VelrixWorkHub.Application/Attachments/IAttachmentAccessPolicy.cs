namespace VelrixWorkHub.Application.Attachments;

public interface IAttachmentAccessPolicy
{
    void EnsureCanRead(string actor, string businessType, Guid businessId);
    void EnsureCanWrite(string actor, string businessType, Guid businessId);
}

public sealed class DefaultAttachmentAccessPolicy : IAttachmentAccessPolicy
{
    public void EnsureCanRead(string actor, string businessType, Guid businessId) => Validate(actor, businessType, businessId);
    public void EnsureCanWrite(string actor, string businessType, Guid businessId) => Validate(actor, businessType, businessId);

    private static void Validate(string actor, string businessType, Guid businessId)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new UnauthorizedAccessException("附件操作缺少操作人身份。");
        if (string.IsNullOrWhiteSpace(businessType) || businessId == Guid.Empty) throw new ArgumentException("附件业务对象无效。");
    }
}
