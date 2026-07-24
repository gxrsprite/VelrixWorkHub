namespace VelrixWorkHub.Domain;

public enum OaOnboardingStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled
}

public sealed class OaOnboardingRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid CandidateId { get; private set; }
    public string EmployeeNo { get; private set; } = string.Empty;
    public string DepartmentName { get; private set; } = string.Empty;
    public string PositionTitle { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly? ProbationEndDate { get; private set; }
    public string? TrainingPlan { get; private set; }
    public bool DocumentsSubmitted { get; private set; }
    public bool ContractSigned { get; private set; }
    public bool AccountRequested { get; private set; }
    public bool TrainingCompleted { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public OaOnboardingStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public OaOnboardingRecord(Guid candidateId, string employeeNo, string departmentName, string positionTitle,
        DateOnly startDate, DateOnly? probationEndDate, string? trainingPlan, string? otherInfo, DateTime createdAt)
    {
        if (candidateId == Guid.Empty) throw new ArgumentException("候选人不能为空。", nameof(candidateId));
        CandidateId = candidateId;
        CreatedAt = createdAt;
        Edit(employeeNo, departmentName, positionTitle, startDate, probationEndDate, trainingPlan, otherInfo);
        Status = OaOnboardingStatus.Pending;
    }

    public void Edit(string employeeNo, string departmentName, string positionTitle, DateOnly startDate,
        DateOnly? probationEndDate, string? trainingPlan, string? otherInfo)
    {
        if (string.IsNullOrWhiteSpace(employeeNo)) throw new ArgumentException("工号不能为空。", nameof(employeeNo));
        if (string.IsNullOrWhiteSpace(departmentName)) throw new ArgumentException("部门不能为空。", nameof(departmentName));
        if (string.IsNullOrWhiteSpace(positionTitle)) throw new ArgumentException("职位不能为空。", nameof(positionTitle));
        if (probationEndDate is DateOnly probation && probation < startDate)
            throw new ArgumentException("试用期结束日期不能早于入职日期。", nameof(probationEndDate));
        EmployeeNo = employeeNo.Trim();
        DepartmentName = departmentName.Trim();
        PositionTitle = positionTitle.Trim();
        StartDate = startDate;
        ProbationEndDate = probationEndDate;
        TrainingPlan = Clean(trainingPlan);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void UpdateChecklist(bool documentsSubmitted, bool contractSigned, bool accountRequested, bool trainingCompleted)
    {
        EnsureNotCompleted();
        DocumentsSubmitted = documentsSubmitted;
        ContractSigned = contractSigned;
        AccountRequested = accountRequested;
        TrainingCompleted = trainingCompleted;
        if (Status == OaOnboardingStatus.Pending && (documentsSubmitted || contractSigned || accountRequested || trainingCompleted))
            Status = OaOnboardingStatus.InProgress;
    }

    public void Complete()
    {
        EnsureNotCompleted();
        if (!DocumentsSubmitted || !ContractSigned || !AccountRequested || !TrainingCompleted)
            throw new InvalidOperationException("入职清单未全部完成。");
        Status = OaOnboardingStatus.Completed;
    }

    private void EnsureNotCompleted()
    {
        if (Status == OaOnboardingStatus.Completed) throw new InvalidOperationException("已完成入职的记录不能再修改。");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
