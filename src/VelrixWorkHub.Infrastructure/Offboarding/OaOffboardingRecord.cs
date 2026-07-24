using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Offboarding;

[Table(Name = "OaOffboarding")]
[Index("OaOffboarding_uk_UserId", nameof(UserId), true)]
public sealed class OaOffboardingRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid UserId { get; set; }
    [Column(IsNullable = false, Position = 3)] public DateTime LastWorkDate { get; set; }
    [Column(StringLength = 1000, IsNullable = false, Position = 4)] public string Reason { get; set; } = string.Empty;
    [Column(StringLength = 4000, Position = 5)] public string? HandoverSummary { get; set; }
    [Column(IsNullable = false, Position = 6)] public bool HandoverCompleted { get; set; }
    [Column(IsNullable = false, Position = 7)] public bool AssetsReturned { get; set; }
    [Column(IsNullable = false, Position = 8)] public bool VehiclesReturned { get; set; }
    [Column(IsNullable = false, Position = 9)] public bool DocumentsReturned { get; set; }
    [Column(IsNullable = false, Position = 10)] public bool AccessRevocationRequested { get; set; }
    [Column(IsNullable = false, Position = 11)] public bool AccountDisabled { get; set; }
    [Column(Position = 12)] public DateTime? AccountDisabledAt { get; set; }
    [Column(StringLength = 100, Position = 13)] public string? AccountDisabledBy { get; set; }
    [Column(StringLength = 1000, Position = 14)] public string? AccountDisableReason { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 15)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, Position = 16)] public OaOffboardingStatus Status { get; set; }
    [Column(Position = 17, ServerTime = DateTimeKind.Local)] public DateTime CreatedAt { get; set; }
    [Column(Position = 18)] public DateTime? CompletedAt { get; set; }
}
