using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

[Table(Name = "LmsCustomerFeature")]
[Index("LmsCustomerFeature_uk_CustomerId_FeatureVersionId", "CustomerId,FeatureVersionId", true)]
public sealed class LmsCustomerFeatureRecord
{
    [Column(IsPrimary = true)] public Guid Id { get; set; }
    [Column(IsNullable = false)] public Guid CustomerId { get; set; }
    [Column(IsNullable = false)] public Guid FeatureVersionId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    [Column(StringLength = 1000)] public string? Notes { get; set; }
    [Column(StringLength = -1, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false)] public LmsCustomerFeatureStatus Status { get; set; }
    [Column(IsNullable = false)] public DateTime CreatedAt { get; set; }
}
