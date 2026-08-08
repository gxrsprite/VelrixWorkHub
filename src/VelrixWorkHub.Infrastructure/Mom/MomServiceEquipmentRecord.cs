using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

[Table(Name = "MomServiceEquipment")]
[Index("MomServiceEquipment_uk_EquipmentNo", "EquipmentNo", true)]
[Index("MomServiceEquipment_uk_SerialNo", "SerialNo", true)]
[Index("MomServiceEquipment_ix_CustomerId", "CustomerId", false)]
[Index("MomServiceEquipment_ix_ShipmentId", "ShipmentId", false)]
public sealed class MomServiceEquipmentRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, StringLength = 80, IsNullable = false)] public string EquipmentNo { get; set; } = string.Empty;
    [Column(Position = 3, IsNullable = false)] public Guid CustomerId { get; set; }
    [Column(Position = 4, IsNullable = false)] public Guid ProductId { get; set; }
    [Column(Position = 5, IsNullable = false)] public Guid SalesOrderId { get; set; }
    [Column(Position = 6, IsNullable = false)] public Guid ShipmentId { get; set; }
    [Column(Position = 7, StringLength = 80, IsNullable = true)] public string? ShipmentSourceNo { get; set; }
    [Column(Position = 8, IsNullable = true)] public Guid? PmsProjectId { get; set; }
    [Column(Position = 9, StringLength = 100, IsNullable = false)] public string SerialNo { get; set; } = string.Empty;
    [Column(Position = 10, StringLength = 200, IsNullable = true)] public string? Model { get; set; }
    [Column(Position = 11, StringLength = 300, IsNullable = true)] public string? InstallationLocation { get; set; }
    [Column(Position = 12, StringLength = 100, IsNullable = true)] public string? InstalledBy { get; set; }
    [Column(Position = 13, IsNullable = true)] public DateTime? InstalledOn { get; set; }
    [Column(Position = 14, IsNullable = true)] public DateTime? WarrantyStartDate { get; set; }
    [Column(Position = 15, IsNullable = true)] public DateTime? WarrantyEndDate { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 16, IsNullable = false)] public MomServiceEquipmentStatus Status { get; set; }
    [Column(Position = 17, StringLength = 100, IsNullable = false)] public string CreatedBy { get; set; } = string.Empty;
    [Column(Position = 18, IsNullable = false)] public DateTime CreatedOn { get; set; }
    [Column(Position = 19, StringLength = 100, IsNullable = true)] public string? RetiredBy { get; set; }
    [Column(Position = 20, IsNullable = true)] public DateTime? RetiredOn { get; set; }
    [Column(Position = 21, StringLength = 1000, IsNullable = true)] public string? RetiredReason { get; set; }
    [Column(Position = 22, StringLength = 1000, IsNullable = true)] public string? Notes { get; set; }
    [Column(StringLength = -1, Position = 23, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}

[Table(Name = "MomServiceEquipmentLifecycleEntry")]
[Index("MomServiceEquipmentLifecycleEntry_ix_EquipmentId_OccurredOn", "EquipmentId,OccurredOn", false)]
public sealed class MomServiceEquipmentLifecycleEntryRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(Position = 2, IsNullable = false)] public Guid EquipmentId { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 3, IsNullable = false)] public MomServiceEquipmentLifecycleAction Action { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 4, IsNullable = true)] public MomServiceEquipmentStatus? FromStatus { get; set; }
    [Column(MapType = typeof(string), StringLength = 50, Position = 5, IsNullable = false)] public MomServiceEquipmentStatus ToStatus { get; set; }
    [Column(Position = 6, StringLength = 100, IsNullable = false)] public string Actor { get; set; } = string.Empty;
    [Column(Position = 7, IsNullable = false)] public DateTime OccurredOn { get; set; }
    [Column(Position = 8, StringLength = 1000, IsNullable = true)] public string? Reason { get; set; }
    [Column(StringLength = -1, Position = 9, IsNullable = false)] public string OtherInfo { get; set; } = "{}";
}
