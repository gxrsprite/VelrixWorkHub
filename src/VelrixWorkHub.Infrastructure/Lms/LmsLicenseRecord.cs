using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

[Table(Name = "LmsLicenseRequest")]
[Index("LmsLicenseRequest_uk_RequestNo", "RequestNo", true)]
public sealed class LmsLicenseRequestRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 80, IsNullable = false, Position = 2)] public string RequestNo { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 3)] public string Applicant { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string ProductName { get; set; } = string.Empty;
    [Column(Position = 5)] public Guid? CustomerId { get; set; }
    [Column(Position = 6)] public Guid? ContactId { get; set; }
    [Column(Position = 7)] public Guid? CustomerMachineId { get; set; }
    [Column(StringLength = 200, Position = 8)] public string? CustomerName { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 9)] public string FeaturesJson { get; set; } = "[]";
    [Column(StringLength = -1, IsNullable = false, Position = 10)] public string FeatureVersionIdsJson { get; set; } = "[]";
    [Column(Position = 11)] public DateTime? RequestedExpiresAt { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 12)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 13)] public LmsLicenseRequestStatus Status { get; set; }
    [Column(IsNullable = false, Position = 14)] public DateTime CreatedAt { get; set; }
    [Column(StringLength = 200, Position = 15)] public string? Model { get; set; }
    [Column(StringLength = 100, Position = 16)] public string? Environment { get; set; }
    [Column(IsNullable = false, Position = 17)] public int GracePeriodDays { get; set; }
}

[Table(Name = "LmsLicenseAuthorization")]
[Index("LmsLicenseAuthorization_uk_LicenseNo", "LicenseNo", true)]
public sealed class LmsLicenseAuthorizationRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2)] public Guid? RequestId { get; set; }
    [Column(Position = 3)] public Guid? SupersedesAuthorizationId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 4)] public LmsLicenseReplacementKind? ReplacementKind { get; set; }
    [Column(StringLength = 120, IsNullable = false, Position = 5)] public string LicenseNo { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 6)] public string ExternalLicense { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 7)] public string ProductName { get; set; } = string.Empty;
    [Column(Position = 8)] public Guid? CustomerId { get; set; }
    [Column(Position = 9)] public Guid? ContactId { get; set; }
    [Column(Position = 10)] public Guid? CustomerMachineId { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 11)] public string FeaturesJson { get; set; } = "[]";
    [Column(StringLength = -1, IsNullable = false, Position = 12)] public string FeatureVersionIdsJson { get; set; } = "[]";
    [Column(Position = 13)] public DateTime? ExpiresAt { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 14)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 15)] public LmsLicenseStatus Status { get; set; }
    [Column(IsNullable = false, Position = 16)] public DateTime CreatedAt { get; set; }
    [Column(Position = 17)] public Guid? ReplacementRequestId { get; set; }
    [Column(StringLength = 200, Position = 18)] public string? Model { get; set; }
    [Column(StringLength = 100, Position = 19)] public string? Environment { get; set; }
    [Column(IsNullable = false, Position = 20)] public int GracePeriodDays { get; set; }
}

[Table(Name = "LmsLicenseLifecycleEntry")]
[Index("LmsLicenseLifecycleEntry_ix_AuthorizationOccurred", "AuthorizationId,OccurredAt", false)]
public sealed class LmsLicenseLifecycleEntryRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid AuthorizationId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 3)] public LmsLicenseLifecycleAction Action { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 4)] public LmsLicenseStatus PreviousStatus { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 5)] public LmsLicenseStatus CurrentStatus { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 6)] public string Actor { get; set; } = string.Empty;
    [Column(StringLength = 500, IsNullable = false, Position = 7)] public string Reason { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 8)] public DateTime OccurredAt { get; set; }
}
