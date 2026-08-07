using FreeSql;
using System.Diagnostics;
using System.Text.Json;
using BootstrapBlazor.Components;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Application.Lms;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.SalesOrders;
using VelrixWorkHub.Infrastructure.Customers;
using VelrixWorkHub.Infrastructure.Notifications;
using VelrixWorkHub.Infrastructure.Lms;
using VelrixWorkHub.Infrastructure.PmsProjects;
using VelrixWorkHub.Infrastructure.PurchaseOrders;
using VelrixWorkHub.Infrastructure.Workflow;

var connectionString = Environment.GetEnvironmentVariable("VELRIX_WORKHUB_POSTGRES_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("缺少 VELRIX_WORKHUB_POSTGRES_CONNECTION，拒绝连接数据库。");

var databaseTypeText = Environment.GetEnvironmentVariable("VELRIX_WORKHUB_WORKFLOW_PROBE_DATABASE_TYPE") ?? nameof(DataType.PostgreSQL);
if (!Enum.TryParse<DataType>(databaseTypeText, ignoreCase: true, out var databaseType) || databaseType is not (DataType.PostgreSQL or DataType.SqlServer))
    throw new InvalidOperationException("Workflow 持久化探针只允许 PostgreSQL 或 SqlServer。");

using var fsql = new FreeSqlBuilder()
    .UseConnectionString(databaseType, connectionString)
    .UseAutoSyncStructure(true)
    .Build();
fsql.CodeFirst.SyncStructure<ExternalNotificationOutboxRecord>();
if (args.Any(x => x.Equals("--external-notification", StringComparison.OrdinalIgnoreCase)))
{
    RunExternalNotificationOutboxProbe(fsql, databaseType);
    return;
}
// 探针支持从空的 PostgreSQL/SQL Server 临时库启动；原子 TryAdd 使用方言 SQL，
// 不能等待 FreeSql 在第一次 ORM 查询时才自动同步这些表。
fsql.CodeFirst.SyncStructure<WorkflowInstanceRecord>();
fsql.CodeFirst.SyncStructure<WorkflowDefinitionRecord>();
fsql.CodeFirst.SyncStructure<WorkflowTaskRecord>();
fsql.CodeFirst.SyncStructure<WorkflowOperationRecord>();
fsql.CodeFirst.SyncStructure<NotificationRecord>();
NotificationSchemaMigration.EnsureReadAtHasNoServerDefault(fsql);
WorkflowSchemaMigration.BackfillInitialRevisions(fsql);
WorkflowSchemaMigration.EnsureDefinitionVersionUniqueness(fsql);
WorkflowSchemaMigration.EnsureRunningBusinessUniqueness(fsql);

if (args.Any(x => x.Equals("--benchmark", StringComparison.OrdinalIgnoreCase)))
{
    RunWorkflowPersistenceBenchmark(fsql, databaseType);
    return;
}

var salesOrders = new FreeSqlSalesOrderRepository(fsql);
var innerInstances = new FreeSqlWorkflowInstanceRepository(fsql);
var instances = new FailingCompletionInstanceRepository(innerInstances);
var tasks = new FreeSqlWorkflowTaskRepository(fsql);
var instanceService = new WorkflowInstanceService(instances);
var order = new SalesOrder($"SO-WF-PG-{Guid.CreateVersion7():N}", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1m, 100m);
WorkflowInstance? instance = null;
WorkflowTask? task = null;

try
{
    salesOrders.Add(order);
    var definition = CreateDefinition();
    instance = instanceService.Start(definition, nameof(SalesOrder), order.Id, startedBy: "workflow-postgres-probe");
    task = new WorkflowTask(instance, definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id, "销售订单审批", "workflow-postgres-probe");
    tasks.Add(task);
    var service = new WorkflowTaskService(
        tasks,
        instanceService,
        new WorkflowActionExecutor([new SalesOrderWorkflowActionHandler(salesOrders)]),
        transactions: new FreeSqlWorkflowTransactionBoundary(fsql));

    try
    {
        service.Approve(task, "workflow-postgres-probe", "故障注入");
        throw new InvalidOperationException("故障注入未触发，流程完成不应成功。");
    }
    catch (InvalidOperationException exception) when (exception.Message == "模拟流程完成持久化失败")
    {
        // 预期：业务动作和待办写入已经执行，但同一事务必须整体回滚。
    }

    if (salesOrders.List().Single(x => x.Id == order.Id).Status != SalesOrderStatus.Draft)
        throw new InvalidOperationException("PostgreSQL 事务回滚失败：销售订单状态发生了部分提交。");
    var persistedTask = tasks.List(instance.Id).Single();
    if (persistedTask.Status != WorkflowTaskStatus.Pending || persistedTask.Revision != 1)
        throw new InvalidOperationException("PostgreSQL 事务回滚失败：审批待办发生了部分提交。");
    if (innerInstances.List(businessId: order.Id).Single().Status != WorkflowInstanceStatus.Running)
        throw new InvalidOperationException("PostgreSQL 事务回滚失败：流程实例发生了部分提交。");

    RunCrossModuleActionRollbackProbes(fsql);
    RunStartRollbackProbe(fsql);
    RunEmptyApproverRollbackProbe(fsql);
    RunApproverLookupCaseInsensitiveProbe(fsql);
    RunWorkflowDefinitionCaseInsensitiveProbe(fsql, databaseType);
    RunWorkflowDefinitionVersionUniquenessProbe(fsql, databaseType, connectionString);
    RunBusinessApproverFieldProbe(fsql);
    RunExistingTaskSurvivesDynamicMembershipProbe(fsql);
    RunApprovalSnapshotRepairProbe(fsql);
    RunApprovalSnapshotTransferProbe(fsql);
    RunApprovalSnapshotCasRollbackProbe(fsql);
    RunConcurrentApprovalSnapshotCompensationProbe(fsql, databaseType, connectionString);
    RunStaleApprovalTaskRepairAfterReturnProbe(fsql, databaseType, connectionString);
    RunStaleApprovalDecisionLockProbe(fsql, databaseType, connectionString);
    RunWithdrawRollbackProbe(fsql);
    RunAnyApprovalModeProbe(fsql);
    RunMajorityApprovalModeProbe(fsql);
    RunParallelAnyApprovalProbe(fsql);
    RunParallelMajorityApprovalProbe(fsql);
    RunParallelAnyApprovalRollbackProbe(fsql);
    RunParallelMajorityApprovalRollbackProbe(fsql);
    RunReturnRollbackProbe(fsql);
    RunParallelJoinProbe(fsql);
    RunParallelSplitRollbackProbe(fsql);
    RunParallelReturnProbe(fsql);
    RunParallelJoinArrivalRollbackProbe(fsql);
    RunParallelConditionProbe(fsql);
    RunParallelReturnRollbackProbe(fsql);
    RunControlledLoopProbe(fsql);
    RunLoopJoinProbe(fsql);
    RunLoopCasRollbackProbe(fsql);
    RunNestedParallelProbe(fsql);
    RunConditionNestedJoinProbe(fsql);
    RunNotificationFailureDoesNotBlockProbe(fsql);
    RunNotificationFailureRollbackProbe(fsql);
    RunNotificationFailureRetryClaimProbe(fsql);
    RunNotificationFailureExistingNotificationProbe(fsql);
    RunNotificationRetryTransactionRollbackProbe(fsql);
    RunNotificationCenterProbe(fsql);
    RunTaskNotificationAfterCommitProbe(fsql);
    RunStandaloneApprovalTaskTransactionProbe(fsql);
    RunExternalTransactionCallbackGuardProbe(fsql);
    RunSeparateBoundaryInstanceProbe(fsql);
    RunPostCommitCallbackIsolationProbe(fsql);
    RunAutomaticActionFailureRollbackProbe(fsql);
    RunParallelAutomaticActionRetryProbe(fsql);
    RunRejectedActionFailureRollbackProbe(fsql);
    RunCancelledActionFailureRollbackProbe(fsql);
    RunTerminationSiblingAuditAndNotificationProbe(fsql);
    RunInvalidNotificationFailurePayloadProbe(fsql);
    RunWorkflowTaskIdempotentInsertProbe(fsql);
    RunConcurrentWorkflowTaskTryAddProbe(fsql, databaseType, connectionString);
    RunWorkflowLegacyBackfillProbe(fsql);
    RunWorkflowLegacyMixedCaseDuplicateGuardProbe(fsql, databaseType);
    RunWorkflowRunningInstanceUniquenessProbe(fsql);
    RunConcurrentWorkflowSchemaMigrationProbe(fsql, databaseType, connectionString);
    RunConcurrentWorkflowInstanceTryAddProbe(fsql, databaseType, connectionString);
    RunConcurrentWorkflowResubmitProbe(fsql, databaseType, connectionString);
    RunConcurrentWorkflowTransferProbe(fsql, databaseType, connectionString);
    RunConcurrentWorkflowTaskCreationWithdrawalProbe(fsql, databaseType, connectionString);
    RunConcurrentOperationAndNotificationTryAddProbe(fsql, databaseType, connectionString);
    RunConcurrentWorkflowRetryProbe(fsql, databaseType, connectionString);
    RunConcurrentWorkflowApprovalProbe(fsql, databaseType, connectionString);
    RunConcurrentWorkflowApprovalWithdrawalProbe(fsql, databaseType, connectionString);
    RunConcurrentWorkflowRetryWithdrawalProbe(fsql, databaseType, connectionString);
    RunConcurrentWorkflowContinueWithdrawalProbe(fsql, databaseType, connectionString);
    RunLmsReplacementSubmittedUniquenessProbe(fsql);
    RunLmsAuthorizationReplacementRollbackProbe(fsql);
    RunLmsReplacementInsertRollbackProbe(fsql);
    Console.WriteLine($"{databaseType} Workflow transaction probe passed.");
}
finally
{
    if (task is not null) fsql.Delete<WorkflowTaskRecord>().Where(x => x.Id == task.Id).ExecuteAffrows();
    if (instance is not null) fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == instance.Id).ExecuteAffrows();
    fsql.Delete<SalesOrderRecord>().Where(x => x.Id == order.Id).ExecuteAffrows();
}

static void RunExternalNotificationOutboxProbe(IFreeSql fsql, DataType databaseType)
{
    var repository = new FreeSqlExternalNotificationOutboxRepository(fsql);
    var now = new DateTime(2026, 7, 22, 11, 0, 0);
    var deferred = new ExternalNotificationMessage(Guid.CreateVersion7(), ExternalNotificationChannel.Email, "probe@example.test", WorkNotificationKind.System, "站外通知探针", "仅验证 PostgreSQL Outbox。", null, $"external-probe:deferred:{Guid.CreateVersion7():N}", now);
    var ready = new ExternalNotificationMessage(Guid.CreateVersion7(), ExternalNotificationChannel.WeCom, "probe-wecom", WorkNotificationKind.System, "站外通知探针", "仅验证 PostgreSQL Outbox。", null, $"external-probe:ready:{Guid.CreateVersion7():N}", now);
    var dedupeKeys = new[] { deferred.DedupeKey, ready.DedupeKey };
    try
    {
        if (!repository.TryAdd(deferred) || !repository.TryAdd(ready))
            throw new InvalidOperationException("站外通知持久化探针未能原子写入测试消息。");
        var first = repository.ListPending(10, now).Single(item => item.Message.DedupeKey == deferred.DedupeKey);
        var readyItem = repository.ListPending(10, now).Single(item => item.Message.DedupeKey == ready.DedupeKey);
        if (!repository.TryClaim(first.Id, now, TimeSpan.FromMinutes(5)))
            throw new InvalidOperationException("站外通知持久化探针首次租约抢占失败。");
        repository.MarkFailed(first.Id, "探针渠道失败", now, now.AddMinutes(5));
        var beforeDue = repository.ListPending(10, now.AddMinutes(4).AddSeconds(59));
        if (beforeDue.Any(item => item.Id == first.Id) || !beforeDue.Any(item => item.Id == readyItem.Id))
            throw new InvalidOperationException("站外通知持久化探针未正确跳过未到期重试或错误隐藏立即可投递记录。");
        var due = repository.ListPending(10, now.AddMinutes(5));
        if (!due.Any(item => item.Id == first.Id) || !repository.TryClaim(first.Id, now.AddMinutes(5), TimeSpan.FromMinutes(5)))
            throw new InvalidOperationException("站外通知持久化探针未在到期时间恢复可抢占状态。");
        Console.WriteLine($"{databaseType} external notification Outbox probe passed.");
    }
    finally
    {
        fsql.Delete<ExternalNotificationOutboxRecord>().Where(item => dedupeKeys.Contains(item.DedupeKey)).ExecuteAffrows();
    }
}

static void RunWorkflowRunningInstanceUniquenessProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    var definition = CreateDefinition();
    var first = WorkflowInstance.Start(definition, "workflow.postgres.running-unique", businessId, startedBy: "workflow-postgres-probe");
    var second = WorkflowInstance.Start(definition, "workflow.postgres.running-unique", businessId, startedBy: "workflow-postgres-probe");
    try
    {
        WorkflowSchemaMigration.EnsureRunningBusinessUniqueness(fsql);
        var repository = new FreeSqlWorkflowInstanceRepository(fsql);
        repository.Add(first);

        var exception = AssertThrows(() => repository.Add(second));
        if (exception is not WorkflowRunningInstanceConflictException)
            throw new InvalidOperationException($"PostgreSQL Workflow 运行实例并发错误未转换为专用异常：{exception.GetType().Name}");
    }
    finally
    {
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == first.Id || x.Id == second.Id).ExecuteAffrows();
    }
}

static void RunWorkflowDefinitionCaseInsensitiveProbe(IFreeSql fsql, DataType databaseType)
{
    var baseCode = $"workflow.definition.case-{Guid.CreateVersion7():N}";
    var first = new WorkflowDefinition(baseCode.ToLowerInvariant(), $"{databaseType} 流程编码大小写探针", versionNumber: 1);
    var repository = new FreeSqlWorkflowDefinitionRepository(fsql);
    WorkflowDefinition? second = null;
    try
    {
        repository.Add(first);
        // 模拟旧库混合大小写历史值，确认仓储读取和后续版本计算仍按逻辑编码工作。
        fsql.Update<WorkflowDefinitionRecord>()
            .Set(x => x.Code, baseCode.ToLowerInvariant())
            .Where(x => x.Id == first.Id)
            .ExecuteAffrows();
        var loaded = repository.List(baseCode.ToUpperInvariant(), WorkflowDefinitionStatus.Draft);
        if (loaded.Count != 1 || loaded[0].Id != first.Id)
        {
            var stored = fsql.Select<WorkflowDefinitionRecord>().Where(x => x.Id == first.Id).ToOne();
            var direct = databaseType == DataType.PostgreSQL
                ? fsql.Ado.Query<WorkflowDefinitionRecord>("SELECT * FROM \"WorkflowDefinition\" WHERE UPPER(\"Code\") = @Code", new { Code = baseCode.ToUpperInvariant() }).Count()
                : -1;
            throw new InvalidOperationException($"{databaseType} Workflow 定义编码大小写无关读取失败：loaded={loaded.Count}, direct={direct}, storedCode={stored?.Code}, expected={baseCode.ToUpperInvariant()}, status={stored?.Status}。");
        }

        var service = new WorkflowDefinitionService(repository);
        second = service.CreateDraft(baseCode.ToUpperInvariant(), $"{databaseType} 流程编码版本探针");
        if (second.VersionNumber != 2)
            throw new InvalidOperationException($"{databaseType} Workflow 定义编码大小写无关版本递增失败，得到版本：{second.VersionNumber}。");

        var versions = service.List(baseCode.ToLowerInvariant(), WorkflowDefinitionStatus.Draft);
        if (versions.Count != 2 || versions.Select(x => x.VersionNumber).OrderBy(x => x).SequenceEqual(new[] { 1, 2 }) is false)
            throw new InvalidOperationException($"{databaseType} Workflow 定义编码大小写无关版本查询结果不完整。");

    }
    finally
    {
        fsql.Delete<WorkflowDefinitionRecord>().Where(x => x.Id == first.Id || (second != null && x.Id == second.Id)).ExecuteAffrows();
    }
}

static void RunWorkflowDefinitionVersionUniquenessProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    var code = $"WF_VERSION_MIGRATION_{Guid.CreateVersion7():N}";
    var first = new WorkflowDefinition(code, "流程版本重复保护", versionNumber: 1);
    var second = new WorkflowDefinition(code.ToLowerInvariant(), "流程版本重复保护旧记录", versionNumber: 1);
    var referencedInstanceId = Guid.CreateVersion7();
    var secondReferencedInstanceId = Guid.CreateVersion7();
    var repository = new FreeSqlWorkflowDefinitionRepository(fsql);

    DropDefinitionVersionIndex(fsql, databaseType);
    try
    {
        repository.Add(first);
        fsql.Insert(new WorkflowDefinitionRecord
        {
            Id = second.Id,
            Code = second.Code.ToLowerInvariant(),
            Name = second.Name,
            Description = string.Empty,
            VersionNumber = second.VersionNumber,
            Status = WorkflowDefinitionStatus.Draft,
            CreatedAt = second.CreatedAt,
            NodesJson = "[]",
            ConnectionsJson = "[]"
        }).ExecuteAffrows();

        fsql.Insert(new WorkflowInstanceRecord
        {
            Id = referencedInstanceId,
            DefinitionId = first.Id,
            DefinitionCode = first.Code,
            DefinitionVersion = first.VersionNumber,
            BusinessType = "workflow.diagnostic",
            BusinessId = Guid.CreateVersion7(),
            StartedBy = "admin",
            DefinitionSnapshotJson = "{}",
            Status = WorkflowInstanceStatus.Cancelled,
            CurrentNodeId = Guid.CreateVersion7(),
            StartedAt = DateTime.Now,
            CompletedAt = DateTime.Now,
            Revision = 1,
            ActiveNodeIdsJson = "[]",
            ParallelJoinArrivalsJson = "{}",
            LoopIterationsJson = "{}",
            ApprovalAssigneesJson = "{}"
        }).ExecuteAffrows();
        fsql.Insert(new WorkflowInstanceRecord
        {
            Id = secondReferencedInstanceId,
            DefinitionId = second.Id,
            DefinitionCode = second.Code,
            DefinitionVersion = second.VersionNumber,
            BusinessType = "workflow.diagnostic",
            BusinessId = Guid.CreateVersion7(),
            StartedBy = "admin",
            DefinitionSnapshotJson = "{}",
            Status = WorkflowInstanceStatus.Cancelled,
            CurrentNodeId = Guid.CreateVersion7(),
            StartedAt = DateTime.Now,
            CompletedAt = DateTime.Now,
            Revision = 1,
            ActiveNodeIdsJson = "[]",
            ParallelJoinArrivalsJson = "{}",
            LoopIterationsJson = "{}",
            ApprovalAssigneesJson = "{}"
        }).ExecuteAffrows();

        var exception = AssertThrows(() => WorkflowSchemaMigration.EnsureDefinitionVersionUniqueness(fsql));
        var report = WorkflowSchemaMigration.FindDefinitionVersionDuplicates(fsql)
            .SingleOrDefault(x => x.Code.Equals(first.Code, StringComparison.OrdinalIgnoreCase) && x.VersionNumber == first.VersionNumber);
        if (report is null || report.DefinitionIds.Count != 2 || !report.DefinitionIds.Contains(first.Id) || !report.DefinitionIds.Contains(second.Id))
            throw new InvalidOperationException($"{databaseType} Workflow 定义版本重复只读报告不完整：{string.Join(",", report?.DefinitionIds ?? [])}");
        var references = WorkflowSchemaMigration.FindDefinitionVersionDuplicateReferences(fsql)
            .SingleOrDefault(x => x.Code.Equals(first.Code, StringComparison.OrdinalIgnoreCase) && x.VersionNumber == first.VersionNumber);
        var firstReference = references?.Definitions.SingleOrDefault(x => x.DefinitionId == first.Id);
        var instanceReference = firstReference?.Instances.SingleOrDefault(x => x.InstanceId == referencedInstanceId);
        if (instanceReference is null
            || instanceReference.BusinessType != "workflow.diagnostic"
            || instanceReference.Status != WorkflowInstanceStatus.Cancelled)
            throw new InvalidOperationException($"{databaseType} Workflow 定义版本引用报告未返回实例引用：{referencedInstanceId}");
        if (!exception.Message.Contains("重复的流程定义版本", StringComparison.Ordinal) || !exception.Message.Contains(first.Id.ToString(), StringComparison.Ordinal) || !exception.Message.Contains(second.Id.ToString(), StringComparison.Ordinal))
            throw new InvalidOperationException($"{databaseType} Workflow 定义版本重复迁移未返回可处理提示：{exception.Message}");

        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == secondReferencedInstanceId).ExecuteAffrows();
        WorkflowSchemaMigration.EnsureDefinitionVersionUniqueness(fsql);
        if (fsql.Select<WorkflowDefinitionRecord>().Where(x => x.Id == first.Id).ToOne() is null
            || fsql.Select<WorkflowDefinitionRecord>().Where(x => x.Id == second.Id).ToOne() is not null)
            throw new InvalidOperationException($"{databaseType} Workflow 定义迁移未仅清理无引用重复版本。");
    }
    finally
    {
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == referencedInstanceId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == secondReferencedInstanceId).ExecuteAffrows();
        fsql.Delete<WorkflowDefinitionRecord>().Where(x => x.Id == first.Id || x.Id == second.Id).ExecuteAffrows();
        WorkflowSchemaMigration.EnsureDefinitionVersionUniqueness(fsql);
    }

    DropDefinitionVersionIndex(fsql, databaseType);
    try
    {
        using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        var migrationResults = Task.WhenAll(
            Task.Run(() => RunDefinitionMigration(left)),
            Task.Run(() => RunDefinitionMigration(right)))
            .GetAwaiter()
            .GetResult();
        var migrationErrors = migrationResults.Where(x => x is not null).ToArray();
        if (migrationErrors.Length > 0)
            throw new InvalidOperationException($"{databaseType} Workflow 定义版本并发迁移失败：{string.Join(" | ", migrationErrors.Select(x => x!.Message))}");

        var concurrentCode = $"WF_VERSION_TRYADD_{Guid.CreateVersion7():N}";
        var leftDefinition = new WorkflowDefinition(concurrentCode, "并发版本一", versionNumber: 1);
        var rightDefinition = new WorkflowDefinition(concurrentCode.ToLowerInvariant(), "并发版本二", versionNumber: 1);
        var leftRepository = new FreeSqlWorkflowDefinitionRepository(left);
        var rightRepository = new FreeSqlWorkflowDefinitionRepository(right);
        var addResults = Task.WhenAll(
            Task.Run(() => leftRepository.TryAdd(leftDefinition)),
            Task.Run(() => rightRepository.TryAdd(rightDefinition)))
            .GetAwaiter()
            .GetResult();
        if (addResults.Count(x => x) != 1)
            throw new InvalidOperationException($"{databaseType} Workflow 定义版本并发 TryAdd 未产生唯一胜出者：{string.Join(",", addResults)}");

        fsql.Delete<WorkflowDefinitionRecord>().Where(x => x.Id == leftDefinition.Id || x.Id == rightDefinition.Id).ExecuteAffrows();
    }
    finally
    {
        WorkflowSchemaMigration.EnsureDefinitionVersionUniqueness(fsql);
    }

    static Exception? RunDefinitionMigration(IFreeSql database)
    {
        try
        {
            WorkflowSchemaMigration.EnsureDefinitionVersionUniqueness(database);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    static void DropDefinitionVersionIndex(IFreeSql database, DataType type)
    {
        if (type == DataType.PostgreSQL)
            database.Ado.ExecuteNonQuery($"DROP INDEX IF EXISTS \"{WorkflowSchemaMigration.DefinitionVersionUniqueIndex}\";");
        else if (type == DataType.SqlServer)
            database.Ado.ExecuteNonQuery($"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{WorkflowSchemaMigration.DefinitionVersionUniqueIndex}' AND object_id = OBJECT_ID(N'WorkflowDefinition')) DROP INDEX [{WorkflowSchemaMigration.DefinitionVersionUniqueIndex}] ON [WorkflowDefinition];");
    }
}

static void RunConcurrentWorkflowSchemaMigrationProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    try
    {
        switch (databaseType)
        {
            case DataType.PostgreSQL:
                fsql.Ado.ExecuteNonQuery($"DROP INDEX IF EXISTS \"{WorkflowSchemaMigration.RunningBusinessDefinitionUniqueIndex}\";");
                break;
            case DataType.SqlServer:
                fsql.Ado.ExecuteNonQuery($"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{WorkflowSchemaMigration.RunningBusinessDefinitionUniqueIndex}' AND object_id = OBJECT_ID(N'WorkflowInstance')) DROP INDEX [{WorkflowSchemaMigration.RunningBusinessDefinitionUniqueIndex}] ON [WorkflowInstance];");
                break;
        }

        using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        var results = Task.WhenAll(
            Task.Run(() => RunMigration(left)),
            Task.Run(() => RunMigration(right)))
            .GetAwaiter()
            .GetResult();
        var errors = results.Where(x => x is not null).ToArray();
        if (errors.Length > 0)
            throw new InvalidOperationException($"{databaseType} Workflow 并发迁移探针失败：{string.Join(" | ", errors.Select(x => x!.Message))}");

        WorkflowSchemaMigration.EnsureRunningBusinessUniqueness(fsql);
    }
    finally
    {
        // 即使并发探针失败，也恢复后续 Workflow 探针所依赖的唯一索引。
        WorkflowSchemaMigration.EnsureRunningBusinessUniqueness(fsql);
    }

    static Exception? RunMigration(IFreeSql database)
    {
        try
        {
            WorkflowSchemaMigration.EnsureRunningBusinessUniqueness(database);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}

static void RunConcurrentWorkflowInstanceTryAddProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    var businessId = Guid.CreateVersion7();
    var definition = CreateDefinition();
    using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
    using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
    try
    {
        var results = Task.WhenAll(
            Task.Run(() => new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(left)).Start(definition, "workflow.postgres.instance-concurrent", businessId, startedBy: "workflow-postgres-left")),
            Task.Run(() => new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(right)).Start(definition, "workflow.postgres.instance-concurrent", businessId, startedBy: "workflow-postgres-right")))
            .GetAwaiter()
            .GetResult();
        if (results.Length != 2 || results[0].Id != results[1].Id)
            throw new InvalidOperationException("PostgreSQL Workflow Start 并发 TryAdd 未让两个调用返回同一胜出实例。");
        var persisted = new FreeSqlWorkflowInstanceRepository(fsql).List("workflow.postgres.instance-concurrent", businessId, WorkflowInstanceStatus.Running);
        if (persisted.Count != 1)
            throw new InvalidOperationException("PostgreSQL Workflow 运行实例并发 TryAdd 产生了多条 Running 实例。");
    }
    finally
    {
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunConcurrentWorkflowResubmitProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    const string businessType = "workflow.postgres.resubmit-concurrent";
    const string definitionCode = "PG_RESUBMIT_CONCURRENT";
    var businessId = Guid.CreateVersion7();
    var definitions = new WorkflowDefinitionService(new InMemoryDefinitionRepository());
    var definition = definitions.CreateDraft(definitionCode, "并发重提");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"workflow-postgres-probe\"}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, approval.Id);
    definition.Connect(approval.Id, end.Id);
    definitions.Publish(definition);

    var baseOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
    var baseTransactions = new FreeSqlWorkflowTransactionBoundary(fsql);
    var baseInstances = new FreeSqlWorkflowInstanceRepository(fsql);
    var baseInstanceService = new WorkflowInstanceService(baseInstances, baseOperations, baseTransactions);
    var baseNotifications = new NotificationService(new FreeSqlNotificationRepository(fsql), transactions: baseTransactions);
    var baseRuntime = new WorkflowRuntimeService(baseInstanceService, new WorkflowActionExecutor([]), baseNotifications, baseOperations, baseTransactions);
    var baseTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(fsql), baseInstanceService, runtime: baseRuntime, operations: baseOperations, transactions: baseTransactions);
    var baseBinding = new WorkflowBindingService(definitions, baseInstanceService, baseTasks, baseRuntime, baseTransactions);
    var rejected = baseBinding.StartOrGet(definitionCode, businessType, businessId, startedBy: "workflow-postgres-probe");
    try
    {
        var rejectedTask = baseTasks.List(rejected.Id, status: WorkflowTaskStatus.Pending).Single();
        baseTasks.Reject(rejectedTask, "workflow-postgres-probe", "需要补充资料");
        if (baseInstances.List(businessType, businessId).Single(x => x.Id == rejected.Id).Status != WorkflowInstanceStatus.Rejected)
            throw new InvalidOperationException("并发重提探针初始化失败：原审批实例未进入 Rejected。" );

        using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        var leftOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(left));
        var rightOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(right));
        var leftTransactions = new FreeSqlWorkflowTransactionBoundary(left);
        var rightTransactions = new FreeSqlWorkflowTransactionBoundary(right);
        var leftInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(left), leftOperations, leftTransactions);
        var rightInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(right), rightOperations, rightTransactions);
        var leftNotifications = new NotificationService(new FreeSqlNotificationRepository(left), transactions: leftTransactions);
        var rightNotifications = new NotificationService(new FreeSqlNotificationRepository(right), transactions: rightTransactions);
        var leftRuntime = new WorkflowRuntimeService(leftInstances, new WorkflowActionExecutor([]), leftNotifications, leftOperations, leftTransactions);
        var rightRuntime = new WorkflowRuntimeService(rightInstances, new WorkflowActionExecutor([]), rightNotifications, rightOperations, rightTransactions);
        var leftTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(left), leftInstances, runtime: leftRuntime, operations: leftOperations, transactions: leftTransactions);
        var rightTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(right), rightInstances, runtime: rightRuntime, operations: rightOperations, transactions: rightTransactions);
        var leftBinding = new WorkflowBindingService(definitions, leftInstances, leftTasks, leftRuntime, leftTransactions);
        var rightBinding = new WorkflowBindingService(definitions, rightInstances, rightTasks, rightRuntime, rightTransactions);

        var results = Task.WhenAll(
            Task.Run(() => RunResubmit(leftBinding, definitionCode, businessType, businessId)),
            Task.Run(() => RunResubmit(rightBinding, definitionCode, businessType, businessId)))
            .GetAwaiter()
            .GetResult();
        if (results.Any(x => !x.Succeeded) || results[0].InstanceId != results[1].InstanceId || results[0].InstanceId == rejected.Id)
            throw new InvalidOperationException($"{databaseType} 并发重提探针失败：两个请求未复用同一新实例。{string.Join(" | ", results.Select(x => x.Succeeded ? x.InstanceId.ToString() : $"{x.Error?.GetType().Name}:{x.Error?.Message}"))}");

        var allInstances = baseInstances.List(businessType, businessId);
        var running = allInstances.Where(x => x.Status == WorkflowInstanceStatus.Running).ToArray();
        var resubmittedId = results[0].InstanceId;
        var resubmittedOperations = baseOperations.List(instanceId: resubmittedId);
        var resubmittedTasks = new FreeSqlWorkflowTaskRepository(fsql).List(resubmittedId, status: WorkflowTaskStatus.Pending);
        if (allInstances.Count != 2
            || allInstances.Count(x => x.Status == WorkflowInstanceStatus.Rejected) != 1
            || running.Length != 1
            || running[0].PreviousInstanceId != rejected.Id
            || resubmittedOperations.Count(x => x.Kind == WorkflowOperationKind.Resubmitted) != 1
            || resubmittedTasks.Count != 1)
            throw new InvalidOperationException($"{databaseType} 并发重提探针失败：重提实例、历史或待办出现重复/半成品。instances={allInstances.Count}, running={running.Length}, resubmittedOperations={resubmittedOperations.Count(x => x.Kind == WorkflowOperationKind.Resubmitted)}, pendingTasks={resubmittedTasks.Count}。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }

    static (bool Succeeded, Guid InstanceId, Exception? Error) RunResubmit(WorkflowBindingService binding, string code, string type, Guid id)
    {
        try
        {
            var instance = binding.Resubmit(code, type, id, startedBy: "workflow-postgres-probe");
            return (true, instance.Id, null);
        }
        catch (Exception exception)
        {
            return (false, Guid.Empty, exception);
        }
    }
}

static void RunConcurrentWorkflowTransferProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    const string businessType = "workflow.postgres.transfer-concurrent";
    var businessId = Guid.CreateVersion7();
    var definition = new WorkflowDefinition("PG_TRANSFER_CONCURRENT", "并发转交");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"workflow-postgres-probe\"}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, approval.Id);
    definition.Connect(approval.Id, end.Id);
    definition.Publish();

    var baseOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
    var baseTransactions = new FreeSqlWorkflowTransactionBoundary(fsql);
    var baseInstances = new FreeSqlWorkflowInstanceRepository(fsql);
    var baseInstanceService = new WorkflowInstanceService(baseInstances, baseOperations, baseTransactions);
    var baseNotifications = new NotificationService(new FreeSqlNotificationRepository(fsql), transactions: baseTransactions);
    var baseRuntime = new WorkflowRuntimeService(baseInstanceService, new WorkflowActionExecutor([]), baseNotifications, baseOperations, baseTransactions);
    var baseTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(fsql), baseInstanceService, runtime: baseRuntime, operations: baseOperations, transactions: baseTransactions);
    var instance = baseInstanceService.Start(definition, businessType, businessId, startedBy: "workflow-postgres-probe");
    try
    {
        if (baseRuntime.Continue(instance).State != WorkflowRuntimeState.WaitingForApproval)
            throw new InvalidOperationException("并发转交探针初始化失败：未进入审批节点。" );
        baseTasks.EnsureCurrentApprovalTask(instance);

        using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        var leftOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(left));
        var rightOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(right));
        var leftTransactions = new FreeSqlWorkflowTransactionBoundary(left);
        var rightTransactions = new FreeSqlWorkflowTransactionBoundary(right);
        var leftInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(left), leftOperations, leftTransactions);
        var rightInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(right), rightOperations, rightTransactions);
        var leftTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(left), leftInstances, operations: leftOperations, transactions: leftTransactions);
        var rightTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(right), rightInstances, operations: rightOperations, transactions: rightTransactions);
        var leftTask = leftTasks.List(instance.Id, status: WorkflowTaskStatus.Pending).Single();
        var rightTask = rightTasks.List(instance.Id, status: WorkflowTaskStatus.Pending).Single();

        var results = Task.WhenAll(
            Task.Run(() => RunTransfer(leftTasks, leftTask, "workflow-postgres-finance")),
            Task.Run(() => RunTransfer(rightTasks, rightTask, "workflow-postgres-director")))
            .GetAwaiter()
            .GetResult();
        if (results.Count(x => x.Succeeded) != 1 || results.Count(x => !x.Succeeded) != 1)
            throw new InvalidOperationException($"{databaseType} 并发转交探针失败：未保持单一胜出转交。{string.Join(" | ", results.Select(x => x.Succeeded ? x.Target : $"{x.Error?.GetType().Name}:{x.Error?.Message}"))}");

        var tasks = new FreeSqlWorkflowTaskRepository(fsql).List(instance.Id);
        var history = baseOperations.List(instanceId: instance.Id);
        var transferred = tasks.SingleOrDefault(x => x.Status == WorkflowTaskStatus.Transferred);
        var pending = tasks.Where(x => x.Status == WorkflowTaskStatus.Pending).ToArray();
        if (transferred is null
            || pending.Length != 1
            || !new[] { "workflow-postgres-finance", "workflow-postgres-director" }.Contains(pending[0].Assignee, StringComparer.OrdinalIgnoreCase)
            || history.Count(x => x.Kind == WorkflowOperationKind.Transferred) != 1
            || baseInstances.List(businessType, businessId).Single().Status != WorkflowInstanceStatus.Running)
            throw new InvalidOperationException($"{databaseType} 并发转交探针失败：原待办、目标待办或历史出现重复/半提交。transferred={transferred?.Status}, pending={pending.Length}, transfers={history.Count(x => x.Kind == WorkflowOperationKind.Transferred)}。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }

    static (bool Succeeded, string Target, Exception? Error) RunTransfer(WorkflowTaskService service, WorkflowTask task, string target)
    {
        try
        {
            service.Transfer(task, "workflow-postgres-probe", target, "并发转交");
            return (true, target, null);
        }
        catch (Exception exception)
        {
            return (false, target, exception);
        }
    }
}

static void RunConcurrentWorkflowTaskCreationWithdrawalProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    const string businessType = "workflow.postgres.task-withdraw-concurrent";
    var businessId = Guid.CreateVersion7();
    var definition = new WorkflowDefinition("PG_TASK_WITHDRAW_CONCURRENT", "并发创建待办与撤回");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"workflow-postgres-probe\"}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, approval.Id);
    definition.Connect(approval.Id, end.Id);
    definition.Publish();

    var baseOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
    var baseTransactions = new FreeSqlWorkflowTransactionBoundary(fsql);
    var baseInstances = new FreeSqlWorkflowInstanceRepository(fsql);
    var baseInstanceService = new WorkflowInstanceService(baseInstances, baseOperations, baseTransactions);
    var baseNotifications = new NotificationService(new FreeSqlNotificationRepository(fsql), transactions: baseTransactions);
    var baseRuntime = new WorkflowRuntimeService(baseInstanceService, new WorkflowActionExecutor([]), baseNotifications, baseOperations, baseTransactions);
    var instance = baseInstanceService.Start(definition, businessType, businessId, startedBy: "workflow-postgres-probe");
    try
    {
        if (baseRuntime.Continue(instance).State != WorkflowRuntimeState.WaitingForApproval)
            throw new InvalidOperationException("并发创建待办/撤回探针初始化失败：未进入审批节点。" );

        using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        var leftOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(left));
        var rightOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(right));
        var leftTransactions = new FreeSqlWorkflowTransactionBoundary(left);
        var rightTransactions = new FreeSqlWorkflowTransactionBoundary(right);
        var leftInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(left), leftOperations, leftTransactions);
        var rightInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(right), rightOperations, rightTransactions);
        var leftTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(left), leftInstances, operations: leftOperations, transactions: leftTransactions);
        var rightTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(right), rightInstances, operations: rightOperations, transactions: rightTransactions);

        var results = Task.WhenAll(
            Task.Run(() => RunCreate(leftTasks, instance, approval)),
            Task.Run(() => RunWithdrawal(rightTasks, instance.Id)))
            .GetAwaiter()
            .GetResult();
        if (!results[1].Succeeded)
            throw new InvalidOperationException($"{databaseType} 创建待办/撤回并发探针失败：撤回未成功。{results[1].Error?.GetType().Name}:{results[1].Error?.Message}");

        var persistedInstance = baseInstances.List(businessType, businessId).Single();
        var persistedTasks = new FreeSqlWorkflowTaskRepository(fsql).List(instance.Id);
        var history = baseOperations.List(instanceId: instance.Id);
        if (persistedInstance.Status != WorkflowInstanceStatus.Cancelled
            || persistedTasks.Any(x => x.Status == WorkflowTaskStatus.Pending)
            || persistedTasks.Count > 1
            || history.Count(x => x.Kind == WorkflowOperationKind.Withdrawn) != 1
            || (results[0].Succeeded && persistedTasks.Single().Status != WorkflowTaskStatus.Cancelled))
            throw new InvalidOperationException($"{databaseType} 创建待办/撤回并发探针失败：撤回后存在孤儿待办或历史不一致。instance={persistedInstance.Status}, tasks={string.Join(",", persistedTasks.Select(x => x.Status))}, withdrawn={history.Count(x => x.Kind == WorkflowOperationKind.Withdrawn)}, createSucceeded={results[0].Succeeded}。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }

    static (bool Succeeded, Exception? Error) RunCreate(WorkflowTaskService service, WorkflowInstance instance, WorkflowNode node)
    {
        try
        {
            service.CreateApprovalTask(instance, node.Id, node.Name, "workflow-postgres-probe");
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }

    static (bool Succeeded, Exception? Error) RunWithdrawal(WorkflowTaskService service, Guid instanceId)
    {
        try
        {
            service.Withdraw(instanceId, "workflow-postgres-probe", "并发撤回");
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }
}

static void RunExternalTransactionCallbackGuardProbe(IFreeSql fsql)
{
    var published = false;
    var exception = AssertThrows(() => fsql.Transaction(() => new FreeSqlWorkflowTransactionBoundary(fsql).Execute(
            static () => { },
            afterRollback: null,
            afterCommit: () => published = true)));
    if (exception is not InvalidOperationException || published)
        throw new InvalidOperationException("PostgreSQL Workflow 事务边界错误：外部事务下不应提前执行提交回调。");
}

static void RunPostCommitCallbackIsolationProbe(IFreeSql fsql)
{
    var secondCallbackExecuted = false;
    var boundary = new FreeSqlWorkflowTransactionBoundary(fsql);
    boundary.Execute(() =>
    {
        boundary.Execute(static () => { }, afterRollback: null, afterCommit: static () => throw new InvalidOperationException("模拟提交后副作用失败"));
        boundary.Execute(static () => { }, afterRollback: null, afterCommit: () => secondCallbackExecuted = true);
    });
    if (!secondCallbackExecuted)
        throw new InvalidOperationException("PostgreSQL Workflow 提交后回调隔离失败：前一个回调异常阻断了后续回调。");
}

static void RunSeparateBoundaryInstanceProbe(IFreeSql fsql)
{
    var outer = new FreeSqlWorkflowTransactionBoundary(fsql);
    var inner = new FreeSqlWorkflowTransactionBoundary(fsql);
    var callbackExecuted = false;
    outer.Execute(() =>
    {
        inner.Execute(static () => { }, afterRollback: null, afterCommit: () => callbackExecuted = true);
    });
    if (!callbackExecuted)
        throw new InvalidOperationException("PostgreSQL Workflow 事务边界实例共享失败：同一 FreeSql 连接的嵌套边界未登记提交回调。");
}

static void RunWorkflowTaskIdempotentInsertProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    var definition = CreateDefinition();
    var instance = WorkflowInstance.Start(definition, "workflow.postgres.task-idempotency", businessId, startedBy: "workflow-postgres-probe");
    var node = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
    var first = new WorkflowTask(instance, node.Id, node.Name, "workflow-postgres-probe", round: 1);
    var retry = new WorkflowTask(instance, node.Id, node.Name, "WORKFLOW-POSTGRES-PROBE", round: 1);
    try
    {
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var tasks = new FreeSqlWorkflowTaskRepository(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var notifications = new NotificationService(new FreeSqlNotificationRepository(fsql));
        var taskDedupeKey = $"workflow-task-assigned:{first.Id}";
        instances.Add(instance);
        if (!tasks.TryAdd(first))
            throw new InvalidOperationException("PostgreSQL Workflow 待办 TryAdd 首次写入失败。");
        if (tasks.TryAdd(retry))
            throw new InvalidOperationException("PostgreSQL Workflow 待办 TryAdd 未拒绝重复待办主键。");
        operations.Record(first, WorkflowOperationKind.Assigned, first.Assignee, "生成审批待办", taskDedupeKey, occurredAt: first.CreatedAt);
        operations.Record(retry, WorkflowOperationKind.Assigned, retry.Assignee, "生成审批待办", taskDedupeKey, occurredAt: retry.CreatedAt);
        var persistedOperation = operations.List(instance.Id).Single(x => x.DedupeKey == taskDedupeKey);
        var duplicateOperation = new WorkflowOperation(
            persistedOperation.InstanceId,
            persistedOperation.TaskId,
            persistedOperation.NodeId,
            persistedOperation.BusinessType,
            persistedOperation.BusinessId,
            persistedOperation.Kind,
            persistedOperation.Actor,
            persistedOperation.TargetAssignee,
            persistedOperation.Comment,
            persistedOperation.DedupeKey,
            persistedOperation.OccurredAt);
        if (new FreeSqlWorkflowOperationRepository(fsql).TryAdd(duplicateOperation))
            throw new InvalidOperationException("PostgreSQL Workflow 操作历史 TryAdd 未拒绝重复 DedupeKey。");
        notifications.Publish(first.Assignee, WorkNotificationKind.Approval, "待审批：审批", "Workflow 业务对象需要你的审批。", null, $"workflow-task:{first.Id}", first.CreatedAt);
        notifications.Publish(retry.Assignee, WorkNotificationKind.Approval, "待审批：审批", "Workflow 业务对象需要你的审批。", null, $"workflow-task:{retry.Id}", retry.CreatedAt);
        var duplicateNotification = new WorkNotification(first.Assignee, WorkNotificationKind.Approval, "并发重复", "不应新增", null, $"workflow-task:{first.Id}", first.CreatedAt);
        if (new FreeSqlNotificationRepository(fsql).TryAdd(duplicateNotification))
            throw new InvalidOperationException("PostgreSQL Workflow 通知 TryAdd 未拒绝重复接收人/去重键。");
        var persisted = tasks.List(instance.Id);
        if (persisted.Count != 1 || persisted[0].Id != first.Id)
            throw new InvalidOperationException("PostgreSQL Workflow 待办幂等插入失败：重复补偿产生了多条待办。");
        if (operations.List(instance.Id).Count(x => x.DedupeKey == taskDedupeKey) != 1 || notifications.List(first.Assignee).Count(x => x.DedupeKey == $"workflow-task:{first.Id}") != 1)
            throw new InvalidOperationException("PostgreSQL Workflow 待办幂等插入失败：重复补偿产生了操作历史或通知。");
    }
    finally
    {
        fsql.Delete<NotificationRecord>().Where(x => x.DedupeKey == $"workflow-task:{first.Id}").ExecuteAffrows();
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.TaskId == first.Id).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.InstanceId == instance.Id).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == instance.Id).ExecuteAffrows();
    }
}

static void RunConcurrentWorkflowTaskTryAddProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    var definition = CreateDefinition();
    var instance = WorkflowInstance.Start(definition, "workflow.postgres.task-concurrent", Guid.CreateVersion7(), startedBy: "workflow-postgres-probe");
    var node = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
    var task = new WorkflowTask(instance, node.Id, node.Name, "workflow-postgres-probe", round: 1);
    var instances = new FreeSqlWorkflowInstanceRepository(fsql);
    if (!instances.TryAdd(instance)) throw new InvalidOperationException("PostgreSQL Workflow 待办并发探针初始化实例写入失败。" );
    using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
    using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
    try
    {
        var results = Task.WhenAll(
            Task.Run(() => new FreeSqlWorkflowTaskRepository(left).TryAdd(task)),
            Task.Run(() => new FreeSqlWorkflowTaskRepository(right).TryAdd(task)))
            .GetAwaiter()
            .GetResult();
        if (results.Count(x => x) != 1)
            throw new InvalidOperationException("PostgreSQL Workflow 待办并发 TryAdd 未保持单一胜出者。");
        if (new FreeSqlWorkflowTaskRepository(fsql).List(instance.Id).Count != 1)
            throw new InvalidOperationException("PostgreSQL Workflow 待办并发 TryAdd 产生了非预期记录数量。");
    }
    finally
    {
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.Id == task.Id).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == instance.Id).ExecuteAffrows();
    }
}

static void RunConcurrentOperationAndNotificationTryAddProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    var instance = WorkflowInstance.Start(CreateDefinition(), "workflow.postgres.operation-notification-concurrent", Guid.CreateVersion7(), startedBy: "workflow-postgres-probe");
    var operationKey = $"workflow-operation-concurrent:{instance.Id:N}";
    var recipient = $"workflow-postgres-{instance.Id:N}";
    var notificationKey = $"workflow-notification-concurrent:{instance.Id:N}";
    using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
    using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
    try
    {
        var operationResults = Task.WhenAll(
            Task.Run(() => RecordOperation(left, instance, operationKey)),
            Task.Run(() => RecordOperation(right, instance, operationKey)))
            .GetAwaiter()
            .GetResult();
        if (operationResults.Count(x => x.Inserted) != 1 || operationResults[0].Operation.Id != operationResults[1].Operation.Id)
            throw new InvalidOperationException("PostgreSQL Workflow 操作历史并发 TryAdd 未保持单一胜出者。");

        var notificationResults = Task.WhenAll(
            Task.Run(() => PublishNotification(left, recipient, notificationKey)),
            Task.Run(() => PublishNotification(right, recipient, notificationKey)))
            .GetAwaiter()
            .GetResult();
        if (notificationResults[0].Id != notificationResults[1].Id)
            throw new InvalidOperationException("PostgreSQL Workflow 通知并发 TryAdd 未让两个调用复用同一胜出记录。");

        if (new FreeSqlWorkflowOperationRepository(fsql).List(instanceId: instance.Id).Count(x => x.DedupeKey == operationKey) != 1
            || new FreeSqlNotificationRepository(fsql).List(recipient).Count(x => x.DedupeKey == notificationKey) != 1)
            throw new InvalidOperationException("PostgreSQL Workflow 操作历史或通知并发 TryAdd 产生了重复记录。");
    }
    finally
    {
        fsql.Delete<NotificationRecord>().Where(x => x.Recipient == recipient && x.DedupeKey == notificationKey).ExecuteAffrows();
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.InstanceId == instance.Id && x.DedupeKey == operationKey).ExecuteAffrows();
    }

    static (bool Inserted, WorkflowOperation Operation) RecordOperation(IFreeSql connection, WorkflowInstance instance, string dedupeKey)
    {
        var service = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(connection));
        var inserted = service.TryRecord(instance, WorkflowOperationKind.NodeExecuted, "workflow-postgres-probe", "并发操作历史", dedupeKey, out var operation, nodeId: instance.CurrentNodeId);
        return (inserted, operation);
    }

    static WorkNotification PublishNotification(IFreeSql connection, string recipient, string dedupeKey)
        => new NotificationService(new FreeSqlNotificationRepository(connection)).Publish(recipient, WorkNotificationKind.System, "并发通知", "并发幂等验证", null, dedupeKey);
}

static void RunConcurrentWorkflowRetryProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    const string businessType = "workflow.postgres.retry-concurrent";
    var businessId = Guid.CreateVersion7();
    var definition = new WorkflowDefinition("PG_RETRY_CONCURRENT", "PostgreSQL 并发失败节点重试");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "自动动作", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, action.Id);
    definition.Connect(action.Id, end.Id);
    definition.Publish();

    var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
    var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
    var instances = new FreeSqlWorkflowInstanceRepository(fsql);
    var instanceService = new WorkflowInstanceService(instances, operations, transactions);
    var handler = new ConcurrentRetryActionHandler();
    var notifications = new NotificationService(new FreeSqlNotificationRepository(fsql));
    var instance = instanceService.Start(definition, businessType, businessId, startedBy: "workflow-postgres-probe");
    try
    {
        var setupRuntime = new WorkflowRuntimeService(
            instanceService,
            new WorkflowActionExecutor([handler]),
            notifications,
            operations,
            transactions);
        try
        {
            setupRuntime.Continue(instance);
            throw new InvalidOperationException("PostgreSQL 并发重试探针初始化失败：首次动作本应失败。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟并发重试首次失败")
        {
            // 预期：NodeFailed 在主事务回滚后保留，实例仍停在失败动作节点。
        }

        var failure = operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed).Single(x => x.NodeId == action.Id);
        handler.AllowSuccess = true;

        using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        var leftInstances = new FreeSqlWorkflowInstanceRepository(left);
        var rightInstances = new FreeSqlWorkflowInstanceRepository(right);
        var leftOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(left));
        var rightOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(right));
        var leftTransactions = new FreeSqlWorkflowTransactionBoundary(left);
        var rightTransactions = new FreeSqlWorkflowTransactionBoundary(right);
        var leftInstanceService = new WorkflowInstanceService(leftInstances, leftOperations, leftTransactions);
        var rightInstanceService = new WorkflowInstanceService(rightInstances, rightOperations, rightTransactions);
        var leftInstance = leftInstances.List(businessType, businessId, WorkflowInstanceStatus.Running).Single();
        var rightInstance = rightInstances.List(businessType, businessId, WorkflowInstanceStatus.Running).Single();
        var leftRuntime = new WorkflowRuntimeService(
            leftInstanceService,
            new WorkflowActionExecutor([handler]),
            new NotificationService(new FreeSqlNotificationRepository(left)),
            leftOperations,
            leftTransactions);
        var rightRuntime = new WorkflowRuntimeService(
            rightInstanceService,
            new WorkflowActionExecutor([handler]),
            new NotificationService(new FreeSqlNotificationRepository(right)),
            rightOperations,
            rightTransactions);

        var retryResults = Task.WhenAll(
            Task.Run(() => RunRetry(leftRuntime, leftInstance, action.Id)),
            Task.Run(() => RunRetry(rightRuntime, rightInstance, action.Id)))
            .GetAwaiter()
            .GetResult();
        if (retryResults.Count(x => x.Succeeded) != 1 || retryResults.Count(x => x.Error?.Message.Contains("其他请求", StringComparison.Ordinal) == true) != 1)
            throw new InvalidOperationException($"PostgreSQL 并发重试探针失败：同一失败节点没有严格保持单一重试胜出者。结果：{string.Join(" | ", retryResults.Select(x => x.Succeeded ? "success" : $"{x.Error?.GetType().Name}:{x.Error?.Message}"))}");

        var persisted = instances.List(businessType, businessId).Single();
        var history = operations.List(instanceId: instance.Id);
        if (persisted.Status != WorkflowInstanceStatus.Completed
            || handler.ExecutionCount != 2
            || history.Count(x => x.Kind == WorkflowOperationKind.Retried && x.NodeId == action.Id) != 1
            || history.Count(x => x.Kind == WorkflowOperationKind.NodeExecuted && x.NodeId == action.Id) != 1
            || history.Single(x => x.Kind == WorkflowOperationKind.Retried).DedupeKey != $"workflow-runtime-retried:{instance.Id}:{action.Id}:{failure.Id:N}")
            throw new InvalidOperationException("PostgreSQL 并发重试探针失败：重试执行、实例终态或稳定审计键不一致。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }

    static (bool Succeeded, Exception? Error) RunRetry(WorkflowRuntimeService runtime, WorkflowInstance instance, Guid actionId)
    {
        try
        {
            runtime.Retry(instance, "workflow-postgres-probe", failedNodeId: actionId);
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }
}

static void RunConcurrentWorkflowApprovalProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    const string businessType = "workflow.postgres.approval-concurrent";
    var businessId = Guid.CreateVersion7();
    var definition = new WorkflowDefinition("PG_APPROVAL_CONCURRENT", "并发审批 CAS");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"workflow-postgres-probe\"}");
    var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "动作", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"}}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, approval.Id);
    definition.Connect(approval.Id, action.Id);
    definition.Connect(action.Id, end.Id);
    definition.Publish();

    var baseOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
    var baseTransactions = new FreeSqlWorkflowTransactionBoundary(fsql);
    var baseInstances = new FreeSqlWorkflowInstanceRepository(fsql);
    var baseInstanceService = new WorkflowInstanceService(baseInstances, baseOperations, baseTransactions);
    var baseNotifications = new NotificationService(new FreeSqlNotificationRepository(fsql), transactions: baseTransactions);
    var baseRuntime = new WorkflowRuntimeService(baseInstanceService, new WorkflowActionExecutor([]), baseNotifications, baseOperations, baseTransactions);
    var baseTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(fsql), baseInstanceService, transactions: baseTransactions);
    var instance = baseInstanceService.Start(definition, businessType, businessId, startedBy: "workflow-postgres-probe");
    WorkflowTask? task = null;
    try
    {
        if (baseRuntime.Continue(instance).State != WorkflowRuntimeState.WaitingForApproval)
            throw new InvalidOperationException("并发审批探针初始化失败：未进入审批节点。");
        task = baseTasks.EnsureCurrentApprovalTask(instance).Single();

        using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        var handler = new ConcurrentApprovalActionHandler();
        var leftTasksRepository = new FreeSqlWorkflowTaskRepository(left);
        var rightTasksRepository = new FreeSqlWorkflowTaskRepository(right);
        var leftInstances = new FreeSqlWorkflowInstanceRepository(left);
        var rightInstances = new FreeSqlWorkflowInstanceRepository(right);
        var leftOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(left));
        var rightOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(right));
        var leftTransactions = new FreeSqlWorkflowTransactionBoundary(left);
        var rightTransactions = new FreeSqlWorkflowTransactionBoundary(right);
        var leftInstanceService = new WorkflowInstanceService(leftInstances, leftOperations, leftTransactions);
        var rightInstanceService = new WorkflowInstanceService(rightInstances, rightOperations, rightTransactions);
        var leftNotifications = new NotificationService(new FreeSqlNotificationRepository(left), transactions: leftTransactions);
        var rightNotifications = new NotificationService(new FreeSqlNotificationRepository(right), transactions: rightTransactions);
        var leftExecutor = new WorkflowActionExecutor([handler]);
        var rightExecutor = new WorkflowActionExecutor([handler]);
        var leftRuntime = new WorkflowRuntimeService(leftInstanceService, leftExecutor, leftNotifications, leftOperations, leftTransactions);
        var rightRuntime = new WorkflowRuntimeService(rightInstanceService, rightExecutor, rightNotifications, rightOperations, rightTransactions);
        var leftTaskService = new WorkflowTaskService(leftTasksRepository, leftInstanceService, leftExecutor, leftNotifications, leftOperations, leftRuntime, leftTransactions);
        var rightTaskService = new WorkflowTaskService(rightTasksRepository, rightInstanceService, rightExecutor, rightNotifications, rightOperations, rightRuntime, rightTransactions);
        var leftInstance = leftInstances.List(businessType, businessId, WorkflowInstanceStatus.Running).Single();
        var rightInstance = rightInstances.List(businessType, businessId, WorkflowInstanceStatus.Running).Single();
        var leftTask = leftTasksRepository.List(leftInstance.Id, status: WorkflowTaskStatus.Pending).Single();
        var rightTask = rightTasksRepository.List(rightInstance.Id, status: WorkflowTaskStatus.Pending).Single();

        var results = Task.WhenAll(
            Task.Run(() => RunApproval(leftTaskService, leftTask)),
            Task.Run(() => RunApproval(rightTaskService, rightTask)))
            .GetAwaiter()
            .GetResult();
        if (results.Count(x => x.Succeeded) != 1 || results.Count(x => x.Error?.Message.Contains("状态已变化", StringComparison.Ordinal) == true) != 1)
            throw new InvalidOperationException($"{databaseType} 并发审批探针失败：结果未保持一胜一拒。{string.Join(" | ", results.Select(x => x.Succeeded ? "success" : $"{x.Error?.GetType().Name}:{x.Error?.Message}"))}");

        var persistedTask = new FreeSqlWorkflowTaskRepository(fsql).List(instance.Id).Single();
        var persistedInstance = baseInstances.List(businessType, businessId).Single();
        var history = baseOperations.List(instanceId: instance.Id);
        if (persistedTask.Status != WorkflowTaskStatus.Approved
            || persistedInstance.Status != WorkflowInstanceStatus.Completed
            || handler.ExecutionCount != 1
            || handler.LastActor != "workflow-postgres-probe"
            || history.Count(x => x.Kind == WorkflowOperationKind.Approved && x.TaskId == task.Id) != 1
            || history.Count(x => x.Kind == WorkflowOperationKind.NodeExecuted && x.Actor == "workflow-postgres-probe") != 1)
            throw new InvalidOperationException($"{databaseType} 并发审批探针失败：待办、实例、handler 或审批历史出现重复/部分提交。task={persistedTask.Status}, instance={persistedInstance.Status}, executions={handler.ExecutionCount}, actor={handler.LastActor}, approvals={history.Count(x => x.Kind == WorkflowOperationKind.Approved)}。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }

    static (bool Succeeded, Exception? Error) RunApproval(WorkflowTaskService service, WorkflowTask task)
    {
        try
        {
            service.Approve(task, "workflow-postgres-probe", "并发审批");
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }
}

static void RunConcurrentWorkflowApprovalWithdrawalProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    const string businessType = "workflow.postgres.approval-withdraw-concurrent";
    var businessId = Guid.CreateVersion7();
    var definition = new WorkflowDefinition("PG_APPROVAL_WITHDRAW_CONCURRENT", "并发审批撤回");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"workflow-postgres-probe\"}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, approval.Id);
    definition.Connect(approval.Id, end.Id);
    definition.Publish();

    var baseOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
    var baseTransactions = new FreeSqlWorkflowTransactionBoundary(fsql);
    var baseInstances = new FreeSqlWorkflowInstanceRepository(fsql);
    var baseInstanceService = new WorkflowInstanceService(baseInstances, baseOperations, baseTransactions);
    var baseNotifications = new NotificationService(new FreeSqlNotificationRepository(fsql), transactions: baseTransactions);
    var baseRuntime = new WorkflowRuntimeService(baseInstanceService, new WorkflowActionExecutor([]), baseNotifications, baseOperations, baseTransactions);
    var baseTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(fsql), baseInstanceService, runtime: baseRuntime, operations: baseOperations, transactions: baseTransactions);
    var instance = baseInstanceService.Start(definition, businessType, businessId, startedBy: "workflow-postgres-probe");
    try
    {
        if (baseRuntime.Continue(instance).State != WorkflowRuntimeState.WaitingForApproval)
            throw new InvalidOperationException("并发审批/撤回探针初始化失败：未进入审批节点。");
        var task = baseTasks.EnsureCurrentApprovalTask(instance).Single();

        using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        var leftOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(left));
        var rightOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(right));
        var leftTransactions = new FreeSqlWorkflowTransactionBoundary(left);
        var rightTransactions = new FreeSqlWorkflowTransactionBoundary(right);
        var leftInstances = new FreeSqlWorkflowInstanceRepository(left);
        var rightInstances = new FreeSqlWorkflowInstanceRepository(right);
        var leftInstanceService = new WorkflowInstanceService(leftInstances, leftOperations, leftTransactions);
        var rightInstanceService = new WorkflowInstanceService(rightInstances, rightOperations, rightTransactions);
        var leftNotifications = new NotificationService(new FreeSqlNotificationRepository(left), transactions: leftTransactions);
        var rightNotifications = new NotificationService(new FreeSqlNotificationRepository(right), transactions: rightTransactions);
        var leftRuntime = new WorkflowRuntimeService(leftInstanceService, new WorkflowActionExecutor([]), leftNotifications, leftOperations, leftTransactions);
        var rightRuntime = new WorkflowRuntimeService(rightInstanceService, new WorkflowActionExecutor([]), rightNotifications, rightOperations, rightTransactions);
        var leftTaskService = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(left), leftInstanceService, runtime: leftRuntime, operations: leftOperations, transactions: leftTransactions);
        var rightTaskService = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(right), rightInstanceService, runtime: rightRuntime, operations: rightOperations, transactions: rightTransactions);
        var leftTask = leftTaskService.List(instance.Id, status: WorkflowTaskStatus.Pending).Single();

        var results = Task.WhenAll(
            Task.Run(() => RunApproval(leftTaskService, leftTask)),
            Task.Run(() => RunWithdrawal(rightTaskService, instance.Id)))
            .GetAwaiter()
            .GetResult();
        if (results.Count(x => x.Succeeded) != 1 || results.Count(x => !x.Succeeded) != 1)
            throw new InvalidOperationException($"{databaseType} 审批/撤回并发探针失败：未保持单一胜出事务。{string.Join(" | ", results.Select(x => x.Succeeded ? "success" : $"{x.Error?.GetType().Name}:{x.Error?.Message}"))}");

        var persistedTask = new FreeSqlWorkflowTaskRepository(fsql).List(instance.Id).Single();
        var persistedInstance = baseInstances.List(businessType, businessId).Single();
        var history = baseOperations.List(instanceId: instance.Id);
        var approvalWon = results[0].Succeeded;
        var expectedTaskStatus = approvalWon ? WorkflowTaskStatus.Approved : WorkflowTaskStatus.Cancelled;
        var expectedInstanceStatus = approvalWon ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.Cancelled;
        var expectedOperation = approvalWon ? WorkflowOperationKind.Approved : WorkflowOperationKind.Withdrawn;
        if (persistedTask.Status != expectedTaskStatus
            || persistedInstance.Status != expectedInstanceStatus
            || history.Count(x => x.Kind == expectedOperation) != 1
            || history.Any(x => x.Kind == (approvalWon ? WorkflowOperationKind.Withdrawn : WorkflowOperationKind.Approved)))
            throw new InvalidOperationException($"{databaseType} 审批/撤回并发探针失败：实例与待办未保持同一终态。task={persistedTask.Status}, instance={persistedInstance.Status}, operations={string.Join(",", history.Select(x => x.Kind))}。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }

    static (bool Succeeded, Exception? Error) RunApproval(WorkflowTaskService service, WorkflowTask task)
    {
        try
        {
            service.Approve(task, "workflow-postgres-probe", "并发审批");
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }

    static (bool Succeeded, Exception? Error) RunWithdrawal(WorkflowTaskService service, Guid instanceId)
    {
        try
        {
            service.Withdraw(instanceId, "workflow-postgres-probe", "并发撤回");
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }
}

static void RunConcurrentWorkflowRetryWithdrawalProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    const string businessType = "workflow.postgres.retry-withdraw-concurrent";
    var businessId = Guid.CreateVersion7();
    var definition = new WorkflowDefinition("PG_RETRY_WITHDRAW_CONCURRENT", "并发重试撤回");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "自动动作", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, action.Id);
    definition.Connect(action.Id, end.Id);
    definition.Publish();

    var baseOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
    var baseTransactions = new FreeSqlWorkflowTransactionBoundary(fsql);
    var baseInstances = new FreeSqlWorkflowInstanceRepository(fsql);
    var baseInstanceService = new WorkflowInstanceService(baseInstances, baseOperations, baseTransactions);
    var handler = new ConcurrentRetryWithdrawalActionHandler();
    var baseNotifications = new NotificationService(new FreeSqlNotificationRepository(fsql), transactions: baseTransactions);
    var baseRuntime = new WorkflowRuntimeService(baseInstanceService, new WorkflowActionExecutor([handler]), baseNotifications, baseOperations, baseTransactions);
    var instance = baseInstanceService.Start(definition, businessType, businessId, startedBy: "workflow-postgres-probe");
    try
    {
        try
        {
            baseRuntime.Continue(instance);
            throw new InvalidOperationException("并发重试/撤回探针初始化失败：首次自动动作本应失败。" );
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟并发重试撤回首次失败")
        {
            // 预期：保留 NodeFailed，实例仍为 Running。
        }

        var failure = baseOperations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed).Single(x => x.NodeId == action.Id);
        handler.AllowSuccess = true;
        using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        var leftOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(left));
        var rightOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(right));
        var leftTransactions = new FreeSqlWorkflowTransactionBoundary(left);
        var rightTransactions = new FreeSqlWorkflowTransactionBoundary(right);
        var leftInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(left), leftOperations, leftTransactions);
        var rightInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(right), rightOperations, rightTransactions);
        var leftRuntime = new WorkflowRuntimeService(leftInstances, new WorkflowActionExecutor([handler]), new NotificationService(new FreeSqlNotificationRepository(left)), leftOperations, leftTransactions);
        var rightTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(right), rightInstances, operations: rightOperations, transactions: rightTransactions);
        var leftInstance = leftInstances.List(businessType, businessId, WorkflowInstanceStatus.Running).Single();
        var rightInstance = rightInstances.List(businessType, businessId, WorkflowInstanceStatus.Running).Single();

        var results = Task.WhenAll(
            Task.Run(() => RunRetry(leftRuntime, leftInstance, action.Id)),
            Task.Run(() => RunWithdrawal(rightTasks, rightInstance.Id)))
            .GetAwaiter()
            .GetResult();
        if (results.Count(x => x.Succeeded) != 1)
            throw new InvalidOperationException($"{databaseType} 并发重试/撤回探针失败：未保持单一胜出事务。{string.Join(" | ", results.Select(x => x.Succeeded ? "success" : $"{x.Error?.GetType().Name}:{x.Error?.Message}"))}");

        var retryWon = results[0].Succeeded;
        var persisted = baseInstances.List(businessType, businessId).Single();
        var history = baseOperations.List(instanceId: instance.Id);
        var expectedStatus = retryWon ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.Cancelled;
        var unexpectedOperation = retryWon ? WorkflowOperationKind.Withdrawn : WorkflowOperationKind.Retried;
        if (persisted.Status != expectedStatus
            || history.Count(x => x.Kind == (retryWon ? WorkflowOperationKind.Retried : WorkflowOperationKind.Withdrawn)) != 1
            || history.Any(x => x.Kind == unexpectedOperation)
            || history.Count(x => x.Kind == WorkflowOperationKind.NodeExecuted && x.NodeId == action.Id) != (retryWon ? 1 : 0)
            || handler.ExecutionCount != (retryWon ? 2 : 1)
            || (retryWon && history.Single(x => x.Kind == WorkflowOperationKind.Retried).DedupeKey != $"workflow-runtime-retried:{instance.Id}:{action.Id}:{failure.Id:N}"))
            throw new InvalidOperationException($"{databaseType} 并发重试/撤回探针失败：终态、失败审计或动作执行出现交叉提交。status={persisted.Status}, retryWon={retryWon}, executions={handler.ExecutionCount}, operations={string.Join(",", history.Select(x => x.Kind))}。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }

    static (bool Succeeded, Exception? Error) RunRetry(WorkflowRuntimeService runtime, WorkflowInstance instance, Guid actionId)
    {
        try
        {
            runtime.Retry(instance, "workflow-postgres-probe", failedNodeId: actionId);
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }

    static (bool Succeeded, Exception? Error) RunWithdrawal(WorkflowTaskService service, Guid instanceId)
    {
        try
        {
            service.Withdraw(instanceId, "workflow-postgres-probe", "并发撤回");
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }
}

static void RunConcurrentWorkflowContinueWithdrawalProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    const string businessType = "workflow.postgres.continue-withdraw-concurrent";
    var businessId = Guid.CreateVersion7();
    var definition = new WorkflowDefinition("PG_CONTINUE_WITHDRAW_CONCURRENT", "并发继续撤回");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "自动动作", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, action.Id);
    definition.Connect(action.Id, end.Id);
    definition.Publish();

    var baseOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
    var baseTransactions = new FreeSqlWorkflowTransactionBoundary(fsql);
    var baseInstances = new FreeSqlWorkflowInstanceRepository(fsql);
    var baseInstanceService = new WorkflowInstanceService(baseInstances, baseOperations, baseTransactions);
    var handler = new ConcurrentContinueWithdrawalActionHandler();
    var baseRuntime = new WorkflowRuntimeService(
        baseInstanceService,
        new WorkflowActionExecutor([handler]),
        new NotificationService(new FreeSqlNotificationRepository(fsql)),
        baseOperations,
        baseTransactions);
    var instance = baseInstanceService.Start(definition, businessType, businessId, startedBy: "workflow-postgres-probe");
    try
    {
        try
        {
            baseRuntime.Continue(instance);
            throw new InvalidOperationException("并发继续/撤回探针初始化失败：首次自动动作本应失败。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟并发继续撤回首次失败")
        {
            // 预期：保留 NodeFailed，实例仍为 Running。
        }

        var failure = baseOperations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed).Single(x => x.NodeId == action.Id);
        handler.AllowSuccess = true;
        using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
        var leftOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(left));
        var rightOperations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(right));
        var leftTransactions = new FreeSqlWorkflowTransactionBoundary(left);
        var rightTransactions = new FreeSqlWorkflowTransactionBoundary(right);
        var leftInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(left), leftOperations, leftTransactions);
        var rightInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(right), rightOperations, rightTransactions);
        var leftRuntime = new WorkflowRuntimeService(leftInstances, new WorkflowActionExecutor([handler]), new NotificationService(new FreeSqlNotificationRepository(left)), leftOperations, leftTransactions);
        var rightTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(right), rightInstances, operations: rightOperations, transactions: rightTransactions);
        var leftInstance = leftInstances.List(businessType, businessId, WorkflowInstanceStatus.Running).Single();
        var rightInstance = rightInstances.List(businessType, businessId, WorkflowInstanceStatus.Running).Single();

        var results = Task.WhenAll(
            Task.Run(() => RunContinue(leftRuntime, leftInstance, action.Id)),
            Task.Run(() => RunWithdrawal(rightTasks, rightInstance.Id)))
            .GetAwaiter()
            .GetResult();
        if (results.Count(x => x.Succeeded) != 1)
            throw new InvalidOperationException($"{databaseType} 并发继续/撤回探针失败：未保持单一胜出事务。{string.Join(" | ", results.Select(x => x.Succeeded ? "success" : $"{x.Error?.GetType().Name}:{x.Error?.Message}"))}");

        var continueWon = results[0].Succeeded;
        var persisted = baseInstances.List(businessType, businessId).Single();
        var history = baseOperations.List(instanceId: instance.Id);
        var expectedStatus = continueWon ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.Cancelled;
        var unexpectedOperation = continueWon ? WorkflowOperationKind.Withdrawn : WorkflowOperationKind.NodeExecuted;
        if (persisted.Status != expectedStatus
            || history.Count(x => x.Kind == (continueWon ? WorkflowOperationKind.NodeCompleted : WorkflowOperationKind.Withdrawn)) < (continueWon ? 1 : 0)
            || history.Any(x => x.Kind == unexpectedOperation && (!continueWon || x.NodeId == action.Id))
            || history.Count(x => x.Kind == WorkflowOperationKind.NodeFailed && x.NodeId == action.Id) != 1
            || history.Count(x => x.Kind == WorkflowOperationKind.NodeExecuted && x.NodeId == action.Id) != (continueWon ? 1 : 0)
            || handler.ExecutionCount != (continueWon ? 2 : 1)
            || failure.NodeId != action.Id)
            throw new InvalidOperationException($"{databaseType} 并发继续/撤回探针失败：终态、失败审计或动作执行出现交叉提交。status={persisted.Status}, continueWon={continueWon}, executions={handler.ExecutionCount}, operations={string.Join(",", history.Select(x => x.Kind))}。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }

    static (bool Succeeded, Exception? Error) RunContinue(WorkflowRuntimeService runtime, WorkflowInstance instance, Guid actionId)
    {
        try
        {
            runtime.Continue(instance, preferredNodeId: actionId);
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }

    static (bool Succeeded, Exception? Error) RunWithdrawal(WorkflowTaskService service, Guid instanceId)
    {
        try
        {
            service.Withdraw(instanceId, "workflow-postgres-probe", "并发撤回");
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }
}

static void RunStandaloneApprovalTaskTransactionProbe(IFreeSql fsql)
{
    var definition = CreateDefinition();
    var instance = WorkflowInstance.Start(definition, "workflow.postgres.standalone-task-transaction", Guid.CreateVersion7(), startedBy: "workflow-postgres-probe");
    var secondInstance = WorkflowInstance.Start(definition, "workflow.postgres.standalone-task-repair-transaction", Guid.CreateVersion7(), startedBy: "workflow-postgres-probe");
    var thirdInstance = WorkflowInstance.Start(definition, "workflow.postgres.standalone-task-batch-transaction", Guid.CreateVersion7(), startedBy: "workflow-postgres-probe");
    var instances = new FreeSqlWorkflowInstanceRepository(fsql);
    var tasks = new FreeSqlWorkflowTaskRepository(fsql);
    var node = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
    try
    {
        instances.Add(instance);
        secondInstance.AdvanceTo(node.Id);
        instances.Add(secondInstance);
        instances.Add(thirdInstance);
        var service = new WorkflowTaskService(
            tasks,
            operations: new WorkflowOperationService(new ThrowingWorkflowOperationRepository(new FreeSqlWorkflowOperationRepository(fsql))),
            transactions: new FreeSqlWorkflowTransactionBoundary(fsql));
        var exception = AssertThrows(() => service.CreateApprovalTask(instance, node.Id, node.Name, "workflow-postgres-probe"));
        if (exception.Message != "模拟 Workflow 操作历史写入失败")
            throw new InvalidOperationException($"PostgreSQL Workflow 独立待办事务故障注入异常不匹配：{exception.Message}");
        if (tasks.List(instance.Id).Count != 0)
            throw new InvalidOperationException("PostgreSQL Workflow 独立待办事务未回滚孤儿待办。");
        var repairException = AssertThrows(() => service.EnsureCurrentApprovalTask(secondInstance));
        if (repairException.Message != "模拟 Workflow 操作历史写入失败")
            throw new InvalidOperationException($"PostgreSQL Workflow 独立待办补偿事务故障注入异常不匹配：{repairException.Message}");
        if (tasks.List(secondInstance.Id).Count != 0 || secondInstance.ApprovalAssigneesJson != "{}")
            throw new InvalidOperationException("PostgreSQL Workflow 独立待办补偿事务未回滚审批人快照或孤儿待办。");
        var batchException = AssertThrows(() => service.EnsureApprovalTasks(thirdInstance, definition));
        if (batchException.Message != "模拟 Workflow 操作历史写入失败")
            throw new InvalidOperationException($"PostgreSQL Workflow 独立批量待办事务故障注入异常不匹配：{batchException.Message}");
        if (tasks.List(thirdInstance.Id).Count != 0 || thirdInstance.ApprovalAssigneesJson != "{}")
            throw new InvalidOperationException("PostgreSQL Workflow 独立批量待办事务未回滚审批人快照或孤儿待办。");
    }
    finally
    {
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.InstanceId == instance.Id || x.InstanceId == secondInstance.Id || x.InstanceId == thirdInstance.Id).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == instance.Id || x.Id == secondInstance.Id || x.Id == thirdInstance.Id).ExecuteAffrows();
    }
}

static void RunWorkflowPersistenceBenchmark(IFreeSql fsql, DataType databaseType)
{
    const int iterations = 200;
    var runKey = Guid.CreateVersion7().ToString("N");
    var definition = CreateDefinition();
    var instanceRepository = new FreeSqlWorkflowInstanceRepository(fsql);
    var taskRepository = new FreeSqlWorkflowTaskRepository(fsql);
    var operationRepository = new FreeSqlWorkflowOperationRepository(fsql);
    var notificationRepository = new FreeSqlNotificationRepository(fsql);
    var operationIds = new List<Guid>(iterations);
    var notificationIds = new List<Guid>(iterations);
    var taskIds = new List<Guid>(iterations);
    var instanceIds = new List<Guid>(iterations);
    var benchmarkInstance = WorkflowInstance.Start(definition, "workflow.postgres.benchmark", Guid.CreateVersion7(), startedBy: "workflow-postgres-benchmark");
    var node = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval);
    var recipient = $"workflow-benchmark-{runKey}";

    try
    {
        // 让独立 benchmark 入口先完成自动建表，避免首个原生 INSERT 把建表耗时混入样本。
        _ = fsql.Select<WorkflowInstanceRecord>().Limit(1).ToList();
        _ = fsql.Select<WorkflowTaskRecord>().Limit(1).ToList();
        _ = fsql.Select<WorkflowOperationRecord>().Limit(1).ToList();
        _ = fsql.Select<NotificationRecord>().Limit(1).ToList();

        var operationStats = Measure(iterations, index =>
        {
            var operation = new WorkflowOperation(
                benchmarkInstance.Id,
                null,
                node.Id,
                benchmarkInstance.BusinessType,
                benchmarkInstance.BusinessId,
                WorkflowOperationKind.NodeExecuted,
                "workflow-postgres-benchmark",
                null,
                "性能基准",
                $"workflow-benchmark-operation:{runKey}:{index}",
                DateTime.Now);
            if (!operationRepository.TryAdd(operation)) throw new InvalidOperationException("Workflow 操作历史性能基准写入未成功。");
            operationIds.Add(operation.Id);
        });
        var notificationStats = Measure(iterations, index =>
        {
            var notification = new WorkNotification(
                recipient,
                WorkNotificationKind.System,
                "性能基准",
                "Workflow 持久化性能基准",
                null,
                $"workflow-benchmark-notification:{runKey}:{index}");
            if (!notificationRepository.TryAdd(notification)) throw new InvalidOperationException("Workflow 通知性能基准写入未成功。");
            notificationIds.Add(notification.Id);
        });
        var taskStats = Measure(iterations, index =>
        {
            var task = new WorkflowTask(benchmarkInstance, node.Id, node.Name, $"workflow-benchmark-{index}", round: 1);
            if (!taskRepository.TryAdd(task)) throw new InvalidOperationException("Workflow 待办性能基准写入未成功。");
            taskIds.Add(task.Id);
        });
        var instanceStats = Measure(iterations, index =>
        {
            var instance = WorkflowInstance.Start(definition, "workflow.postgres.benchmark", Guid.CreateVersion7(), startedBy: "workflow-postgres-benchmark");
            if (!instanceRepository.TryAdd(instance)) throw new InvalidOperationException("Workflow 实例性能基准写入未成功。");
            instanceIds.Add(instance.Id);
        });

        Console.WriteLine($"Workflow persistence benchmark ({databaseType}, n={iterations}):");
        Console.WriteLine($"  WorkflowInstance.TryAdd  p50={instanceStats.P50Ms:F3}ms p95={instanceStats.P95Ms:F3}ms p99={instanceStats.P99Ms:F3}ms");
        Console.WriteLine($"  WorkflowTask.TryAdd      p50={taskStats.P50Ms:F3}ms p95={taskStats.P95Ms:F3}ms p99={taskStats.P99Ms:F3}ms");
        Console.WriteLine($"  WorkflowOperation.TryAdd p50={operationStats.P50Ms:F3}ms p95={operationStats.P95Ms:F3}ms p99={operationStats.P99Ms:F3}ms");
        Console.WriteLine($"  Notification.TryAdd      p50={notificationStats.P50Ms:F3}ms p95={notificationStats.P95Ms:F3}ms p99={notificationStats.P99Ms:F3}ms");
    }
    finally
    {
        if (instanceIds.Count > 0) fsql.Delete<WorkflowInstanceRecord>().Where(x => instanceIds.Contains(x.Id)).ExecuteAffrows();
        if (taskIds.Count > 0) fsql.Delete<WorkflowTaskRecord>().Where(x => taskIds.Contains(x.Id)).ExecuteAffrows();
        if (operationIds.Count > 0) fsql.Delete<WorkflowOperationRecord>().Where(x => operationIds.Contains(x.Id)).ExecuteAffrows();
        if (notificationIds.Count > 0) fsql.Delete<NotificationRecord>().Where(x => notificationIds.Contains(x.Id)).ExecuteAffrows();
    }

    static BenchmarkStats Measure(int count, Action<int> action)
    {
        var samples = new double[count];
        for (var index = 0; index < count; index++)
        {
            var start = Stopwatch.GetTimestamp();
            action(index);
            samples[index] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        Array.Sort(samples);
        return new(samples[count / 2], samples[Math.Min(count - 1, (int)Math.Ceiling(count * 0.95) - 1)], samples[Math.Min(count - 1, (int)Math.Ceiling(count * 0.99) - 1)]);
    }

}

static void RunWorkflowLegacyBackfillProbe(IFreeSql fsql)
{
    var definition = CreateDefinition();
    var instance = WorkflowInstance.Start(definition, "workflow.postgres.legacy-backfill", Guid.CreateVersion7(), startedBy: "workflow-postgres-probe");
    var repository = new FreeSqlWorkflowInstanceRepository(fsql);
    try
    {
        repository.Add(instance);
        fsql.Update<WorkflowInstanceRecord>()
            .Set(x => x.Revision, 0L)
            .Set(x => x.DefinitionCode, definition.Code.ToLowerInvariant())
            .Set(x => x.ActiveNodeIdsJson, string.Empty)
            .Set(x => x.ParallelJoinArrivalsJson, string.Empty)
            .Set(x => x.LoopIterationsJson, string.Empty)
            .Set(x => x.ApprovalAssigneesJson, string.Empty)
            .Where(x => x.Id == instance.Id)
            .ExecuteAffrows();

        WorkflowSchemaMigration.BackfillInitialRevisions(fsql);
        var persisted = fsql.Select<WorkflowInstanceRecord>().Where(x => x.Id == instance.Id).ToOne();
        if (persisted is null || persisted.Revision != 1L || persisted.DefinitionCode != definition.Code
            || persisted.ActiveNodeIdsJson != $"[\"{persisted.CurrentNodeId}\"]"
            || persisted.ParallelJoinArrivalsJson != "{}" || persisted.LoopIterationsJson != "{}" || persisted.ApprovalAssigneesJson != "{}")
            throw new InvalidOperationException($"PostgreSQL Workflow 旧实例批量回填结果不完整：revision={persisted?.Revision}, active={persisted?.ActiveNodeIdsJson}, join={persisted?.ParallelJoinArrivalsJson}, loop={persisted?.LoopIterationsJson}, approval={persisted?.ApprovalAssigneesJson}。");

        fsql.Update<WorkflowInstanceRecord>()
            .Set(x => x.Revision, 0L)
            .Set(x => x.ActiveNodeIdsJson, string.Empty)
            .Set(x => x.ParallelJoinArrivalsJson, string.Empty)
            .Set(x => x.LoopIterationsJson, string.Empty)
            .Set(x => x.ApprovalAssigneesJson, string.Empty)
            .Where(x => x.Id == instance.Id)
            .ExecuteAffrows();
        AssertThrows(() => fsql.Transaction(() =>
        {
            WorkflowSchemaMigration.BackfillInitialRevisions(fsql);
            throw new InvalidOperationException("验证迁移外层事务回滚。");
        }));
        var rolledBack = fsql.Select<WorkflowInstanceRecord>().Where(x => x.Id == instance.Id).ToOne();
        if (rolledBack is null || rolledBack.Revision != 0L || rolledBack.ActiveNodeIdsJson != string.Empty
            || rolledBack.ParallelJoinArrivalsJson != string.Empty || rolledBack.LoopIterationsJson != string.Empty || rolledBack.ApprovalAssigneesJson != string.Empty)
            throw new InvalidOperationException("PostgreSQL Workflow 迁移未复用外层事务，回滚后留下了部分回填。");
    }
    finally
    {
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == instance.Id).ExecuteAffrows();
    }
}

static void RunWorkflowLegacyMixedCaseDuplicateGuardProbe(IFreeSql fsql, DataType databaseType)
{
    var definition = CreateDefinition();
    var businessId = Guid.CreateVersion7();
    var first = WorkflowInstance.Start(definition, "workflow.postgres.legacy-duplicate", businessId, startedBy: "workflow-postgres-probe");
    var second = WorkflowInstance.Start(definition, "workflow.postgres.legacy-duplicate", businessId, startedBy: "workflow-postgres-probe");
    var repository = new FreeSqlWorkflowInstanceRepository(fsql);
    try
    {
        switch (databaseType)
        {
            case DataType.PostgreSQL:
                fsql.Ado.ExecuteNonQuery($"DROP INDEX IF EXISTS \"{WorkflowSchemaMigration.RunningBusinessDefinitionUniqueIndex}\";");
                break;
            case DataType.SqlServer:
                fsql.Ado.ExecuteNonQuery($"IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{WorkflowSchemaMigration.RunningBusinessDefinitionUniqueIndex}' AND object_id = OBJECT_ID(N'WorkflowInstance')) DROP INDEX [{WorkflowSchemaMigration.RunningBusinessDefinitionUniqueIndex}] ON [WorkflowInstance];");
                break;
        }
        repository.Add(first);
        // 绕过仓储自身的业务唯一键 MERGE/冲突语义，直接构造旧库中可能存在的
        // 混合大小写历史记录；迁移负责在创建唯一索引前给出可处理的重复提示。
        fsql.Insert<WorkflowInstanceRecord>().AppendData(new WorkflowInstanceRecord
        {
            Id = second.Id,
            DefinitionId = second.DefinitionId,
            DefinitionCode = definition.Code.ToLowerInvariant(),
            DefinitionVersion = second.DefinitionVersion,
            BusinessType = second.BusinessType,
            BusinessId = second.BusinessId,
            StartedBy = second.StartedBy,
            DefinitionSnapshotJson = second.DefinitionSnapshotJson,
            Status = second.Status,
            CurrentNodeId = second.CurrentNodeId,
            StartedAt = second.StartedAt,
            CompletedAt = second.CompletedAt,
            PreviousInstanceId = second.PreviousInstanceId,
            Revision = second.Revision,
            ActiveNodeIdsJson = second.ActiveNodeIdsJson,
            ParallelJoinArrivalsJson = second.ParallelJoinArrivalsJson,
            LoopIterationsJson = second.LoopIterationsJson,
            ApprovalAssigneesJson = second.ApprovalAssigneesJson
        }).ExecuteAffrows();

        var exception = AssertThrows(() => WorkflowSchemaMigration.BackfillInitialRevisions(fsql));
        if (!exception.Message.Contains("同一业务", StringComparison.Ordinal))
            throw new InvalidOperationException($"Workflow 历史混合大小写重复检测未返回业务提示：{exception.Message}");
    }
    finally
    {
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == first.Id || x.Id == second.Id).ExecuteAffrows();
        WorkflowSchemaMigration.EnsureRunningBusinessUniqueness(fsql);
    }
}

static void RunLmsReplacementSubmittedUniquenessProbe(IFreeSql fsql)
{
    var originalAuthorizationId = Guid.CreateVersion7();
    var first = new LmsLicenseReplacementRequest($"LMS-PG-SUBMITTED-UNIQUE-{Guid.CreateVersion7():N}", originalAuthorizationId, LmsLicenseReplacementKind.Renewal, null, $"LIC-PG-SUBMITTED-UNIQUE-{Guid.CreateVersion7():N}", "opaque-postgres-license", DateTime.Today.AddYears(1), "{}", "workflow-postgres-probe", "验证审批中唯一索引", DateTime.Now);
    var second = new LmsLicenseReplacementRequest($"LMS-PG-SUBMITTED-UNIQUE-{Guid.CreateVersion7():N}", originalAuthorizationId, LmsLicenseReplacementKind.Reissue, null, $"LIC-PG-SUBMITTED-UNIQUE-{Guid.CreateVersion7():N}", "opaque-postgres-license", DateTime.Today.AddYears(1), "{}", "workflow-postgres-probe", "验证审批中唯一索引", DateTime.Now);
    try
    {
        LmsLicenseReplacementRequestSchemaMigration.EnsureSubmittedRequestUniqueness(fsql);
        var repository = new FreeSqlLmsLicenseReplacementRequestRepository(fsql);
        first.Submit();
        repository.Add(first);
        repository.Add(second);
        second.Submit();

        var exception = AssertThrows(() => repository.Update(second));
        if (exception.Message != "该原授权已有审批中的替代申请。")
            throw new InvalidOperationException($"PostgreSQL LMS 替代申请并发错误未转换为业务提示：{exception.Message}");
    }
    finally
    {
        fsql.Delete<LmsLicenseReplacementRequestRecord>().Where(x => x.Id == first.Id || x.Id == second.Id).ExecuteAffrows();
    }
}

static Exception AssertThrows(Action action)
{
    try
    {
        action();
    }
    catch (Exception exception)
    {
        return exception;
    }
    throw new InvalidOperationException("预期操作应抛出异常。");
}

static void RunLmsAuthorizationReplacementRollbackProbe(IFreeSql fsql)
{
    var original = new LmsLicenseAuthorization(
        null,
        $"LIC-PG-REPLACE-{Guid.CreateVersion7():N}",
        "opaque-postgres-license",
        "Velrix",
        "[]",
        DateTime.Today.AddDays(30),
        "{}",
        DateTime.Now);
    try
    {
        fsql.CodeFirst.SyncStructure<LmsLicenseAuthorizationRecord>();
        fsql.CodeFirst.SyncStructure<LmsLicenseLifecycleEntryRecord>();
        var repository = new FreeSqlLmsLicenseRepository(fsql);
        repository.Add(original);
        var service = new LmsLicenseService(
            new ThrowingLmsLifecycleRepository(repository),
            transactions: new FreeSqlWorkflowTransactionBoundary(fsql));

        try
        {
            service.ReplaceAuthorization(
                original,
                LmsLicenseReplacementKind.Renewal,
                $"LIC-PG-REPLACEMENT-{Guid.CreateVersion7():N}",
                "opaque-replacement-license",
                DateTime.Today.AddYears(1),
                "{}",
                "workflow-postgres-probe",
                "模拟生命周期审计失败");
            throw new InvalidOperationException("LMS 生命周期故障注入未触发。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟 LMS 生命周期审计写入失败")
        {
            // 预期：旧授权状态、替代授权和生命周期记录同一事务整体回滚。
        }

        var persisted = repository.ListAuthorizations().Single(x => x.Id == original.Id);
        if (persisted.Status != LmsLicenseStatus.Active || original.Status != LmsLicenseStatus.Active)
            throw new InvalidOperationException("PostgreSQL LMS 替代回滚失败：旧授权被部分停用。");
        if (repository.ListAuthorizations().Any(x => x.SupersedesAuthorizationId == original.Id))
            throw new InvalidOperationException("PostgreSQL LMS 替代回滚失败：留下了替代授权。");
        if (repository.ListLifecycleEntries(original.Id).Count != 0)
            throw new InvalidOperationException("PostgreSQL LMS 替代回滚失败：留下了生命周期审计。");
    }
    finally
    {
        fsql.Delete<LmsLicenseLifecycleEntryRecord>().Where(x => x.AuthorizationId == original.Id).ExecuteAffrows();
        fsql.Delete<LmsLicenseAuthorizationRecord>().Where(x => x.Id == original.Id || x.SupersedesAuthorizationId == original.Id).ExecuteAffrows();
    }
}

static void RunLmsReplacementInsertRollbackProbe(IFreeSql fsql)
{
    var original = new LmsLicenseAuthorization(
        null,
        $"LIC-PG-REPLACE-INSERT-{Guid.CreateVersion7():N}",
        "opaque-postgres-license",
        "Velrix",
        "[]",
        DateTime.Today.AddDays(30),
        "{}",
        DateTime.Now);
    try
    {
        var repository = new FreeSqlLmsLicenseRepository(fsql);
        repository.Add(original);
        var service = new LmsLicenseService(
            new ThrowingLmsReplacementInsertRepository(repository),
            transactions: new FreeSqlWorkflowTransactionBoundary(fsql));

        try
        {
            service.ReplaceAuthorization(
                original,
                LmsLicenseReplacementKind.Reissue,
                $"LIC-PG-REPLACEMENT-INSERT-{Guid.CreateVersion7():N}",
                "opaque-replacement-license",
                DateTime.Today.AddYears(1),
                "{}",
                "workflow-postgres-probe",
                "模拟替代授权写入失败");
            throw new InvalidOperationException("LMS 替代授权写入故障注入未触发。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟 LMS 替代授权写入失败")
        {
            // 预期：已写入的旧授权停用与生命周期审计都随失败整体回滚。
        }

        var persisted = repository.ListAuthorizations().Single(x => x.Id == original.Id);
        if (persisted.Status != LmsLicenseStatus.Active || original.Status != LmsLicenseStatus.Active)
            throw new InvalidOperationException("PostgreSQL LMS 替代回滚失败：替代写入失败后旧授权被部分停用。");
        if (repository.ListLifecycleEntries(original.Id).Count != 0)
            throw new InvalidOperationException("PostgreSQL LMS 替代回滚失败：替代写入失败后留下生命周期审计。");
    }
    finally
    {
        fsql.Delete<LmsLicenseLifecycleEntryRecord>().Where(x => x.AuthorizationId == original.Id).ExecuteAffrows();
        fsql.Delete<LmsLicenseAuthorizationRecord>().Where(x => x.Id == original.Id || x.SupersedesAuthorizationId == original.Id).ExecuteAffrows();
    }
}

static WorkflowDefinition CreateDefinition()
{
    var definition = new WorkflowDefinition("SALES_ORDER_POSTGRES_TRANSACTION", "销售订单 PostgreSQL 事务探针");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"workflow-postgres-probe\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, approval.Id);
    definition.Connect(approval.Id, end.Id);
    definition.Publish();
    return definition;
}

static WorkflowDefinition CreateApplicationApprovalDefinition(string code, string businessType, string approvedValue)
{
    var definition = new WorkflowDefinition(code, $"{businessType} Application 事务探针");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var approval = definition.AddNode(
        Guid.CreateVersion7(),
        WorkflowNodeType.Approval,
        "审批",
        configJson: $"{{\"approver\":\"workflow-postgres-probe\",\"onApproved\":{{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"{approvedValue}\"}}}}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, approval.Id);
    definition.Connect(approval.Id, end.Id);
    definition.Publish();
    return definition;
}

static void RunCrossModuleActionRollbackProbes(IFreeSql fsql)
{
    var contract = new SalesContract(Guid.CreateVersion7(), null, $"CT-PG-ROLLBACK-{Guid.CreateVersion7():N}", "合同事务回滚", 100m, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
    var contracts = new FreeSqlSalesContractRepository(fsql);
    RunCrossModuleActionRollbackProbe(
        fsql,
        "PG_CONTRACT_ACTION_ROLLBACK",
        nameof(SalesContract),
        contract.Id,
        nameof(ContractStatus.Active),
        new SalesContractWorkflowActionHandler(contracts),
        () => contracts.Add(contract),
        () => contracts.List().Single(x => x.Id == contract.Id).Status == ContractStatus.Draft,
        () => fsql.Delete<SalesContractRecord>().Where(x => x.Id == contract.Id).ExecuteAffrows());

    var projectChange = new PmsProjectChange(Guid.CreateVersion7(), "项目变更事务回滚", "验证跨模块动作失败回滚", null, "workflow-postgres-probe", DateTime.Now);
    var changes = new FreeSqlPmsProjectChangeRepository(fsql);
    RunCrossModuleActionRollbackProbe(
        fsql,
        "PG_PROJECT_CHANGE_ACTION_ROLLBACK",
        nameof(PmsProjectChange),
        projectChange.Id,
        nameof(PmsProjectChangeStatus.Approved),
        new PmsProjectChangeWorkflowActionHandler(changes),
        () => changes.Add(projectChange),
        () => changes.List().Single(x => x.Id == projectChange.Id).Status == PmsProjectChangeStatus.Proposed,
        () => fsql.Delete<PmsProjectChangeRecord>().Where(x => x.Id == projectChange.Id).ExecuteAffrows());

    var purchaseOrder = new PurchaseOrder($"PO-PG-ROLLBACK-{Guid.CreateVersion7():N}", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 2m, 10m);
    var purchaseOrders = new FreeSqlPurchaseOrderRepository(fsql);
    RunCrossModuleActionRollbackProbe(
        fsql,
        "PG_PURCHASE_ORDER_ACTION_ROLLBACK",
        nameof(PurchaseOrder),
        purchaseOrder.Id,
        nameof(PurchaseOrderStatus.Submitted),
        new PurchaseOrderWorkflowActionHandler(purchaseOrders),
        () => purchaseOrders.Add(purchaseOrder),
        () => purchaseOrders.List().Single(x => x.Id == purchaseOrder.Id).Status == PurchaseOrderStatus.Draft,
        () => fsql.Delete<PurchaseOrderRecord>().Where(x => x.Id == purchaseOrder.Id).ExecuteAffrows());
}

static void RunCrossModuleActionRollbackProbe(
    IFreeSql fsql,
    string definitionCode,
    string businessType,
    Guid businessId,
    string approvedValue,
    IWorkflowActionHandler handler,
    Action seed,
    Func<bool> businessStateIsRestored,
    Action cleanup)
{
    var innerInstances = new FreeSqlWorkflowInstanceRepository(fsql);
    var instances = new FailingCompletionInstanceRepository(innerInstances);
    var instanceService = new WorkflowInstanceService(instances);
    var tasks = new FreeSqlWorkflowTaskRepository(fsql);
    WorkflowInstance? instance = null;
    WorkflowTask? task = null;

    try
    {
        seed();
        var definition = CreateApplicationApprovalDefinition(definitionCode, businessType, approvedValue);
        instance = instanceService.Start(definition, businessType, businessId, startedBy: "workflow-postgres-probe");
        task = new WorkflowTask(instance, definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id, "跨模块审批", "workflow-postgres-probe");
        tasks.Add(task);
        var service = new WorkflowTaskService(
            tasks,
            instanceService,
            new WorkflowActionExecutor([handler]),
            transactions: new FreeSqlWorkflowTransactionBoundary(fsql));

        try
        {
            service.Approve(task, "workflow-postgres-probe", "故障注入");
            throw new InvalidOperationException($"{businessType} 跨模块动作故障注入未触发。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟流程完成持久化失败")
        {
            // 预期：业务动作已先写入，但流程终态持久化失败后必须整体回滚。
        }

        if (!businessStateIsRestored())
            throw new InvalidOperationException($"PostgreSQL {businessType} 跨模块动作回滚失败：业务状态发生了部分提交。");
        var persistedTask = tasks.List(instance.Id).Single();
        if (persistedTask.Status != WorkflowTaskStatus.Pending || persistedTask.Revision != 1)
            throw new InvalidOperationException($"PostgreSQL {businessType} 跨模块动作回滚失败：审批待办发生了部分提交。");
        if (innerInstances.List(businessId: businessId).Single().Status != WorkflowInstanceStatus.Running)
            throw new InvalidOperationException($"PostgreSQL {businessType} 跨模块动作回滚失败：流程实例发生了部分提交。");
    }
    finally
    {
        if (task is not null) fsql.Delete<WorkflowTaskRecord>().Where(x => x.Id == task.Id).ExecuteAffrows();
        if (instance is not null) fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == instance.Id).ExecuteAffrows();
        cleanup();
    }
}

static void RunControlledLoopProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, transactions: transactions);
        var definition = new WorkflowDefinition("PG_CONTROLLED_LOOP", "PostgreSQL 受控循环探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "补充审批", configJson: "{\"approver\":\"workflow-postgres-probe\"}");
        var loop = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Loop, "两轮上限", configJson: "{\"maxIterations\":2}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, loop.Id);
        definition.Connect(loop.Id, approval.Id, WorkflowLoopConfiguration.RepeatKey);
        definition.Connect(loop.Id, end.Id, WorkflowLoopConfiguration.ExitKey);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.loop", businessId, startedBy: "workflow-postgres-probe");
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), transactions: transactions);

        runtime.Continue(instance);
        runtime.ContinueAfterApproval(instance, approval.Id);
        var afterFirst = instances.List("workflow.postgres.loop", businessId).Single();
        if (afterFirst.CurrentNodeId != approval.Id || !afterFirst.LoopIterationsJson.Contains($"\"{loop.Id}\":1", StringComparison.Ordinal))
            throw new InvalidOperationException("PostgreSQL 受控循环失败：首轮未持久化循环计数并回到审批节点。");
        runtime.ContinueAfterApproval(instance, approval.Id);
        var completed = instances.List("workflow.postgres.loop", businessId).Single();
        if (completed.Status != WorkflowInstanceStatus.Completed || completed.CurrentNodeId != end.Id || !completed.LoopIterationsJson.Contains($"\"{loop.Id}\":2", StringComparison.Ordinal))
            throw new InvalidOperationException("PostgreSQL 受控循环失败：达到上限后未持久化退出并结束流程。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunLoopCasRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var inner = new FreeSqlWorkflowInstanceRepository(fsql);
        var setup = new WorkflowInstanceService(inner, transactions: transactions);
        var definition = new WorkflowDefinition("PG_LOOP_CAS_ROLLBACK", "PostgreSQL 循环 CAS 回滚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var loop = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Loop, "循环", configJson: "{\"maxIterations\":2}");
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "循环通知", configJson: "{\"recipients\":\"workflow-postgres-probe\",\"content\":\"循环中\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, loop.Id);
        definition.Connect(loop.Id, notification.Id, WorkflowLoopConfiguration.RepeatKey);
        definition.Connect(notification.Id, loop.Id);
        definition.Connect(loop.Id, end.Id, WorkflowLoopConfiguration.ExitKey);
        definition.Publish();
        var instance = setup.Start(definition, "workflow.postgres.loop-cas", businessId, startedBy: "workflow-postgres-probe");
        setup.Advance(instance, loop.Id);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var failing = new WorkflowInstanceService(new ThrowingLoopInstanceRepository(inner), operations, transactions);
        var runtime = new WorkflowRuntimeService(failing, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);

        try
        {
            runtime.Continue(instance);
            throw new InvalidOperationException("故障注入未触发，Loop CAS 不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟循环实例持久化失败") { }

        var persisted = inner.List("workflow.postgres.loop-cas", businessId).Single();
        if (persisted.CurrentNodeId != loop.Id || persisted.LoopIterationsJson != "{}" || persisted.Revision != 2 || operations.List(instanceId: instance.Id).Count != 0)
            throw new InvalidOperationException("PostgreSQL 事务回滚失败：Loop CAS 留下了循环计数、节点推进或操作历史。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunNestedParallelProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, transactions: transactions);
        var definition = new WorkflowDefinition("PG_NESTED_PARALLEL", "PostgreSQL 嵌套并行探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var outerSplit = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "外层拆分");
        var outerApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "外层审批", configJson: "{\"approver\":\"workflow-postgres-probe-outer\"}");
        var innerSplit = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "内层拆分");
        var innerFirst = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "内层部门审批", configJson: "{\"approver\":\"workflow-postgres-probe-department\"}");
        var innerSecond = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "内层财务审批", configJson: "{\"approver\":\"workflow-postgres-probe-finance\"}");
        var innerJoin = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "内层汇聚");
        var outerJoin = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "外层汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, outerSplit.Id);
        definition.Connect(outerSplit.Id, outerApproval.Id);
        definition.Connect(outerSplit.Id, innerSplit.Id);
        definition.Connect(outerApproval.Id, outerJoin.Id);
        definition.Connect(innerSplit.Id, innerFirst.Id);
        definition.Connect(innerSplit.Id, innerSecond.Id);
        definition.Connect(innerFirst.Id, innerJoin.Id);
        definition.Connect(innerSecond.Id, innerJoin.Id);
        definition.Connect(innerJoin.Id, outerJoin.Id);
        definition.Connect(outerJoin.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.nested-parallel", businessId, startedBy: "workflow-postgres-probe");
        var tasks = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), transactions: transactions);
        var service = new WorkflowTaskService(tasks, instanceService, runtime: runtime, transactions: transactions);

        runtime.Continue(instance);
        var pending = service.EnsureCurrentApprovalTask(instance);
        if (pending.Count != 3 || instance.ActiveNodeIds.Count != 3)
            throw new InvalidOperationException("PostgreSQL 嵌套并行失败：未同时激活外层与内层审批分支。");
        service.Approve(pending.Single(x => x.NodeId == outerApproval.Id), "workflow-postgres-probe-outer", "外层通过");
        service.Approve(pending.Single(x => x.NodeId == innerFirst.Id), "workflow-postgres-probe-department", "部门通过");
        if (instances.List("workflow.postgres.nested-parallel", businessId).Single().Status != WorkflowInstanceStatus.Running)
            throw new InvalidOperationException("PostgreSQL 嵌套并行失败：内层首条分支提前结束实例。");
        service.Approve(pending.Single(x => x.NodeId == innerSecond.Id), "workflow-postgres-probe-finance", "财务通过");
        if (instances.List("workflow.postgres.nested-parallel", businessId).Single().Status != WorkflowInstanceStatus.Completed)
            throw new InvalidOperationException("PostgreSQL 嵌套并行失败：两层汇聚后未完成实例。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunLoopJoinProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_LOOP_JOIN", "PostgreSQL 循环分支汇聚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"workflow-postgres-probe\"}");
        var loop = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Loop, "循环出口", configJson: "{\"maxIterations\":1}");
        var retry = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "循环重试", configJson: "{\"recipients\":\"system\",\"content\":\"重试\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(split.Id, loop.Id);
        definition.Connect(approval.Id, join.Id);
        definition.Connect(loop.Id, retry.Id, "repeat");
        definition.Connect(loop.Id, join.Id, "exit");
        definition.Connect(retry.Id, loop.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var taskRepository = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);
        var tasks = new WorkflowTaskService(taskRepository, instanceService, operations: operations, runtime: runtime, transactions: transactions);
        var instance = instanceService.Start(definition, "workflow.postgres.loop-join", businessId, startedBy: "workflow-postgres-probe");

        runtime.Continue(instance);
        if (!instance.ActiveNodeIds.SetEquals([approval.Id]) || !instance.ParallelJoinArrivalsJson.Contains(loop.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PostgreSQL 循环分支汇聚失败：Loop 未作为 Join 到达来源等待审批分支。");
        var task = tasks.EnsureCurrentApprovalTask(instance).Single();
        tasks.Approve(task, "workflow-postgres-probe", "通过");
        if (instances.List("workflow.postgres.loop-join", businessId).Single().Status != WorkflowInstanceStatus.Completed)
            throw new InvalidOperationException("PostgreSQL 循环分支汇聚失败：审批分支完成后实例未汇聚结束。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunStartRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    var code = $"PG_START_ROLLBACK_{Guid.CreateVersion7():N}";
    try
    {
        var definitions = new WorkflowDefinitionService(new InMemoryDefinitionRepository());
        var definition = definitions.CreateDraft(code, "PostgreSQL 启动回滚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"workflow-postgres-probe\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definitions.Publish(definition);

        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var tasks = new WorkflowTaskService(new ThrowingTaskAddRepository(new FreeSqlWorkflowTaskRepository(fsql)), instanceService, operations: operations);
        var binding = new WorkflowBindingService(definitions, instanceService, tasks, transactions: transactions);

        try
        {
            binding.StartOrGet(code, "workflow.postgres.start", businessId, startedBy: "workflow-postgres-probe");
            throw new InvalidOperationException("故障注入未触发，流程启动不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟初始待办写入失败")
        {
            // 预期。
        }

        if (instances.List("workflow.postgres.start", businessId).Count != 0 ||
            fsql.Select<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).Count() != 0 ||
            fsql.Select<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).Count() != 0)
            throw new InvalidOperationException("PostgreSQL 事务回滚失败：流程启动留下了实例、待办或操作历史。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunEmptyApproverRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    var code = $"PG_EMPTY_APPROVER_{Guid.CreateVersion7():N}";
    try
    {
        var definitions = new WorkflowDefinitionService(new InMemoryDefinitionRepository());
        var definition = definitions.CreateDraft(code, "PostgreSQL 空审批人回滚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "动态审批", configJson: "{\"approverOrgs\":[\"不存在的组织\"]}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definitions.Publish(definition);

        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var tasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(fsql), instanceService, operations: operations, approverResolver: new EmptyApproverResolver());
        var binding = new WorkflowBindingService(definitions, instanceService, tasks, transactions: transactions);

        try
        {
            binding.StartOrGet(code, "workflow.postgres.empty-approver", businessId, startedBy: "workflow-postgres-probe");
            throw new InvalidOperationException("空审批人解析不应允许启动流程。");
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("未解析到可用审批人", StringComparison.Ordinal))
        {
            // 预期：事务必须回滚实例、待办与操作历史。
        }

        if (instances.List("workflow.postgres.empty-approver", businessId).Count != 0 ||
            fsql.Select<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).Count() != 0 ||
            fsql.Select<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).Count() != 0)
            throw new InvalidOperationException("PostgreSQL 事务回滚失败：空审批人解析留下了半成品流程状态。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunApproverLookupCaseInsensitiveProbe(IFreeSql fsql)
{
    var suffix = Guid.CreateVersion7().ToString("N");
    var organization = new SysOrg
    {
        Id = Guid.CreateVersion7(),
        Label = $"PG-Workflow-Org-{suffix}",
        Type = SysOrg.OrgType.部门,
        IsEnabled = true,
        Description = "Workflow PostgreSQL 审批人查询探针"
    };
    var role = new SysRole
    {
        Id = Guid.CreateVersion7(),
        Name = $"PG-Workflow-Role-{suffix}",
        Description = "Workflow PostgreSQL 审批人查询探针"
    };
    var enabledUser = new SysUser
    {
        Id = Guid.CreateVersion7(),
        Username = $"pgwf-u-{suffix}",
        Nickname = "Workflow 探针用户",
        OrgId = organization.Id,
        IsEnabled = true,
        CreatedTime = DateTime.Now,
        CreatedUserName = "workflow-postgres-probe"
    };
    var disabledUser = new SysUser
    {
        Id = Guid.CreateVersion7(),
        Username = $"pgwf-d-{suffix}",
        Nickname = "Workflow 禁用探针用户",
        OrgId = organization.Id,
        IsEnabled = false,
        CreatedTime = DateTime.Now,
        CreatedUserName = "workflow-postgres-probe"
    };
    try
    {
        fsql.Insert(organization).ExecuteAffrows();
        fsql.Insert(role).ExecuteAffrows();
        fsql.Insert(enabledUser).ExecuteAffrows();
        fsql.Insert(disabledUser).ExecuteAffrows();
        fsql.Insert(new SysRoleUser { RoleId = role.Id, UserId = enabledUser.Id }).ExecuteAffrows();
        fsql.Insert(new SysRoleUser { RoleId = role.Id, UserId = disabledUser.Id }).ExecuteAffrows();

        var roleUsers = new FreeSqlWorkflowRoleApproverLookup(fsql).FindUsernames([role.Name.ToLowerInvariant()]);
        var organizationUsers = new FreeSqlWorkflowOrganizationApproverLookup(fsql).FindUsernames([organization.Label.ToLowerInvariant()]);
        if (!roleUsers.SequenceEqual([enabledUser.Username], StringComparer.OrdinalIgnoreCase) ||
            !organizationUsers.SequenceEqual([enabledUser.Username], StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("PostgreSQL 审批人查询失败：角色/组织大小写无关匹配或禁用用户过滤不正确。");
    }
    finally
    {
        fsql.Delete<SysRoleUser>().Where(x => x.RoleId == role.Id).ExecuteAffrows();
        fsql.Delete<SysUser>().Where(x => x.Id == enabledUser.Id || x.Id == disabledUser.Id).ExecuteAffrows();
        fsql.Delete<SysRole>().Where(x => x.Id == role.Id).ExecuteAffrows();
        fsql.Delete<SysOrg>().Where(x => x.Id == organization.Id).ExecuteAffrows();
    }
}

static void RunBusinessApproverFieldProbe(IFreeSql fsql)
{
    fsql.CodeFirst.SyncStructure<PmsProjectChangeRecord>();
    var projectId = Guid.CreateVersion7();
    var change = new PmsProjectChange(projectId, "Workflow 审批人字段探针", "验证项目变更申请人", null, "pgwf-requester", DateTime.Now);
    var emptyChange = new PmsProjectChange(projectId, "Workflow 空审批人字段探针", "验证空申请人回滚", null, null, DateTime.Now);
    var emptyBusinessId = emptyChange.Id;
    try
    {
        var repository = new FreeSqlPmsProjectChangeRepository(fsql);
        repository.Add(change);
        var instance = WorkflowInstance.Start(CreateDefinition(), nameof(PmsProjectChange), change.Id, startedBy: "workflow-postgres-probe");
        var lookup = new DefaultWorkflowBusinessApproverLookup([new PmsProjectChangeWorkflowApproverSource(repository)]);
        var resolver = new DefaultWorkflowApproverResolver(businessLookup: lookup);

        var users = resolver.Resolve(instance, "{\"approverBusinessFields\":[\"requestername\"]}");
        if (!users.SequenceEqual(["pgwf-requester"], StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("PostgreSQL 业务字段审批人查询失败：未解析到项目变更申请人。");

        repository.Add(emptyChange);
        var code = $"PG_BUSINESS_APPROVER_{Guid.CreateVersion7():N}";
        var definitions = new WorkflowDefinitionService(new InMemoryDefinitionRepository());
        var definition = definitions.CreateDraft(code, "PostgreSQL 业务字段审批人探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "申请人审批", configJson: "{\"approverBusinessFields\":[\"RequesterName\"]}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definitions.Publish(definition);
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var taskRepository = new FreeSqlWorkflowTaskRepository(fsql);
        var tasks = new WorkflowTaskService(taskRepository, instanceService, operations: operations, approverResolver: resolver);
        var binding = new WorkflowBindingService(definitions, instanceService, tasks, transactions: transactions);
        var positiveInstance = binding.StartOrGet(code, nameof(PmsProjectChange), change.Id, startedBy: "workflow-postgres-probe");
        var positiveTasks = taskRepository.List(positiveInstance.Id, status: WorkflowTaskStatus.Pending);
        if (positiveTasks.Count != 1 || !positiveTasks[0].Assignee.Equals("pgwf-requester", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PostgreSQL 业务字段审批人分派失败：未为项目变更申请人创建待办。");

        try
        {
            binding.StartOrGet(code, nameof(PmsProjectChange), emptyBusinessId, startedBy: "workflow-postgres-probe");
            throw new InvalidOperationException("空项目变更申请人不应允许启动流程。");
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("未解析到可用审批人", StringComparison.Ordinal))
        {
            // 预期：真实业务字段返回空值时，流程事务必须完整回滚。
        }

        if (instances.List(nameof(PmsProjectChange), emptyBusinessId).Count != 0 ||
            fsql.Select<WorkflowTaskRecord>().Where(x => x.BusinessId == emptyBusinessId).Count() != 0 ||
            fsql.Select<WorkflowOperationRecord>().Where(x => x.BusinessId == emptyBusinessId).Count() != 0)
            throw new InvalidOperationException("PostgreSQL 业务字段空审批人回滚失败：留下了实例、待办或操作历史。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == change.Id).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == change.Id).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == change.Id).ExecuteAffrows();
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == emptyBusinessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == emptyBusinessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == emptyBusinessId).ExecuteAffrows();
        fsql.Delete<PmsProjectChangeRecord>().Where(x => x.Id == emptyChange.Id).ExecuteAffrows();
        fsql.Delete<PmsProjectChangeRecord>().Where(x => x.Id == change.Id).ExecuteAffrows();
    }
}

static void RunExistingTaskSurvivesDynamicMembershipProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances);
        var definition = new WorkflowDefinition("PG_DYNAMIC_MEMBER_SNAPSHOT", "PostgreSQL 动态审批人待办快照探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "组织审批", configJson: "{\"approverOrgs\":[\"已变更组织\"]}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.dynamic-member", businessId, startedBy: "workflow-postgres-probe");
        instanceService.Advance(instance, approval.Id);
        var repository = new FreeSqlWorkflowTaskRepository(fsql);
        var existing = new WorkflowTask(instance, approval.Id, approval.Name, "workflow-postgres-former-member");
        repository.Add(existing);
        var service = new WorkflowTaskService(repository, instanceService, approverResolver: new NewApproverResolver());

        var created = service.EnsureCurrentApprovalTask(instance);
        var pending = repository.List(instance.Id, status: WorkflowTaskStatus.Pending);
        if (created.Count != 0 || pending.Count != 1 || pending[0].Id != existing.Id)
            throw new InvalidOperationException("PostgreSQL 动态审批人成员变更失败：既有待办未被保留或被重复创建。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunApprovalSnapshotRepairProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances);
        var definition = new WorkflowDefinition("PG_APPROVAL_SNAPSHOT_REPAIR", "PostgreSQL 审批人快照补偿探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "会签", configJson: "{\"approvers\":[\"workflow-postgres-admin\",\"workflow-postgres-finance\"]}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.approval-snapshot", businessId, startedBy: "workflow-postgres-probe");
        instanceService.Advance(instance, approval.Id);
        var repository = new FreeSqlWorkflowTaskRepository(fsql);
        new WorkflowTaskService(repository, instanceService).EnsureCurrentApprovalTask(instance);
        var finance = repository.List(instance.Id, status: WorkflowTaskStatus.Pending).Single(x => x.Assignee == "workflow-postgres-finance");
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.Id == finance.Id).ExecuteAffrows();

        var restarted = instances.List(businessId: businessId).Single();
        var repaired = new WorkflowTaskService(repository, instanceService, approverResolver: new NewApproverResolver()).EnsureCurrentApprovalTask(restarted);
        var pending = repository.List(restarted.Id, status: WorkflowTaskStatus.Pending);
        if (repaired.Count != 1
            || repaired[0].Assignee != "workflow-postgres-finance"
            || pending.Count != 2
            || pending.Any(x => x.Assignee == "workflow-postgres-new-member")
            || !restarted.ApprovalAssigneesJson.Contains("workflow-postgres-finance", StringComparison.Ordinal))
            throw new InvalidOperationException("PostgreSQL 审批人快照补偿失败：重启后未按原快照补齐缺失待办，或错误扩容了新成员。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunApprovalSnapshotTransferProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances);
        var definition = new WorkflowDefinition("PG_APPROVAL_SNAPSHOT_TRANSFER", "PostgreSQL 审批人快照转交探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "会签", configJson: "{\"approvers\":[\"workflow-postgres-admin\",\"workflow-postgres-finance\"]}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.approval-transfer", businessId, startedBy: "workflow-postgres-probe");
        instanceService.Advance(instance, approval.Id);
        var repository = new FreeSqlWorkflowTaskRepository(fsql);
        var service = new WorkflowTaskService(repository, instanceService);
        var original = service.EnsureCurrentApprovalTask(instance).Single(x => x.Assignee == "workflow-postgres-admin");
        service.Transfer(original, "workflow-postgres-admin", "workflow-postgres-director", "请负责人处理");
        var finance = repository.List(instance.Id, status: WorkflowTaskStatus.Pending).Single(x => x.Assignee == "workflow-postgres-finance");
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.Id == finance.Id).ExecuteAffrows();

        var restarted = instances.List(businessId: businessId).Single();
        var repaired = service.EnsureCurrentApprovalTask(restarted);
        var pending = repository.List(restarted.Id, status: WorkflowTaskStatus.Pending);
        if (repaired.Count != 1
            || repaired[0].Assignee != "workflow-postgres-finance"
            || pending.Count != 2
            || !pending.Any(x => x.Assignee == "workflow-postgres-finance")
            || !pending.Any(x => x.Assignee == "workflow-postgres-director")
            || pending.Any(x => x.Assignee == "workflow-postgres-admin"))
            throw new InvalidOperationException("PostgreSQL 审批人快照转交失败：补偿错误复活了原审批人。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunApprovalSnapshotCasRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var innerInstances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instances = new ThrowingApprovalSnapshotInstanceRepository(innerInstances);
        var instanceService = new WorkflowInstanceService(instances, transactions: transactions);
        var definition = new WorkflowDefinition("PG_APPROVAL_SNAPSHOT_CAS", "PostgreSQL 审批人快照 CAS 回滚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"workflow-postgres-probe\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.approval-snapshot-cas", businessId, startedBy: "workflow-postgres-probe");
        instanceService.Advance(instance, approval.Id);
        var repository = new FreeSqlWorkflowTaskRepository(fsql);
        var tasks = new WorkflowTaskService(repository, instanceService);

        try
        {
            tasks.EnsureCurrentApprovalTask(instance);
            throw new InvalidOperationException("故障注入未触发，审批人快照 CAS 不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟审批人快照持久化失败")
        {
            // 预期：快照和待办均不提交。
        }

        var persisted = innerInstances.List("workflow.postgres.approval-snapshot-cas", businessId).Single();
        if (persisted.ApprovalAssigneesJson != "{}"
            || repository.List(instance.Id).Count != 0
            || persisted.CurrentNodeId != approval.Id
            || persisted.Revision != 2)
            throw new InvalidOperationException("PostgreSQL 审批人快照 CAS 回滚失败：快照或待办留下了部分提交。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunConcurrentApprovalSnapshotCompensationProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    var businessId = Guid.CreateVersion7();
    using var left = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
    using var right = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
    var definition = new WorkflowDefinition($"PG_CONCURRENT_APPROVAL_SNAPSHOT_{Guid.CreateVersion7():N}", "并发审批人快照补偿");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "并发审批", configJson: "{\"approvers\":[\"workflow-snapshot-a\",\"workflow-snapshot-b\"]}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, approval.Id);
    definition.Connect(approval.Id, end.Id);
    definition.Publish();

    try
    {
        var seedInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(fsql));
        var seed = seedInstances.Start(definition, "workflow.concurrent-snapshot", businessId, startedBy: "workflow-postgres-probe");
        seedInstances.Advance(seed, approval.Id);

        var leftInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(left));
        var rightInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(right));
        var leftInstance = leftInstances.List(businessId: businessId).Single();
        var rightInstance = rightInstances.List(businessId: businessId).Single();
        var leftTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(left), leftInstances);
        var rightTasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(right), rightInstances);
        // 先顺序预热两个独立 FreeSql 上下文的查询/更新元数据，避免把 ORM 首次编译的共享缓存竞态混入应用层并发断言。
        _ = leftTasks.List(leftInstance.Id);
        _ = rightTasks.List(rightInstance.Id);
        left.Update<WorkflowInstanceRecord>().Set(x => x.Revision, 1).Where(x => false).ExecuteAffrows();
        right.Update<WorkflowInstanceRecord>().Set(x => x.Revision, 1).Where(x => false).ExecuteAffrows();
        var results = Task.WhenAll(
                Task.Run(() => EnsureSnapshot(leftTasks, leftInstance)),
                Task.Run(() => EnsureSnapshot(rightTasks, rightInstance)))
            .GetAwaiter()
            .GetResult();

        var persistedInstance = new FreeSqlWorkflowInstanceRepository(fsql).List(businessId: businessId).Single();
        var pending = new FreeSqlWorkflowTaskRepository(fsql).List(persistedInstance.Id, status: WorkflowTaskStatus.Pending);
        if (results.Any(x => x is not null)
            || persistedInstance.GetApprovalAssignees(approval.Id).Count != 2
            || pending.Count != 2
            || pending.Any(x => x.Round != 1))
            throw new InvalidOperationException($"{databaseType} 并发审批人快照补偿失败：错误={string.Join(" | ", results.Where(x => x is not null).Select(x => x!.ToString()))}，快照={persistedInstance.ApprovalAssigneesJson}，待办数={pending.Count}");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }

    static Exception? EnsureSnapshot(WorkflowTaskService tasks, WorkflowInstance instance)
    {
        try
        {
            tasks.EnsureCurrentApprovalTask(instance);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}

static void RunStaleApprovalTaskRepairAfterReturnProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    var businessId = Guid.CreateVersion7();
    using var staleConnection = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
    var definition = new WorkflowDefinition($"PG_STALE_TASK_REPAIR_{Guid.CreateVersion7():N}", "陈旧审批待办补偿");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approvers\":[\"workflow-stale-a\"]}");
    var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: $"{{\"approvers\":[\"workflow-stale-b\"],\"returnTargets\":[\"{first.Id}\"]}}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, first.Id);
    definition.Connect(first.Id, second.Id);
    definition.Connect(second.Id, end.Id);
    definition.Publish();

    try
    {
        var instances = new WorkflowInstanceService(
            new FreeSqlWorkflowInstanceRepository(fsql),
            transactions: new FreeSqlWorkflowTransactionBoundary(fsql));
        var instance = instances.Start(definition, "workflow.stale-task-repair", businessId, startedBy: "workflow-postgres-probe");
        instances.Advance(instance, first.Id);
        instances.EnsureApprovalAssigneeSnapshot(instance, first.Id, ["workflow-stale-a"]);
        instances.Advance(instance, second.Id);
        instances.EnsureApprovalAssigneeSnapshot(instance, second.Id, ["workflow-stale-b"]);

        var staleInstances = new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(staleConnection));
        var stale = staleInstances.List(businessId: businessId).Single();
        instances.ReturnTo(instance, second.Id, first.Id);

        var tasks = new WorkflowTaskService(
            new FreeSqlWorkflowTaskRepository(staleConnection),
            staleInstances,
            transactions: new FreeSqlWorkflowTransactionBoundary(staleConnection));
        var repaired = tasks.EnsureCurrentApprovalTask(stale);
        var pending = new FreeSqlWorkflowTaskRepository(fsql).List(instance.Id, status: WorkflowTaskStatus.Pending);
        if (repaired.Count != 1
            || repaired[0].NodeId != first.Id
            || pending.Count != 1
            || pending[0].NodeId != first.Id
            || pending.Any(x => x.NodeId == second.Id)
            || stale.CurrentNodeId != first.Id)
            throw new InvalidOperationException($"{databaseType} 陈旧审批待办补偿失败：补偿数={repaired.Count}，待办={string.Join(",", pending.Select(x => x.NodeId))}，当前节点={stale.CurrentNodeId}");

        try
        {
            tasks.CreateApprovalTask(stale, second.Id, second.Name, "workflow-stale-b");
            throw new InvalidOperationException($"{databaseType} 历史审批节点仍可直接创建待办。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "审批待办节点不属于流程实例当前活动审批节点，不能创建。")
        {
            // 预期：离开历史节点后，事务化独立创建入口也必须遵守活动节点门禁。
        }
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunStaleApprovalDecisionLockProbe(IFreeSql fsql, DataType databaseType, string connectionString)
{
    var businessId = Guid.CreateVersion7();
    using var staleConnection = new FreeSqlBuilder().UseConnectionString(databaseType, connectionString).Build();
    var definition = new WorkflowDefinition($"PG_STALE_DECISION_LOCK_{Guid.CreateVersion7():N}", "陈旧审批决策锁");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approvers\":[\"workflow-decision-a\"]}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, approval.Id);
    definition.Connect(approval.Id, end.Id);
    definition.Publish();

    try
    {
        var instances = new WorkflowInstanceService(
            new FreeSqlWorkflowInstanceRepository(fsql),
            transactions: new FreeSqlWorkflowTransactionBoundary(fsql));
        var instance = instances.Start(definition, "workflow.stale-decision", businessId, startedBy: "workflow-postgres-probe");
        instances.Advance(instance, approval.Id);
        var taskRepository = new FreeSqlWorkflowTaskRepository(fsql);
        taskRepository.Add(new WorkflowTask(instance, approval.Id, approval.Name, "workflow-decision-a"));

        var staleInnerRepository = new FreeSqlWorkflowInstanceRepository(staleConnection);
        var stale = new WorkflowInstanceService(staleInnerRepository).List(businessId: businessId).Single();
        instances.EnsureApprovalAssigneeSnapshot(instance, approval.Id, ["workflow-decision-a"]);

        var staleTaskRepository = new FreeSqlWorkflowTaskRepository(staleConnection);
        var staleTask = staleTaskRepository.List(stale.Id).Single();
        var staleInstances = new WorkflowInstanceService(new StaleReadWorkflowInstanceRepository(staleInnerRepository, stale));
        var staleTasks = new WorkflowTaskService(
            staleTaskRepository,
            staleInstances,
            transactions: new FreeSqlWorkflowTransactionBoundary(staleConnection));
        try
        {
            staleTasks.Approve(staleTask, "workflow-decision-a");
            throw new InvalidOperationException($"{databaseType} 陈旧审批决策未被实例行锁拒绝。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "流程实例状态已变化，请刷新后重试。")
        {
            // 预期：实例 Revision 已变化，但待办仍 Pending；旧进程不能继续执行审批动作。
        }

        try
        {
            staleTasks.CreateApprovalTask(stale, approval.Id, approval.Name, "workflow-decision-b");
            throw new InvalidOperationException($"{databaseType} 陈旧独立待办创建未被实例行锁拒绝。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "流程实例状态已变化，请刷新后重试。")
        {
            // 预期：独立 CreateApprovalTask 也不能绕过实例 Revision 校验。
        }

        var batchTasks = new WorkflowTaskService(
            staleTaskRepository,
            new WorkflowInstanceService(new FreeSqlWorkflowInstanceRepository(staleConnection)),
            transactions: new FreeSqlWorkflowTransactionBoundary(staleConnection));
        if (batchTasks.EnsureApprovalTasks(stale, definition).Count != 0)
            throw new InvalidOperationException($"{databaseType} 陈旧批量待办补偿不应重复创建当前审批待办。");

        var persisted = new FreeSqlWorkflowInstanceRepository(fsql).List(businessId: businessId).Single();
        var pending = taskRepository.List(instance.Id, status: WorkflowTaskStatus.Pending);
        if (persisted.Status != WorkflowInstanceStatus.Running
            || pending.Count != 1
            || pending[0].Status != WorkflowTaskStatus.Pending
            || pending[0].Revision != 1)
            throw new InvalidOperationException($"{databaseType} 陈旧审批决策锁回滚失败：实例状态={persisted.Status}，待办数={pending.Count}，待办状态={pending.SingleOrDefault()?.Status}，待办版本={pending.SingleOrDefault()?.Revision}");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunWithdrawRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var instance = instanceService.Start(CreateDefinition(), "workflow.postgres.withdraw", businessId, startedBy: "workflow-postgres-probe");
        var innerTasks = new FreeSqlWorkflowTaskRepository(fsql);
        var approvalNodeId = instance.GetOutgoingTransitions().Single().TargetNodeId;
        innerTasks.Add(new WorkflowTask(instance, approvalNodeId, "审批 A", "workflow-postgres-probe-a"));
        innerTasks.Add(new WorkflowTask(instance, approvalNodeId, "审批 B", "workflow-postgres-probe-b"));
        var tasks = new WorkflowTaskService(new ThrowingSecondDecisionTaskRepository(innerTasks), instanceService, operations: operations, transactions: transactions);

        try
        {
            tasks.Withdraw(instance.Id, "workflow-postgres-probe", "故障注入");
            throw new InvalidOperationException("故障注入未触发，流程撤回不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟第二个待办写入失败")
        {
            // 预期。
        }

        if (instances.List("workflow.postgres.withdraw", businessId).Single().Status != WorkflowInstanceStatus.Running ||
            innerTasks.List(instance.Id).Any(x => x.Status != WorkflowTaskStatus.Pending || x.Revision != 1) ||
            operations.List(instanceId: instance.Id).Any(x => x.Kind is WorkflowOperationKind.Withdrawn or WorkflowOperationKind.Cancelled))
            throw new InvalidOperationException("PostgreSQL 事务回滚失败：流程撤回留下了部分状态或操作历史。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunAnyApprovalModeProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_ANY_APPROVAL", "PostgreSQL 或签探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "或签", configJson: "{\"approvers\":[\"workflow-postgres-probe-a\",\"workflow-postgres-probe-b\"],\"approvalMode\":\"Any\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.any", businessId, startedBy: "workflow-postgres-probe");
        instanceService.Advance(instance, approval.Id);
        var tasks = new FreeSqlWorkflowTaskRepository(fsql);
        var service = new WorkflowTaskService(tasks, instanceService, operations: operations, transactions: transactions);
        var first = service.CreateApprovalTask(instance, approval.Id, approval.Name, "workflow-postgres-probe-a");
        var second = service.CreateApprovalTask(instance, approval.Id, approval.Name, "workflow-postgres-probe-b");

        service.Approve(first, "workflow-postgres-probe-a", "或签同意");

        var persistedTasks = tasks.List(instance.Id).OrderBy(x => x.Assignee).ToArray();
        if (persistedTasks.Length != 2 || persistedTasks.Single(x => x.Id == first.Id).Status != WorkflowTaskStatus.Approved ||
            persistedTasks.Single(x => x.Id == second.Id).Status != WorkflowTaskStatus.Cancelled ||
            instances.List("workflow.postgres.any", businessId).Single().Status != WorkflowInstanceStatus.Completed ||
            !operations.List(instanceId: instance.Id).Any(x => x.Kind == WorkflowOperationKind.Cancelled && x.TaskId == second.Id))
            throw new InvalidOperationException("PostgreSQL 或签失败：未原子取消同节点待办并完成流程。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunMajorityApprovalModeProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_MAJORITY_APPROVAL", "PostgreSQL 多数会签探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "多数会签", configJson: "{\"approvers\":[\"workflow-postgres-probe-a\",\"workflow-postgres-probe-b\",\"workflow-postgres-probe-c\"],\"approvalMode\":\"Majority\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.majority", businessId, startedBy: "workflow-postgres-probe");
        instanceService.Advance(instance, approval.Id);
        var repository = new FreeSqlWorkflowTaskRepository(fsql);
        var service = new WorkflowTaskService(repository, instanceService, operations: operations, transactions: transactions);
        var pending = service.EnsureCurrentApprovalTask(instance);

        service.Approve(pending.Single(x => x.Assignee == "workflow-postgres-probe-a"), "workflow-postgres-probe-a", "第一票");
        var waiting = instances.List("workflow.postgres.majority", businessId).Single();
        if (waiting.Status != WorkflowInstanceStatus.Running || repository.List(instance.Id, status: WorkflowTaskStatus.Pending).Count != 2)
            throw new InvalidOperationException("PostgreSQL 多数会签失败：首票不应提前完成流程。 ");

        service.Approve(repository.List(instance.Id, status: WorkflowTaskStatus.Pending).Single(x => x.Assignee == "workflow-postgres-probe-b"), "workflow-postgres-probe-b", "第二票");
        var persisted = repository.List(instance.Id);
        if (instances.List("workflow.postgres.majority", businessId).Single().Status != WorkflowInstanceStatus.Completed
            || persisted.Single(x => x.Assignee == "workflow-postgres-probe-c").Status != WorkflowTaskStatus.Cancelled
            || !operations.List(instanceId: instance.Id).Any(x => x.Kind == WorkflowOperationKind.Cancelled && x.TaskId == persisted.Single(task => task.Assignee == "workflow-postgres-probe-c").Id))
            throw new InvalidOperationException("PostgreSQL 多数会签失败：达到门槛后未原子取消剩余待办并完成流程。 ");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunParallelAnyApprovalProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_PARALLEL_ANY_APPROVAL", "PostgreSQL 并行或签探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var anyApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门或签", configJson: "{\"approvers\":[\"workflow-postgres-probe-a\",\"workflow-postgres-probe-b\"],\"approvalMode\":\"Any\"}");
        var otherApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "法务审批", configJson: "{\"approver\":\"workflow-postgres-probe-legal\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, anyApproval.Id);
        definition.Connect(split.Id, otherApproval.Id);
        definition.Connect(anyApproval.Id, join.Id);
        definition.Connect(otherApproval.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.parallel-any", businessId, startedBy: "workflow-postgres-probe");
        var repository = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);
        var tasks = new WorkflowTaskService(repository, instanceService, operations: operations, runtime: runtime, transactions: transactions);
        runtime.Continue(instance);
        var pending = tasks.EnsureCurrentApprovalTask(instance);

        tasks.Approve(pending.Single(x => x.NodeId == anyApproval.Id && x.Assignee == "workflow-postgres-probe-a"), "workflow-postgres-probe-a", "部门通过");
        var waiting = instances.List("workflow.postgres.parallel-any", businessId).Single();
        var waitingTasks = repository.List(instance.Id);
        if (waiting.Status != WorkflowInstanceStatus.Running
            || !waiting.ActiveNodeIds.SetEquals([otherApproval.Id])
            || !waitingTasks.Any(x => x.NodeId == anyApproval.Id && x.Assignee == "workflow-postgres-probe-b" && x.Status == WorkflowTaskStatus.Cancelled)
            || !waitingTasks.Any(x => x.NodeId == otherApproval.Id && x.Assignee == "workflow-postgres-probe-legal" && x.Status == WorkflowTaskStatus.Pending))
            throw new InvalidOperationException("PostgreSQL 并行或签失败：或签取消越界或另一分支未保持待审批。");

        tasks.Approve(waitingTasks.Single(x => x.NodeId == otherApproval.Id && x.Status == WorkflowTaskStatus.Pending), "workflow-postgres-probe-legal", "法务通过");
        if (instances.List("workflow.postgres.parallel-any", businessId).Single().Status != WorkflowInstanceStatus.Completed)
            throw new InvalidOperationException("PostgreSQL 并行或签失败：另一分支完成后未通过 Join 结束流程。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunParallelMajorityApprovalProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_PARALLEL_MAJORITY_APPROVAL", "PostgreSQL 并行多数会签探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var majority = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门多数会签", configJson: "{\"approvers\":[\"workflow-postgres-probe-a\",\"workflow-postgres-probe-b\",\"workflow-postgres-probe-c\"],\"approvalMode\":\"Majority\"}");
        var legal = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "法务审批", configJson: "{\"approver\":\"workflow-postgres-probe-legal\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, majority.Id);
        definition.Connect(split.Id, legal.Id);
        definition.Connect(majority.Id, join.Id);
        definition.Connect(legal.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.parallel-majority", businessId, startedBy: "workflow-postgres-probe");
        var repository = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);
        var tasks = new WorkflowTaskService(repository, instanceService, operations: operations, runtime: runtime, transactions: transactions);
        runtime.Continue(instance);
        var pending = tasks.EnsureCurrentApprovalTask(instance);
        tasks.Approve(pending.Single(x => x.NodeId == majority.Id && x.Assignee == "workflow-postgres-probe-a"), "workflow-postgres-probe-a", "第一票");
        if (!instances.List("workflow.postgres.parallel-majority", businessId).Single().ActiveNodeIds.Contains(majority.Id))
            throw new InvalidOperationException("PostgreSQL 并行多数会签失败：首票不应离开多数会签分支。");

        tasks.Approve(repository.List(instance.Id, status: WorkflowTaskStatus.Pending).Single(x => x.NodeId == majority.Id && x.Assignee == "workflow-postgres-probe-b"), "workflow-postgres-probe-b", "第二票");
        var waiting = instances.List("workflow.postgres.parallel-majority", businessId).Single();
        var waitingTasks = repository.List(instance.Id);
        if (waiting.Status != WorkflowInstanceStatus.Running
            || !waiting.ActiveNodeIds.SetEquals([legal.Id])
            || !waitingTasks.Any(x => x.NodeId == majority.Id && x.Assignee == "workflow-postgres-probe-c" && x.Status == WorkflowTaskStatus.Cancelled)
            || !waitingTasks.Any(x => x.NodeId == legal.Id && x.Status == WorkflowTaskStatus.Pending))
            throw new InvalidOperationException("PostgreSQL 并行多数会签失败：门槛取消越界或法务分支未保持待处理。");

        tasks.Approve(waitingTasks.Single(x => x.NodeId == legal.Id && x.Status == WorkflowTaskStatus.Pending), "workflow-postgres-probe-legal", "法务通过");
        if (instances.List("workflow.postgres.parallel-majority", businessId).Single().Status != WorkflowInstanceStatus.Completed)
            throw new InvalidOperationException("PostgreSQL 并行多数会签失败：法务分支完成后未通过 Join 结束流程。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunParallelAnyApprovalRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_PARALLEL_ANY_ROLLBACK", "PostgreSQL 并行或签回滚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var anyApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门或签", configJson: "{\"approvers\":[\"workflow-postgres-probe-a\",\"workflow-postgres-probe-b\"],\"approvalMode\":\"Any\"}");
        var otherApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "法务审批", configJson: "{\"approver\":\"workflow-postgres-probe-legal\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, anyApproval.Id);
        definition.Connect(split.Id, otherApproval.Id);
        definition.Connect(anyApproval.Id, join.Id);
        definition.Connect(otherApproval.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.parallel-any-rollback", businessId, startedBy: "workflow-postgres-probe");
        var innerTasks = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);
        var setup = new WorkflowTaskService(innerTasks, instanceService, operations: operations, runtime: runtime, transactions: transactions);
        runtime.Continue(instance);
        var pending = setup.EnsureCurrentApprovalTask(instance);
        var service = new WorkflowTaskService(new ThrowingSecondTaskUpdateRepository(innerTasks), instanceService, operations: operations, runtime: runtime, transactions: transactions);

        try
        {
            service.Approve(pending.Single(x => x.NodeId == anyApproval.Id && x.Assignee == "workflow-postgres-probe-a"), "workflow-postgres-probe-a", "故障注入");
            throw new InvalidOperationException("故障注入未触发，并行或签不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟或签兄弟待办取消失败")
        {
            // 预期：审批结果和首个兄弟取消同一事务回滚。
        }

        var persisted = instances.List("workflow.postgres.parallel-any-rollback", businessId).Single();
        var persistedTasks = innerTasks.List(instance.Id);
        if (!persisted.ActiveNodeIds.SetEquals([anyApproval.Id, otherApproval.Id])
            || persistedTasks.Count(x => x.Status == WorkflowTaskStatus.Pending) != 3
            || persistedTasks.Any(x => x.Status != WorkflowTaskStatus.Pending || x.Revision != 1)
            || operations.List(instanceId: instance.Id).Any(x => (x.Kind is WorkflowOperationKind.Approved or WorkflowOperationKind.Cancelled)
                || (x.Kind == WorkflowOperationKind.NodeCompleted && x.NodeId == anyApproval.Id)))
            throw new InvalidOperationException("PostgreSQL 并行或签回滚失败：审批或取消留下了部分状态。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunParallelMajorityApprovalRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_PARALLEL_MAJORITY_ROLLBACK", "PostgreSQL 并行多数会签回滚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var majority = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门多数会签", configJson: "{\"approvers\":[\"workflow-postgres-probe-a\",\"workflow-postgres-probe-b\",\"workflow-postgres-probe-c\"],\"approvalMode\":\"Majority\"}");
        var legal = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "法务审批", configJson: "{\"approver\":\"workflow-postgres-probe-legal\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id); definition.Connect(split.Id, majority.Id); definition.Connect(split.Id, legal.Id);
        definition.Connect(majority.Id, join.Id); definition.Connect(legal.Id, join.Id); definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.parallel-majority-rollback", businessId, startedBy: "workflow-postgres-probe");
        var inner = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);
        var setup = new WorkflowTaskService(inner, instanceService, operations: operations, runtime: runtime, transactions: transactions);
        runtime.Continue(instance);
        var pending = setup.EnsureCurrentApprovalTask(instance);
        setup.Approve(pending.Single(x => x.NodeId == majority.Id && x.Assignee == "workflow-postgres-probe-a"), "workflow-postgres-probe-a", "第一票");
        var service = new WorkflowTaskService(new ThrowingSecondTaskUpdateRepository(inner), instanceService, operations: operations, runtime: runtime, transactions: transactions);
        try
        {
            service.Approve(inner.List(instance.Id, status: WorkflowTaskStatus.Pending).Single(x => x.NodeId == majority.Id && x.Assignee == "workflow-postgres-probe-b"), "workflow-postgres-probe-b", "故障注入");
            throw new InvalidOperationException("故障注入未触发，并行多数会签不应成功。");
        }
        catch (InvalidOperationException ex) when (ex.Message == "模拟或签兄弟待办取消失败") { }

        var persisted = inner.List(instance.Id);
        var restored = instances.List("workflow.postgres.parallel-majority-rollback", businessId).Single();
        if (!restored.ActiveNodeIds.SetEquals([majority.Id, legal.Id])
            || persisted.Single(x => x.Assignee == "workflow-postgres-probe-a").Status != WorkflowTaskStatus.Approved
            || persisted.Where(x => x.Assignee is "workflow-postgres-probe-b" or "workflow-postgres-probe-c" or "workflow-postgres-probe-legal").Any(x => x.Status != WorkflowTaskStatus.Pending)
            || operations.List(instanceId: instance.Id).Any(x => x.NodeId == majority.Id && (x.Kind == WorkflowOperationKind.Cancelled || (x.Kind == WorkflowOperationKind.Approved && x.TaskId == persisted.Single(task => task.Assignee == "workflow-postgres-probe-b").Id))))
            throw new InvalidOperationException("PostgreSQL 并行多数会签回滚失败：第二票、取消、Join 到达或历史出现部分提交。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunReturnRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_RETURN_ROLLBACK", "PostgreSQL 回退回滚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var firstApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"workflow-postgres-probe-a\"}");
        var secondApproval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "复审", configJson: $"{{\"approver\":\"workflow-postgres-probe-b\",\"returnTargets\":[\"{firstApproval.Id}\"]}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, firstApproval.Id);
        definition.Connect(firstApproval.Id, secondApproval.Id);
        definition.Connect(secondApproval.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.return", businessId, startedBy: "workflow-postgres-probe");
        instanceService.Advance(instance, firstApproval.Id);
        var innerTasks = new FreeSqlWorkflowTaskRepository(fsql);
        var setup = new WorkflowTaskService(innerTasks, instanceService, operations: operations, transactions: transactions);
        var initial = setup.CreateApprovalTask(instance, firstApproval.Id, firstApproval.Name, "workflow-postgres-probe-a");
        setup.Approve(initial, "workflow-postgres-probe-a", "初审通过");
        var review = innerTasks.List(instance.Id, status: WorkflowTaskStatus.Pending).Single(x => x.NodeId == secondApproval.Id);
        var returning = new WorkflowTaskService(new ThrowingTaskAddRepository(innerTasks), instanceService, operations: operations, transactions: transactions);

        try
        {
            returning.ReturnToNode(review, "workflow-postgres-probe-b", firstApproval.Id, "模拟回退待办写入失败");
            throw new InvalidOperationException("故障注入未触发，流程回退不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟初始待办写入失败")
        {
            // 预期：退回当前待办、实例节点与操作历史必须同一事务整体回滚。
        }

        var persisted = innerTasks.List(instance.Id).ToArray();
        if (instances.List("workflow.postgres.return", businessId).Single().CurrentNodeId != secondApproval.Id ||
            persisted.Single(x => x.Id == initial.Id).Status != WorkflowTaskStatus.Approved ||
            persisted.Single(x => x.Id == review.Id).Status != WorkflowTaskStatus.Pending ||
            persisted.Any(x => x.NodeId == firstApproval.Id && x.Round > 1) ||
            operations.List(instanceId: instance.Id).Any(x => x.Kind == WorkflowOperationKind.Returned))
            throw new InvalidOperationException("PostgreSQL 事务回滚失败：流程回退留下了部分状态、待办或操作历史。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunParallelJoinProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_PARALLEL_JOIN", "PostgreSQL 并行汇聚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"workflow-postgres-probe-a\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"workflow-postgres-probe-b\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, first.Id);
        definition.Connect(split.Id, second.Id);
        definition.Connect(first.Id, join.Id);
        definition.Connect(second.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.parallel", businessId, startedBy: "workflow-postgres-probe");
        var tasks = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);
        var service = new WorkflowTaskService(tasks, instanceService, operations: operations, runtime: runtime, transactions: transactions);

        runtime.Continue(instance);
        var pending = service.EnsureCurrentApprovalTask(instance);
        if (pending.Count != 2 || instance.ActiveNodeIds.Count != 2) throw new InvalidOperationException("PostgreSQL 并行拆分失败：未同时激活两条审批分支。");
        service.Approve(pending.Single(x => x.NodeId == first.Id), "workflow-postgres-probe-a", "部门通过");
        if (instances.List("workflow.postgres.parallel", businessId).Single().Status != WorkflowInstanceStatus.Running || tasks.List(instance.Id, status: WorkflowTaskStatus.Pending).Count != 1)
            throw new InvalidOperationException("PostgreSQL 并行汇聚失败：首条分支提前结束或未保留另一待办。");
        service.Approve(pending.Single(x => x.NodeId == second.Id), "workflow-postgres-probe-b", "财务通过");
        if (instances.List("workflow.postgres.parallel", businessId).Single().Status != WorkflowInstanceStatus.Completed ||
            !operations.List(instanceId: instance.Id).Any(x => x.Kind == WorkflowOperationKind.NodeEntered && x.NodeId == join.Id))
            throw new InvalidOperationException("PostgreSQL 并行汇聚失败：最后分支未激活汇聚并结束流程。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunParallelSplitRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var innerInstances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instances = new ThrowingParallelSplitInstanceRepository(innerInstances);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_PARALLEL_SPLIT_ROLLBACK", "PostgreSQL 并行拆分回滚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"workflow-postgres-probe-a\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"workflow-postgres-probe-b\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, first.Id);
        definition.Connect(split.Id, second.Id);
        definition.Connect(first.Id, join.Id);
        definition.Connect(second.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.parallel.rollback", businessId, startedBy: "workflow-postgres-probe");
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);

        try
        {
            runtime.Continue(instance);
            throw new InvalidOperationException("故障注入未触发，并行拆分不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟并行拆分持久化失败")
        {
            // 预期：开始节点已独立推进到 Split，但 Split 的分支激活与操作历史必须整体回滚。
        }

        var persisted = innerInstances.List("workflow.postgres.parallel.rollback", businessId).Single();
        if (persisted.CurrentNodeId != split.Id || persisted.ActiveNodeIds.Count != 1 || !persisted.ActiveNodeIds.Contains(split.Id) ||
            operations.List(instanceId: instance.Id).Any(x => x.Kind == WorkflowOperationKind.NodeEntered && (x.NodeId == first.Id || x.NodeId == second.Id)))
            throw new InvalidOperationException("PostgreSQL 事务回滚失败：并行拆分留下了活动分支或分支进入历史。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunParallelReturnProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_PARALLEL_RETURN", "PostgreSQL 并行分支回退探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var initial = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"workflow-postgres-probe-admin\"}");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var returning = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: $"{{\"approver\":\"workflow-postgres-probe-a\",\"returnTargets\":[\"{initial.Id}\"]}}");
        var sibling = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "法务审批", configJson: "{\"approver\":\"workflow-postgres-probe-b\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, initial.Id);
        definition.Connect(initial.Id, split.Id);
        definition.Connect(split.Id, sibling.Id);
        definition.Connect(split.Id, returning.Id);
        definition.Connect(returning.Id, join.Id);
        definition.Connect(sibling.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.parallel.return", businessId, startedBy: "workflow-postgres-probe");
        var tasks = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);
        var service = new WorkflowTaskService(tasks, instanceService, operations: operations, runtime: runtime, transactions: transactions);

        runtime.Continue(instance);
        var first = service.EnsureCurrentApprovalTask(instance).Single();
        service.Approve(first, "workflow-postgres-probe-admin", "初审通过");
        var branches = tasks.List(instance.Id, status: WorkflowTaskStatus.Pending).ToArray();
        var returnTask = branches.Single(x => x.NodeId == returning.Id);
        var siblingTask = branches.Single(x => x.NodeId == sibling.Id);
        service.ReturnToNode(returnTask, "workflow-postgres-probe-a", initial.Id, "请补充资料");

        var persistedTasks = tasks.List(instance.Id).ToArray();
        var persisted = instances.List("workflow.postgres.parallel.return", businessId).Single();
        if (persisted.CurrentNodeId != initial.Id ||
            persistedTasks.Single(x => x.Id == returnTask.Id).Status != WorkflowTaskStatus.Returned ||
            persistedTasks.Single(x => x.Id == siblingTask.Id).Status != WorkflowTaskStatus.Cancelled ||
            !persistedTasks.Any(x => x.NodeId == initial.Id && x.Status == WorkflowTaskStatus.Pending && x.Round == 2))
            throw new InvalidOperationException("PostgreSQL 并行分支回退失败：未取消其他分支或未生成初审新轮次。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunParallelJoinArrivalRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var innerInstances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instances = new ThrowingParallelJoinArrivalInstanceRepository(innerInstances);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_PARALLEL_JOIN_ROLLBACK", "PostgreSQL 并行汇聚回滚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"workflow-postgres-probe-a\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"workflow-postgres-probe-b\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, first.Id);
        definition.Connect(split.Id, second.Id);
        definition.Connect(first.Id, join.Id);
        definition.Connect(second.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.parallel.join.rollback", businessId, startedBy: "workflow-postgres-probe");
        var tasks = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);
        var service = new WorkflowTaskService(tasks, instanceService, operations: operations, runtime: runtime, transactions: transactions);
        runtime.Continue(instance);
        var pending = service.EnsureCurrentApprovalTask(instance);

        try
        {
            service.Approve(pending.Single(x => x.NodeId == first.Id), "workflow-postgres-probe-a", "故障注入");
            throw new InvalidOperationException("故障注入未触发，并行汇聚到达不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟并行汇聚到达持久化失败")
        {
            // 预期：审批决策、实例活动分支、Join 到达状态和操作历史整体回滚。
        }

        var persisted = innerInstances.List("workflow.postgres.parallel.join.rollback", businessId).Single();
        if (persisted.ActiveNodeIds.Count != 2 || !persisted.ActiveNodeIds.Contains(first.Id) || !persisted.ActiveNodeIds.Contains(second.Id) ||
            tasks.List(instance.Id).Any(x => x.Status != WorkflowTaskStatus.Pending || x.Revision != 1) ||
            operations.List(instanceId: instance.Id).Any(x => x.Kind == WorkflowOperationKind.Approved || (x.Kind == WorkflowOperationKind.NodeCompleted && x.NodeId == first.Id)))
            throw new InvalidOperationException("PostgreSQL 事务回滚失败：并行汇聚首分支到达留下了部分审批或实例状态。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunParallelConditionProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_PARALLEL_CONDITION", "PostgreSQL 并行条件探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"workflow-postgres-probe-a\"}");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "金额条件", configJson: "{\"branches\":[{\"key\":\"high\",\"expression\":\"amount > 100\"},{\"key\":\"normal\",\"expression\":\"amount <= 100\"}]}" );
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(split.Id, condition.Id);
        definition.Connect(approval.Id, join.Id);
        definition.Connect(condition.Id, join.Id, "high");
        definition.Connect(condition.Id, join.Id, "normal");
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.parallel.condition", businessId, startedBy: "workflow-postgres-probe");
        var tasks = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);
        var service = new WorkflowTaskService(tasks, instanceService, operations: operations, runtime: runtime, transactions: transactions);

        if (runtime.Continue(instance).State != WorkflowRuntimeState.WaitingForApproval)
            throw new InvalidOperationException("PostgreSQL 并行条件失败：无字段时未优先返回活动审批。");
        runtime.ContinueAfterCondition(instance, condition.Id, new Dictionary<string, object?> { ["amount"] = 10m });
        if (instances.List("workflow.postgres.parallel.condition", businessId).Single().ActiveNodeIds.Count != 1 || tasks.List(instance.Id, status: WorkflowTaskStatus.Pending).Count != 0)
            throw new InvalidOperationException("PostgreSQL 并行条件失败：条件到达 Join 后未只保留审批分支。");
        var task = service.EnsureCurrentApprovalTask(instance).Single();
        service.Approve(task, "workflow-postgres-probe-a", "通过");
        if (instances.List("workflow.postgres.parallel.condition", businessId).Single().Status != WorkflowInstanceStatus.Completed)
            throw new InvalidOperationException("PostgreSQL 并行条件失败：审批分支完成后未汇聚并结束。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunParallelReturnRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_PARALLEL_RETURN_ROLLBACK", "PostgreSQL 并行回退回滚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var initial = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "初审", configJson: "{\"approver\":\"workflow-postgres-probe-admin\"}");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var sibling = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "法务审批", configJson: "{\"approver\":\"workflow-postgres-probe-b\"}");
        var returning = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: $"{{\"approver\":\"workflow-postgres-probe-a\",\"returnTargets\":[\"{initial.Id}\"]}}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, initial.Id);
        definition.Connect(initial.Id, split.Id);
        definition.Connect(split.Id, sibling.Id);
        definition.Connect(split.Id, returning.Id);
        definition.Connect(sibling.Id, join.Id);
        definition.Connect(returning.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.parallel.return.rollback", businessId, startedBy: "workflow-postgres-probe");
        var innerTasks = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);
        var setup = new WorkflowTaskService(innerTasks, instanceService, operations: operations, runtime: runtime, transactions: transactions);
        runtime.Continue(instance);
        setup.Approve(setup.EnsureCurrentApprovalTask(instance).Single(), "workflow-postgres-probe-admin", "初审通过");
        var returnTask = innerTasks.List(instance.Id, status: WorkflowTaskStatus.Pending).Single(x => x.NodeId == returning.Id);
        var service = new WorkflowTaskService(new ThrowingTaskAddRepository(innerTasks), instanceService, operations: operations, runtime: runtime, transactions: transactions);

        try
        {
            service.ReturnToNode(returnTask, "workflow-postgres-probe-a", initial.Id, "故障注入");
            throw new InvalidOperationException("故障注入未触发，并行回退不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟初始待办写入失败") { }

        var persisted = instances.List("workflow.postgres.parallel.return.rollback", businessId).Single();
        if (persisted.ActiveNodeIds.Count != 2 || !persisted.ActiveNodeIds.Contains(sibling.Id) || !persisted.ActiveNodeIds.Contains(returning.Id) ||
            innerTasks.List(instance.Id).Where(x => x.NodeId != initial.Id).Any(x => x.Status != WorkflowTaskStatus.Pending) || operations.List(instanceId: instance.Id).Any(x => x.Kind == WorkflowOperationKind.Returned))
            throw new InvalidOperationException("PostgreSQL 事务回滚失败：并行回退未恢复完整活动分支和待办状态。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunNotificationFailureDoesNotBlockProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    var instanceId = Guid.Empty;
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_NOTIFICATION_FAILURE", "PostgreSQL 通知失败隔离探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "通知", configJson: "{\"recipients\":\"workflow-postgres-probe\",\"content\":\"请处理\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, notification.Id);
        definition.Connect(notification.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.notification-failure", businessId, startedBy: "workflow-postgres-probe");
        instanceId = instance.Id;
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new ThrowingNotificationRepository(new FreeSqlNotificationRepository(fsql)), new FreeSqlNotificationFailureRecorder(fsql), transactions), operations, transactions);

        var result = runtime.Continue(instance);

        var failure = fsql.Select<NotificationFailureRecord>().Where(x => x.DedupeKey.Contains(instance.Id.ToString())).ToOne();
        var payload = failure?.PayloadJson is null
            ? null
            : JsonSerializer.Deserialize<NotificationDeliveryPayload>(failure.PayloadJson, JsonSerializationDefaults.CreateWeb());
        if (result.State != WorkflowRuntimeState.Completed || instances.List("workflow.postgres.notification-failure", businessId).Single().Status != WorkflowInstanceStatus.Completed ||
            !operations.List(instanceId: instance.Id).Any(x => x.Kind == WorkflowOperationKind.NodeExecuted && x.NodeId == notification.Id) ||
            failure is null || payload is null || payload.Recipient != "workflow-postgres-probe" || payload.Content != "请处理" || payload.Kind != WorkNotificationKind.System ||
            !failure.PayloadJson!.Contains("\"kind\":\"System\"", StringComparison.Ordinal) || !failure.PayloadJson.Contains("请处理", StringComparison.Ordinal))
            throw new InvalidOperationException("PostgreSQL 通知失败隔离失败：通知写入异常阻断了流程节点推进。");
        var retryService = new NotificationFailureRetryService(new FreeSqlNotificationRepository(fsql), new FreeSqlNotificationFailureRepository(fsql), transactions: transactions);
        if (!retryService.Retry(failure.Id) ||
            fsql.Select<NotificationRecord>().Where(x => x.Recipient == "workflow-postgres-probe" && x.DedupeKey == failure.DedupeKey).Count() != 1 ||
            fsql.Select<NotificationFailureRecord>().Where(x => x.Id == failure.Id && x.Status == NotificationFailureStatus.Resolved && x.RetryCount == 1 && x.ResolvedAt != null).Count() != 1)
            throw new InvalidOperationException("PostgreSQL 通知失败重试失败：未恢复通知或未关闭失败记录。");
    }
    finally
    {
        if (instanceId != Guid.Empty)
        {
            fsql.Delete<NotificationRecord>().Where(x => x.DedupeKey.Contains(instanceId.ToString())).ExecuteAffrows();
            fsql.Delete<NotificationFailureRecord>().Where(x => x.DedupeKey.Contains(instanceId.ToString())).ExecuteAffrows();
        }
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunNotificationFailureRollbackProbe(IFreeSql fsql)
{
    var dedupeKey = $"workflow-postgres-notification-rollback:{Guid.CreateVersion7()}";
    var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
    var notifications = new NotificationService(
        new ThrowingNotificationRepository(new FreeSqlNotificationRepository(fsql)),
        new FreeSqlNotificationFailureRecorder(fsql),
        transactions);
    try
    {
        try
        {
            transactions.Execute(() =>
            {
                notifications.Publish("workflow-postgres-probe", WorkNotificationKind.System, "通知", "回滚通知", null, dedupeKey);
                throw new InvalidOperationException("模拟主事务回滚");
            });
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟主事务回滚")
        {
            // 失败记录必须等待主事务提交；主事务回滚后不能留下孤儿记录。
        }

        if (fsql.Select<NotificationFailureRecord>().Where(x => x.DedupeKey == dedupeKey).Any())
            throw new InvalidOperationException("PostgreSQL 通知失败回滚失败：主事务回滚后仍存在失败记录。");
    }
    finally
    {
        fsql.Delete<NotificationFailureRecord>().Where(x => x.DedupeKey == dedupeKey).ExecuteAffrows();
        fsql.Delete<NotificationRecord>().Where(x => x.DedupeKey == dedupeKey).ExecuteAffrows();
    }
}

static void RunNotificationFailureRetryClaimProbe(IFreeSql fsql)
{
    var failureId = Guid.CreateVersion7();
    var attemptedAt = new DateTime(2026, 7, 18, 12, 0, 0);
    var payload = new NotificationDeliveryPayload("workflow-postgres-probe", WorkNotificationKind.System, "补投", "并发补投", null, $"workflow-postgres-notification-claim:{failureId}", attemptedAt);
    try
    {
        fsql.Insert(new NotificationFailureRecord
        {
            Id = failureId,
            Operation = "publish",
            Recipient = payload.Recipient,
            DedupeKey = payload.DedupeKey,
            PayloadJson = JsonSerializer.Serialize(payload, JsonSerializationDefaults.CreateWeb()),
            Error = "初始通知失败",
            OccurredAt = attemptedAt,
            Status = NotificationFailureStatus.Pending,
            RetryCount = 0
        }).ExecuteAffrows();

        var repository = new FreeSqlNotificationFailureRepository(fsql);
        var results = Task.WhenAll(
            Task.Run(() => repository.TryClaim(failureId, attemptedAt, TimeSpan.FromMinutes(5))),
            Task.Run(() => repository.TryClaim(failureId, attemptedAt, TimeSpan.FromMinutes(5))))
            .GetAwaiter()
            .GetResult();
        var record = fsql.Select<NotificationFailureRecord>().Where(x => x.Id == failureId).ToOne();
        if (results.Count(x => x) != 1 || record is null || record.RetryCount != 1 || record.LastRetryAt != attemptedAt)
            throw new InvalidOperationException($"PostgreSQL 通知失败补投 CAS 失败：成功数={results.Count(x => x)}，RetryCount={record?.RetryCount}，LastRetryAt={record?.LastRetryAt:O}，Expected={attemptedAt:O}。");

        repository.MarkResolved(failureId, attemptedAt.AddSeconds(1));
    }
    finally
    {
        fsql.Delete<NotificationFailureRecord>().Where(x => x.Id == failureId).ExecuteAffrows();
    }
}

static void RunNotificationRetryTransactionRollbackProbe(IFreeSql fsql)
{
    var failureId = Guid.CreateVersion7();
    var attemptedAt = new DateTime(2026, 7, 18, 12, 30, 0);
    var dedupeKey = $"workflow-postgres-notification-retry-rollback:{failureId}";
    var payload = new NotificationDeliveryPayload("workflow-postgres-probe", WorkNotificationKind.System, "补投回滚", "补投回滚内容", null, dedupeKey, attemptedAt);
    try
    {
        fsql.Insert(new NotificationFailureRecord
        {
            Id = failureId,
            Operation = "publish",
            Recipient = payload.Recipient,
            DedupeKey = dedupeKey,
            PayloadJson = JsonSerializer.Serialize(payload, JsonSerializationDefaults.CreateWeb()),
            Error = "初始通知失败",
            OccurredAt = attemptedAt,
            Status = NotificationFailureStatus.Pending,
            RetryCount = 0
        }).ExecuteAffrows();

        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var failures = new ThrowingFailureResolveRepository(new FreeSqlNotificationFailureRepository(fsql));
        var service = new NotificationFailureRetryService(new FreeSqlNotificationRepository(fsql), failures, transactions: transactions);

        if (service.Retry(failureId, attemptedAt.AddMinutes(6)))
            throw new InvalidOperationException("PostgreSQL 通知补投事务回滚探针未触发故障注入。");

        var record = fsql.Select<NotificationFailureRecord>().Where(x => x.Id == failureId).ToOne();
        if (fsql.Select<NotificationRecord>().Where(x => x.DedupeKey == dedupeKey).Any() || record is null || record.Status != NotificationFailureStatus.Pending || record.RetryCount != 1)
            throw new InvalidOperationException("PostgreSQL 通知补投事务回滚失败：通知写入或失败记录状态出现部分提交。");
    }
    finally
    {
        fsql.Delete<NotificationRecord>().Where(x => x.DedupeKey == dedupeKey).ExecuteAffrows();
        fsql.Delete<NotificationFailureRecord>().Where(x => x.Id == failureId).ExecuteAffrows();
    }
}

static void RunNotificationFailureExistingNotificationProbe(IFreeSql fsql)
{
    var failureId = Guid.CreateVersion7();
    var attemptedAt = new DateTime(2026, 7, 18, 12, 45, 0);
    var payload = new NotificationDeliveryPayload("workflow-postgres-probe", WorkNotificationKind.System, "已存在通知", "不应重复投递", null, $"workflow-postgres-notification-existing:{failureId}", attemptedAt);
    try
    {
        fsql.Insert(new NotificationFailureRecord
        {
            Id = failureId,
            Operation = "publish",
            Recipient = payload.Recipient,
            DedupeKey = payload.DedupeKey,
            PayloadJson = JsonSerializer.Serialize(payload, JsonSerializationDefaults.CreateWeb()),
            Error = "初始通知失败",
            OccurredAt = attemptedAt,
            Status = NotificationFailureStatus.Pending,
            RetryCount = 0
        }).ExecuteAffrows();
        new FreeSqlNotificationRepository(fsql).Add(new WorkNotification(payload.Recipient, payload.Kind, payload.Title, payload.Content, payload.Href, payload.DedupeKey, payload.CreatedAt));

        var service = new NotificationFailureRetryService(
            new FreeSqlNotificationRepository(fsql),
            new FreeSqlNotificationFailureRepository(fsql),
            transactions: new FreeSqlWorkflowTransactionBoundary(fsql));
        if (!service.Retry(failureId, attemptedAt.AddMinutes(6)))
            throw new InvalidOperationException("PostgreSQL 通知已存在补投探针未成功收敛失败记录。");

        var record = fsql.Select<NotificationFailureRecord>().Where(x => x.Id == failureId).ToOne();
        var notificationCount = fsql.Select<NotificationRecord>().Where(x => x.Recipient == payload.Recipient && x.DedupeKey == payload.DedupeKey).Count();
        if (record is null || record.Status != NotificationFailureStatus.Resolved || record.RetryCount != 1 || notificationCount != 1)
            throw new InvalidOperationException("PostgreSQL 通知已存在补投失败：通知重复写入或失败记录未标记 Resolved。");
    }
    finally
    {
        fsql.Delete<NotificationRecord>().Where(x => x.DedupeKey == payload.DedupeKey).ExecuteAffrows();
        fsql.Delete<NotificationFailureRecord>().Where(x => x.Id == failureId).ExecuteAffrows();
    }
}

static void RunConditionNestedJoinProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    try
    {
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, transactions: transactions);
        var definition = new WorkflowDefinition("PG_CONDITION_NESTED_JOIN", "PostgreSQL 条件嵌套汇聚探针");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "外层拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"workflow-postgres-probe\"}");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "金额条件", configJson: "{\"branches\":[{\"key\":\"high\",\"expression\":\"amount > 100\"},{\"key\":\"normal\",\"expression\":\"amount <= 100\"}]}");
        var innerJoin = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "条件汇聚");
        var outerJoin = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "外层汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(split.Id, condition.Id);
        definition.Connect(condition.Id, innerJoin.Id, "high");
        definition.Connect(condition.Id, innerJoin.Id, "normal");
        definition.Connect(innerJoin.Id, outerJoin.Id);
        definition.Connect(approval.Id, outerJoin.Id);
        definition.Connect(outerJoin.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, "workflow.postgres.condition-nested-join", businessId, startedBy: "workflow-postgres-probe");
        var tasks = new FreeSqlWorkflowTaskRepository(fsql);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([]), new NotificationService(new FreeSqlNotificationRepository(fsql)), transactions: transactions);
        var service = new WorkflowTaskService(tasks, instanceService, runtime: runtime, transactions: transactions);

        runtime.Continue(instance);
        runtime.Continue(instance, new Dictionary<string, object?> { ["amount"] = 200m });
        if (instance.ActiveNodeIds.Count != 1 || !instance.ActiveNodeIds.Contains(approval.Id))
            throw new InvalidOperationException("PostgreSQL 条件嵌套汇聚失败：条件分支未作为内层 Join 来源收口。");
        service.Approve(service.EnsureCurrentApprovalTask(instance).Single(), "workflow-postgres-probe", "审批通过");
        if (instances.List("workflow.postgres.condition-nested-join", businessId).Single().Status != WorkflowInstanceStatus.Completed)
            throw new InvalidOperationException("PostgreSQL 条件嵌套汇聚失败：外层 Join 未在审批完成后结束实例。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == businessId).ExecuteAffrows();
    }
}

static void RunAutomaticActionFailureRollbackProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    var order = new SalesOrder($"SO-WF-ACTION-FAIL-{Guid.CreateVersion7():N}", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1m, 100m);
    try
    {
        var orders = new FreeSqlSalesOrderRepository(fsql);
        orders.Add(order);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_ACTION_FAILURE", "PostgreSQL 自动动作失败回滚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "提交订单", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, action.Id);
        definition.Connect(action.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, nameof(SalesOrder), order.Id, startedBy: "workflow-postgres-probe");
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([new ThrowAfterSalesOrderUpdateHandler(orders)]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);

        try
        {
            runtime.Continue(instance);
            throw new InvalidOperationException("故障注入未触发，自动业务动作不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟业务动作写入后失败")
        {
            // 预期：订单更新、节点推进和 NodeExecuted 均与自动动作同一事务回滚。
        }

        var persisted = instances.List(nameof(SalesOrder), order.Id).Single();
        if (orders.List().Single(x => x.Id == order.Id).Status != SalesOrderStatus.Draft ||
            persisted.CurrentNodeId != action.Id || persisted.Status != WorkflowInstanceStatus.Running ||
            operations.List(instanceId: instance.Id).Any(x => x.Kind == WorkflowOperationKind.NodeExecuted && x.NodeId == action.Id) ||
            operations.List(instanceId: instance.Id).Count(x => x.Kind == WorkflowOperationKind.NodeFailed && x.NodeId == action.Id) != 1)
            throw new InvalidOperationException("PostgreSQL 自动业务动作失败回滚失败：业务状态、节点推进或执行历史发生部分提交。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<SalesOrderRecord>().Where(x => x.Id == order.Id).ExecuteAffrows();
    }
}

static void RunParallelAutomaticActionRetryProbe(IFreeSql fsql)
{
    var order = new SalesOrder($"SO-WF-PARALLEL-RETRY-{Guid.CreateVersion7():N}", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1m, 100m);
    try
    {
        var orders = new FreeSqlSalesOrderRepository(fsql);
        orders.Add(order);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var definition = new WorkflowDefinition("PG_PARALLEL_ACTION_RETRY", "PostgreSQL 并行自动动作重试");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var action = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.BusinessAction, "提交订单", configJson: "{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"workflow-postgres-probe\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, action.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(action.Id, join.Id);
        definition.Connect(approval.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, nameof(SalesOrder), order.Id, startedBy: "workflow-postgres-probe");
        var handler = new FailOnceSalesOrderUpdateHandler(orders);
        var runtime = new WorkflowRuntimeService(instanceService, new WorkflowActionExecutor([handler]), new NotificationService(new FreeSqlNotificationRepository(fsql)), operations, transactions);
        var tasks = new WorkflowTaskService(new FreeSqlWorkflowTaskRepository(fsql), instanceService, runtime: runtime, transactions: transactions);

        try
        {
            runtime.Continue(instance);
            throw new InvalidOperationException("故障注入未触发，并行自动业务动作不应首次成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟并行自动业务动作首次失败")
        {
            // 预期：动作写入回滚，另一审批分支仍保持活动。
        }

        var failed = instances.List(nameof(SalesOrder), order.Id).Single();
        if (orders.List().Single(x => x.Id == order.Id).Status != SalesOrderStatus.Draft
            || !failed.ActiveNodeIds.SetEquals([action.Id, approval.Id])
            || operations.List(instanceId: instance.Id).Count(x => x.Kind == WorkflowOperationKind.NodeFailed && x.NodeId == action.Id) != 1)
            throw new InvalidOperationException("PostgreSQL 并行自动动作失败回滚失败：动作状态或另一活动分支未完整保留。");

        var failure = operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.NodeFailed).Single(x => x.NodeId == action.Id);
        if (tasks.Retry(instance, "workflow-postgres-probe", failedNodeId: action.Id).State != WorkflowRuntimeState.WaitingForApproval)
            throw new InvalidOperationException("PostgreSQL 并行自动动作重试失败：成功动作未等待另一审批分支。");
        var retry = operations.List(instanceId: instance.Id, kind: WorkflowOperationKind.Retried).Single(x => x.NodeId == action.Id);
        if (retry.DedupeKey != $"workflow-runtime-retried:{instance.Id}:{action.Id}:{failure.Id:N}")
            throw new InvalidOperationException("PostgreSQL 并行自动动作重试幂等失败：Retried 审计未绑定最近一次失败记录。");
        var task = tasks.List(instance.Id, status: WorkflowTaskStatus.Pending).Single();
        tasks.Approve(task, "workflow-postgres-probe", "通过");
        var completed = instances.List(nameof(SalesOrder), order.Id).Single();
        if (orders.List().Single(x => x.Id == order.Id).Status != SalesOrderStatus.Submitted
            || completed.Status != WorkflowInstanceStatus.Completed
            || handler.ExecutionCount != 2
            || operations.List(instanceId: instance.Id).Count(x => x.Kind == WorkflowOperationKind.NodeExecuted && x.NodeId == action.Id) != 1)
            throw new InvalidOperationException("PostgreSQL 并行自动动作重试失败：重试后未正确汇聚完成。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<SalesOrderRecord>().Where(x => x.Id == order.Id).ExecuteAffrows();
    }
}

static void RunRejectedActionFailureRollbackProbe(IFreeSql fsql)
{
    var order = new SalesOrder($"SO-WF-REJECT-FAIL-{Guid.CreateVersion7():N}", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1m, 100m);
    try
    {
        var orders = new FreeSqlSalesOrderRepository(fsql);
        orders.Add(order);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var tasks = new FreeSqlWorkflowTaskRepository(fsql);
        var definition = new WorkflowDefinition("PG_REJECT_ACTION_FAILURE", "PostgreSQL 拒绝动作失败回滚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"workflow-postgres-probe\",\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, nameof(SalesOrder), order.Id, startedBy: "workflow-postgres-probe");
        var task = new WorkflowTask(instance, approval.Id, approval.Name, "workflow-postgres-probe");
        tasks.Add(task);
        var service = new WorkflowTaskService(tasks, instanceService, new WorkflowActionExecutor([new ThrowAfterSalesOrderUpdateHandler(orders)]), operations: operations, transactions: transactions);

        try
        {
            service.Reject(task, "workflow-postgres-probe", "故障注入");
            throw new InvalidOperationException("故障注入未触发，拒绝动作不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟业务动作写入后失败")
        {
            // 预期：拒绝动作与待办/实例状态同一事务回滚。
        }

        if (orders.List().Single(x => x.Id == order.Id).Status != SalesOrderStatus.Draft ||
            tasks.List(instance.Id).Single().Status != WorkflowTaskStatus.Pending || tasks.List(instance.Id).Single().Revision != 1 ||
            instances.List(nameof(SalesOrder), order.Id).Single().Status != WorkflowInstanceStatus.Running ||
            operations.List(instanceId: instance.Id).Any(x => x.Kind == WorkflowOperationKind.Rejected))
            throw new InvalidOperationException("PostgreSQL 拒绝动作失败回滚失败：业务状态、待办、实例或操作历史发生部分提交。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<SalesOrderRecord>().Where(x => x.Id == order.Id).ExecuteAffrows();
    }
}

static void RunCancelledActionFailureRollbackProbe(IFreeSql fsql)
{
    var order = new SalesOrder($"SO-WF-CANCEL-FAIL-{Guid.CreateVersion7():N}", Guid.CreateVersion7(), Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.Today), 1m, 100m);
    try
    {
        var orders = new FreeSqlSalesOrderRepository(fsql);
        orders.Add(order);
        var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
        var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
        var instances = new FreeSqlWorkflowInstanceRepository(fsql);
        var instanceService = new WorkflowInstanceService(instances, operations, transactions);
        var tasks = new FreeSqlWorkflowTaskRepository(fsql);
        var definition = new WorkflowDefinition("PG_CANCEL_ACTION_FAILURE", "PostgreSQL 取消动作失败回滚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"workflow-postgres-probe\",\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = instanceService.Start(definition, nameof(SalesOrder), order.Id, startedBy: "workflow-postgres-probe");
        var task = new WorkflowTask(instance, approval.Id, approval.Name, "workflow-postgres-probe");
        tasks.Add(task);
        var service = new WorkflowTaskService(tasks, instanceService, new WorkflowActionExecutor([new ThrowAfterSalesOrderUpdateHandler(orders)]), operations: operations, transactions: transactions);

        try
        {
            service.Cancel(task, "workflow-postgres-probe", "故障注入");
            throw new InvalidOperationException("故障注入未触发，取消动作不应成功。");
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟业务动作写入后失败") { }

        if (orders.List().Single(x => x.Id == order.Id).Status != SalesOrderStatus.Draft ||
            tasks.List(instance.Id).Single().Status != WorkflowTaskStatus.Pending || tasks.List(instance.Id).Single().Revision != 1 ||
            instances.List(nameof(SalesOrder), order.Id).Single().Status != WorkflowInstanceStatus.Running ||
            operations.List(instanceId: instance.Id).Any(x => x.Kind == WorkflowOperationKind.Cancelled))
            throw new InvalidOperationException("PostgreSQL 取消动作失败回滚失败：业务状态、待办、实例或操作历史发生部分提交。");
    }
    finally
    {
        fsql.Delete<WorkflowOperationRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<WorkflowTaskRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<WorkflowInstanceRecord>().Where(x => x.BusinessId == order.Id).ExecuteAffrows();
        fsql.Delete<SalesOrderRecord>().Where(x => x.Id == order.Id).ExecuteAffrows();
    }
}

static void RunTerminationSiblingAuditAndNotificationProbe(IFreeSql fsql)
{
    var businessId = Guid.CreateVersion7();
    var definition = new WorkflowDefinition($"PG_TERMINATION_SIBLING_{Guid.CreateVersion7():N}", "PostgreSQL 终止多待办审计");
    var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
    var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approvers\":[\"workflow-postgres-probe\",\"workflow-postgres-finance\"]}");
    var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
    definition.Connect(start.Id, approval.Id);
    definition.Connect(approval.Id, end.Id);
    definition.Publish();

    var instances = new FreeSqlWorkflowInstanceRepository(fsql);
    var tasks = new FreeSqlWorkflowTaskRepository(fsql);
    var operations = new WorkflowOperationService(new FreeSqlWorkflowOperationRepository(fsql));
    var notifications = new NotificationService(new FreeSqlNotificationRepository(fsql));
    var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);
    WorkflowInstance? instance = null;
    WorkflowTask? rejected = null;
    WorkflowTask? sibling = null;
    try
    {
        var instanceService = new WorkflowInstanceService(instances);
        instance = instanceService.Start(definition, "workflow.postgres.termination", businessId, startedBy: "workflow-postgres-probe");
        var service = new WorkflowTaskService(
            tasks,
            instanceService,
            notifications: notifications,
            operations: operations,
            transactions: transactions);
        rejected = service.CreateApprovalTask(instance, approval.Id, approval.Name, "workflow-postgres-probe");
        sibling = service.CreateApprovalTask(instance, approval.Id, approval.Name, "workflow-postgres-finance");

        if (notifications.UnreadCount(rejected.Assignee) != 1 || notifications.UnreadCount(sibling.Assignee) != 1)
            throw new InvalidOperationException("PostgreSQL 终止多待办探针初始化失败：审批通知未按接收人各生成一条。");

        service.Reject(rejected, rejected.Assignee, "资料不完整", new DateTime(2026, 7, 18, 16, 0, 0));

        var persistedTasks = tasks.List(instance.Id);
        if (persistedTasks.Single(x => x.Id == rejected.Id).Status != WorkflowTaskStatus.Rejected ||
            persistedTasks.Single(x => x.Id == sibling.Id).Status != WorkflowTaskStatus.Cancelled ||
            instances.List("workflow.postgres.termination", businessId).Single().Status != WorkflowInstanceStatus.Rejected)
            throw new InvalidOperationException("PostgreSQL 终止多待办探针失败：终止状态没有整体持久化。");
        if (!operations.List(instance.Id).Any(x => x.TaskId == sibling.Id && x.Kind == WorkflowOperationKind.Cancelled && x.DedupeKey == $"workflow-task-cancelled:{sibling.Id}"))
            throw new InvalidOperationException("PostgreSQL 终止多待办探针失败：被动取消待办缺少取消审计。");
        if (notifications.UnreadCount(rejected.Assignee) != 0 || notifications.UnreadCount(sibling.Assignee) != 0)
            throw new InvalidOperationException("PostgreSQL 终止多待办探针失败：终止后仍有未读审批通知。");
    }
    finally
    {
        if (rejected is not null) fsql.Delete<NotificationRecord>().Where(x => x.DedupeKey == $"workflow-task:{rejected.Id}").ExecuteAffrows();
        if (sibling is not null) fsql.Delete<NotificationRecord>().Where(x => x.DedupeKey == $"workflow-task:{sibling.Id}").ExecuteAffrows();
        if (instance is not null)
        {
            fsql.Delete<WorkflowOperationRecord>().Where(x => x.InstanceId == instance.Id).ExecuteAffrows();
            fsql.Delete<WorkflowTaskRecord>().Where(x => x.InstanceId == instance.Id).ExecuteAffrows();
            fsql.Delete<WorkflowInstanceRecord>().Where(x => x.Id == instance.Id).ExecuteAffrows();
        }
    }
}

static void RunInvalidNotificationFailurePayloadProbe(IFreeSql fsql)
{
    var id = Guid.CreateVersion7();
    try
    {
        fsql.Insert(new NotificationFailureRecord
        {
            Id = id,
            Operation = "publish",
            Recipient = "workflow-postgres-probe",
            DedupeKey = $"workflow-invalid-payload:{id}",
            PayloadJson = "{not-json}",
            Error = "模拟损坏重放负载",
            OccurredAt = DateTime.Now,
            Status = NotificationFailureStatus.Pending
        }).ExecuteAffrows();
        var repository = new FreeSqlNotificationFailureRepository(fsql);
        if (repository.FindPending(id) is not null ||
            fsql.Select<NotificationFailureRecord>().Where(x => x.Id == id && x.Status == NotificationFailureStatus.InvalidPayload).Count() != 1)
            throw new InvalidOperationException("PostgreSQL 无效通知重放负载未被隔离。");
    }
    finally
    {
        fsql.Delete<NotificationFailureRecord>().Where(x => x.Id == id).ExecuteAffrows();
    }
}

static void RunNotificationCenterProbe(IFreeSql fsql)
{
    var recipient = $"workflow-notification-{Guid.CreateVersion7():N}";
    var foreignRecipient = $"workflow-notification-foreign-{Guid.CreateVersion7():N}";
    var notifications = new NotificationService(new FreeSqlNotificationRepository(fsql));
    var first = notifications.Publish(recipient, WorkNotificationKind.System, "通知一", "分页测试", null, $"notification-center:{recipient}:1", new DateTime(2026, 7, 18, 10, 0, 0));
    var second = notifications.Publish(recipient, WorkNotificationKind.System, "通知二", "删除测试", null, $"notification-center:{recipient}:2", new DateTime(2026, 7, 18, 11, 0, 0));
    var foreign = notifications.Publish(foreignRecipient, WorkNotificationKind.System, "他人通知", "范围测试", null, $"notification-center:{foreignRecipient}:1", new DateTime(2026, 7, 18, 12, 0, 0));
    try
    {
        var page = notifications.ListPage(recipient, pageIndex: 2, pageSize: 1);
        if (page.TotalCount != 2 || page.PageCount != 2 || page.Items.Count != 1 || page.Items[0].Id != first.Id)
            throw new InvalidOperationException($"PostgreSQL 通知中心分页口径错误：total={page.TotalCount}, pages={page.PageCount}, items={page.Items.Count}, expected={first.Id}, actual={page.Items.FirstOrDefault()?.Id}。");
        if (notifications.UnreadCount(recipient) != 2)
        {
            var direct = fsql.Select<NotificationRecord>().Where(x => x.Recipient == recipient).ToList();
            throw new InvalidOperationException($"PostgreSQL 通知中心未读计数错误：service={notifications.UnreadCount(recipient)}, list={notifications.List(recipient, unreadOnly: true).Count}, rows={direct.Count}, nulls={direct.Count(x => x.ReadAt is null)}。");
        }
        var deleted = notifications.DeleteMany(recipient, [second.Id, foreign.Id]);
        if (deleted != 1 || notifications.List(recipient).Count != 1 || notifications.UnreadCount(recipient) != 1 || notifications.List(foreignRecipient).Count != 1)
            throw new InvalidOperationException($"PostgreSQL 通知中心接收人删除边界错误：deleted={deleted}, own={notifications.List(recipient).Count}, unread={notifications.UnreadCount(recipient)}, foreign={notifications.List(foreignRecipient).Count}。");
    }
    finally
    {
        fsql.Delete<NotificationRecord>().Where(x => x.Recipient == recipient || x.Recipient == foreignRecipient).ExecuteAffrows();
    }
}

static void RunTaskNotificationAfterCommitProbe(IFreeSql fsql)
{
    var recipient = $"workflow-after-commit-{Guid.CreateVersion7():N}";
    var dedupeKey = $"workflow-task-after-commit:{Guid.CreateVersion7():N}";
    var notifications = new NotificationService(new FreeSqlNotificationRepository(fsql));
    var transactions = new FreeSqlWorkflowTransactionBoundary(fsql);

    try
    {
        try
        {
            transactions.Execute(() =>
            {
                transactions.Execute(
                    static () => { },
                    afterRollback: null,
                    afterCommit: () => notifications.Publish(recipient, WorkNotificationKind.Approval, "待审批", "回滚不应投递", null, dedupeKey));
                throw new InvalidOperationException("模拟外层 Workflow 回滚");
            });
        }
        catch (InvalidOperationException exception) when (exception.Message == "模拟外层 Workflow 回滚")
        {
            // 预期：事务回滚时丢弃提交回调。
        }

        if (notifications.List(recipient).Count != 0)
            throw new InvalidOperationException("PostgreSQL 通知提交边界错误：Workflow 回滚后留下了审批通知。");

        transactions.Execute(() =>
        {
            transactions.Execute(
                static () => { },
                afterRollback: null,
                afterCommit: () => notifications.Publish(recipient, WorkNotificationKind.Approval, "待审批", "提交后投递", null, dedupeKey));
            transactions.Execute(
                static () => { },
                afterRollback: null,
                afterCommit: () => notifications.Publish(recipient, WorkNotificationKind.Approval, "待审批", "重复提交只保留一条", null, dedupeKey));
        });

        if (notifications.List(recipient).Count != 1)
            throw new InvalidOperationException("PostgreSQL 通知提交边界错误：提交后通知未按去重键只保留一条。");
    }
    finally
    {
        fsql.Delete<NotificationRecord>().Where(x => x.Recipient == recipient).ExecuteAffrows();
    }
}

readonly record struct BenchmarkStats(double P50Ms, double P95Ms, double P99Ms);

sealed class ThrowingWorkflowOperationRepository(IWorkflowOperationRepository inner) : IWorkflowOperationRepository
{
    public IReadOnlyList<WorkflowOperation> List(Guid? instanceId = null, string? businessType = null, Guid? businessId = null, WorkflowOperationKind? kind = null)
        => inner.List(instanceId, businessType, businessId, kind);
    public WorkflowOperation? FindByDedupeKey(string dedupeKey) => inner.FindByDedupeKey(dedupeKey);
    public void Add(WorkflowOperation operation) => throw new InvalidOperationException("模拟 Workflow 操作历史写入失败");
    public bool TryAdd(WorkflowOperation operation) => throw new InvalidOperationException("模拟 Workflow 操作历史写入失败");
}

sealed class ThrowingNotificationRepository(INotificationRepository inner) : INotificationRepository
{
    public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => inner.List(recipient, unreadOnly);
    public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => inner.FindByDedupeKey(recipient, dedupeKey);
    public void Add(WorkNotification notification) => throw new InvalidOperationException("模拟通知写入失败");
    public bool TryAdd(WorkNotification notification) => throw new InvalidOperationException("模拟通知写入失败");
    public void Update(WorkNotification notification) => inner.Update(notification);
    public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => inner.Delete(recipient, notificationIds);
}

sealed class ThrowingFailureResolveRepository(INotificationFailureRepository inner) : INotificationFailureRepository
{
    public IReadOnlyList<PersistedNotificationFailure> ListPending(int take) => inner.ListPending(take);
    public PersistedNotificationFailure? FindPending(Guid id) => inner.FindPending(id);
    public bool TryClaim(Guid id, DateTime attemptedAt, TimeSpan lease) => inner.TryClaim(id, attemptedAt, lease);
    public void MarkRetryFailed(Guid id, string error, DateTime attemptedAt) => inner.MarkRetryFailed(id, error, attemptedAt);
    public void MarkResolved(Guid id, DateTime resolvedAt) => throw new InvalidOperationException("模拟补投完成写入失败");
}

sealed class EmptyApproverResolver : IWorkflowApproverResolver
{
    public IReadOnlyList<string> Resolve(WorkflowInstance instance, string nodeConfigJson) => [];
}

sealed class NewApproverResolver : IWorkflowApproverResolver
{
    public IReadOnlyList<string> Resolve(WorkflowInstance instance, string nodeConfigJson) => ["workflow-postgres-new-member"];
}

sealed class ThrowAfterSalesOrderUpdateHandler(ISalesOrderRepository orders) : IWorkflowActionHandler
{
    public bool CanHandle(string businessType) => businessType.Equals(nameof(SalesOrder), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        var order = orders.List().Single(x => x.Id == context.Instance.BusinessId);
        order.SetStatus(SalesOrderStatus.Submitted);
        orders.Update(order);
        throw new InvalidOperationException("模拟业务动作写入后失败");
    }
}

sealed class FailOnceSalesOrderUpdateHandler(ISalesOrderRepository orders) : IWorkflowActionHandler
{
    public int ExecutionCount { get; private set; }
    public bool CanHandle(string businessType) => businessType.Equals(nameof(SalesOrder), StringComparison.OrdinalIgnoreCase);

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        ExecutionCount++;
        var order = orders.List().Single(x => x.Id == context.Instance.BusinessId);
        order.SetStatus(SalesOrderStatus.Submitted);
        orders.Update(order);
        if (ExecutionCount == 1) throw new InvalidOperationException("模拟并行自动业务动作首次失败");
    }
}

sealed class ConcurrentRetryActionHandler : IWorkflowActionHandler
{
    private int executionCount;

    public bool AllowSuccess { get; set; }
    public int ExecutionCount => Volatile.Read(ref executionCount);

    public bool CanHandle(string businessType) => businessType == "workflow.postgres.retry-concurrent";

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        Interlocked.Increment(ref executionCount);
        if (!AllowSuccess) throw new InvalidOperationException("模拟并发重试首次失败");
    }
}

sealed class ConcurrentRetryWithdrawalActionHandler : IWorkflowActionHandler
{
    private int executionCount;

    public bool AllowSuccess { get; set; }
    public int ExecutionCount => Volatile.Read(ref executionCount);

    public bool CanHandle(string businessType) => businessType == "workflow.postgres.retry-withdraw-concurrent";

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        Interlocked.Increment(ref executionCount);
        if (!AllowSuccess) throw new InvalidOperationException("模拟并发重试撤回首次失败");
    }
}

sealed class ConcurrentContinueWithdrawalActionHandler : IWorkflowActionHandler
{
    private int executionCount;

    public bool AllowSuccess { get; set; }
    public int ExecutionCount => Volatile.Read(ref executionCount);

    public bool CanHandle(string businessType) => businessType == "workflow.postgres.continue-withdraw-concurrent";

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        Interlocked.Increment(ref executionCount);
        if (!AllowSuccess) throw new InvalidOperationException("模拟并发继续撤回首次失败");
    }
}

sealed class ConcurrentApprovalActionHandler : IWorkflowActionHandler
{
    private int executionCount;

    public int ExecutionCount => Volatile.Read(ref executionCount);
    public string? LastActor { get; private set; }

    public bool CanHandle(string businessType) => businessType == "workflow.postgres.approval-concurrent";

    public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
    {
        Interlocked.Increment(ref executionCount);
        LastActor = context.Actor;
    }
}

sealed class FailingCompletionInstanceRepository(IWorkflowInstanceRepository inner) : IWorkflowInstanceRepository
{
    public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
        => inner.List(businessType, businessId, status);

    public void Add(WorkflowInstance instance) => inner.Add(instance);
    public bool TryAdd(WorkflowInstance instance) => inner.TryAdd(instance);
    public void Update(WorkflowInstance instance) => inner.Update(instance);

    public bool TryUpdate(WorkflowInstance instance)
    {
        if (instance.Status == WorkflowInstanceStatus.Completed)
            throw new InvalidOperationException("模拟流程完成持久化失败");
        return inner.TryUpdate(instance);
    }
}

sealed class StaleReadWorkflowInstanceRepository(IWorkflowInstanceRepository inner, WorkflowInstance stale) : IWorkflowInstanceRepository, IWorkflowInstanceLockRepository
{
    public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
        => (businessType is null || stale.BusinessType == businessType)
            && (businessId is null || stale.BusinessId == businessId)
            && (status is null || stale.Status == status)
            ? [stale]
            : [];

    public void Add(WorkflowInstance instance) => inner.Add(instance);
    public bool TryAdd(WorkflowInstance instance) => inner.TryAdd(instance);
    public void Update(WorkflowInstance instance) => inner.Update(instance);
    public bool TryUpdate(WorkflowInstance instance) => inner.TryUpdate(instance);

    public void LockForUpdate(WorkflowInstance instance)
    {
        if (inner is IWorkflowInstanceLockRepository locking)
            locking.LockForUpdate(instance);
    }
}

sealed class ThrowingApprovalSnapshotInstanceRepository(IWorkflowInstanceRepository inner) : IWorkflowInstanceRepository
{
    public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
        => inner.List(businessType, businessId, status);

    public void Add(WorkflowInstance instance) => inner.Add(instance);
    public bool TryAdd(WorkflowInstance instance) => inner.TryAdd(instance);
    public void Update(WorkflowInstance instance) => inner.Update(instance);

    public bool TryUpdate(WorkflowInstance instance)
    {
        if (instance.ApprovalAssigneesJson != "{}") throw new InvalidOperationException("模拟审批人快照持久化失败");
        return inner.TryUpdate(instance);
    }
}

sealed class ThrowingLmsLifecycleRepository(ILmsLicenseRepository inner) : ILmsLicenseRepository
{
    public IReadOnlyList<LmsLicenseRequest> ListRequests() => inner.ListRequests();
    public IReadOnlyList<LmsLicenseAuthorization> ListAuthorizations() => inner.ListAuthorizations();
    public IReadOnlyList<LmsLicenseLifecycleEntry> ListLifecycleEntries(Guid authorizationId) => inner.ListLifecycleEntries(authorizationId);
    public void Add(LmsLicenseRequest item) => inner.Add(item);
    public void Update(LmsLicenseRequest item) => inner.Update(item);
    public void RemoveRequest(Guid requestId) => inner.RemoveRequest(requestId);
    public void Add(LmsLicenseAuthorization item) => inner.Add(item);
    public void Update(LmsLicenseAuthorization item) => inner.Update(item);
    public void Add(LmsLicenseLifecycleEntry item) => throw new InvalidOperationException("模拟 LMS 生命周期审计写入失败");
}

sealed class ThrowingLmsReplacementInsertRepository(ILmsLicenseRepository inner) : ILmsLicenseRepository
{
    public IReadOnlyList<LmsLicenseRequest> ListRequests() => inner.ListRequests();
    public IReadOnlyList<LmsLicenseAuthorization> ListAuthorizations() => inner.ListAuthorizations();
    public IReadOnlyList<LmsLicenseLifecycleEntry> ListLifecycleEntries(Guid authorizationId) => inner.ListLifecycleEntries(authorizationId);
    public void Add(LmsLicenseRequest item) => inner.Add(item);
    public void Update(LmsLicenseRequest item) => inner.Update(item);
    public void RemoveRequest(Guid requestId) => inner.RemoveRequest(requestId);
    public void Add(LmsLicenseAuthorization item) => throw new InvalidOperationException("模拟 LMS 替代授权写入失败");
    public void Update(LmsLicenseAuthorization item) => inner.Update(item);
    public void Add(LmsLicenseLifecycleEntry item) => inner.Add(item);
}

sealed class ThrowingParallelSplitInstanceRepository(IWorkflowInstanceRepository inner) : IWorkflowInstanceRepository
{
    public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
        => inner.List(businessType, businessId, status);

    public void Add(WorkflowInstance instance) => inner.Add(instance);
    public bool TryAdd(WorkflowInstance instance) => inner.TryAdd(instance);
    public void Update(WorkflowInstance instance) => inner.Update(instance);

    public bool TryUpdate(WorkflowInstance instance)
    {
        if (instance.ActiveNodeIds.Count > 1) throw new InvalidOperationException("模拟并行拆分持久化失败");
        return inner.TryUpdate(instance);
    }
}

sealed class ThrowingLoopInstanceRepository(IWorkflowInstanceRepository inner) : IWorkflowInstanceRepository
{
    public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
        => inner.List(businessType, businessId, status);

    public void Add(WorkflowInstance instance) => inner.Add(instance);
    public bool TryAdd(WorkflowInstance instance) => inner.TryAdd(instance);
    public void Update(WorkflowInstance instance) => inner.Update(instance);
    public bool TryUpdate(WorkflowInstance instance) => throw new InvalidOperationException("模拟循环实例持久化失败");
}

sealed class ThrowingParallelJoinArrivalInstanceRepository(IWorkflowInstanceRepository inner) : IWorkflowInstanceRepository
{
    public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null)
        => inner.List(businessType, businessId, status);

    public void Add(WorkflowInstance instance) => inner.Add(instance);
    public bool TryAdd(WorkflowInstance instance) => inner.TryAdd(instance);
    public void Update(WorkflowInstance instance) => inner.Update(instance);

    public bool TryUpdate(WorkflowInstance instance)
    {
        if (instance.ParallelJoinArrivalsJson != "{}") throw new InvalidOperationException("模拟并行汇聚到达持久化失败");
        return inner.TryUpdate(instance);
    }
}

sealed class InMemoryDefinitionRepository : IWorkflowDefinitionRepository
{
    private readonly List<WorkflowDefinition> items = [];

    public IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null)
        => items.Where(x => (code is null || x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) && (status is null || x.Status == status)).ToArray();

    public void Add(WorkflowDefinition definition) => items.Add(definition);
    public bool TryAdd(WorkflowDefinition definition)
    {
        if (items.Any(x => x.Id == definition.Id || (x.Code.Equals(definition.Code, StringComparison.OrdinalIgnoreCase) && x.VersionNumber == definition.VersionNumber))) return false;
        Add(definition);
        return true;
    }
    public void Update(WorkflowDefinition definition) { }
    public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
}

sealed class ThrowingTaskAddRepository(IWorkflowTaskRepository inner) : IWorkflowTaskRepository
{
    public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
        => inner.List(instanceId, assignee, status);

    public void Add(WorkflowTask task) => throw new InvalidOperationException("模拟初始待办写入失败");
    public bool TryAdd(WorkflowTask task) => AddAndThrow(task);
    private static bool AddAndThrow(WorkflowTask task) { throw new InvalidOperationException("模拟初始待办写入失败"); }
    public void Update(WorkflowTask task) => inner.Update(task);
    public bool TryUpdate(WorkflowTask task) => inner.TryUpdate(task);
}

sealed class ThrowingSecondDecisionTaskRepository(IWorkflowTaskRepository inner) : IWorkflowTaskRepository
{
    private int updateCount;

    public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
        => inner.List(instanceId, assignee, status);

    public void Add(WorkflowTask task) => inner.Add(task);
    public bool TryAdd(WorkflowTask task) => inner.TryAdd(task);
    public void Update(WorkflowTask task) => inner.Update(task);

    public bool TryUpdate(WorkflowTask task)
    {
        if (++updateCount == 4) throw new InvalidOperationException("模拟第二个待办写入失败");
        return inner.TryUpdate(task);
    }
}

sealed class ThrowingSecondTaskUpdateRepository(IWorkflowTaskRepository inner) : IWorkflowTaskRepository
{
    private int updateCount;

    public IReadOnlyList<WorkflowTask> List(Guid? instanceId = null, string? assignee = null, WorkflowTaskStatus? status = null)
        => inner.List(instanceId, assignee, status);

    public void Add(WorkflowTask task) => inner.Add(task);
    public bool TryAdd(WorkflowTask task) => inner.TryAdd(task);
    public void Update(WorkflowTask task) => inner.Update(task);

    public bool TryUpdate(WorkflowTask task)
    {
        if (++updateCount == 2) throw new InvalidOperationException("模拟或签兄弟待办取消失败");
        return inner.TryUpdate(task);
    }
}
