namespace VelrixWorkHub.Domain;

public enum OaRecruitmentCandidateStatus
{
    Active,
    Hired,
    Rejected,
    Withdrawn
}

public enum OaInterviewResult
{
    Pending,
    Pass,
    Fail,
    Reschedule
}

public sealed class OaRecruitmentCandidate
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string CandidateName { get; private set; } = string.Empty;
    public string PositionTitle { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Source { get; private set; }
    public string? ResumeSummary { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public OaRecruitmentCandidateStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public OaRecruitmentCandidate(
        string candidateName,
        string positionTitle,
        string? phone,
        string? email,
        string? source,
        string? resumeSummary,
        string? otherInfo,
        DateTime createdAt)
    {
        Edit(candidateName, positionTitle, phone, email, source, resumeSummary, otherInfo);
        CreatedAt = createdAt;
        Status = OaRecruitmentCandidateStatus.Active;
    }

    public void Edit(string candidateName, string positionTitle, string? phone, string? email, string? source, string? resumeSummary, string? otherInfo)
    {
        if (string.IsNullOrWhiteSpace(candidateName)) throw new ArgumentException("候选人姓名不能为空。", nameof(candidateName));
        if (string.IsNullOrWhiteSpace(positionTitle)) throw new ArgumentException("应聘岗位不能为空。", nameof(positionTitle));
        CandidateName = candidateName.Trim();
        PositionTitle = positionTitle.Trim();
        Phone = Clean(phone);
        Email = Clean(email);
        Source = Clean(source);
        ResumeSummary = Clean(resumeSummary);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void SetStatus(OaRecruitmentCandidateStatus status)
    {
        if (Status == OaRecruitmentCandidateStatus.Hired && status != OaRecruitmentCandidateStatus.Hired)
            throw new InvalidOperationException("已录用候选人不能直接改回其他状态。");
        Status = status;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class OaRecruitmentInterview
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CandidateId { get; private set; }
    public int Round { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public string Interviewer { get; private set; } = string.Empty;
    public string? Evaluation { get; private set; }
    public OaInterviewResult Result { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public OaRecruitmentInterview(Guid candidateId, int round, DateTime scheduledAt, string interviewer, string? notes, DateTime createdAt)
    {
        if (candidateId == Guid.Empty) throw new ArgumentException("候选人不能为空。", nameof(candidateId));
        CandidateId = candidateId;
        Edit(round, scheduledAt, interviewer, notes);
        CreatedAt = createdAt;
        Result = OaInterviewResult.Pending;
    }

    public void Edit(int round, DateTime scheduledAt, string interviewer, string? notes)
    {
        if (round <= 0) throw new ArgumentOutOfRangeException(nameof(round), "面试轮次必须大于 0。");
        if (string.IsNullOrWhiteSpace(interviewer)) throw new ArgumentException("面试官不能为空。", nameof(interviewer));
        Round = round;
        ScheduledAt = scheduledAt;
        Interviewer = interviewer.Trim();
        Notes = Clean(notes);
    }

    public void Complete(OaInterviewResult result, string? evaluation, string? notes)
    {
        if (result == OaInterviewResult.Pending) throw new InvalidOperationException("面试结论不能保持待评价。");
        if (string.IsNullOrWhiteSpace(evaluation)) throw new ArgumentException("面试评价不能为空。", nameof(evaluation));
        Result = result;
        Evaluation = evaluation.Trim();
        Notes = Clean(notes);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
