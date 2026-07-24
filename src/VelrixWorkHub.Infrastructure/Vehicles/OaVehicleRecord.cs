using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Vehicles;

[Table(Name = "OaVehicle")]
[Index("OaVehicle_uk_PlateNumber", nameof(PlateNumber), true)]
public sealed class OaVehicleRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 50, IsNullable = false, Position = 2)] public string PlateNumber { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 3)] public string VehicleType { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 4)] public string BrandModel { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 5)] public int SeatCount { get; set; }
    [Column(IsNullable = true, Position = 6)] public Guid? ResponsibleUserId { get; set; }
    [Column(IsNullable = true, Position = 7)] public DateTime? AnnualInspectionExpiresOn { get; set; }
    [Column(IsNullable = true, Position = 8)] public DateTime? InsuranceExpiresOn { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 9)] public OaVehicleStatus Status { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 10)] public string OtherInfo { get; set; } = "{}";
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 11)] public DateTime CreatedAt { get; set; }
}

[Table(Name = "OaVehicleUseRequest")]
[Index("OaVehicleUseRequest_ix_VehicleId", nameof(VehicleId), false)]
public sealed class OaVehicleUseRequestRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid VehicleId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid ApplicantUserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string ApplicantName { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string DriverName { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 6)] public DateTime StartAt { get; set; }
    [Column(IsNullable = false, Position = 7)] public DateTime EndAt { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = true, Position = 8)] public decimal? StartMileage { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = true, Position = 9)] public decimal? EndMileage { get; set; }
    [Column(StringLength = 300, IsNullable = false, Position = 10)] public string Destination { get; set; } = string.Empty;
    [Column(StringLength = 2000, IsNullable = false, Position = 11)] public string Purpose { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 12)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 13)] public OaVehicleUseRequestStatus Status { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 14)] public string? RejectionReason { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 15)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 16)] public DateTime? SubmittedAt { get; set; }
    [Column(IsNullable = true, Position = 17)] public DateTime? ReturnedAt { get; set; }
}
