namespace VelrixWorkHub.Domain;
public enum FollowUpType { Phone, Visit, Email, Meeting, Other }
public sealed class CustomerFollowUp
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CustomerId { get; private set; }
    public Guid? ContactId { get; private set; }
    public FollowUpType Type { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateOnly? NextFollowUpDate { get; private set; }
    public DateTime CreatedTime { get; init; } = DateTime.Now;
    public CustomerFollowUp(Guid customerId, Guid? contactId, FollowUpType type, string content, DateOnly? nextFollowUpDate)
    {
        Edit(customerId, contactId, type, content, nextFollowUpDate);
    }
    public void Edit(Guid customerId, Guid? contactId, FollowUpType type, string content, DateOnly? nextFollowUpDate)
    {
        if (customerId == Guid.Empty) throw new ArgumentException("必须选择客户。", nameof(customerId));
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("跟进内容不能为空。", nameof(content));
        CustomerId = customerId; ContactId = contactId; Type = type; Content = content.Trim(); NextFollowUpDate = nextFollowUpDate;
    }
}
