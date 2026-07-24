using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Assets;

[Table(Name = "OaAssetAssignment")]
[Index("OaAssetAssignment_ix_AssetId", nameof(AssetId), false)]
[Index("OaAssetAssignment_ix_UserId", nameof(UserId), false)]
public sealed class OaAssetAssignmentRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid AssetId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid UserId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 4)] public OaAssetAssignmentStatus Status { get; set; }
    [Column(Position = 5, ServerTime = DateTimeKind.Local)] public DateTime AssignedAt { get; set; }
    [Column(Position = 6)] public DateTime? ReturnedAt { get; set; }
}
