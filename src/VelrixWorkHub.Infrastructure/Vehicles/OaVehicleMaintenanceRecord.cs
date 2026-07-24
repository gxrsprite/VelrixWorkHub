using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Vehicles;

[Table(Name = "OaVehicleMaintenance")]
[Index("OaVehicleMaintenance_ix_VehicleId", nameof(VehicleId), false)]
public sealed class OaVehicleMaintenanceRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid VehicleId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid ReporterUserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 4)] public string ReporterName { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 5)] public DateTime StartedAt { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = true, Position = 6)] public decimal? Mileage { get; set; }
    [Column(StringLength = 2000, IsNullable = false, Position = 7)] public string Description { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = true, Position = 8)] public string? ServiceProvider { get; set; }
    [Column(DbType = "numeric(18,2)", IsNullable = true, Position = 9)] public decimal? Cost { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 10)] public string OtherInfo { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 11)] public OaVehicleMaintenanceStatus Status { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 12)] public string? CompletionNotes { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 13)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 14)] public DateTime? CompletedAt { get; set; }
}
