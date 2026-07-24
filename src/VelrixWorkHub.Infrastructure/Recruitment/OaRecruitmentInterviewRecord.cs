using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Recruitment;

[Table(Name = "OaRecruitmentInterview")]
[Index("OaRecruitmentInterview_uk_Candidate_Round", nameof(CandidateId) + ",Round", true)]
public sealed class OaRecruitmentInterviewRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid CandidateId { get; set; }
    [Column(IsNullable = false, Position = 3)] public int Round { get; set; }
    [Column(IsNullable = false, Position = 4)] public DateTime ScheduledAt { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string Interviewer { get; set; } = string.Empty;
    [Column(StringLength = 4000, Position = 6)] public string? Evaluation { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 7)] public OaInterviewResult Result { get; set; }
    [Column(StringLength = 4000, Position = 8)] public string? Notes { get; set; }
    [Column(Position = 9, ServerTime = DateTimeKind.Local)] public DateTime CreatedAt { get; set; }
}
