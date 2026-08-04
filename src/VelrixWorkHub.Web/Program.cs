using AdminBlazor;
using FreeSql;
using Serilog;
using VelrixWorkHub.Application.Tasks;
using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Recruitment;
using VelrixWorkHub.Application.Onboarding;
using VelrixWorkHub.Application.Offboarding;
using VelrixWorkHub.Application.Assets;
using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Application.Overtime;
using VelrixWorkHub.Application.Vehicles;
using VelrixWorkHub.Application.Announcements;
using VelrixWorkHub.Application.Schedules;
using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Application.Contacts;
using VelrixWorkHub.Application.FollowUps;
using VelrixWorkHub.Application.Opportunities;
using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Application.Lms;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.Inventory;
using VelrixWorkHub.Application.SalesOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Attachments;
using VelrixWorkHub.Application.ExpenseReimbursements;
using VelrixWorkHub.Application.CashAdvances;
using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Application.ProcurementRequests;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Application.SimpleForms;
using VelrixWorkHub.Application.Navigation;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.Reports;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Web.Notifications;
using VelrixWorkHub.Infrastructure.Announcements;
using VelrixWorkHub.Infrastructure.Schedules;
using VelrixWorkHub.Infrastructure.Customers;
using VelrixWorkHub.Infrastructure.Tasks;
using VelrixWorkHub.Infrastructure.Employees;
using VelrixWorkHub.Infrastructure.Recruitment;
using VelrixWorkHub.Infrastructure.Onboarding;
using VelrixWorkHub.Infrastructure.Offboarding;
using VelrixWorkHub.Infrastructure.Assets;
using VelrixWorkHub.Infrastructure.Leave;
using VelrixWorkHub.Infrastructure.Overtime;
using VelrixWorkHub.Infrastructure.Vehicles;
using VelrixWorkHub.Infrastructure.Products;
using VelrixWorkHub.Infrastructure.Warehouses;
using VelrixWorkHub.Infrastructure.Suppliers;
using VelrixWorkHub.Infrastructure.PmpProjects;
using VelrixWorkHub.Infrastructure.Lms;
using VelrixWorkHub.Infrastructure.PurchaseOrders;
using VelrixWorkHub.Infrastructure.Inventory;
using VelrixWorkHub.Infrastructure.SalesOrders;
using VelrixWorkHub.Infrastructure.Settlements;
using VelrixWorkHub.Infrastructure.Attachments;
using VelrixWorkHub.Infrastructure.ExpenseReimbursements;
using VelrixWorkHub.Infrastructure.CashAdvances;
using VelrixWorkHub.Infrastructure.PaymentRequests;
using VelrixWorkHub.Infrastructure.ProcurementRequests;
using VelrixWorkHub.Infrastructure.Workflow;
using VelrixWorkHub.Infrastructure.SimpleForms;
using VelrixWorkHub.Infrastructure.Navigation;
using VelrixWorkHub.Infrastructure.Notifications;
using VelrixWorkHub.Web;
using VelrixWorkHub.Web.Components;
using VelrixWorkHub.Web.Lms;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.ConfigureHttpJsonOptions(options => JsonSerializationDefaults.Configure(options.SerializerOptions));

builder.Services.AddScoped<IWorkTaskRepository, FreeSqlWorkTaskRepository>();
builder.Services.AddScoped<WorkTaskService>();
builder.Services.AddScoped<IEmployeeDirectoryRepository, FreeSqlEmployeeDirectoryRepository>();
builder.Services.AddScoped<EmployeeDirectoryService>();
builder.Services.AddScoped<IEmployeeAccountLifecycleService, FreeSqlEmployeeAccountLifecycleService>();
builder.Services.AddScoped<OaWorkflowOutcomeNotificationService>();
builder.Services.AddScoped<IOaEmployeeProfileRepository, FreeSqlOaEmployeeProfileRepository>();
builder.Services.AddScoped<EmployeeProfileService>();
builder.Services.AddScoped<IOaRecruitmentRepository, FreeSqlRecruitmentRepository>();
builder.Services.AddScoped<RecruitmentService>();
builder.Services.AddScoped<IOaOnboardingRepository, FreeSqlOnboardingRepository>();
builder.Services.AddScoped<OnboardingService>();
builder.Services.AddScoped<IOaOffboardingRepository, FreeSqlOffboardingRepository>();
    builder.Services.AddScoped<IOaAssetRepository, FreeSqlAssetRepository>();
    builder.Services.AddScoped<IOaAssetAssignmentRepository, FreeSqlAssetRepository>();
    builder.Services.AddScoped<IOaAssetOperationRepository, FreeSqlAssetRepository>();
    builder.Services.AddScoped<IOaAssetTransferRepository, FreeSqlAssetTransferRepository>();
    builder.Services.AddScoped<IOaAssetStocktakeRepository, FreeSqlAssetStocktakeRepository>();
    builder.Services.AddScoped<AssetService>();
    builder.Services.AddScoped<IOaConsumableSupplyRepository, FreeSqlConsumableSupplyRepository>();
    builder.Services.AddScoped<IOaConsumableTransactionRepository, FreeSqlConsumableSupplyRepository>();
    builder.Services.AddScoped<ConsumableSupplyService>();
    builder.Services.AddScoped<IOaAssetRequestRepository, FreeSqlAssetRequestRepository>();
    builder.Services.AddScoped<AssetRequestService>();
    builder.Services.AddScoped<IOaAssetRequestWorkflowApprover>(sp => sp.GetRequiredService<AssetRequestService>());
builder.Services.AddScoped<OffboardingRiskService>();
builder.Services.AddScoped<IOaOffboardingRiskProvider>(sp => sp.GetRequiredService<OffboardingRiskService>());
builder.Services.AddScoped<OffboardingService>();
builder.Services.AddScoped<IOaLeaveRequestRepository, FreeSqlLeaveRequestRepository>();
builder.Services.AddScoped<IOaLeaveCalendarEntryRepository, FreeSqlLeaveCalendarEntryRepository>();
builder.Services.AddScoped<IOaLeaveBalanceRepository, FreeSqlLeaveBalanceRepository>();
builder.Services.AddScoped<IOaLeaveBalanceReservationRepository, FreeSqlLeaveBalanceRepository>();
builder.Services.AddScoped<LeaveBalanceService>();
builder.Services.AddScoped<LeaveCalendarService>();
builder.Services.AddScoped<LeaveRequestService>();
builder.Services.AddScoped<IOaLeaveRequestWorkflowApprover>(sp => sp.GetRequiredService<LeaveRequestService>());
builder.Services.AddScoped<IOaOvertimeRequestRepository, FreeSqlOvertimeRequestRepository>();
builder.Services.AddScoped<OvertimeRequestService>();
builder.Services.AddScoped<IOaOvertimeRequestWorkflowApprover>(sp => sp.GetRequiredService<OvertimeRequestService>());
builder.Services.AddScoped<IOaOvertimeConversionRepository, FreeSqlOvertimeConversionRepository>();
builder.Services.AddScoped<OvertimeConversionService>();
builder.Services.AddScoped<IOaVehicleRepository, FreeSqlVehicleRepository>();
builder.Services.AddScoped<IOaVehicleUseRequestRepository, FreeSqlVehicleUseRequestRepository>();
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<VehicleComplianceReminderService>();
builder.Services.AddScoped<IOaVehicleUseWorkflowApprover>(sp => sp.GetRequiredService<VehicleService>());
builder.Services.AddScoped<IOaVehicleMaintenanceRepository, FreeSqlVehicleMaintenanceRepository>();
builder.Services.AddScoped<VehicleMaintenanceService>();
builder.Services.AddScoped<IOaExpenseReimbursementRepository, FreeSqlExpenseReimbursementRepository>();
builder.Services.AddScoped<IOaExpenseLineRepository, FreeSqlExpenseLineRepository>();
builder.Services.AddScoped<ExpenseReimbursementService>();
builder.Services.AddScoped<ExpenseReimbursementPaymentService>();
builder.Services.AddScoped<IOaExpenseReimbursementWorkflowApprover>(sp => sp.GetRequiredService<ExpenseReimbursementService>());
builder.Services.AddScoped<IOaCashAdvanceRepository, FreeSqlCashAdvanceRepository>();
builder.Services.AddScoped<IOaCashAdvanceOffsetRepository, FreeSqlCashAdvanceOffsetRepository>();
builder.Services.AddScoped<IOaCashAdvanceRepaymentRepository, FreeSqlCashAdvanceRepaymentRepository>();
builder.Services.AddScoped<CashAdvanceService>();
builder.Services.AddScoped<IOaCashAdvanceWorkflowApprover>(sp => sp.GetRequiredService<CashAdvanceService>());
builder.Services.AddScoped<CashAdvanceRepaymentService>();
builder.Services.AddScoped<IOaCashAdvanceRepaymentWorkflowApprover>(sp => sp.GetRequiredService<CashAdvanceRepaymentService>());
    builder.Services.AddScoped<IOaPaymentRequestRepository, FreeSqlPaymentRequestRepository>();
    builder.Services.AddScoped<IOaPaymentRequestStatusHistoryRepository, FreeSqlPaymentRequestStatusHistoryRepository>();
    builder.Services.AddScoped<IOaPaymentBudgetRepository, FreeSqlPaymentBudgetRepository>();
    builder.Services.AddScoped<IOaPaymentBudgetReservationRepository, FreeSqlPaymentBudgetReservationRepository>();
    builder.Services.AddScoped<PaymentBudgetService>();
    builder.Services.AddScoped<PaymentRequestService>();
    builder.Services.AddScoped<IOaPaymentRequestWorkflowApprover>(sp => sp.GetRequiredService<PaymentRequestService>());
    builder.Services.AddScoped<IOaPaymentExecutionRepository, FreeSqlPaymentExecutionRepository>();
    builder.Services.AddScoped<PaymentExecutionService>();
    builder.Services.AddScoped<IOaPaymentBatchRepository, FreeSqlPaymentBatchRepository>();
    builder.Services.AddScoped<IOaPaymentBatchItemRepository, FreeSqlPaymentBatchItemRepository>();
    builder.Services.AddScoped<PaymentBatchService>();
builder.Services.AddScoped<IOaProcurementRequestRepository, FreeSqlProcurementRequestRepository>();
builder.Services.AddScoped<IOaProcurementRequestLineRepository, FreeSqlProcurementRequestLineRepository>();
builder.Services.AddScoped<IOaProcurementBudgetRepository, FreeSqlProcurementBudgetRepository>();
builder.Services.AddScoped<IOaProcurementBudgetReservationRepository, FreeSqlProcurementBudgetReservationRepository>();
builder.Services.AddScoped<ProcurementBudgetService>();
builder.Services.AddScoped<IOaProcurementSourcingRepository, FreeSqlProcurementSourcingRepository>();
builder.Services.AddScoped<IOaProcurementSourcingQuoteRepository, FreeSqlProcurementSourcingQuoteRepository>();
builder.Services.AddScoped<ProcurementSourcingService>();
builder.Services.AddScoped<ProcurementSourcingPurchaseOrderService>();
builder.Services.AddScoped<ProcurementRequestService>();
builder.Services.AddScoped<ProcurementRequestPurchaseOrderService>();
builder.Services.AddScoped<IOaProcurementRequestWorkflowApprover>(sp => sp.GetRequiredService<ProcurementRequestService>());
builder.Services.AddScoped<IAnnouncementRepository, FreeSqlAnnouncementRepository>();
builder.Services.AddScoped<AnnouncementService>();
builder.Services.AddScoped<IWorkScheduleRepository, FreeSqlWorkScheduleRepository>();
builder.Services.AddScoped<WorkScheduleService>();
builder.Services.AddScoped<ICustomerRepository, FreeSqlCustomerRepository>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ICustomerContactRepository, FreeSqlCustomerContactRepository>();
builder.Services.AddScoped<CustomerContactService>();
builder.Services.AddScoped<ICustomerFollowUpRepository, FreeSqlCustomerFollowUpRepository>();
builder.Services.AddScoped<CustomerFollowUpService>();
builder.Services.AddScoped<ISalesOpportunityRepository, FreeSqlSalesOpportunityRepository>();
builder.Services.AddScoped<SalesOpportunityService>();
builder.Services.AddScoped<ISalesContractRepository, FreeSqlSalesContractRepository>();
builder.Services.AddScoped<SalesContractService>();
builder.Services.AddScoped<ISalesContractWorkflowApprover>(sp => sp.GetRequiredService<SalesContractService>());
builder.Services.AddScoped<IProductRepository, FreeSqlProductRepository>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<IWarehouseRepository, FreeSqlWarehouseRepository>();
builder.Services.AddScoped<WarehouseService>();
builder.Services.AddScoped<ISupplierRepository, FreeSqlSupplierRepository>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<IPmpProjectRepository, FreeSqlPmpProjectRepository>();
builder.Services.AddScoped<IPmpProjectStatusHistoryRepository, FreeSqlPmpProjectStatusHistoryRepository>();
builder.Services.AddScoped<PmpProjectService>();
builder.Services.AddScoped<IPmpProjectPhaseRepository, FreeSqlPmpProjectPhaseRepository>();
builder.Services.AddScoped<PmpProjectPhaseService>();
builder.Services.AddScoped<IPmpProjectCalendarOverrideRepository, FreeSqlPmpProjectCalendarOverrideRepository>();
builder.Services.AddScoped<PmpProjectCalendarService>();
builder.Services.AddScoped<IPmpWbsTaskRepository, FreeSqlPmpWbsTaskRepository>();
builder.Services.AddScoped<PmpWbsTaskService>();
builder.Services.AddScoped<IPmpProjectMemberRepository, FreeSqlPmpProjectMemberRepository>();
builder.Services.AddScoped<PmpProjectMemberService>();
builder.Services.AddScoped<IPmpProjectIssueRepository, FreeSqlPmpProjectIssueRepository>();
builder.Services.AddScoped<PmpProjectIssueService>();
builder.Services.AddScoped<IPmpProjectWorkItemRepository, FreeSqlPmpProjectWorkItemRepository>();
builder.Services.AddScoped<IPmpProjectWorkItemActivityRepository, FreeSqlPmpProjectWorkItemActivityRepository>();
builder.Services.AddScoped<PmpProjectWorkItemService>();
builder.Services.AddScoped<IPmpProjectWorkItemWorkflowApprover>(sp => sp.GetRequiredService<PmpProjectWorkItemService>());
builder.Services.AddScoped<PmpProjectWorkItemReminderService>();
builder.Services.AddHostedService<PmpProjectWorkItemReminderWorker>();
builder.Services.AddHostedService<VehicleComplianceReminderWorker>();
builder.Services.AddScoped<IPmpProjectMeetingRepository, FreeSqlPmpProjectMeetingRepository>();
builder.Services.AddScoped<PmpProjectMeetingService>();
builder.Services.AddScoped<IPmpDeliveryRecordRepository, FreeSqlPmpDeliveryRecordRepository>();
builder.Services.AddScoped<IPmpDeliveryRecordStatusHistoryRepository, FreeSqlPmpDeliveryRecordStatusHistoryRepository>();
builder.Services.AddScoped<PmpDeliveryRecordService>();
builder.Services.AddScoped<IPmpRequirementRepository, FreeSqlPmpRequirementRepository>();
builder.Services.AddScoped<PmpRequirementService>();
builder.Services.AddScoped<IPmpProjectBaselineRepository, FreeSqlPmpProjectBaselineRepository>();
builder.Services.AddScoped<PmpProjectBaselineService>();
builder.Services.AddScoped<IPmpProjectChangeRepository, FreeSqlPmpProjectChangeRepository>();
builder.Services.AddScoped<PmpProjectChangeService>();
builder.Services.AddScoped<IPmpProjectChangeWorkflowApprover>(sp => sp.GetRequiredService<PmpProjectChangeService>());
builder.Services.AddScoped<ILmsLicenseRepository, FreeSqlLmsLicenseRepository>();
builder.Services.AddScoped<ILmsLicenseReplacementRequestRepository, FreeSqlLmsLicenseReplacementRequestRepository>();
builder.Services.AddScoped<LmsLicenseReplacementRequestService>();
builder.Services.AddScoped<ILmsLicenseProductRepository, FreeSqlLmsLicenseProductRepository>();
builder.Services.AddScoped<LmsLicenseProductService>();
builder.Services.AddScoped<ILmsFeatureRepository, FreeSqlLmsFeatureRepository>();
builder.Services.AddScoped<LmsFeatureService>();
builder.Services.AddScoped<ILmsFeatureVersionRepository, FreeSqlLmsFeatureVersionRepository>();
builder.Services.AddScoped<LmsFeatureVersionService>();
builder.Services.AddScoped<ILmsCustomerMachineRepository, FreeSqlLmsCustomerMachineRepository>();
builder.Services.AddScoped<LmsCustomerMachineService>();
builder.Services.AddScoped<LmsCustomerReferenceService>();
builder.Services.AddScoped<ILmsCustomerFeatureRepository, FreeSqlLmsCustomerFeatureRepository>();
builder.Services.AddScoped<LmsCustomerFeatureService>();
builder.Services.AddScoped<ILmsMachineFeatureRepository, FreeSqlLmsMachineFeatureRepository>();
builder.Services.AddScoped<LmsMachineFeatureService>();
builder.Services.AddScoped<LmsLicenseService>();
builder.Services.AddScoped<LmsLicenseAccessService>();
builder.Services.AddScoped<LmsLicenseAttachmentService>();
builder.Services.AddScoped<LmsLicenseRequestDetailService>();
builder.Services.AddScoped<LmsLicenseExpiryReminderService>();
builder.Services.AddScoped<LmsLicenseOperationsSnapshotService>();
builder.Services.AddHostedService<LmsLicenseExpiryReminderWorker>();
builder.Services.AddHostedService<NotificationFailureRetryWorker>();
builder.Services.AddHostedService<SimpleFormCompletionOutboxWorker>();
builder.Services.AddScoped<IPmpWorkLogRepository, FreeSqlPmpWorkLogRepository>();
builder.Services.AddScoped<PmpWorkLogService>();
builder.Services.AddScoped<IPmpWeeklyWorkLogSubmissionRepository, FreeSqlPmpWeeklyWorkLogSubmissionRepository>();
builder.Services.AddScoped<PmpWeeklyWorkLogSubmissionService>();
builder.Services.AddScoped<PmpWeeklyWorkLogSubmissionWorkflowHistoryService>();
builder.Services.AddScoped<PmpWeeklyWorkLogOutcomeNotificationService>();
builder.Services.AddScoped<IPmpWeeklyWorkLogSubmissionWorkflowApprover>(sp => sp.GetRequiredService<PmpWeeklyWorkLogSubmissionService>());
builder.Services.AddScoped<PmpEvmService>();
builder.Services.AddScoped<IPurchaseOrderRepository, FreeSqlPurchaseOrderRepository>();
builder.Services.AddScoped<PurchaseOrderService>();
builder.Services.AddScoped<IPurchaseOrderWorkflowApprover>(sp => sp.GetRequiredService<PurchaseOrderService>());
builder.Services.AddScoped<IInventoryTransactionRepository, FreeSqlInventoryTransactionRepository>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ISalesOrderRepository, FreeSqlSalesOrderRepository>();
builder.Services.AddScoped<SalesOrderService>();
builder.Services.AddScoped<ISalesOrderWorkflowApprover>(sp => sp.GetRequiredService<SalesOrderService>());
builder.Services.AddScoped<CrossModuleSearchService>();
builder.Services.AddScoped<ISettlementRepository, FreeSqlSettlementRepository>();
builder.Services.AddScoped<SettlementService>();
builder.Services.AddScoped<IWorkflowActionHandler, ErpSettlementWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, PurchaseOrderWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, SalesOrderWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, SalesContractWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, PmpProjectChangeWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, PmpProjectWorkItemWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, PmpWeeklyWorkLogSubmissionWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, LmsLicenseWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, LmsLicenseReplacementWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, ExpenseReimbursementWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, CashAdvanceWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, CashAdvanceRepaymentWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, PaymentRequestWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, ProcurementRequestWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, LeaveRequestWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, OvertimeRequestWorkflowActionHandler>();
builder.Services.AddScoped<ISimpleFormDefinitionRepository, FreeSqlSimpleFormDefinitionRepository>();
builder.Services.AddScoped<ISimpleFormDefinitionVersionRepository, FreeSqlSimpleFormDefinitionVersionRepository>();
builder.Services.AddScoped<ISimpleFormSubmissionRepository, FreeSqlSimpleFormSubmissionRepository>();
builder.Services.AddScoped<ISimpleFormWorkflowSnapshotRepository, FreeSqlSimpleFormWorkflowSnapshotRepository>();
builder.Services.AddScoped<ISimpleFormCompletionEventRepository, FreeSqlSimpleFormCompletionEventRepository>();
builder.Services.AddScoped<SimpleFormCompletionOutboxService>();
builder.Services.AddScoped<SimpleFormService>();
builder.Services.AddScoped<SimpleFormAttachmentService>();
builder.Services.AddScoped<ISimpleFormSubmissionWorkflowApprover>(sp => sp.GetRequiredService<SimpleFormService>());
builder.Services.AddScoped<ISimpleFormCompletionHandler, NoopSimpleFormCompletionHandler>();
builder.Services.AddScoped<ISimpleFormCompletionHandler, SealRequestNotificationHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, SimpleFormSubmissionWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, VehicleUseRequestWorkflowActionHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, AssetRequestWorkflowActionHandler>();
builder.Services.AddScoped<WorkflowActionExecutor>();
builder.Services.AddScoped<IAttachmentRepository, FreeSqlAttachmentRepository>();
builder.Services.AddScoped<IAttachmentAuditRepository, FreeSqlAttachmentAuditRepository>();
builder.Services.AddScoped<IAttachmentAccessPolicy, DefaultAttachmentAccessPolicy>();
builder.Services.AddSingleton<IAttachmentContentScanner, BasicAttachmentContentScanner>();
builder.Services.AddScoped<IAttachmentContentStore>(_ => new LocalAttachmentContentStore(builder.Environment.ContentRootPath));
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<IWorkflowDefinitionRepository, FreeSqlWorkflowDefinitionRepository>();
builder.Services.AddScoped<WorkflowDefinitionService>();
builder.Services.AddScoped<IWorkflowInstanceRepository, FreeSqlWorkflowInstanceRepository>();
builder.Services.AddScoped<IWorkflowOperationRepository, FreeSqlWorkflowOperationRepository>();
builder.Services.AddScoped<WorkflowOperationService>();
builder.Services.AddScoped<WorkflowInstanceService>();
builder.Services.AddScoped<WorkflowBindingService>(sp => new WorkflowBindingService(
    sp.GetRequiredService<WorkflowDefinitionService>(),
    sp.GetRequiredService<WorkflowInstanceService>(),
    tasks: sp.GetRequiredService<WorkflowTaskService>(),
    transactions: sp.GetRequiredService<IWorkflowTransactionBoundary>(),
    serviceProvider: sp));
builder.Services.AddScoped<WorkflowApprovalService>();
builder.Services.AddScoped<IWorkflowTaskRepository, FreeSqlWorkflowTaskRepository>();
builder.Services.AddScoped<IWorkflowTransactionBoundary, FreeSqlWorkflowTransactionBoundary>();
builder.Services.AddScoped<IWorkflowRoleApproverLookup, FreeSqlWorkflowRoleApproverLookup>();
builder.Services.AddScoped<IWorkflowOrganizationApproverLookup, FreeSqlWorkflowOrganizationApproverLookup>();
builder.Services.AddScoped<IWorkflowBusinessApproverSource, PmpProjectChangeWorkflowApproverSource>();
builder.Services.AddScoped<IWorkflowBusinessApproverLookup, DefaultWorkflowBusinessApproverLookup>();
builder.Services.AddScoped<IWorkflowApproverResolver, DefaultWorkflowApproverResolver>();
// WorkflowTaskService 的动作执行器会解析业务处理器，而业务处理器又依赖 WorkflowApprovalService -> WorkflowBindingService。
// 通过工厂延迟解析动作执行器和运行时，避免 Web 容器在创建待办服务时形成循环依赖；纯引擎测试仍可显式传入这些依赖。
builder.Services.AddScoped<WorkflowTaskService>(sp => new WorkflowTaskService(
    sp.GetRequiredService<IWorkflowTaskRepository>(),
    sp.GetRequiredService<WorkflowInstanceService>(),
    notifications: sp.GetRequiredService<NotificationService>(),
    operations: sp.GetRequiredService<WorkflowOperationService>(),
    transactions: sp.GetRequiredService<IWorkflowTransactionBoundary>(),
    approverResolver: sp.GetRequiredService<IWorkflowApproverResolver>(),
    serviceProvider: sp));
builder.Services.AddScoped<WorkflowRuntimeService>();
builder.Services.AddScoped<IUserMenuPreferenceRepository, FreeSqlUserMenuPreferenceRepository>();
builder.Services.AddScoped<UserMenuPreferenceService>();
builder.Services.AddScoped<INotificationRepository, FreeSqlNotificationRepository>();
builder.Services.AddScoped<IWorkNotificationRecipientProvider, FreeSqlWorkNotificationRecipientProvider>();
builder.Services.AddScoped<INotificationFailureRecorder, FreeSqlNotificationFailureRecorder>();
builder.Services.AddScoped<INotificationFailureRepository, FreeSqlNotificationFailureRepository>();
builder.Services.AddScoped<INotificationFailureAuditRepository, FreeSqlNotificationFailureAuditRepository>();
builder.Services.AddScoped<NotificationFailureRetryService>();
builder.Services.AddScoped<IExternalNotificationRecipientResolver, EmployeeProfileExternalNotificationRecipientResolver>();
builder.Services.AddScoped<IExternalNotificationDispatcher, ExternalNotificationDispatcher>();
builder.Services.AddScoped<IExternalNotificationOutboxRepository, FreeSqlExternalNotificationOutboxRepository>();
builder.Services.AddScoped<ExternalNotificationOutboxService>();
builder.Services.AddConfiguredExternalNotificationEmail(builder.Configuration);
builder.Services.AddHostedService<ExternalNotificationOutboxWorker>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<CrossModuleReminderService>();
builder.Services.AddHostedService<CrossModuleReminderWorker>();

builder.AddAdminBlazor(new AdminBlazorOptions
{
    DebugTenantId = "main",
    Assemblies = [typeof(Program).Assembly]
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(AdminBlazor.Components.Pages.Login).Assembly);
app.MapGet("/api/attachments/{id:guid}/download", async (Guid id, HttpContext httpContext, AttachmentService attachmentService, IAttachmentRepository attachmentRepository, IAttachmentContentStore contentStore, IAdminSessionService sessions, VelrixWorkHub.Application.Lms.LmsLicenseAccessService lmsAccess, SimpleFormAttachmentService simpleFormAttachments, CancellationToken cancellationToken) =>
{
    try
    {
        var session = await sessions.LoadAsync(httpContext.Request.Cookies["AdminBlazor_Auth"], cancellationToken);
        if (session is null) return Results.Forbid();
        var attachment = attachmentRepository.List(includeDeleted: false).FirstOrDefault(x => x.Id == id);
        if (attachment is null) return Results.NotFound();
        var isAdministrator = session.Roles.Any(x => x.IsAdministrator);
        if (attachment.BusinessType.Equals(nameof(LmsLicenseRequest), StringComparison.OrdinalIgnoreCase)
            && !lmsAccess.CanReadRequest(attachment.BusinessId, session.User.Username, isAdministrator))
            return Results.Forbid();
        if (attachment.BusinessType.Equals(nameof(SimpleFormSubmission), StringComparison.OrdinalIgnoreCase))
            _ = simpleFormAttachments.List(attachment.BusinessId, session.User.Id);
        var actor = session.User.Username;
        var download = await attachmentService.DownloadAsync(id, actor, contentStore, cancellationToken);
        return Results.File(download.Content, download.Item.ContentType, download.Item.FileName);
    }
    catch (FileNotFoundException) { return Results.NotFound(); }
    catch (UnauthorizedAccessException) { return Results.Forbid(); }
    catch (InvalidDataException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity); }
});
app.UseAdminOmniApi();

using (var scope = app.Services.CreateScope())
{
    var fsql = scope.ServiceProvider.GetRequiredService<FreeSqlCloud<string>>();
    WorkHubSeedData.Initialize(fsql);
}

Log.Information("Velrix Work Hub started in {EnvironmentName}", app.Environment.EnvironmentName);

try
{
    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Velrix Work Hub terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
