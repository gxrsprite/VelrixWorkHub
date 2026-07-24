using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

[Table(Name = "LmsMachineFeature")]
[Index("LmsMachineFeature_uk_CustomerMachineId_FeatureVersionId", "CustomerMachineId,FeatureVersionId", true)]
public sealed class LmsMachineFeatureRecord
{
    [Column(IsPrimary = true)] public Guid Id { get; set; }
    [Column(IsNullable = false)] public Guid CustomerMachineId { get; set; }
    [Column(IsNullable = false)] public Guid FeatureVersionId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    [Column(StringLength = 1000)] public string? Notes { get; set; }
    [Column(StringLength = -1, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false)] public LmsMachineFeatureStatus Status { get; set; }
    [Column(IsNullable = false)] public DateTime CreatedAt { get; set; }
}
