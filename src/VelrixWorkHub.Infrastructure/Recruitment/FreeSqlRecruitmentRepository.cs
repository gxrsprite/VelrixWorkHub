using FreeSql;
using VelrixWorkHub.Application.Recruitment;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Recruitment;

public sealed class FreeSqlRecruitmentRepository(IFreeSql fsql) : IOaRecruitmentRepository
{
    public IReadOnlyList<OaRecruitmentCandidate> ListCandidates() =>
        fsql.Select<OaRecruitmentCandidateRecord>().ToList().Select(ToCandidate).ToArray();

    public OaRecruitmentCandidate? GetCandidate(Guid candidateId) =>
        fsql.Select<OaRecruitmentCandidateRecord>().Where(item => item.Id == candidateId).ToList().Select(ToCandidate).FirstOrDefault();

    public IReadOnlyList<OaRecruitmentInterview> ListInterviews(Guid? candidateId = null)
    {
        var query = fsql.Select<OaRecruitmentInterviewRecord>();
        if (candidateId is Guid id) query = query.Where(item => item.CandidateId == id);
        return query.ToList().Select(ToInterview).ToArray();
    }

    public void AddCandidate(OaRecruitmentCandidate candidate) => fsql.Insert(ToRecord(candidate)).ExecuteAffrows();

    public void UpdateCandidate(OaRecruitmentCandidate candidate)
    {
        var rows = fsql.Update<OaRecruitmentCandidateRecord>()
            .Set(item => item.CandidateName, candidate.CandidateName)
            .Set(item => item.PositionTitle, candidate.PositionTitle)
            .Set(item => item.Phone, candidate.Phone)
            .Set(item => item.Email, candidate.Email)
            .Set(item => item.Source, candidate.Source)
            .Set(item => item.ResumeSummary, candidate.ResumeSummary)
            .Set(item => item.OtherInfo, candidate.OtherInfo)
            .Set(item => item.Status, candidate.Status)
            .Where(item => item.Id == candidate.Id)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("候选人不存在或已被删除。");
    }

    public void AddInterview(OaRecruitmentInterview interview) => fsql.Insert(ToRecord(interview)).ExecuteAffrows();

    public void UpdateInterview(OaRecruitmentInterview interview)
    {
        var rows = fsql.Update<OaRecruitmentInterviewRecord>()
            .Set(item => item.ScheduledAt, interview.ScheduledAt)
            .Set(item => item.Interviewer, interview.Interviewer)
            .Set(item => item.Evaluation, interview.Evaluation)
            .Set(item => item.Result, interview.Result)
            .Set(item => item.Notes, interview.Notes)
            .Where(item => item.Id == interview.Id)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("面试记录不存在或已被删除。");
    }

    private static OaRecruitmentCandidate ToCandidate(OaRecruitmentCandidateRecord record)
    {
        var item = new OaRecruitmentCandidate(record.CandidateName, record.PositionTitle, record.Phone, record.Email, record.Source, record.ResumeSummary, record.OtherInfo, record.CreatedAt) { Id = record.Id };
        item.SetStatus(record.Status);
        return item;
    }

    private static OaRecruitmentInterview ToInterview(OaRecruitmentInterviewRecord record)
    {
        var item = new OaRecruitmentInterview(record.CandidateId, record.Round, record.ScheduledAt, record.Interviewer, record.Notes, record.CreatedAt) { Id = record.Id };
        if (record.Result != OaInterviewResult.Pending) item.Complete(record.Result, record.Evaluation, record.Notes);
        return item;
    }

    private static OaRecruitmentCandidateRecord ToRecord(OaRecruitmentCandidate item) => new()
    {
        Id = item.Id, CandidateName = item.CandidateName, PositionTitle = item.PositionTitle, Phone = item.Phone, Email = item.Email,
        Source = item.Source, ResumeSummary = item.ResumeSummary, OtherInfo = item.OtherInfo, Status = item.Status, CreatedAt = item.CreatedAt
    };

    private static OaRecruitmentInterviewRecord ToRecord(OaRecruitmentInterview item) => new()
    {
        Id = item.Id, CandidateId = item.CandidateId, Round = item.Round, ScheduledAt = item.ScheduledAt, Interviewer = item.Interviewer,
        Evaluation = item.Evaluation, Result = item.Result, Notes = item.Notes, CreatedAt = item.CreatedAt
    };
}
