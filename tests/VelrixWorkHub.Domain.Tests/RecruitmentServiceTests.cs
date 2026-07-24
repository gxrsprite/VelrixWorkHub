using VelrixWorkHub.Application.Recruitment;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class RecruitmentServiceTests
{
    [Fact]
    public void Candidate_ValidatesRequiredFieldsAndOtherInfo()
    {
        var candidate = new OaRecruitmentCandidate(" 张三 ", " 项目经理 ", null, null, "内推", null, "{\"school\":\"A\"}", DateTime.Now);

        Assert.Equal("张三", candidate.CandidateName);
        Assert.Equal(OaRecruitmentCandidateStatus.Active, candidate.Status);
        Assert.Throws<ArgumentException>(() => new OaRecruitmentCandidate("", "开发", null, null, null, null, null, DateTime.Now));
        Assert.Throws<ArgumentException>(() => new OaRecruitmentCandidate("张三", "开发", null, null, null, null, "[]", DateTime.Now));
    }

    [Fact]
    public void Service_RequiresPassedInterviewBeforeHiring()
    {
        var repository = new TestRepository();
        var service = new RecruitmentService(repository);
        var candidate = service.CreateCandidate("张三", "项目经理", null, null, null, null, null);
        var interview = service.ScheduleInterview(candidate, 1, DateTime.Now.AddDays(1), "admin", null);

        Assert.Throws<InvalidOperationException>(() => service.SetCandidateStatus(candidate, OaRecruitmentCandidateStatus.Hired));
        service.CompleteInterview(interview, OaInterviewResult.Pass, "沟通和项目经验符合岗位要求", null);
        service.SetCandidateStatus(candidate, OaRecruitmentCandidateStatus.Hired);

        Assert.Equal(OaRecruitmentCandidateStatus.Hired, candidate.Status);
        Assert.Equal(OaInterviewResult.Pass, interview.Result);
    }

    [Fact]
    public void Service_PreventsDuplicateRoundsAndEditingHiredCandidate()
    {
        var repository = new TestRepository();
        var service = new RecruitmentService(repository);
        var candidate = service.CreateCandidate("李四", "后端工程师", null, null, null, null, null);
        service.ScheduleInterview(candidate, 1, DateTime.Now.AddDays(1), "admin", null);

        Assert.Throws<InvalidOperationException>(() => service.ScheduleInterview(candidate, 1, DateTime.Now.AddDays(2), "admin", null));
        var second = service.ScheduleInterview(candidate, 2, DateTime.Now.AddDays(2), "admin", null);
        service.CompleteInterview(second, OaInterviewResult.Pass, "通过", null);
        service.SetCandidateStatus(candidate, OaRecruitmentCandidateStatus.Hired);

        Assert.Throws<InvalidOperationException>(() => service.EditCandidate(candidate, "李四", "技术负责人", null, null, null, null, null));
    }

    [Fact]
    public void Service_FiltersCandidatesAndRequiresInterviewEvaluation()
    {
        var repository = new TestRepository();
        var service = new RecruitmentService(repository);
        var candidate = service.CreateCandidate("王五", "财务经理", "13800001234", null, "招聘网站", null, null);
        service.CreateCandidate("赵六", "项目经理", null, null, "内推", null, null);
        var interview = service.ScheduleInterview(candidate, 1, DateTime.Now.AddDays(1), "admin", null);

        Assert.Single(service.ListCandidates("财务"));
        Assert.Throws<ArgumentException>(() => service.CompleteInterview(interview, OaInterviewResult.Fail, " ", null));
    }

    private sealed class TestRepository : IOaRecruitmentRepository
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
}
