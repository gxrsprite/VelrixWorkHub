using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Assets;

[Table(Name = "OaAssetRequest")]
[Index("OaAssetRequest_ix_ApplicantCreatedAt", nameof(ApplicantUserId) + "," + nameof(CreatedAt), false)]
[Index("OaAssetRequest_ix_AssetStatus", nameof(AssetId) + "," + nameof(Status), false)]
public sealed class OaAssetRequestRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid AssetId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid ApplicantUserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string ApplicantName { get; set; } = string.Empty;
    [Column(StringLength = 2000, IsNullable = false, Position = 5)] public string Reason { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 6)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 7)] public OaAssetRequestStatus Status { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 8)] public string? RejectionReason { get; set; }
    [Column(IsNullable = true, Position = 9)] public Guid? AssignmentId { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 10)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 11)] public DateTime? SubmittedAt { get; set; }
    [Column(IsNullable = true, Position = 12)] public DateTime? ApprovedAt { get; set; }
}
