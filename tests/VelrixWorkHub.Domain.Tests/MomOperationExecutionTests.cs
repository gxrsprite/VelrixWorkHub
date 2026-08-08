using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomOperationExecutionTests
{
    [Fact]
    public void ReleasedBomCreatesFrozenOperationsAndIsIdempotent()
    {
        var fixture = Fixture.Create();

        var first = fixture.Service.EnsureOperations(fixture.WorkOrder.Id);
        var second = fixture.Service.EnsureOperations(fixture.WorkOrder.Id);

        Assert.Equal(2, first.Count);
        Assert.Equal([10, 20], first.Select(x => x.OperationSequence).ToArray());
        Assert.Equal(first.Select(x => x.Id), second.Select(x => x.Id));
        Assert.All(first, x => Assert.Equal(MomOperationStatus.Pending, x.Status));
    }

    [Fact]
    public void OperationGenerationFreezesCrossWorkCenterRouteAndStandardHours()
    {
        var fixture = Fixture.Create();

        var operations = fixture.Service.EnsureOperations(fixture.WorkOrder.Id);

        Assert.Equal(fixture.SecondWorkCenter.Id, operations[1].WorkCenterId);
        Assert.Equal(3m, operations[1].StandardHours);
        Assert.Equal(1m, operations[1].StandardSetupHours);
        Assert.Equal(0.2m, operations[1].StandardRunHoursPerUnit);
    }

    [Fact]
    public void WorkOrderCompletionGateRequiresEveryGeneratedOperationToBeCompleted()
    {
        var fixture = Fixture.Create();
        var operations = fixture.Service.EnsureOperations(fixture.WorkOrder.Id);
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureWorkOrderCanComplete(fixture.WorkOrder.Id));
        fixture.Service.Accept(operations[0].Id, "operator");
        fixture.Service.Start(operations[0].Id);
        fixture.Service.Report(operations[0].Id, 10m, 0m, "operator");
        fixture.Service.Complete(operations[0].Id);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.EnsureWorkOrderCanComplete(fixture.WorkOrder.Id));

        fixture.Service.Accept(operations[1].Id, "operator");
        fixture.Service.Start(operations[1].Id);
        fixture.Service.Report(operations[1].Id, 10m, 0m, "operator");
        fixture.Service.Complete(operations[1].Id);

        fixture.Service.EnsureWorkOrderCanComplete(fixture.WorkOrder.Id);
    }

    [Fact]
    public void CompletedOperationCorrectionRollbackRestoresCompletedLifecycle()
    {
        var transaction = new RollbackOnCallBoundary { FailOnCall = int.MaxValue };
        var fixture = Fixture.Create(transaction, failCorrection: true);
        var operation = Assert.Single(fixture.Service.EnsureOperations(fixture.WorkOrder.Id).Take(1));
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);
        fixture.Service.Accept(operation.Id, "operator");
        fixture.Service.Start(operation.Id);
        var report = fixture.Service.Report(operation.Id, 10m, 0m, "operator");
        fixture.Service.Complete(operation.Id);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.CorrectReport(report.Id, 1m, 0m, "operator"));

        var restored = fixture.Service.List(fixture.WorkOrder.Id).First();
        Assert.Equal(MomOperationStatus.Completed, restored.Status);
        Assert.Equal(10m, restored.ReportedQuantity);
        Assert.Empty(fixture.Service.ListCorrections(operation.Id));
    }

    [Fact]
    public void PredecessorMustCompleteBeforeNextOperationIsAccepted()
    {
        var fixture = Fixture.Create();
        var operations = fixture.Service.EnsureOperations(fixture.WorkOrder.Id);
        var first = operations[0]; var second = operations[1];
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.Accept(second.Id, "operator"));
        fixture.Service.Accept(first.Id, "operator");
        fixture.Service.Start(first.Id);
        fixture.Service.Report(first.Id, 10m, 0m, "operator");
        fixture.Service.Complete(first.Id);
        fixture.Service.Accept(second.Id, "operator");

        Assert.Equal(MomOperationStatus.Completed, fixture.Service.List(fixture.WorkOrder.Id)[0].Status);
        Assert.Equal(MomOperationStatus.Ready, fixture.Service.List(fixture.WorkOrder.Id)[1].Status);
    }

    [Fact]
    public void PauseResumeAndReportKeepGoodScrapTotalsAndRejectOverReport()
    {
        var fixture = Fixture.Create();
        var operation = Assert.Single(fixture.Service.EnsureOperations(fixture.WorkOrder.Id).Take(1));
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);
        fixture.Service.Accept(operation.Id, "operator");
        fixture.Service.Start(operation.Id);
        fixture.Service.Pause(operation.Id);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Report(operation.Id, 1m, 0m, "operator"));
        fixture.Service.Resume(operation.Id);
        var report = fixture.Service.Report(operation.Id, 7m, 2m, "operator", notes: "首件确认");

        Assert.Equal(9m, report.Quantity);
        Assert.Equal(7m, report.GoodQuantity);
        Assert.Equal(2m, report.ScrapQuantity);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Report(operation.Id, 2m, 0m, "operator"));
        var current = fixture.Service.List(fixture.WorkOrder.Id).First();
        Assert.Equal(MomOperationStatus.InProgress, current.Status);
        Assert.Equal(9m, current.ReportedQuantity);
        Assert.Single(fixture.Service.ListReports(operation.Id));
    }

    [Fact]
    public void ReportCorrectionKeepsOriginalReportAndReducesOnlyItsUncorrectedTotals()
    {
        var fixture = Fixture.Create();
        var operation = Assert.Single(fixture.Service.EnsureOperations(fixture.WorkOrder.Id).Take(1));
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);
        fixture.Service.Accept(operation.Id, "operator");
        fixture.Service.Start(operation.Id);
        var report = fixture.Service.Report(operation.Id, 7m, 2m, "operator");

        var correction = fixture.Service.CorrectReport(report.Id, 2m, 1m, "operator", notes: "复核发现重复计入");

        Assert.StartsWith("MORC-", correction.SourceNo, StringComparison.Ordinal);
        Assert.Equal(report.Id, correction.ReportId);
        var current = fixture.Service.List(fixture.WorkOrder.Id).First();
        Assert.Equal(5m, current.GoodQuantity);
        Assert.Equal(1m, current.ScrapQuantity);
        Assert.Equal(6m, current.ReportedQuantity);
        Assert.Single(fixture.Service.ListReports(operation.Id));
        Assert.Single(fixture.Service.ListCorrections(operation.Id));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.CorrectReport(report.Id, 6m, 0m, "operator"));
    }

    [Fact]
    public void ReportCorrectionRejectsExcessiveScrapAndReopensCompletedOperation()
    {
        var fixture = Fixture.Create();
        var operation = Assert.Single(fixture.Service.EnsureOperations(fixture.WorkOrder.Id).Take(1));
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);
        fixture.Service.Accept(operation.Id, "operator");
        fixture.Service.Start(operation.Id);
        var report = fixture.Service.Report(operation.Id, 10m, 0m, "operator");

        Assert.Throws<InvalidOperationException>(() => fixture.Service.CorrectReport(report.Id, 0m, 1m, "operator"));
        fixture.Service.Complete(operation.Id);
        fixture.Service.CorrectReport(report.Id, 1m, 0m, "operator");

        var reopened = fixture.Service.List(fixture.WorkOrder.Id).First();
        Assert.Equal(MomOperationStatus.InProgress, reopened.Status);
        Assert.Equal(9m, reopened.ReportedQuantity);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.CorrectReport(report.Id, 10m, 0m, "operator"));
    }

    [Fact]
    public void WorkLogRequiresActiveOperatorAndRejectsOverlappingIntervals()
    {
        var fixture = Fixture.Create();
        var operation = Assert.Single(fixture.Service.EnsureOperations(fixture.WorkOrder.Id).Take(1));
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);
        fixture.Service.Accept(operation.Id, "operator");
        fixture.Service.Start(operation.Id);

        var started = new DateTime(2026, 8, 7, 8, 0, 0);
        var first = fixture.Service.LogWork(operation.Id, fixture.ActiveOperator.UserId, fixture.Equipment.Id, started, started.AddHours(2), "首件装配");

        Assert.Equal(2m, first.Hours);
        Assert.StartsWith("MOWL-", first.SourceNo, StringComparison.Ordinal);
        Assert.Single(fixture.Service.ListWorkLogs(operation.Id));
        Assert.Equal(fixture.Equipment.Name, first.EquipmentName);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.LogWork(operation.Id, fixture.ActiveOperator.UserId, fixture.Equipment.Id, started.AddHours(1), started.AddHours(3)));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.LogWork(operation.Id, fixture.InactiveOperator.UserId, fixture.Equipment.Id, started.AddHours(3), started.AddHours(4)));
        Assert.Throws<InvalidOperationException>(() => fixture.Service.LogWork(operation.Id, fixture.ActiveOperator.UserId, Guid.CreateVersion7(), started.AddHours(3), started.AddHours(4)));
    }

    [Fact]
    public void OperationCompletionUsesIpqcQualityGate()
    {
        var fixture = Fixture.Create();
        var operation = Assert.Single(fixture.Service.EnsureOperations(fixture.WorkOrder.Id).Take(1));
        fixture.WorkOrder.SetStatus(MomWorkOrderStatus.InProgress);
        fixture.Service.Accept(operation.Id, "operator");
        fixture.Service.Start(operation.Id);
        fixture.Service.Report(operation.Id, 10m, 0m, "operator");

        var inspection = fixture.QualityService.Create(fixture.WorkOrder.Id, MomQualityInspectionType.Ipqc, operation.Id, null, "B-QA-001", null, 10m);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Complete(operation.Id));
        fixture.QualityService.RecordResult(inspection.Id, 10m, 0m, "inspector");
        fixture.Service.Complete(operation.Id);

        Assert.Equal(MomOperationStatus.Completed, fixture.Service.List(fixture.WorkOrder.Id).First().Status);
    }

    private sealed class Fixture
    {
        public MomWorkOrder WorkOrder { get; private init; } = null!;
        public MomOperationExecutionService Service { get; private init; } = null!;
        public MomQualityInspectionService QualityService { get; private init; } = null!;

        public static Fixture Create(IWorkflowTransactionBoundary? transactions = null, bool failCorrection = false)
        {
            var parent = new Product("FG-OP-001", "工序成品", "件", 100, null);
            var componentA = new Product("RM-OP-001", "工序组件一", "件", 10, null);
            var componentB = new Product("RM-OP-002", "工序组件二", "件", 10, null);
            var products = new InMemoryProductRepository([parent, componentA, componentB]);
            var factory = new MomFactory("FACT-OP-001", "工序工厂");
            var workCenter = new MomWorkCenter(factory.Id, "WC-OP-001", "工序工作中心", MomWorkCenterType.Assembly, 8);
            var secondWorkCenter = new MomWorkCenter(factory.Id, "WC-OP-002", "测试工作中心", MomWorkCenterType.Testing, 8);
            var workCenters = new InMemoryWorkCenterRepository([workCenter, secondWorkCenter]);
            var equipment = new MomEquipment(workCenter.Id, "EQ-OP-001", "工序设备");
            var version = new MomManufacturingVersion(parent.Id, "V-OP-001", "工序版本", DateOnly.FromDateTime(DateTime.Today));
            var versions = new InMemoryVersionRepository([version]);
            var components = new InMemoryComponentRepository([
                new MomManufacturingComponent(version.Id, 10, componentA.Id, 1, operationSequence: 10),
                new MomManufacturingComponent(version.Id, 20, componentB.Id, 1, operationSequence: 20)]);
            version.Release();
            var operationStandards = new InMemoryOperationStandardRepository();
            operationStandards.Add(new MomManufacturingOperationStandard(version.Id, 20, "TEST-020", "成品测试", secondWorkCenter.Id, 1m, 0.2m));
            var workOrder = new MomWorkOrder("MO-OP-001", parent.Id, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(3)), 10);
            workOrder.SetWorkCenter(workCenter.Id); workOrder.SetStatus(MomWorkOrderStatus.Planned); workOrder.SetStatus(MomWorkOrderStatus.Released);
            var workOrders = new InMemoryWorkOrderRepository([workOrder]);
            var warehouses = new InMemoryWarehouseRepository([new Warehouse("WH-OP-001", "工序仓", null)]);
            var inventory = new InventoryService(new InMemoryInventoryRepository(), products, warehouses);
            var requirements = new InMemoryRequirementRepository();
            var kitting = new MomMaterialKittingService(workOrders, requirements, new InMemoryMovementRepository(), new InMemoryDeliveryRepository(), new InMemoryConsumptionRepository(), new InMemoryDeliveryReversalRepository(), new InMemoryConsumptionAllocationRepository(), new InMemoryConsumptionReversalRepository(), versions, components, workCenters, products, warehouses, inventory);
            var operationRepository = new InMemoryOperationRepository();
            var reportRepository = new InMemoryReportRepository();
            var correctionRepository = new InMemoryReportCorrectionRepository(failCorrection);
            var workLogRepository = new InMemoryWorkLogRepository();
            var qualityRepository = new InMemoryQualityInspectionRepository();
            var activeOperator = new MomOperator(Guid.CreateVersion7(), "operator", "操作员");
            var inactiveOperator = new MomOperator(Guid.CreateVersion7(), "suspended", "停职员工");
            var operators = new InMemoryOperatorResolver([activeOperator]);
            var equipmentResolver = new InMemoryEquipmentResolver([new MomEquipmentOption(equipment.Id, workCenter.Id, equipment.Code, equipment.Name, equipment.Model)]);
            var qualityService = new MomQualityInspectionService(qualityRepository, workOrders, operationRepository, products);
            return new Fixture { WorkOrder = workOrder, SecondWorkCenter = secondWorkCenter, Equipment = equipment, QualityService = qualityService, Service = new MomOperationExecutionService(workOrders, operationRepository, reportRepository, correctionRepository, workLogRepository, operators, equipmentResolver, kitting, components, workCenters, operationStandards, transactions, qualityService), ActiveOperator = activeOperator, InactiveOperator = inactiveOperator };
        }

        public MomOperator ActiveOperator { get; private init; } = null!;
        public MomOperator InactiveOperator { get; private init; } = null!;
        public MomEquipment Equipment { get; private init; } = null!;
        public MomWorkCenter SecondWorkCenter { get; private init; } = null!;
    }

    private sealed class InMemoryOperationRepository : IMomWorkOrderOperationRepository
    {
        private readonly List<MomWorkOrderOperation> items = [];
        public IReadOnlyList<MomWorkOrderOperation> List() => items;
        public void Add(MomWorkOrderOperation item) => items.Add(item);
        public void Update(MomWorkOrderOperation item) { }
    }

    private sealed class InMemoryReportRepository : IMomWorkOrderOperationReportRepository
    {
        private readonly List<MomWorkOrderOperationReport> items = [];
        public IReadOnlyList<MomWorkOrderOperationReport> List() => items;
        public void Add(MomWorkOrderOperationReport item) => items.Add(item);
    }

    private sealed class InMemoryReportCorrectionRepository(bool failOnAdd = false) : IMomWorkOrderOperationReportCorrectionRepository
    {
        private readonly List<MomWorkOrderOperationReportCorrection> items = [];
        public IReadOnlyList<MomWorkOrderOperationReportCorrection> List() => items;
        public void Add(MomWorkOrderOperationReportCorrection item) { if (failOnAdd) throw new InvalidOperationException("更正记录写入失败"); items.Add(item); }
    }

    private sealed class InMemoryWorkLogRepository : IMomWorkOrderOperationWorkLogRepository
    {
        private readonly List<MomWorkOrderOperationWorkLog> items = [];
        public IReadOnlyList<MomWorkOrderOperationWorkLog> List() => items;
        public void Add(MomWorkOrderOperationWorkLog item) => items.Add(item);
    }

    private sealed class InMemoryQualityInspectionRepository : IMomQualityInspectionRepository
    {
        private readonly List<MomQualityInspection> items = [];
        public IReadOnlyList<MomQualityInspection> List() => items;
        public void Add(MomQualityInspection item) => items.Add(item);
        public void Update(MomQualityInspection item) { }
    }

    private sealed class InMemoryOperatorResolver(IReadOnlyList<MomOperator> active) : IMomOperatorResolver
    {
        public IReadOnlyList<MomOperator> ListActive() => active;
        public MomOperator? GetActive(Guid userId) => active.FirstOrDefault(x => x.UserId == userId);
    }

    private sealed class InMemoryEquipmentResolver(IReadOnlyList<MomEquipmentOption> active) : IMomEquipmentResolver
    {
        public IReadOnlyList<MomEquipmentOption> ListActive(Guid? workCenterId = null) => active.Where(x => workCenterId is null || x.WorkCenterId == workCenterId).ToArray();
        public MomEquipmentOption? GetActive(Guid equipmentId) => active.FirstOrDefault(x => x.Id == equipmentId);
    }

    private sealed class InMemoryRequirementRepository : IMomWorkOrderMaterialRequirementRepository
    {
        private readonly List<MomWorkOrderMaterialRequirement> items = [];
        public IReadOnlyList<MomWorkOrderMaterialRequirement> List() => items;
        public void Add(MomWorkOrderMaterialRequirement item) => items.Add(item);
        public void Update(MomWorkOrderMaterialRequirement item) { }
    }

    private sealed class InMemoryMovementRepository : IMomMaterialMovementRepository
    {
        private readonly List<MomMaterialMovement> items = [];
        public IReadOnlyList<MomMaterialMovement> List() => items;
        public void Add(MomMaterialMovement item) => items.Add(item);
    }

    private sealed class InMemoryDeliveryRepository : IMomMaterialDeliveryRepository
    {
        public IReadOnlyList<MomMaterialDelivery> List() => [];
        public void Add(MomMaterialDelivery item) { }
    }

    private sealed class InMemoryConsumptionRepository : IMomMaterialConsumptionRepository
    {
        public IReadOnlyList<MomMaterialConsumption> List() => [];
        public void Add(MomMaterialConsumption item) { }
    }

    private sealed class InMemoryDeliveryReversalRepository : IMomMaterialDeliveryReversalRepository
    {
        public IReadOnlyList<MomMaterialDeliveryReversal> List() => [];
        public void Add(MomMaterialDeliveryReversal item) { }
    }

    private sealed class InMemoryConsumptionAllocationRepository : IMomMaterialConsumptionAllocationRepository
    {
        public IReadOnlyList<MomMaterialConsumptionAllocation> List() => [];
        public void Add(MomMaterialConsumptionAllocation item) { }
    }

    private sealed class InMemoryConsumptionReversalRepository : IMomMaterialConsumptionReversalRepository
    {
        public IReadOnlyList<MomMaterialConsumptionReversal> List() => [];
        public void Add(MomMaterialConsumptionReversal item) { }
    }

    private sealed class InMemoryOperationStandardRepository : IMomManufacturingOperationStandardRepository
    {
        private readonly List<MomManufacturingOperationStandard> items = [];
        public IReadOnlyList<MomManufacturingOperationStandard> List() => items;
        public void Add(MomManufacturingOperationStandard item) => items.Add(item);
        public void Update(MomManufacturingOperationStandard item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryVersionRepository(IReadOnlyList<MomManufacturingVersion> seed) : IMomManufacturingVersionRepository
    {
        private readonly List<MomManufacturingVersion> items = seed.ToList();
        public IReadOnlyList<MomManufacturingVersion> List() => items;
        public void Add(MomManufacturingVersion item) => items.Add(item);
        public void Update(MomManufacturingVersion item) { }
    }

    private sealed class InMemoryComponentRepository(IReadOnlyList<MomManufacturingComponent> seed) : IMomManufacturingComponentRepository
    {
        private readonly List<MomManufacturingComponent> items = seed.ToList();
        public IReadOnlyList<MomManufacturingComponent> List() => items;
        public void Add(MomManufacturingComponent item) => items.Add(item);
        public void Update(MomManufacturingComponent item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryWorkOrderRepository(IReadOnlyList<MomWorkOrder> seed) : IMomWorkOrderRepository
    {
        private readonly List<MomWorkOrder> items = seed.ToList();
        public IReadOnlyList<MomWorkOrder> List() => items;
        public void Add(MomWorkOrder item) => items.Add(item);
        public void Update(MomWorkOrder item) { }
    }

    private sealed class InMemoryWorkCenterRepository(IReadOnlyList<MomWorkCenter> seed) : IMomWorkCenterRepository
    {
        private readonly List<MomWorkCenter> items = seed.ToList();
        public IReadOnlyList<MomWorkCenter> List() => items;
        public void Add(MomWorkCenter item) => items.Add(item);
        public void Update(MomWorkCenter item) { }
    }

    private sealed class InMemoryProductRepository(IReadOnlyList<Product> seed) : IProductRepository
    {
        private readonly List<Product> items = seed.ToList();
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) => items.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryWarehouseRepository(IReadOnlyList<Warehouse> seed) : IWarehouseRepository
    {
        private readonly List<Warehouse> items = seed.ToList();
        public IReadOnlyList<Warehouse> List() => items;
        public void Add(Warehouse item) => items.Add(item);
        public void Update(Warehouse item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
        public void AddLocation(WarehouseLocation item) { }
        public void RemoveLocation(Guid id) { }
        public void UpsertLocationProductCapacity(WarehouseLocationProductCapacity item) { }
        public void RemoveLocationProductCapacity(Guid locationId, Guid productId) { }
    }

    private sealed class InMemoryInventoryRepository : IInventoryTransactionRepository
    {
        public IReadOnlyList<InventoryTransaction> List() => [];
        public void Add(InventoryTransaction item) { }
    }

    private sealed class RollbackOnCallBoundary : IWorkflowTransactionBoundary
    {
        public int FailOnCall { get; init; }
        private int calls;
        public void Execute(Action operation, Action<Exception>? afterRollback = null)
        {
            calls++;
            try { operation(); if (calls == FailOnCall) throw new InvalidOperationException("事务故障"); }
            catch (Exception ex) { afterRollback?.Invoke(ex); throw; }
        }
    }
}
