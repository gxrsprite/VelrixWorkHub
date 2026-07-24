using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Recruitment;

public interface IOaRecruitmentRepository
{
    IReadOnlyList<OaRecruitmentCandidate> ListCandidates();
    OaRecruitmentCandidate? GetCandidate(Guid candidateId);
    IReadOnlyList<OaRecruitmentInterview> ListInterviews(Guid? candidateId = null);
    void AddCandidate(OaRecruitmentCandidate candidate);
    void UpdateCandidate(OaRecruitmentCandidate candidate);
    void AddInterview(OaRecruitmentInterview interview);
    void UpdateInterview(OaRecruitmentInterview interview);
}

public sealed class RecruitmentService(IOaRecruitmentRepository repository)
{
    public OaRecruitmentCandidate? GetCandidate(Guid candidateId) => repository.GetCandidate(candidateId);
    public IReadOnlyList<OaRecruitmentCandidate> ListCandidates(string? keyword = null, OaRecruitmentCandidateStatus? status = null)
    {
        var text = keyword?.Trim();
        var query = repository.ListCandidates().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(text))
        {
            query = query.Where(item =>
                item.CandidateName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || item.PositionTitle.Contains(text, StringComparison.OrdinalIgnoreCase)
                || (item.Phone?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.Email?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                || (item.Source?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (status is OaRecruitmentCandidateStatus selectedStatus)
            query = query.Where(item => item.Status == selectedStatus);
        return query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.CandidateName).ToArray();
    }

    public IReadOnlyList<OaRecruitmentInterview> ListInterviews(Guid? candidateId = null) =>
        repository.ListInterviews(candidateId).OrderBy(item => item.ScheduledAt).ThenBy(item => item.Round).ToArray();

    public OaRecruitmentCandidate CreateCandidate(string candidateName, string positionTitle, string? phone, string? email, string? source, string? resumeSummary, string? otherInfo)
    {
        var candidate = new OaRecruitmentCandidate(candidateName, positionTitle, phone, email, source, resumeSummary, otherInfo, DateTime.Now);
        repository.AddCandidate(candidate);
        return candidate;
    }

    public void EditCandidate(OaRecruitmentCandidate candidate, string candidateName, string positionTitle, string? phone, string? email, string? source, string? resumeSummary, string? otherInfo)
    {
        if (candidate.Status == OaRecruitmentCandidateStatus.Hired) throw new InvalidOperationException("已录用候选人不能编辑基础资料。");
        candidate.Edit(candidateName, positionTitle, phone, email, source, resumeSummary, otherInfo);
        repository.UpdateCandidate(candidate);
    }

    public OaRecruitmentInterview ScheduleInterview(OaRecruitmentCandidate candidate, int round, DateTime scheduledAt, string interviewer, string? notes)
    {
        if (candidate.Status != OaRecruitmentCandidateStatus.Active) throw new InvalidOperationException("只有招聘中的候选人可以安排面试。");
        if (repository.ListInterviews(candidate.Id).Any(item => item.Round == round)) throw new InvalidOperationException("该候选人的面试轮次已存在。");
        var interview = new OaRecruitmentInterview(candidate.Id, round, scheduledAt, interviewer, notes, DateTime.Now);
        repository.AddInterview(interview);
        return interview;
    }

    public void CompleteInterview(OaRecruitmentInterview interview, OaInterviewResult result, string? evaluation, string? notes)
    {
        interview.Complete(result, evaluation, notes);
        repository.UpdateInterview(interview);
    }

    public void SetCandidateStatus(OaRecruitmentCandidate candidate, OaRecruitmentCandidateStatus status)
    {
        if (status == OaRecruitmentCandidateStatus.Hired
            && !repository.ListInterviews(candidate.Id).Any(item => item.Result == OaInterviewResult.Pass))
            throw new InvalidOperationException("候选人至少通过一轮面试后才能录用。");
        candidate.SetStatus(status);
        repository.UpdateCandidate(candidate);
    }
}
