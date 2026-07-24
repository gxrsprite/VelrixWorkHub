using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

[Table(Name = "LmsLicenseReplacementRequest")]
[Index("LmsLicenseReplacementRequest_uk_RequestNo", "RequestNo", true)]
public sealed class LmsLicenseReplacementRequestRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string RequestNo { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 3)] public Guid OriginalAuthorizationId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 4)] public LmsLicenseReplacementKind Kind { get; set; }
    [Column(Position = 5)] public Guid? TargetMachineId { get; set; }
    [Column(StringLength = 120, IsNullable = false, Position = 6)] public string LicenseNo { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 7)] public string ExternalLicense { get; set; } = string.Empty;
    [Column(Position = 8)] public DateTime? ExpiresAt { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 9)] public string OtherInfo { get; set; } = "{}";
    [Column(StringLength = 100, IsNullable = false, Position = 10)] public string Applicant { get; set; } = string.Empty;
    [Column(StringLength = 500, IsNullable = false, Position = 11)] public string Reason { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 12)] public LmsLicenseReplacementRequestStatus Status { get; set; }
    [Column(IsNullable = false, Position = 13)] public DateTime CreatedAt { get; set; }
}
