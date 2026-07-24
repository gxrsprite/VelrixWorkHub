using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

[Table(Name = "LmsFeatureVersion")]
[Index("LmsFeatureVersion_uk_FeatureId_Version", "FeatureId,Version", true)]
public sealed class LmsFeatureVersionRecord
{
    [Column(IsPrimary = true)] public Guid Id { get; set; }
    [Column(IsNullable = false)] public Guid FeatureId { get; set; }
    [Column(StringLength = 80, IsNullable = false)] public string Version { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false)] public LmsFeatureLevel Level { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false)] public LmsFeatureScope Scope { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false)] public LmsFeatureVersionStatus Status { get; set; }
    [Column(StringLength = -1, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
    [Column(IsNullable = false)] public DateTime CreatedAt { get; set; }
}
