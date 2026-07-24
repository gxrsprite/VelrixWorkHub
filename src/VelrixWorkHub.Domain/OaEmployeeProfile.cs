namespace VelrixWorkHub.Domain;

public enum OaEmploymentStatus
{
    Candidate,
    PendingOnboarding,
    Employed,
    Suspended,
    Resigned
}

public sealed class OaEmployeeProfile
{
    public Guid UserId { get; init; }
    public string? EmployeeNo { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? WeComUserId { get; private set; }
    public string? DingTalkUserId { get; private set; }
    public string? PositionTitle { get; private set; }
    public DateOnly? HireDate { get; private set; }
    public OaEmploymentStatus Status { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public OaEmployeeProfile(
        Guid userId,
        string? employeeNo = null,
        string? phone = null,
        string? email = null,
        string? positionTitle = null,
        DateOnly? hireDate = null,
        OaEmploymentStatus status = OaEmploymentStatus.Employed,
        string? otherInfo = null,
        string? weComUserId = null,
        string? dingTalkUserId = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("员工用户不能为空。", nameof(userId));
        UserId = userId;
        Edit(employeeNo, phone, email, positionTitle, hireDate, status, otherInfo, weComUserId, dingTalkUserId);
    }

    public void Edit(
        string? employeeNo,
        string? phone,
        string? email,
        string? positionTitle,
        DateOnly? hireDate,
        OaEmploymentStatus status,
        string? otherInfo,
        string? weComUserId = null,
        string? dingTalkUserId = null)
    {
        EmployeeNo = Clean(employeeNo);
        Phone = Clean(phone);
        Email = Clean(email);
        WeComUserId = Clean(weComUserId, 200);
        DingTalkUserId = Clean(dingTalkUserId, 200);
        PositionTitle = Clean(positionTitle);
        HireDate = hireDate;
        Status = status;
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    private static string? Clean(string? value, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = value.Trim();
        if (maxLength is int length && cleaned.Length > length) throw new ArgumentException($"字段长度不能超过 {length} 个字符。", nameof(value));
        return cleaned;
    }
}
