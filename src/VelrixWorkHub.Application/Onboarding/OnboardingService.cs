using VelrixWorkHub.Application.Recruitment;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Onboarding;

public interface IOaOnboardingRepository
{
    IReadOnlyList<OaOnboardingRecord> List();
    OaOnboardingRecord? Get(Guid id);
    OaOnboardingRecord? GetByCandidate(Guid candidateId);
    void Add(OaOnboardingRecord record);
    void Update(OaOnboardingRecord record);
}

public sealed class OnboardingService(IOaOnboardingRepository repository, RecruitmentService recruitment)
{
    public IReadOnlyList<OaOnboardingRecord> List() => repository.List().OrderByDescending(item => item.StartDate).ThenBy(item => item.EmployeeNo).ToArray();

    public OaOnboardingRecord Create(Guid candidateId, string employeeNo, string departmentName, string positionTitle,
        DateOnly startDate, DateOnly? probationEndDate, string? trainingPlan, string? otherInfo)
    {
        var candidate = recruitment.GetCandidate(candidateId) ?? throw new InvalidOperationException("候选人不存在。");
        if (candidate.Status != OaRecruitmentCandidateStatus.Hired) throw new InvalidOperationException("只有已录用候选人才能办理入职。");
        if (repository.GetByCandidate(candidateId) is not null) throw new InvalidOperationException("该候选人已存在入职办理记录。");
        var record = new OaOnboardingRecord(candidateId, employeeNo, departmentName, positionTitle, startDate, probationEndDate, trainingPlan, otherInfo, DateTime.Now);
        repository.Add(record);
        return record;
    }

    public void Edit(OaOnboardingRecord record, string employeeNo, string departmentName, string positionTitle,
        DateOnly startDate, DateOnly? probationEndDate, string? trainingPlan, string? otherInfo)
    {
        if (record.Status == OaOnboardingStatus.Completed) throw new InvalidOperationException("已完成入职的记录不能再修改。");
        record.Edit(employeeNo, departmentName, positionTitle, startDate, probationEndDate, trainingPlan, otherInfo);
        repository.Update(record);
    }

    public void UpdateChecklist(OaOnboardingRecord record, bool documentsSubmitted, bool contractSigned, bool accountRequested, bool trainingCompleted)
    {
        record.UpdateChecklist(documentsSubmitted, contractSigned, accountRequested, trainingCompleted);
        repository.Update(record);
    }

    public void Complete(OaOnboardingRecord record)
    {
        record.Complete();
        repository.Update(record);
    }
}
