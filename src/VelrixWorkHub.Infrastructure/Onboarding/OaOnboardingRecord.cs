using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Onboarding;

[Table(Name = "OaOnboarding")]
[Index("OaOnboarding_uk_CandidateId", nameof(CandidateId), true)]
public sealed class OaOnboardingRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid CandidateId { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 3)] public string EmployeeNo { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string DepartmentName { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string PositionTitle { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 6)] public DateTime StartDate { get; set; }
    [Column(Position = 7)] public DateTime? ProbationEndDate { get; set; }
    [Column(StringLength = 4000, Position = 8)] public string? TrainingPlan { get; set; }
    [Column(IsNullable = false, Position = 9)] public bool DocumentsSubmitted { get; set; }
    [Column(IsNullable = false, Position = 10)] public bool ContractSigned { get; set; }
    [Column(IsNullable = false, Position = 11)] public bool AccountRequested { get; set; }
    [Column(IsNullable = false, Position = 12)] public bool TrainingCompleted { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 13)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, Position = 14)] public OaOnboardingStatus Status { get; set; }
    [Column(Position = 15, ServerTime = DateTimeKind.Local)] public DateTime CreatedAt { get; set; }
}
