using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.WorkItems;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Web.Notifications;

/// <summary>每天扫描跨模块风险并投影到统一 OA 通知；重复扫描由通知去重键抑制。</summary>
public sealed class CrossModuleReminderWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CrossModuleReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ScanAsync(stoppingToken);
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await ScanAsync(stoppingToken);
    }

    private Task ScanAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var contracts = scope.ServiceProvider.GetRequiredService<SalesContractService>().List();
            var settlementService = scope.ServiceProvider.GetRequiredService<SettlementService>();
            var balances = settlementService.OrderBalances(ErpSettlementKind.Payable)
                .Concat(settlementService.OrderBalances(ErpSettlementKind.Receivable));
            var productService = scope.ServiceProvider.GetRequiredService<ProductService>();
            var inventoryService = scope.ServiceProvider.GetRequiredService<InventoryService>();
            var inventoryByProduct = inventoryService.Balances()
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
            var inventoryRisks = productService.List(status: ProductStatus.Active)
                .Where(x => x.SafetyStock is > 0)
                .Select(x => new InventoryRiskTodo(x.Id, x.Name, x.SafetyStock!.Value, inventoryByProduct.GetValueOrDefault(x.Id)))
                .ToArray();
            var result = scope.ServiceProvider.GetRequiredService<CrossModuleReminderService>().Scan(
                DateTime.Now,
                contracts,
                balances,
                inventoryRisks,
                scope.ServiceProvider.GetRequiredService<PmsProjectIssueService>().List(),
                scope.ServiceProvider.GetRequiredService<PmsProjectPhaseService>().List());
            logger.LogInformation("跨模块提醒扫描完成：事件 {Events}，接收人 {Recipients}，通知投递尝试 {Attempts}。", result.CandidateEventCount, result.RecipientCount, result.NotificationAttemptCount);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "跨模块提醒扫描失败，将在下一轮重试。");
        }
        return Task.CompletedTask;
    }
}
