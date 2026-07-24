using FreeSql;
using VelrixWorkHub.Application.Onboarding;
using VelrixWorkHub.Domain;
using OnboardingDomain = VelrixWorkHub.Domain.OaOnboardingRecord;

namespace VelrixWorkHub.Infrastructure.Onboarding;

public sealed class FreeSqlOnboardingRepository(IFreeSql fsql) : IOaOnboardingRepository
{
    public IReadOnlyList<OnboardingDomain> List() => fsql.Select<OaOnboardingRecord>().ToList().Select(ToDomain).ToArray();
    public OnboardingDomain? Get(Guid id) => fsql.Select<OaOnboardingRecord>().Where(item => item.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public OnboardingDomain? GetByCandidate(Guid candidateId) => fsql.Select<OaOnboardingRecord>().Where(item => item.CandidateId == candidateId).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OnboardingDomain record) => fsql.Insert(ToRecord(record)).ExecuteAffrows();

    public void Update(OnboardingDomain record)
    {
        var rows = fsql.Update<OaOnboardingRecord>()
            .Set(item => item.EmployeeNo, record.EmployeeNo).Set(item => item.DepartmentName, record.DepartmentName)
            .Set(item => item.PositionTitle, record.PositionTitle).Set(item => item.StartDate, record.StartDate.ToDateTime(TimeOnly.MinValue))
            .Set(item => item.ProbationEndDate, record.ProbationEndDate?.ToDateTime(TimeOnly.MinValue)).Set(item => item.TrainingPlan, record.TrainingPlan)
            .Set(item => item.DocumentsSubmitted, record.DocumentsSubmitted).Set(item => item.ContractSigned, record.ContractSigned)
            .Set(item => item.AccountRequested, record.AccountRequested).Set(item => item.TrainingCompleted, record.TrainingCompleted)
            .Set(item => item.OtherInfo, record.OtherInfo).Set(item => item.Status, record.Status)
            .Where(item => item.Id == record.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("入职办理记录不存在或已被删除。");
    }

    private static OnboardingDomain ToDomain(OaOnboardingRecord item)
    {
        var domain = new OnboardingDomain(item.CandidateId, item.EmployeeNo, item.DepartmentName, item.PositionTitle,
            DateOnly.FromDateTime(item.StartDate), item.ProbationEndDate is DateTime probation ? DateOnly.FromDateTime(probation) : null,
            item.TrainingPlan, item.OtherInfo, item.CreatedAt) { Id = item.Id };
        domain.UpdateChecklist(item.DocumentsSubmitted, item.ContractSigned, item.AccountRequested, item.TrainingCompleted);
        if (item.Status == OaOnboardingStatus.Completed) domain.Complete();
        return domain;
    }

    private static OaOnboardingRecord ToRecord(OnboardingDomain item) => new()
    {
        Id = item.Id, CandidateId = item.CandidateId, EmployeeNo = item.EmployeeNo, DepartmentName = item.DepartmentName,
        PositionTitle = item.PositionTitle, StartDate = item.StartDate.ToDateTime(TimeOnly.MinValue),
        ProbationEndDate = item.ProbationEndDate?.ToDateTime(TimeOnly.MinValue), TrainingPlan = item.TrainingPlan,
        DocumentsSubmitted = item.DocumentsSubmitted, ContractSigned = item.ContractSigned, AccountRequested = item.AccountRequested,
        TrainingCompleted = item.TrainingCompleted, OtherInfo = item.OtherInfo, Status = item.Status, CreatedAt = item.CreatedAt
    };
}
