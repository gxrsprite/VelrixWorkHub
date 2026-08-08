using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

/// <summary>
/// MOM-04 一层 BOM MRP 计算。它只读取各模块 Application 仓储，生成冻结计划快照，不创建下游单据。
/// </summary>
public sealed class MomMaterialPlanningService(
    IMomMaterialPlanningRunRepository runRepository,
    IMomMaterialPlanningLineRepository lineRepository,
    ISalesOrderRepository salesOrderRepository,
    IPurchaseOrderRepository purchaseOrderRepository,
    IInventoryTransactionRepository inventoryRepository,
    IMomWorkOrderRepository workOrderRepository,
    IProductRepository productRepository,
    IMomManufacturingVersionRepository manufacturingVersionRepository,
    IMomManufacturingComponentRepository manufacturingComponentRepository,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<MomMaterialPlanningRun> ListRuns() => runRepository.List().OrderByDescending(x => x.ReferenceDate).ThenByDescending(x => x.PlanNo).ToArray();

    public IReadOnlyList<MomMaterialPlanningLine> ListLines(Guid planningRunId) => lineRepository.List().Where(x => x.PlanningRunId == planningRunId).OrderBy(x => x.LineNo).ToArray();

    public MomMaterialPlanningRun Simulate(string planNo, DateOnly referenceDate, DateOnly horizonDate, string? otherInfo = null)
    {
        if (runRepository.List().Any(x => x.PlanNo.Equals(planNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("MRP 批次号已存在。");
        var run = new MomMaterialPlanningRun(planNo, referenceDate, horizonDate, otherInfo);
        var lines = Calculate(run.Id, referenceDate, horizonDate);

        void Persist()
        {
            runRepository.Add(run);
            foreach (var line in lines) lineRepository.Add(line);
        }

        if (transactions is null) Persist();
        else transactions.Execute(Persist);
        return run;
    }

    public void Confirm(MomMaterialPlanningRun run)
    {
        run.Confirm();
        runRepository.Update(run);
    }

    public void Cancel(MomMaterialPlanningRun run)
    {
        run.Cancel();
        runRepository.Update(run);
    }

    private IReadOnlyList<MomMaterialPlanningLine> Calculate(Guid runId, DateOnly referenceDate, DateOnly horizonDate)
    {
        var demands = new Dictionary<Guid, DemandAccumulator>();
        var supplySources = new Dictionary<Guid, HashSet<string>>();
        var salesOrders = salesOrderRepository.List().Where(x => x.Status == SalesOrderStatus.Submitted && x.DueDate <= horizonDate).ToArray();
        var openWorkOrders = workOrderRepository.List().Where(x => (x.Status is MomWorkOrderStatus.Planned or MomWorkOrderStatus.Released or MomWorkOrderStatus.InProgress) && x.PlannedStart <= horizonDate && x.RemainingQuantity > 0).ToArray();
        var purchases = purchaseOrderRepository.List().Where(x => x.Status == PurchaseOrderStatus.Submitted && x.DueDate <= horizonDate).ToArray();
        var inventoryBalances = inventoryRepository.List().GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => Math.Max(0, x.Sum(y => y.SignedQuantity)));
        var purchaseSupply = purchases.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        var workOrderSupply = openWorkOrders.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.Sum(y => y.RemainingQuantity));

        foreach (var order in salesOrders) AddDemand(demands, order.ProductId, order.Quantity, $"SO:{order.OrderNo}");

        foreach (var workOrder in openWorkOrders)
        {
            AddSupplySource(supplySources, workOrder.ProductId, $"MO:{workOrder.WorkOrderNo}");
            var version = FindReleasedVersion(workOrder.ProductId, workOrder.PlannedStart);
            if (version is null) continue;
            foreach (var component in manufacturingComponentRepository.List().Where(x => x.ManufacturingVersionId == version.Id))
            {
                var quantity = CalculateComponentQuantity(workOrder.RemainingQuantity, component);
                AddDemand(demands, component.ComponentProductId, quantity, $"MO:{workOrder.WorkOrderNo}/{version.VersionCode}");
            }
        }

        foreach (var salesGroup in salesOrders.GroupBy(x => x.ProductId))
        {
            var existingDemand = demands.TryGetValue(salesGroup.Key, out var bucket) ? bucket.Quantity : 0;
            var available = GetSupply(inventoryBalances, purchaseSupply, workOrderSupply, salesGroup.Key);
            var shortage = Math.Max(0, existingDemand - available);
            var version = FindReleasedVersion(salesGroup.Key, salesGroup.Max(x => x.DueDate));
            if (version is null || shortage <= 0) continue;
            var source = string.Join("+", salesGroup.Select(x => $"SO:{x.OrderNo}"));
            foreach (var component in manufacturingComponentRepository.List().Where(x => x.ManufacturingVersionId == version.Id))
            {
                var quantity = CalculateComponentQuantity(shortage, component);
                AddDemand(demands, component.ComponentProductId, quantity, $"{source}/{version.VersionCode}");
            }
        }

        var productIds = new HashSet<Guid>(demands.Keys);
        productIds.UnionWith(inventoryBalances.Keys);
        productIds.UnionWith(purchaseSupply.Keys);
        productIds.UnionWith(workOrderSupply.Keys);
        var knownProductIds = productRepository.List().Select(x => x.Id).ToHashSet();
        productIds.RemoveWhere(x => !knownProductIds.Contains(x));
        var lines = new List<MomMaterialPlanningLine>();
        foreach (var productId in productIds.OrderBy(x => x))
        {
            var demand = demands.TryGetValue(productId, out var demandBucket) ? demandBucket.Quantity : 0;
            var onHand = inventoryBalances.TryGetValue(productId, out var inventory) ? inventory : 0;
            var purchaseInTransit = purchaseSupply.TryGetValue(productId, out var purchase) ? purchase : 0;
            var openWorkOrder = workOrderSupply.TryGetValue(productId, out var workOrder) ? workOrder : 0;
            var shortage = Math.Max(0, demand - onHand - purchaseInTransit - openWorkOrder);
            var version = FindReleasedVersion(productId, referenceDate);
            var recommendation = shortage <= 0 ? MomMaterialPlanningRecommendation.None : version is null ? MomMaterialPlanningRecommendation.Purchase : MomMaterialPlanningRecommendation.Production;
            var sources = new List<string>();
            if (demandBucket is not null) sources.AddRange(demandBucket.Sources);
            if (onHand > 0) sources.Add("INV:OnHand");
            if (purchaseInTransit > 0) sources.AddRange(purchases.Where(x => x.ProductId == productId).Select(x => $"PO:{x.OrderNo}"));
            if (openWorkOrder > 0) sources.AddRange(supplySources.TryGetValue(productId, out var sourceSet) ? sourceSet : []);
            if (sources.Count == 0) sources.Add("无直接需求");
            lines.Add(new MomMaterialPlanningLine(runId, lines.Count + 1, productId, Round(demand), Round(onHand), Round(purchaseInTransit), Round(openWorkOrder), recommendation, Round(shortage), version?.Id, string.Join(", ", sources.Distinct(StringComparer.OrdinalIgnoreCase).Take(20))));
        }
        return lines;
    }

    private MomManufacturingVersion? FindReleasedVersion(Guid productId, DateOnly date) => manufacturingVersionRepository.List().Where(x => x.ProductId == productId && x.Status == MomManufacturingVersionStatus.Released && x.EffectiveFrom <= date && (x.EffectiveTo is null || x.EffectiveTo >= date)).OrderByDescending(x => x.EffectiveFrom).FirstOrDefault();
    private static decimal GetSupply(IReadOnlyDictionary<Guid, decimal> inventory, IReadOnlyDictionary<Guid, decimal> purchases, IReadOnlyDictionary<Guid, decimal> workOrders, Guid productId) => (inventory.TryGetValue(productId, out var onHand) ? onHand : 0) + (purchases.TryGetValue(productId, out var purchase) ? purchase : 0) + (workOrders.TryGetValue(productId, out var workOrder) ? workOrder : 0);
    private static decimal CalculateComponentQuantity(decimal parentQuantity, MomManufacturingComponent component) => Round(parentQuantity * component.QuantityPer * (1 + component.ScrapRatePercent / 100m));
    private static decimal Round(decimal quantity) => decimal.Round(Math.Max(0, quantity), 6, MidpointRounding.AwayFromZero);
    private static void AddDemand(Dictionary<Guid, DemandAccumulator> demands, Guid productId, decimal quantity, string source) { if (!demands.TryGetValue(productId, out var bucket)) demands[productId] = bucket = new DemandAccumulator(); bucket.Quantity += quantity; bucket.Sources.Add(source); }
    private static void AddSupplySource(Dictionary<Guid, HashSet<string>> sources, Guid productId, string source) { if (!sources.TryGetValue(productId, out var set)) sources[productId] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); set.Add(source); }

    private sealed class DemandAccumulator
    {
        public decimal Quantity { get; set; }
        public HashSet<string> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
