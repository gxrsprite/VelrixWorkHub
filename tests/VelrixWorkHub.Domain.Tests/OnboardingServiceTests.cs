using VelrixWorkHub.Application.Onboarding;
using VelrixWorkHub.Application.Recruitment;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class OnboardingServiceTests
{
    [Fact]
    public void Create_RequiresHiredCandidateAndIsUnique()
    {
        var recruitmentRepository = new RecruitmentRepository();
        var recruitment = new RecruitmentService(recruitmentRepository);
        var candidate = recruitment.CreateCandidate("张三", "项目经理", null, null, null, null, null);
        var onboarding = new OnboardingService(new OnboardingRepository(), recruitment);

        Assert.Throws<InvalidOperationException>(() => onboarding.Create(candidate.Id, "E001", "项目部", "项目经理", DateOnly.FromDateTime(DateTime.Today), null, null, null));
        var interview = recruitment.ScheduleInterview(candidate, 1, DateTime.Now.AddDays(1), "admin", null);
        recruitment.CompleteInterview(interview, OaInterviewResult.Pass, "通过", null);
        recruitment.SetCandidateStatus(candidate, OaRecruitmentCandidateStatus.Hired);
        onboarding.Create(candidate.Id, "E001", "项目部", "项目经理", DateOnly.FromDateTime(DateTime.Today), null, "入职培训", null);

        Assert.Throws<InvalidOperationException>(() => onboarding.Create(candidate.Id, "E002", "项目部", "项目经理", DateOnly.FromDateTime(DateTime.Today), null, null, null));
    }

    [Fact]
    public void Checklist_TransitionsAndCompletionRequiresAllItems()
    {
        var (service, record) = CreateServiceAndRecord();
        Assert.Equal(OaOnboardingStatus.Pending, record.Status);

        service.UpdateChecklist(record, true, false, false, false);
        Assert.Equal(OaOnboardingStatus.InProgress, record.Status);
        Assert.Throws<InvalidOperationException>(() => service.Complete(record));

        service.UpdateChecklist(record, true, true, true, true);
        service.Complete(record);
        Assert.Equal(OaOnboardingStatus.Completed, record.Status);
        Assert.Throws<InvalidOperationException>(() => service.UpdateChecklist(record, true, true, true, true));
    }

    [Fact]
    public void Record_ValidatesDatesAndOtherInfo()
    {
        var candidateId = Guid.CreateVersion7();
        Assert.Throws<ArgumentException>(() => new OaOnboardingRecord(candidateId, "E001", "项目部", "项目经理", new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 19), null, null, DateTime.Now));
        Assert.Throws<ArgumentException>(() => new OaOnboardingRecord(candidateId, "E001", "项目部", "项目经理", new DateOnly(2026, 7, 20), null, null, "[]", DateTime.Now));
    }

    private static (OnboardingService Service, OaOnboardingRecord Record) CreateServiceAndRecord()
    {
        var recruitmentRepository = new RecruitmentRepository();
        var recruitment = new RecruitmentService(recruitmentRepository);
        var candidate = recruitment.CreateCandidate("李四", "后端工程师", null, null, null, null, null);
        var interview = recruitment.ScheduleInterview(candidate, 1, DateTime.Now.AddDays(1), "admin", null);
        recruitment.CompleteInterview(interview, OaInterviewResult.Pass, "通过", null);
        recruitment.SetCandidateStatus(candidate, OaRecruitmentCandidateStatus.Hired);
        var repository = new OnboardingRepository();
        var service = new OnboardingService(repository, recruitment);
        return (service, service.Create(candidate.Id, "E002", "研发部", "后端工程师", DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddMonths(3)), null, null));
    }

    private sealed class RecruitmentRepository : IOaRecruitmentRepository
    {
        private readonly List<OaRecruitmentCandidate> candidates = [];
        private readonly List<OaRecruitmentInterview> interviews = [];
        public IReadOnlyList<OaRecruitmentCandidate> ListCandidates() => candidates;
        public OaRecruitmentCandidate? GetCandidate(Guid candidateId) => candidates.FirstOrDefault(item => item.Id == candidateId);
        public IReadOnlyList<OaRecruitmentInterview> ListInterviews(Guid? candidateId = null) => candidateId is Guid id ? interviews.Where(item => item.CandidateId == id).ToArray() : interviews;
        public void AddCandidate(OaRecruitmentCandidate candidate) => candidates.Add(candidate);
        public void UpdateCandidate(OaRecruitmentCandidate candidate) { }
        public void AddInterview(OaRecruitmentInterview interview) => interviews.Add(interview);
        public void UpdateInterview(OaRecruitmentInterview interview) { }
    }

    private sealed class OnboardingRepository : IOaOnboardingRepository
    {
        private readonly List<OaOnboardingRecord> records = [];
        public IReadOnlyList<OaOnboardingRecord> List() => records;
        public OaOnboardingRecord? Get(Guid id) => records.FirstOrDefault(item => item.Id == id);
        public OaOnboardingRecord? GetByCandidate(Guid candidateId) => records.FirstOrDefault(item => item.CandidateId == candidateId);
        public void Add(OaOnboardingRecord record) => records.Add(record);
        public void Update(OaOnboardingRecord record) { }
    }
}
