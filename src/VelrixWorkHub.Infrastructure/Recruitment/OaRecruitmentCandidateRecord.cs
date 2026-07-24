using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Recruitment;

[Table(Name = "OaRecruitmentCandidate")]
public sealed class OaRecruitmentCandidateRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 2)] public string CandidateName { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 3)] public string PositionTitle { get; set; } = string.Empty;
    [Column(StringLength = 50, Position = 4)] public string? Phone { get; set; }
    [Column(StringLength = 200, Position = 5)] public string? Email { get; set; }
    [Column(StringLength = 100, Position = 6)] public string? Source { get; set; }
    [Column(StringLength = 4000, Position = 7)] public string? ResumeSummary { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 8)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, Position = 9)] public OaRecruitmentCandidateStatus Status { get; set; }
    [Column(Position = 10, ServerTime = DateTimeKind.Local)] public DateTime CreatedAt { get; set; }
}
