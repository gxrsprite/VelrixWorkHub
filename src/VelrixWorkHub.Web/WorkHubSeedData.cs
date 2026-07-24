using AdminBlazor;
using BootstrapBlazor.Components;
using FreeSql;
using VelrixWorkHub.Infrastructure.Tasks;
using VelrixWorkHub.Infrastructure.Employees;
using VelrixWorkHub.Infrastructure.Recruitment;
using VelrixWorkHub.Infrastructure.Onboarding;
using VelrixWorkHub.Infrastructure.Offboarding;
using VelrixWorkHub.Infrastructure.Assets;
using VelrixWorkHub.Infrastructure.Leave;
using VelrixWorkHub.Infrastructure.Overtime;
using VelrixWorkHub.Infrastructure.Vehicles;
using VelrixWorkHub.Infrastructure.Announcements;
using VelrixWorkHub.Infrastructure.Schedules;
using VelrixWorkHub.Infrastructure.Customers;
using VelrixWorkHub.Infrastructure.Products;
using VelrixWorkHub.Infrastructure.Warehouses;
using VelrixWorkHub.Infrastructure.Suppliers;
using VelrixWorkHub.Infrastructure.PmpProjects;
using VelrixWorkHub.Infrastructure.Lms;
using VelrixWorkHub.Infrastructure.PurchaseOrders;
using VelrixWorkHub.Infrastructure.Inventory;
using VelrixWorkHub.Infrastructure.SalesOrders;
using VelrixWorkHub.Infrastructure.Attachments;
using VelrixWorkHub.Infrastructure.ExpenseReimbursements;
using VelrixWorkHub.Infrastructure.CashAdvances;
using VelrixWorkHub.Infrastructure.PaymentRequests;
using VelrixWorkHub.Infrastructure.ProcurementRequests;
using VelrixWorkHub.Infrastructure.Workflow;
using VelrixWorkHub.Infrastructure.SimpleForms;
using VelrixWorkHub.Infrastructure.Navigation;
using VelrixWorkHub.Infrastructure.Notifications;

namespace VelrixWorkHub.Web;

internal static class WorkHubSeedData
{
    public static void Initialize(FreeSqlCloud<string> fsql)
    {
        // Non-administrator menu authorization reads this explicit many-to-many mapping.
        fsql.CodeFirst.SyncStructure<SysRoleMenu>();

        if (fsql.Select<SysTenant>().Where(item => item.Id == "main").First() == null)
        {
            fsql.Insert(new SysTenant
            {
                Id = "main",
                Title = "Velrix Work Hub",
                Host = "localhost",
                IsEnabled = true
            }).ExecuteAffrows();
        }

        var administratorRole = fsql.Select<SysRole>().Where(item => item.IsAdministrator).First();
        if (administratorRole == null)
        {
            administratorRole = new SysRole
            {
                Id = Guid.CreateVersion7(),
                Name = "管理员",
                IsAdministrator = true
            };
            fsql.Insert(administratorRole).ExecuteAffrows();
        }

        var administrator = fsql.Select<SysUser>().Where(item => item.Username == "admin").First();
        if (administrator == null)
        {
            administrator = new SysUser
            {
                Id = Guid.CreateVersion7(),
                Username = "admin",
                PasswordHash = PasswordHasher.Hash("admin"),
                Nickname = "系统管理员",
                IsEnabled = true,
                CreatedTime = DateTime.Now,
                CreatedUserName = "system"
            };
            fsql.Insert(administrator).ExecuteAffrows();
        }

        if (!fsql.Select<SysRoleUser>().Any(item => item.UserId == administrator.Id && item.RoleId == administratorRole.Id))
        {
            fsql.Insert(new SysRoleUser { UserId = administrator.Id, RoleId = administratorRole.Id }).ExecuteAffrows();
        }

        var oaRoot = EnsureRootMenu(fsql, "协同办公", "fa fa-sitemap", 10);
        EnsureChildMenu(fsql, oaRoot, "OA 工作台", "Oa/Overview", 100);
        EnsureChildMenu(fsql, oaRoot, "我的任务", "Oa/Task", 101);
        EnsureChildMenu(fsql, oaRoot, "公告中心", "Oa/Announcement", 102);
        EnsureChildMenu(fsql, oaRoot, "我的日程", "Oa/Schedule", 103);
        EnsureChildMenu(fsql, oaRoot, "通知中心", "Oa/Notification", 104);
        var employeeDirectoryMenu = EnsureChildMenu(fsql, oaRoot, "员工通讯录", "Oa/Directory", 105);
        EnsureButtonMenu(fsql, employeeDirectoryMenu, "编辑员工档案", "Oa/Directory/Edit", 106);
        var recruitmentMenu = EnsureChildMenu(fsql, oaRoot, "招聘与面试", "Oa/Recruitment", 107);
        EnsureButtonMenu(fsql, recruitmentMenu, "新建候选人", "Oa/Recruitment/Create", 108);
        EnsureButtonMenu(fsql, recruitmentMenu, "编辑候选人", "Oa/Recruitment/Edit", 109);
        EnsureButtonMenu(fsql, recruitmentMenu, "安排与评价面试", "Oa/Recruitment/Interview", 110);
        EnsureButtonMenu(fsql, recruitmentMenu, "变更招聘状态", "Oa/Recruitment/Status", 111);
        var onboardingMenu = EnsureChildMenu(fsql, oaRoot, "入职办理", "Oa/Onboarding", 112);
        EnsureButtonMenu(fsql, onboardingMenu, "新建入职办理", "Oa/Onboarding/Create", 113);
        EnsureButtonMenu(fsql, onboardingMenu, "维护入职信息", "Oa/Onboarding/Edit", 114);
        EnsureButtonMenu(fsql, onboardingMenu, "完成入职", "Oa/Onboarding/Complete", 115);
        var offboardingMenu = EnsureChildMenu(fsql, oaRoot, "离职办理", "Oa/Offboarding", 116);
        EnsureButtonMenu(fsql, offboardingMenu, "新建离职办理", "Oa/Offboarding/Create", 117);
        EnsureButtonMenu(fsql, offboardingMenu, "维护离职信息", "Oa/Offboarding/Edit", 118);
        EnsureButtonMenu(fsql, offboardingMenu, "完成离职", "Oa/Offboarding/Complete", 119);
        var assetMenu = EnsureChildMenu(fsql, oaRoot, "资产与办公用品", "Oa/Asset", 149);
        EnsureButtonMenu(fsql, assetMenu, "维护资产", "Oa/Asset/Manage", 150);
        EnsureButtonMenu(fsql, assetMenu, "资产领用归还", "Oa/Asset/Assign", 152);
        EnsureButtonMenu(fsql, assetMenu, "转移资产", "Oa/Asset/Transfer", 184);
        EnsureButtonMenu(fsql, assetMenu, "资产盘点", "Oa/Asset/Stocktake", 185);
        EnsureButtonMenu(fsql, assetMenu, "维护办公用品库存", "Oa/Asset/Consumable", 186);
        EnsureButtonMenu(fsql, assetMenu, "新建资产申请", "Oa/Asset/Request/Create", 180);
        EnsureButtonMenu(fsql, assetMenu, "编辑资产申请", "Oa/Asset/Request/Edit", 181);
        EnsureButtonMenu(fsql, assetMenu, "提交资产申请", "Oa/Asset/Request/Submit", 182);
        EnsureButtonMenu(fsql, assetMenu, "撤回资产申请", "Oa/Asset/Request/Cancel", 183);
        var leaveMenu = EnsureChildMenu(fsql, oaRoot, "请假申请", "Oa/Leave", 120);
        EnsureButtonMenu(fsql, leaveMenu, "新建请假", "Oa/Leave/Create", 121);
        EnsureButtonMenu(fsql, leaveMenu, "编辑请假", "Oa/Leave/Edit", 122);
        EnsureButtonMenu(fsql, leaveMenu, "提交请假", "Oa/Leave/Submit", 123);
        EnsureButtonMenu(fsql, leaveMenu, "撤回请假", "Oa/Leave/Cancel", 124);
        EnsureChildMenu(fsql, oaRoot, "请假日历", "Oa/LeaveCalendar", 125);
        var leaveBalanceMenu = EnsureChildMenu(fsql, oaRoot, "请假额度", "Oa/LeaveBalance", 150);
        EnsureButtonMenu(fsql, leaveBalanceMenu, "维护请假额度", "Oa/LeaveBalance/Manage", 151);
        var overtimeMenu = EnsureChildMenu(fsql, oaRoot, "加班申请", "Oa/Overtime", 153);
        EnsureButtonMenu(fsql, overtimeMenu, "新建加班", "Oa/Overtime/Create", 154);
        EnsureButtonMenu(fsql, overtimeMenu, "编辑加班", "Oa/Overtime/Edit", 155);
        EnsureButtonMenu(fsql, overtimeMenu, "提交加班", "Oa/Overtime/Submit", 156);
        EnsureButtonMenu(fsql, overtimeMenu, "撤回加班", "Oa/Overtime/Cancel", 157);
        var overtimeFinanceMenu = EnsureChildMenu(fsql, oaRoot, "加班费财务处理", "Oa/OvertimeFinance", 158);
        EnsureButtonMenu(fsql, overtimeFinanceMenu, "完成加班费财务处理", "Oa/OvertimeFinance/Process", 159);
        var expenseMenu = EnsureChildMenu(fsql, oaRoot, "费用报销", "Oa/ExpenseReimbursement", 125);
        EnsureButtonMenu(fsql, expenseMenu, "新建报销", "Oa/ExpenseReimbursement/Create", 126);
        EnsureButtonMenu(fsql, expenseMenu, "编辑报销", "Oa/ExpenseReimbursement/Edit", 127);
        EnsureButtonMenu(fsql, expenseMenu, "提交报销", "Oa/ExpenseReimbursement/Submit", 128);
        EnsureButtonMenu(fsql, expenseMenu, "撤回报销", "Oa/ExpenseReimbursement/Cancel", 129);
        EnsureButtonMenu(fsql, expenseMenu, "创建报销付款申请", "Oa/ExpenseReimbursement/CreatePaymentRequest", 130);
        var cashAdvanceMenu = EnsureChildMenu(fsql, oaRoot, "借款与备用金", "Oa/CashAdvance", 130);
        EnsureButtonMenu(fsql, cashAdvanceMenu, "新建借款", "Oa/CashAdvance/Create", 131);
        EnsureButtonMenu(fsql, cashAdvanceMenu, "编辑借款", "Oa/CashAdvance/Edit", 132);
        EnsureButtonMenu(fsql, cashAdvanceMenu, "提交借款", "Oa/CashAdvance/Submit", 133);
        EnsureButtonMenu(fsql, cashAdvanceMenu, "撤回借款", "Oa/CashAdvance/Cancel", 134);
        EnsureButtonMenu(fsql, cashAdvanceMenu, "登记报销冲销", "Oa/CashAdvance/Offset", 135);
        EnsureButtonMenu(fsql, cashAdvanceMenu, "登记借款还款", "Oa/CashAdvance/Repayment", 136);
        EnsureButtonMenu(fsql, cashAdvanceMenu, "编辑借款还款", "Oa/CashAdvance/Repayment/Edit", 137);
        EnsureButtonMenu(fsql, cashAdvanceMenu, "重新提交借款还款", "Oa/CashAdvance/Repayment/Submit", 138);
        EnsureButtonMenu(fsql, cashAdvanceMenu, "撤回借款还款", "Oa/CashAdvance/Repayment/Cancel", 139);
        var paymentRequestMenu = EnsureChildMenu(fsql, oaRoot, "付款申请", "Oa/PaymentRequest", 136);
        EnsureButtonMenu(fsql, paymentRequestMenu, "新建付款申请", "Oa/PaymentRequest/Create", 137);
        EnsureButtonMenu(fsql, paymentRequestMenu, "编辑付款申请", "Oa/PaymentRequest/Edit", 138);
        EnsureButtonMenu(fsql, paymentRequestMenu, "提交付款申请", "Oa/PaymentRequest/Submit", 139);
        EnsureButtonMenu(fsql, paymentRequestMenu, "撤回付款申请", "Oa/PaymentRequest/Cancel", 140);
        EnsureButtonMenu(fsql, paymentRequestMenu, "财务复核付款申请", "Oa/PaymentRequest/FinanceReview", 158);
        EnsureButtonMenu(fsql, paymentRequestMenu, "登记实际付款", "Oa/PaymentRequest/RegisterPayment", 159);
        var paymentBudgetMenu = EnsureChildMenu(fsql, oaRoot, "付款预算", "Oa/PaymentBudget", 160);
        EnsureButtonMenu(fsql, paymentBudgetMenu, "新建付款预算", "Oa/PaymentBudget/Create", 161);
        EnsureButtonMenu(fsql, paymentBudgetMenu, "关闭付款预算", "Oa/PaymentBudget/Close", 162);
        var paymentBatchMenu = EnsureChildMenu(fsql, oaRoot, "付款批次", "Oa/PaymentBatch", 163);
        EnsureButtonMenu(fsql, paymentBatchMenu, "新建付款批次", "Oa/PaymentBatch/Create", 164);
        EnsureButtonMenu(fsql, paymentBatchMenu, "加入付款申请", "Oa/PaymentBatch/Add", 165);
        EnsureButtonMenu(fsql, paymentBatchMenu, "移除付款申请", "Oa/PaymentBatch/Remove", 166);
        EnsureButtonMenu(fsql, paymentBatchMenu, "提交付款批次", "Oa/PaymentBatch/Submit", 167);
        EnsureButtonMenu(fsql, paymentBatchMenu, "撤回付款批次", "Oa/PaymentBatch/Cancel", 168);
        var procurementRequestMenu = EnsureChildMenu(fsql, oaRoot, "采购申请", "Oa/ProcurementRequest", 141);
        EnsureButtonMenu(fsql, procurementRequestMenu, "新建采购申请", "Oa/ProcurementRequest/Create", 142);
        EnsureButtonMenu(fsql, procurementRequestMenu, "编辑采购申请", "Oa/ProcurementRequest/Edit", 143);
        EnsureButtonMenu(fsql, procurementRequestMenu, "提交采购申请", "Oa/ProcurementRequest/Submit", 144);
        EnsureButtonMenu(fsql, procurementRequestMenu, "撤回采购申请", "Oa/ProcurementRequest/Cancel", 145);
        EnsureButtonMenu(fsql, procurementRequestMenu, "采购复核并生成订单", "Oa/ProcurementRequest/GeneratePurchaseOrder", 154);
        var procurementBudgetMenu = EnsureChildMenu(fsql, oaRoot, "采购预算", "Oa/ProcurementBudget", 169);
        EnsureButtonMenu(fsql, procurementBudgetMenu, "新建采购预算", "Oa/ProcurementBudget/Create", 170);
        EnsureButtonMenu(fsql, procurementBudgetMenu, "关闭采购预算", "Oa/ProcurementBudget/Close", 171);
        var procurementSourcingMenu = EnsureChildMenu(fsql, oaRoot, "采购寻源", "Oa/ProcurementSourcing", 172);
        EnsureButtonMenu(fsql, procurementSourcingMenu, "新建寻源单", "Oa/ProcurementSourcing/Create", 173);
        EnsureButtonMenu(fsql, procurementSourcingMenu, "录入供应商报价", "Oa/ProcurementSourcing/AddQuote", 174);
        EnsureButtonMenu(fsql, procurementSourcingMenu, "提交寻源比价", "Oa/ProcurementSourcing/Submit", 175);
        EnsureButtonMenu(fsql, procurementSourcingMenu, "选择中选报价", "Oa/ProcurementSourcing/Award", 176);
        EnsureButtonMenu(fsql, procurementSourcingMenu, "撤回寻源单", "Oa/ProcurementSourcing/Cancel", 177);
        EnsureButtonMenu(fsql, procurementSourcingMenu, "中选报价转采购订单", "Oa/ProcurementSourcing/CreatePurchaseOrder", 178);
        var vehicleMenu = EnsureChildMenu(fsql, oaRoot, "车辆管理", "Oa/Vehicle", 146);
        EnsureButtonMenu(fsql, vehicleMenu, "新增车辆", "Oa/Vehicle/Create", 147);
        EnsureButtonMenu(fsql, vehicleMenu, "编辑车辆", "Oa/Vehicle/Edit", 148);
        EnsureButtonMenu(fsql, vehicleMenu, "新建用车申请", "Oa/Vehicle/Request", 149);
        EnsureButtonMenu(fsql, vehicleMenu, "提交用车申请", "Oa/Vehicle/Submit", 150);
        EnsureButtonMenu(fsql, vehicleMenu, "撤回用车申请", "Oa/Vehicle/Cancel", 151);
        EnsureButtonMenu(fsql, vehicleMenu, "车辆归还", "Oa/Vehicle/Return", 152);
        EnsureButtonMenu(fsql, vehicleMenu, "登记车辆维修", "Oa/Vehicle/Maintenance", 153);
        var crmRoot = EnsureRootMenu(fsql, "客户经营", "fa fa-handshake-o", 20);
        EnsureChildMenu(fsql, crmRoot, "CRM 经营看板", "Crm/Overview", 200);
        EnsureChildMenu(fsql, crmRoot, "客户列表", "Crm/Customer", 201);
        EnsureChildMenu(fsql, crmRoot, "联系人", "Crm/Contact", 202);
        EnsureChildMenu(fsql, crmRoot, "客户跟进", "Crm/FollowUp", 203);
        EnsureChildMenu(fsql, crmRoot, "商机管理", "Crm/Opportunity", 204);
        EnsureChildMenu(fsql, crmRoot, "合同管理", "Crm/Contract", 205);
        EnsureChildMenu(fsql, crmRoot, "客户交易视图", "Crm/CustomerLedger", 206);
        EnsureChildMenu(fsql, crmRoot, "合同订单追溯", "Crm/ContractLedger", 207);
        var erpRoot = EnsureRootMenu(fsql, "企业资源", "fa fa-cubes", 30);
        EnsureChildMenu(fsql, erpRoot, "商品主数据", "Erp/Product", 301);
        EnsureChildMenu(fsql, erpRoot, "ERP 运营概览", "Erp/Overview", 300);
        EnsureChildMenu(fsql, erpRoot, "ERP 基础报表", "Erp/Report", 309);
        EnsureChildMenu(fsql, erpRoot, "收付款核销", "Erp/Settlement", 310);
        EnsureChildMenu(fsql, erpRoot, "供应商交易视图", "Erp/SupplierLedger", 311);
        EnsureChildMenu(fsql, erpRoot, "仓库与库位", "Erp/Warehouse", 302);
        EnsureChildMenu(fsql, erpRoot, "供应商主数据", "Erp/Supplier", 303);
        EnsureChildMenu(fsql, erpRoot, "采购订单", "Erp/PurchaseOrder", 304);
        EnsureChildMenu(fsql, erpRoot, "库存流水", "Erp/Inventory", 305);
        EnsureChildMenu(fsql, erpRoot, "销售订单", "Erp/SalesOrder", 306);
        EnsureChildMenu(fsql, erpRoot, "库存调拨", "Erp/InventoryTransfer", 307);
        EnsureChildMenu(fsql, erpRoot, "库存盘点", "Erp/InventoryStocktake", 308);
        var pmpRoot = EnsureRootMenu(fsql, "项目管理", "fa fa-briefcase", 40);
        EnsureChildMenu(fsql, pmpRoot, "项目组合概览", "Pmp/Overview", 400);
        var projectMenu = EnsureChildMenu(fsql, pmpRoot, "项目主数据", "Pmp/Project", 401);
        EnsureButtonMenu(fsql, projectMenu, "新建/编辑项目", "Pmp/Project/Edit", 410);
        EnsureButtonMenu(fsql, projectMenu, "变更项目状态", "Pmp/Project/Status", 411);
        EnsureChildMenu(fsql, pmpRoot, "阶段与里程碑", "Pmp/Phase", 402);
        EnsureChildMenu(fsql, pmpRoot, "项目工作日历", "Pmp/Calendar", 414);
        EnsureChildMenu(fsql, pmpRoot, "WBS 任务树", "Pmp/Wbs", 403);
        EnsureChildMenu(fsql, pmpRoot, "项目成员与角色", "Pmp/Member", 404);
        EnsureChildMenu(fsql, pmpRoot, "风险与问题", "Pmp/Issue", 405);
        var workItemMenu = EnsureChildMenu(fsql, pmpRoot, "项目工作项", "Pmp/WorkItem", 412);
        EnsureButtonMenu(fsql, workItemMenu, "新建工作项", "Pmp/WorkItem/Create", 421);
        EnsureButtonMenu(fsql, workItemMenu, "编辑工作项", "Pmp/WorkItem/Edit", 422);
        EnsureButtonMenu(fsql, workItemMenu, "推进工作项", "Pmp/WorkItem/Status", 423);
        EnsureButtonMenu(fsql, workItemMenu, "添加工作项批注", "Pmp/WorkItem/Comment", 424);
        EnsureButtonMenu(fsql, workItemMenu, "提交工作项验收", "Pmp/WorkItem/Submit", 425);
        EnsureButtonMenu(fsql, workItemMenu, "撤回工作项验收", "Pmp/WorkItem/Withdraw", 426);
        var meetingMenu = EnsureChildMenu(fsql, pmpRoot, "项目会议", "Pmp/Meeting", 413);
        EnsureButtonMenu(fsql, meetingMenu, "新建会议", "Pmp/Meeting/Create", 431);
        EnsureButtonMenu(fsql, meetingMenu, "编辑会议", "Pmp/Meeting/Edit", 432);
        EnsureButtonMenu(fsql, meetingMenu, "创建会议行动项", "Pmp/Meeting/ActionItem", 433);
        var deliveryMenu = EnsureChildMenu(fsql, pmpRoot, "交付追溯", "Pmp/Delivery", 414);
        EnsureButtonMenu(fsql, deliveryMenu, "新建交付记录", "Pmp/Delivery/Create", 441);
        EnsureButtonMenu(fsql, deliveryMenu, "编辑交付记录", "Pmp/Delivery/Edit", 442);
        EnsureButtonMenu(fsql, deliveryMenu, "推进交付状态", "Pmp/Delivery/Status", 443);
        EnsureChildMenu(fsql, pmpRoot, "需求管理", "Pmp/Requirement", 410);
        EnsureChildMenu(fsql, pmpRoot, "团队资源分配", "Pmp/Resource", 411);
        EnsureChildMenu(fsql, pmpRoot, "项目基线", "Pmp/Baseline", 406);
        EnsureChildMenu(fsql, pmpRoot, "项目变更", "Pmp/Change", 407);
        EnsureChildMenu(fsql, pmpRoot, "项目工时", "Pmp/WorkLog", 408);
        EnsureChildMenu(fsql, pmpRoot, "项目 EVM", "Pmp/Evm", 409);
        var workflowRoot = EnsureRootMenu(fsql, "流程平台", "fa fa-random", 50);
        EnsureChildMenu(fsql, workflowRoot, "流程工作台", "Workflow/Overview", 500);
        var workflowDefinitionMenu = EnsureChildMenu(fsql, workflowRoot, "流程定义", "Workflow/Definition", 501);
        EnsureButtonMenu(fsql, workflowDefinitionMenu, "新建流程", "Workflow/Definition/Create", 511);
        EnsureButtonMenu(fsql, workflowDefinitionMenu, "发布流程", "Workflow/Definition/Publish", 512);
        EnsureButtonMenu(fsql, workflowDefinitionMenu, "归档流程", "Workflow/Definition/Archive", 513);
        EnsureButtonMenu(fsql, workflowDefinitionMenu, "删除草稿", "Workflow/Definition/Delete", 514);
        var simpleFormMenu = EnsureChildMenu(fsql, workflowRoot, "简单表单", "Workflow/SimpleForm", 503);
        EnsureButtonMenu(fsql, simpleFormMenu, "新建表单", "Workflow/SimpleForm/Create", 531);
        EnsureButtonMenu(fsql, simpleFormMenu, "编辑表单", "Workflow/SimpleForm/Edit", 532);
        EnsureButtonMenu(fsql, simpleFormMenu, "发布表单", "Workflow/SimpleForm/Publish", 533);
        EnsureButtonMenu(fsql, simpleFormMenu, "新建申请", "Workflow/SimpleForm/Submission/Create", 534);
        EnsureButtonMenu(fsql, simpleFormMenu, "编辑申请", "Workflow/SimpleForm/Submission/Edit", 535);
        EnsureButtonMenu(fsql, simpleFormMenu, "提交申请", "Workflow/SimpleForm/Submission/Submit", 536);
        EnsureButtonMenu(fsql, simpleFormMenu, "撤回申请", "Workflow/SimpleForm/Submission/Cancel", 537);
        var workflowInboxMenu = EnsureChildMenu(fsql, workflowRoot, "审批收件箱", "Workflow/Inbox", 502);
        EnsureButtonMenu(fsql, workflowInboxMenu, "同意", "Workflow/Inbox/Approve", 521);
        EnsureButtonMenu(fsql, workflowInboxMenu, "拒绝", "Workflow/Inbox/Reject", 522);
        EnsureButtonMenu(fsql, workflowInboxMenu, "退回", "Workflow/Inbox/Return", 523);
        EnsureButtonMenu(fsql, workflowInboxMenu, "转交", "Workflow/Inbox/Transfer", 524);
        EnsureButtonMenu(fsql, workflowInboxMenu, "撤回", "Workflow/Inbox/Withdraw", 525);
        EnsureButtonMenu(fsql, workflowInboxMenu, "重试失败节点", "Workflow/Inbox/Retry", 526);
        var lmsRoot = EnsureRootMenu(fsql, "许可证管理", "fa fa-key", 60);
        EnsureChildMenu(fsql, lmsRoot, "许可证运营概览", "Lms/Overview", 600);
        EnsureChildMenu(fsql, lmsRoot, "许可证产品", "Lms/Product", 601);
        EnsureChildMenu(fsql, lmsRoot, "许可证特性", "Lms/Feature", 602);
        EnsureChildMenu(fsql, lmsRoot, "特性版本与等级", "Lms/FeatureVersion", 603);
        EnsureChildMenu(fsql, lmsRoot, "客户机台", "Lms/Machine", 604);
        EnsureChildMenu(fsql, lmsRoot, "客户特性", "Lms/CustomerFeature", 605);
        EnsureChildMenu(fsql, lmsRoot, "机台特性", "Lms/MachineFeature", 606);
        var lmsLicenseMenu = EnsureChildMenu(fsql, lmsRoot, "许可证申请与授权", "Lms/License", 607);
        EnsureButtonMenu(fsql, lmsLicenseMenu, "新建申请", "Lms/License/Create", 611);
        EnsureButtonMenu(fsql, lmsLicenseMenu, "提交审批", "Lms/License/Submit", 612);
        EnsureButtonMenu(fsql, lmsLicenseMenu, "登记外部授权", "Lms/License/Register", 613);
        EnsureButtonMenu(fsql, lmsLicenseMenu, "变更授权生命周期", "Lms/License/Lifecycle", 614);
        EnsureButtonMenu(fsql, lmsLicenseMenu, "删除草稿申请", "Lms/License/DeleteDraft", 615);
        EnsureButtonMenu(fsql, lmsLicenseMenu, "取消许可证申请", "Lms/License/Cancel", 616);
        var lmsReplacementMenu = EnsureChildMenu(fsql, lmsRoot, "授权续期、重发与换机", "Lms/LicenseReplacement", 608);
        EnsureButtonMenu(fsql, lmsReplacementMenu, "创建并提交替代审批", "Lms/LicenseReplacement/CreateSubmit", 621);
        EnsureButtonMenu(fsql, lmsReplacementMenu, "重新提交替代审批", "Lms/LicenseReplacement/Resubmit", 622);
        var adminRoot = EnsureRootMenu(fsql, "系统管理", "fa fa-cog", 70);
        EnsureChildMenu(fsql, adminRoot, "系统运维工作台", "Admin/Overview", 708);
        var notificationFailuresMenu = EnsureChildMenu(fsql, adminRoot, "通知失败处置", "Admin/NotificationFailures", 709);
        EnsureButtonMenu(fsql, notificationFailuresMenu, "手动重试", "Admin/NotificationFailures/Retry", 710);
        EnsureButtonMenu(fsql, notificationFailuresMenu, "批量重试", "Admin/NotificationFailures/BatchRetry", 711);
        EnsureChildMenu(fsql, adminRoot, "权限变更审计", "Admin/PermissionAudit", 712);
        EnsureChildMenu(fsql, adminRoot, "站外通知队列", "Admin/ExternalNotificationOutbox", 713);
        WorkTaskSeedData.Initialize(fsql.Use("main"));
        AnnouncementSeedData.Initialize(fsql.Use("main"));
        WorkScheduleSeedData.Initialize(fsql.Use("main"));
        CustomerSeedData.Initialize(fsql.Use("main"));
        CustomerContactSeedData.Initialize(fsql.Use("main"));
        CustomerFollowUpSeedData.Initialize(fsql.Use("main"));
        SalesOpportunitySeedData.Initialize(fsql.Use("main"));
        SalesContractSeedData.Initialize(fsql.Use("main"));
        ProductSeedData.Initialize(fsql.Use("main"));
        WarehouseSeedData.Initialize(fsql.Use("main"));
        SupplierSeedData.Initialize(fsql.Use("main"));
        PmpProjectSeedData.Initialize(fsql.Use("main"));
        PmpProjectPhaseSeedData.Initialize(fsql.Use("main"));
        PmpWbsTaskSeedData.Initialize(fsql.Use("main"));
        PmpProjectMemberSeedData.Initialize(fsql.Use("main"));
        PmpProjectIssueSeedData.Initialize(fsql.Use("main"));
        PmpRequirementSeedData.Initialize(fsql.Use("main"));
        PmpProjectBaselineSeedData.Initialize(fsql.Use("main"));
        PmpProjectChangeSeedData.Initialize(fsql.Use("main"));
        PmpWorkLogSeedData.Initialize(fsql.Use("main"));
        PurchaseOrderSeedData.Initialize(fsql.Use("main"));
        InventorySeedData.Initialize(fsql.Use("main"));
        SalesOrderSeedData.Initialize(fsql.Use("main"));
        fsql.CodeFirst.SyncStructure<BusinessAttachmentRecord>();
        fsql.CodeFirst.SyncStructure<AttachmentAuditRecord>();
        fsql.CodeFirst.SyncStructure<WorkflowDefinitionRecord>();
        fsql.CodeFirst.SyncStructure<WorkflowInstanceRecord>();
        fsql.CodeFirst.SyncStructure<WorkflowTaskRecord>();
        fsql.CodeFirst.SyncStructure<WorkflowOperationRecord>();
        fsql.CodeFirst.SyncStructure<UserMenuPreferenceRecord>();
        fsql.CodeFirst.SyncStructure<NotificationRecord>();
        NotificationSchemaMigration.EnsureReadAtHasNoServerDefault(fsql.Use("main"));
        fsql.CodeFirst.SyncStructure<ExternalNotificationOutboxRecord>();
        fsql.CodeFirst.SyncStructure<NotificationFailureRecord>();
        fsql.CodeFirst.SyncStructure<NotificationFailureAuditRecord>();
        fsql.CodeFirst.SyncStructure<OaEmployeeProfileRecord>();
        fsql.CodeFirst.SyncStructure<OaRecruitmentCandidateRecord>();
        fsql.CodeFirst.SyncStructure<OaRecruitmentInterviewRecord>();
        fsql.CodeFirst.SyncStructure<OaOnboardingRecord>();
        fsql.CodeFirst.SyncStructure<OaOffboardingRecord>();
        fsql.CodeFirst.SyncStructure<OaAssetRecord>();
        fsql.CodeFirst.SyncStructure<OaAssetAssignmentRecord>();
        fsql.CodeFirst.SyncStructure<OaAssetOperationRecord>();
        fsql.CodeFirst.SyncStructure<OaAssetTransferRecord>();
        fsql.CodeFirst.SyncStructure<OaAssetStocktakeRecord>();
        fsql.CodeFirst.SyncStructure<OaConsumableSupplyRecord>();
        fsql.CodeFirst.SyncStructure<OaConsumableTransactionRecord>();
        fsql.CodeFirst.SyncStructure<OaAssetRequestRecord>();
        fsql.CodeFirst.SyncStructure<OaLeaveRequestRecord>();
        fsql.CodeFirst.SyncStructure<OaLeaveCalendarEntryRecord>();
        fsql.CodeFirst.SyncStructure<OaLeaveBalanceRecord>();
        fsql.CodeFirst.SyncStructure<OaLeaveBalanceReservationRecord>();
        fsql.CodeFirst.SyncStructure<OaOvertimeRequestRecord>();
        fsql.CodeFirst.SyncStructure<OaOvertimeConversionRecord>();
        fsql.CodeFirst.SyncStructure<OaExpenseReimbursementRecord>();
        fsql.CodeFirst.SyncStructure<OaExpenseLineRecord>();
        fsql.CodeFirst.SyncStructure<OaCashAdvanceRecord>();
        fsql.CodeFirst.SyncStructure<OaCashAdvanceOffsetRecord>();
        fsql.CodeFirst.SyncStructure<OaCashAdvanceRepaymentRecord>();
        fsql.CodeFirst.SyncStructure<OaVehicleMaintenanceRecord>();
        fsql.CodeFirst.SyncStructure<SimpleFormDefinitionRecord>();
        fsql.CodeFirst.SyncStructure<SimpleFormDefinitionVersionRecord>();
        fsql.CodeFirst.SyncStructure<SimpleFormSubmissionRecord>();
        fsql.CodeFirst.SyncStructure<SimpleFormWorkflowSnapshotRecord>();
        fsql.CodeFirst.SyncStructure<SimpleFormCompletionEventRecord>();
        fsql.CodeFirst.SyncStructure<PmpProjectWorkItemRecord>();
        fsql.CodeFirst.SyncStructure<PmpProjectWorkItemActivityRecord>();
        fsql.CodeFirst.SyncStructure<PmpProjectMeetingRecord>();
        fsql.CodeFirst.SyncStructure<PmpDeliveryRecordRecord>();
        fsql.CodeFirst.SyncStructure<PmpDeliveryRecordStatusHistoryRecord>();
        fsql.CodeFirst.SyncStructure<OaPaymentRequestRecord>();
        fsql.CodeFirst.SyncStructure<OaPaymentExecutionRecord>();
        fsql.CodeFirst.SyncStructure<OaPaymentRequestStatusHistoryRecord>();
        fsql.CodeFirst.SyncStructure<OaPaymentBudgetRecord>();
        fsql.CodeFirst.SyncStructure<OaPaymentBudgetReservationRecord>();
        fsql.CodeFirst.SyncStructure<OaPaymentBatchRecord>();
        fsql.CodeFirst.SyncStructure<OaPaymentBatchItemRecord>();
        fsql.CodeFirst.SyncStructure<OaProcurementRequestRecord>();
        fsql.CodeFirst.SyncStructure<OaProcurementRequestLineRecord>();
        fsql.CodeFirst.SyncStructure<OaProcurementBudgetRecord>();
        fsql.CodeFirst.SyncStructure<OaProcurementBudgetReservationRecord>();
        fsql.CodeFirst.SyncStructure<OaProcurementSourcingRecord>();
        fsql.CodeFirst.SyncStructure<OaProcurementSourcingQuoteRecord>();
        fsql.CodeFirst.SyncStructure<LmsLicenseRequestRecord>();
        fsql.CodeFirst.SyncStructure<LmsLicenseProductRecord>();
        fsql.CodeFirst.SyncStructure<LmsFeatureRecord>();
        fsql.CodeFirst.SyncStructure<LmsFeatureVersionRecord>();
        fsql.CodeFirst.SyncStructure<LmsCustomerMachineRecord>();
        fsql.CodeFirst.SyncStructure<LmsCustomerFeatureRecord>();
        fsql.CodeFirst.SyncStructure<LmsMachineFeatureRecord>();
        if (!fsql.Select<LmsLicenseProductRecord>().Any(x => x.Code == "LMS-CORE")) fsql.Insert(new LmsLicenseProductRecord { Id = Guid.CreateVersion7(), Code = "LMS-CORE", Name = "Velrix", Description = "默认许可证产品", OtherInfo = "{}", Status = VelrixWorkHub.Domain.LmsLicenseProductStatus.Active, CreatedAt = DateTime.Now }).ExecuteAffrows();
        fsql.CodeFirst.SyncStructure<LmsLicenseAuthorizationRecord>();
        fsql.CodeFirst.SyncStructure<LmsLicenseLifecycleEntryRecord>();
        fsql.CodeFirst.SyncStructure<LmsLicenseReplacementRequestRecord>();
        LmsLicenseReplacementRequestSchemaMigration.EnsureSubmittedRequestUniqueness(fsql.Use("main"));
        WorkflowSchemaMigration.BackfillInitialRevisions(fsql.Use("main"));
        WorkflowSchemaMigration.EnsureDefinitionVersionUniqueness(fsql.Use("main"));
        WorkflowSchemaMigration.EnsureRunningBusinessUniqueness(fsql.Use("main"));
        WorkflowSeedData.Initialize(fsql.Use("main"));
        SimpleFormSeedData.Initialize(fsql.Use("main"));
    }

    private static SysMenu EnsureRootMenu(FreeSqlCloud<string> fsql, string label, string icon, int sort)
    {
        var existing = fsql.Select<SysMenu>().Where(item => item.Label == label && (item.Path ?? "") == "").First();
        if (existing != null) return existing;

        var root = new SysMenu
        {
            Id = Guid.CreateVersion7(),
            Label = label,
            Icon = icon,
            Path = string.Empty,
            Sort = sort,
            Type = SysMenuType.菜单,
            IsSystem = true
        };
        fsql.Insert(root).ExecuteAffrows();
        return root;
    }

    private static SysMenu EnsureChildMenu(FreeSqlCloud<string> fsql, SysMenu parent, string label, string path, int sort)
    {
        var existing = fsql.Select<SysMenu>().Where(item => item.Path == path).First();
        if (existing == null)
        {
            existing = new SysMenu
            {
                Id = Guid.CreateVersion7(), ParentId = parent.Id, Label = label, Path = path,
                Sort = sort, Type = SysMenuType.菜单, IsSystem = true
            };
            fsql.Insert(existing).ExecuteAffrows();
        }
        else if (existing.ParentId != parent.Id || existing.Label != label)
        {
            existing.ParentId = parent.Id;
            existing.Label = label;
            existing.Sort = sort;
            existing.Type = SysMenuType.菜单;
            existing.IsSystem = true;
            fsql.Update<SysMenu>().SetSource(existing).ExecuteAffrows();
        }
        return existing;
    }

    private static void EnsureButtonMenu(FreeSqlCloud<string> fsql, SysMenu parent, string label, string path, int sort)
    {
        var existing = fsql.Select<SysMenu>().Where(item => item.Path == path).First();
        if (existing == null)
        {
            fsql.Insert(new SysMenu
            {
                Id = Guid.CreateVersion7(), ParentId = parent.Id, Label = label, Path = path,
                Sort = sort, Type = SysMenuType.按钮, IsSystem = true
            }).ExecuteAffrows();
        }
        else if (existing.ParentId != parent.Id || existing.Label != label || existing.Type != SysMenuType.按钮)
        {
            existing.ParentId = parent.Id;
            existing.Label = label;
            existing.Sort = sort;
            existing.Type = SysMenuType.按钮;
            existing.IsSystem = true;
            fsql.Update<SysMenu>().SetSource(existing).ExecuteAffrows();
        }
    }
}
