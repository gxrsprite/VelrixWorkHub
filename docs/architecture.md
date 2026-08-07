# 架构

## PMS 技术命名与数据库迁移

项目管理模块的代码命名空间、领域类型、Application 服务、Infrastructure 仓储、Web 页面目录和页面路由统一使用 `Pms`/`PMS`；数据库表和销售订单项目列也使用 `Pms...` 命名。现有 PostgreSQL 或 SQL Server 数据库不得依赖启动时自动建新表来完成改名，必须先执行 `scripts/migrate-project-module-to-pms-postgresql.sql` 或 `scripts/migrate-project-module-to-pms-sqlserver.sql`。脚本在事务内幂等改名 18 张项目表、`ErpSalesOrder.PmpProjectId` 列、菜单路径、Workflow 业务类型/编码、项目工作项来源以及通知去重键；检测到新旧对象同时存在时主动失败，不覆盖或合并数据。`PMS_*` 是当前流程编码，历史 `PMP_*` 数据由脚本迁移后再启动新版本宿主。

## 定位

OA 报销与付款申请的跨模块级联由 `ExpenseReimbursementPaymentService` 编排，不直接访问对方仓储：已批准报销按 `DocumentNo` 只允许生成一份 `EmployeePayment` 付款申请，付款申请以前置单据保存报销来源，创建成功后报销进入 `Reimbursed`。`PaymentExecutionService` 在员工付款登记前重新解析关联报销，校验申请人、金额和报销状态；付款申请与实际付款记录、报销 `Paid` 回写处于同一 Application 事务边界，重复创建返回既有付款申请，重复登记沿用付款流水幂等。借款冲销继续由 `CashAdvanceService` 通过 `ExpenseReimbursementService` 查询和更新，不跨模块直读表。

ERP 采购订单收货由 `PurchaseOrderService.Receive` 维护订单状态与库存流水的一致性：页面必须选择实际收货仓库，可选具体库位；服务先校验仓库/库位归属和稳定来源号，再在 `IWorkflowTransactionBoundary` 中将订单推进为 `Received`，并经 `InventoryService.Create` 写入唯一入库流水，因此收货同样复用商品/仓库启停、来源号和库位商品容量等库存维度门禁；数据库事务失败时使用仅供恢复的领域入口还原内存状态，已存在的 `{OrderNo}-IN` 来源号在写入前幂等拒绝。没有显式参数的旧 Application 调用只兼容选择首个启用仓库，Web 不使用该回退。页面只负责选择目标并触发用例，订单和库存规则仍由 Application/Domain 执行。

ERP 库存以不可变 `InventoryTransaction` 为唯一库存变动来源。`InventoryService` 同时投影商品/仓库、库位、批次和序列号余额；带批次号的出库只从相同商品、仓库、库位、批次及（传入时）保质期的批次余额扣减，不能以总库存绕过批次短缺。未指定批次的出库可选择 FIFO：仅从同商品、仓库和库位的正批次余额分配，按最早保质期（未填写保质期的批次最后）逐批写入独立的 `{来源单号}-Bnn` 出库流水；预检不足或单号冲突时不写入任何流水，Web PostgreSQL/SQL Server 宿主通过共享 `IWorkflowTransactionBoundary` 成组提交。FIFO 不消耗未批次化的通用库存，也不替代手工指定批次。序列号流水必须为单件数量；同商品的在库序列号只能存在一处，出库、调拨和负向盘点必须从相同仓库/库位的在库序列号扣减，调拨在调出后再写入调入流水以保留同一追溯标识。调拨将批次号、保质期和序列号原样写入调出/调入两条流水，并在同一事务边界成对提交，避免调入写入失败后遗留单边调出；批次盘点根据同一维度计算差异并新增调整流水，不改写历史。库位物理容量按“库位 + 商品”配置最大库存数量，避免把不同计量单位混入一个容量数值；入库、调拨调入和正向盘点在写入前以库位商品账面余额校验，调拨还会在调出前预检调入容量，失败不留下单边流水。`ExpiryAlerts` 只对正余额且已过期或未来 30 天到期的批次生成只读投影；`StagnantBatchAlerts` 只对正余额且最后流水距今至少 180 天的批次生成只读投影。库存页分别展示已过期/临期、呆滞批次和在库序列号，支持通过批次号或序列号精确筛选及 `batchNo`/`serialNo` 深链追溯对应余额、预警与流水，不自动调整库存或阻断其他交易。未指定批次的既有通用库存继续按仓库或库位余额处理。

商品可配置可选“最大库存”；`InventoryService.OverstockAlerts` 以所有仓库的账面流水余额合计为口径，只对启用商品且实际结存超过上限时投影超储预警。预警只读，不自动调账、不拦截入库/出库/调拨/盘点；它与会阻断写入的库位商品容量是两套边界，不能互相替代。

采购寻源由 `ProcurementSourcingService` 管理寻源单、供应商准入、报价、提交和定标；由于 `Sourcing` 类型申请按规则不绑定 ERP 商品，`ProcurementSourcingPurchaseOrderService` 生成订单时要求采购复核明确商品、数量和到期日，同时自动带入中选报价的供应商与报价金额。订单使用 `PurchaseOrderSourceKind.Sourcing` 和寻源编号作为来源，重复操作返回已有未取消订单，取消来源订单后才允许重新生单；寻源模块不直接访问采购订单表。

OA Workflow 结果通知由 `OaWorkflowOutcomeNotificationService` 统一编排，按平台用户 ID 解析启用申请人的用户名，覆盖请假、加班、报销、借款、借款还款和付款申请的批准/驳回结果。通知去重键固定包含业务类型、业务 ID、Workflow 实例 ID 和结果，驳回意见随通知内容保存；通知失败继续交给既有失败记录/重试边界，不阻断业务状态回写，也不创建 OA 私有通知模型。

Velrix Work Hub 是一个模块化单体，而不是把 OA、CRM、ERP 与 PMS 做成彼此隔离的系统。组织、用户、角色、菜单、文件、字典和审计能力由 `Admin` 提供；各业务模块仅实现自己的规则，通过稳定的应用用例和引用关系协作。

Workflow 模块概览 `/Workflow/Overview` 不保存统计副本，只聚合既有 Application 服务。审批摘要始终以当前会话用户名调用 `WorkflowTaskService.List(assignee, Pending)`，不接受查询参数或页面输入替代审批人；流程定义和简单表单摘要分别以 `Workflow/Definition`、`Workflow/SimpleForm` 菜单权限为门禁，简单表单申请只按当前会话用户 ID 调用 `SimpleFormService.ListMine`。因此概览不能成为读取全局待办、他人申请或未授权流程资产的旁路。

系统运维概览 `/Admin/Overview` 同样不保存统计副本。通知风险只在具备 `Admin/NotificationFailures` 菜单权限时通过 `NotificationFailureRetryService.InspectPending` 读取 Pending、高重试和最大重试计数；权限治理只在具备 `Admin/PermissionAudit` 菜单权限时通过 `IAdminPermissionAuditService.ListAsync` 读取最近审计窗口，再派生 24 小时变更摘要。站外队列由独立 `/Admin/ExternalNotificationOutbox` 菜单查看，只读展示渠道、通知类型、创建时间、重试次数、最后尝试和状态，不展示收件人、标题、正文、链接、去重键或错误正文。概览不读取通知失败的可重放负载，不提供重试、权限调整、删除或审计回放操作，所有明细与写操作仍分别在既有专用页面及其按钮权限下执行。

通知中心 `/Oa/Notification` 在读取列表前必须完成 `IAdminContext.InitAsync` 并验证 `Oa/Notification` 菜单权限；异步会话加载尚未完成时不能以空用户名执行一次性 `Reload` 后停留在空列表。简单表单的最终状态通过持久化 `SimpleFormCompletionEvent` Outbox 投递：印章申请 Approved 时由 `SealRequestNotificationHandler` 解析冻结数据中的受控 `recipient` 人员引用，向该启用目录人员发布幂等系统通知。页面只展示接收人为当前会话用户的通知，Outbox 的失败保留重试边界，不在通知页直接处理表单业务状态。非管理员菜单和按钮授权依赖 `SysRoleMenu` 显式关联，Web 初始化必须通过 CodeFirst 同步该表；权限查询再从已授权叶子补齐父级菜单，不能把管理员角色作为普通角色回归的替代。

### 平台前端内置化计划

Workflow 审批待办的模块映射由 `UnifiedTodoService` 统一维护；`OaCashAdvanceRepayment` 与借款、报销、付款申请一样归入 OA，标题显示“OA 借款还款审批”，并保留当前审批人的收件箱深链，避免回退到 ERP 默认分类。

付款申请在 `OA_PAYMENT_REQUEST_APPROVAL` 批准后进入独立财务复核边界：`PaymentRequestService.ReviewFinance` 只允许已批准申请由具备复核入口的当前操作者处理；通过后才允许后续登记付款，驳回会回到 OA `Rejected` 并保留复核人、时间和原因，申请人可编辑后重新提交，提交会清空旧复核结果。当前已增加 OA 付款预算台账：带预算编号的申请提交时按主体公司、部门、币种占用额度，驳回/撤回释放，实际付款转为已执行；预算只作为 OA 申请门禁，不替代 ERP 财务预算总账。付款批次首版由 `PaymentBatchService` 只对已批准且财务复核通过、尚未实际付款的申请组批，批次固定币种和金额汇总；提交前再次校验明细条数与汇总金额一致且至少一条明细，提交后不可改，撤回保留明细历史并允许重新组批。当前不触发银行指令或外部批量支付，FreeSql 使用字符串枚举和增量列同步 PostgreSQL/SQL Server。

请假审批通过后，`LeaveRequestService` 在同一 Application 事务边界调用 `LeaveCalendarService` 创建 `OaLeaveCalendarEntry`。该投影以 `LeaveRequestId` 唯一，冻结员工、请假类型、起止时间和事由，重复审批不重复创建；`/Oa/LeaveCalendar` 只按当前登录用户查询并提供原请假单深链。它不复用无人员归属的 `WorkSchedule`，不直接写入 PMS 项目日历、班次或考勤结果；代理人、部门审核、考勤计算和数据库级并发回归继续分段落地。

Workflow 待办处理在流程离开 Start 后要求待办节点属于实例当前活动节点，即使实例只有一个活动节点也不例外；这样历史待办在退回、推进或并发更新后只能被拒绝，不能继续触发审批动作或业务状态变更。Start 阶段保留手工构造待办的兼容性，供应用夹具和迁移场景使用。

统一工作台的 Workflow 待办继续按业务类型映射到所属模块；LMS 许可证申请和授权替代审批归入 LMS，而不是 ERP。首页通过同一 `UnifiedTodoService` 提供 LMS 筛选和原单深链，并从 `LmsLicenseOperationsSnapshotService` 读取申请总数、待审批、已批准、已取消和授权状态分布，状态卡片统一深链到许可证明细；授权生命周期近期活动从已有生命周期审计按时间倒序聚合最多 5 条，同样只读深链原授权，不复制第二套活动日志。`LmsLicenseRequestDetailService` 按申请 ID 聚合申请特性引用、Workflow 实例、`WorkflowOperation` 历史和统一 `AttachmentService` 的附件版本列表，详情页只读展示最近操作、实例数量及附件数量，不建立 LMS 私有审批或附件模型。`LmsLicenseService` 在新建申请和登记外部授权的 Application 入口拒绝当前或过去的到期时间，历史授权仍由查询派生 `Expired` 而不改写人工状态；申请提交的申请人通知在 Workflow 事务提交后发布，启动失败不留下“已提交审批”通知。`LmsLicenseAttachmentService` 只允许 Draft/Submitted/Rejected/Withdrawn 申请写入最多 6 个、单个不超过 2MB 且扩展名/MIME 匹配的附件，写入统一存储前调用可替换的 `IAttachmentContentScanner`，默认实现拒绝明显的可执行伪装和常见脚本载荷；`LmsLicenseAttachmentPanel` 提供详情页上传/删除和附件来源/扩展 `OtherInfo` 编辑。通用 `AttachmentPanel` 同样允许 CRM、ERP、PMS 业务附件录入并展示版本级 `OtherInfo`，审计操作者优先取当前登录用户，调用方的 `Actor` 仅作为无会话测试回退，避免来源元数据或操作人只在 LMS 可见。实际内容存储、版本、哈希和审计仍由 AttachmentService 完成。附件下载端点必须通过 Admin 会话，LMS 附件额外经过 `LmsLicenseAccessService` 的申请范围门禁。通知仍由 OA 的统一通知中心承载，LMS 不复制待办、已读或通知状态模型。

统一待办在各模块输入完成后按 `(Source, SourceId)` 形成稳定聚合键；同一来源对象因重复查询或事件重放进入集合时，只保留优先级最高、截止时间最早的一项，再统一排序。当前已接入 PMS 未完成且已逾期的阶段/里程碑，使用计划结束日作为截止日、保留 `Pms/Phase?projectId=...` 深链并按高优先级展示；ERP 商品在启用安全库存且汇总账面库存低于安全线时生成 `InventoryRisk` 高优先级待办，跳转商品主数据页；ERP 采购/销售订单的未核销余额使用订单到期日作为结算待办截止日，逾期自动提升为高优先级，历史未填写到期日的数据默认使用订单日期后 30 天；已完成或取消节点、未启用安全库存或库存达到安全线时不生成对应风险待办。`CrossModuleReminderService` 复用同一待办集合，将合同临期、逾期应收/应付、库存低于安全线、逾期项目节点和高优先级风险问题投影为 OA `Reminder` 通知，使用接收人加来源、对象、截止日和优先级组成稳定去重键；宿主每日扫描启用用户全集，通知失败继续由 `NotificationService` 记录为可重试后置失败，不改变主数据状态。首页筛选通过 `UnifiedTodoService.Filter` 统一执行模块与优先级组合条件，并保留来源顺序和计数口径，避免 Razor 页面复制聚合规则；人员、组织数据范围和通知关闭仍是后续范围。该去重只合并同一来源对象，不会把 Workflow 的不同审批待办或不同模块的后续动作错误合并。

CRM 客户与 ERP 商品、供应商、仓库主数据统一提供 `OtherInfo` JSON 对象扩展字段。Domain 入口通过 `JsonObjectValue` 将空值归一化为 `{}`，拒绝数组、`null` 和非法 JSON；FreeSql 记录使用非空文本列并由 CodeFirst 增量同步，历史行保持 `{}` 默认值。页面按已定义键提取并渲染业务字段，不直接编辑或展示该扩展对象的原始 JSON，也不把自定义字段提升为固定列；后续新增主数据扩展沿用同一校验和持久化边界。

自 2026-07-26 起，`OtherInfo` 的页面规则调整为：它仍是领域、存储和界面内部可提取的扩展载荷，但普通 UI 不得直接提供原始 JSON 的编辑框、确认摘要、卡片文本或附件版本文本；页面需要扩展字段时，只读取已定义键并渲染对应业务控件。LMS 申请、授权和附件面板、PMS 项目/需求/交付记录/会议/工作项编辑器，以及 OA 资产、资产申请、盘点和办公用品页面已按该规则直接收口；保留全局 UI 守卫兼容其余历史模板，但守卫不得隐藏同一行的其他业务信息。编辑既有业务记录时必须原样保留其 `OtherInfo`，页面隐藏不得删除或覆盖历史 JSON。

`/Admin/NotificationFailures` 只读加载 Pending 失败记录及独立 `NotificationFailureAudit` 审计，展示重试次数、最后尝试时间和最近人工处置结果；页面不读取或展示可重放负载正文。单条/批量重试仍由 `NotificationFailureRetryService` 和独立按钮权限负责，审计查询失败不能反向阻断通知补投。`/Admin/ExternalNotificationOutbox` 独立显示站外投递队列的安全元数据和 Pending/失败尝试摘要；Provider 缺失的记录保持 Pending，当前页面不提供绕过 Worker 的人工发送或删除，避免运维页面直接触发第三方网络调用。

LMS `/Lms/License` 页面沿用 Admin 的菜单/按钮权限模型：页面入口先校验 `Lms/License`，未授权用户不加载申请和授权列表；创建申请、提交/重提审批、登记外部授权、启停/作废生命周期、草稿删除和申请取消分别校验独立按钮路径。新建申请使用仅负责页面状态编排的三步向导：第一步收集申请上下文，第二步选择机台特性版本并校验 `OtherInfo` JSON 对象，第三步只读展示确认摘要；最终仍由 `LmsLicenseService` 一次性创建 Draft，不在步骤之间写入半成品业务记录。普通用户创建机台申请或兼容的无机台申请时，页面以当前登录用户作为只读申请人，Application 再次校验申请人与操作者一致；管理员可以代申请。页面隐藏按钮只是体验层，具体事件处理仍再次校验按钮权限；列表、详情深链和授权选择器统一经过 `LmsLicenseAccessService`，管理员看全量，普通用户只看本人申请及其关联授权，组织级数据范围后续按平台角色和 CRM 数据范围继续收口。
草稿删除由 `LmsLicenseService.DeleteDraft` 提供，仓储物理删除只允许 `Draft`，且 Application 层要求申请人本人或管理员；已提交或已有审批历史的申请一律拒绝，因此删除入口不会绕过 Workflow 的提交、撤回和审批记录。直接提交、启动审批提交及撤回后重提同样在 Application 层校验操作者范围，再访问 Workflow 绑定，页面权限不构成唯一安全边界。
关联申请登记外部 License 由 `RegisterExternalLicenseFromRequest` 复核当前操作者的 LMS 申请读取范围；普通用户只能登记本人申请，管理员可处理全量，越权校验在唯一编号、冲突检查和仓储写入前执行。无申请的外部登记仍要求非空操作者并沿用页面按钮权限边界。授权启用、停用和作废同样由 `ChangeAuthorization` 先复核授权是否属于当前用户可见范围；普通用户只能变更本人申请关联的授权，无关联申请的历史授权仅管理员可变更，状态和生命周期审计仍共用原事务边界。申请取消由 `LmsLicenseService.Cancel` 提供，仅允许 `Draft` 或 `Submitted`，且 Application 层要求操作者是申请人本人或管理员，不能仅依赖页面按钮权限。提交中的申请先通过 `WorkflowBindingService` 撤回运行实例和待办（不触发业务字段回写），再置为终态 `Cancelled`；`Cancelled` 不允许再次提交。状态成功持久化后向申请人发布带原因和原单深链的统一 OA 通知，通知按申请 ID 去重且失败不阻断主状态。Workflow 发起人撤回仍由通用 Workflow 记录为 `Withdrawn`，两者语义不混用。

`/Lms/LicenseReplacement` 使用同一边界：页面入口先校验菜单权限，创建并提交替代审批、驳回/撤回后的重新提交分别受按钮权限控制；替代申请列表在权限初始化完成前返回空集，避免未授权首屏数据闪现。页面权限不替代替代申请服务自身的原授权、唯一 Submitted 和 Workflow 发起人规则。

LMS 附件列表、上传和删除由 `LmsLicenseAttachmentService` 统一复核申请数据范围，普通用户只能操作本人申请，管理员可处理全量；详情页不直接调用通用 `AttachmentService.Delete`，下载端点在读取内容前同时校验 Admin 会话与 LMS 申请范围。附件 `OtherInfo` 由 Domain 统一校验为 JSON 对象并随版本保存，来源标识不提升为固定列；通用 AttachmentService 继续负责存储、版本、哈希和审计。

统一通知中心由 `NotificationService` 提供列表、未读统计、单条/批量已读、分页、单条删除和批量删除；分页由 `INotificationRepository.ListPage` 定义统一口径，未读数由 `Count` 定义统一口径，FreeSql 实现使用数据库 `COUNT` 与 `Skip/Take`，测试替身保留兼容默认实现。`ReadAt` 是业务可空状态，不得配置为 `ServerTime`；`NotificationSchemaMigration` 会清除历史 PostgreSQL 默认值并兼容 SQL Server 默认约束，避免新通知被错误写成已读。删除接口始终以接收人作为数据库条件，批量操作对不属于当前接收人的 ID 静默忽略，避免通过通知 ID 越权。通知删除采用物理删除，释放 `(Recipient, DedupeKey)` 唯一键，使失败重试或同一业务事件的后续投递不会被历史删除记录卡住；LMS 继续只发布通知事件和原单深链，不维护私有通知状态。

Workflow 条件节点只在输入上下文明确提供值时执行排序和文本比较；缺失值不会被转换为空字符串而误命中 `<=`、`contains` 等分支，只有显式与 `null` 比较才会匹配。条件表达式仍限制为字段比较、逻辑与/或及受控文本运算，不执行脚本或访问业务对象。

平台前端已内置到本仓库的 `src/VelrixWorkHub.Admin`，并由 `VelrixWorkHub.Web` 直接引用；项目/程序集名统一为 `VelrixWorkHub.Admin`，由 Work Hub 自己维护版本和发布边界。认证、菜单、`AdminContext`、登录页、静态资源和 Omni API 的行为由本项目统一维护。

Admin 模块采用稳定的菜单与按钮权限模型：页面入口先校验菜单权限，写操作再校验对应按钮权限；权限判断使用当前登录用户的菜单快照，未通过菜单校验的页面首屏也不加载业务列表，业务状态变更仍由 Application 服务负责。所有使用 `Admin.AuthButton` 的页面必须在自身初始化阶段等待 `Admin.InitAsync`，不能只依赖布局初始化，避免 Blazor 页面首次渲染时权限上下文为空而误隐藏授权按钮；该初始化只解决 UI 权限快照加载，服务端事件处理仍必须再次校验权限。Workflow 收件箱的审批动作与定义管理动作均按按钮路径拆分，定义页新建向导只负责把审批人、可选复审节点、`All/Any/Majority/Quorum` 策略和 Quorum 门槛编排为节点配置，复审节点通过 `returnTargets` 指向初审节点，发布校验仍由 Domain 执行；损坏的历史快照只影响对应入口，不应阻断整页渲染。这样可以避免仅依赖前端隐藏按钮，同时保持 Admin、Workflow 和业务模块之间的职责边界清晰。

`/Admin/PermissionAudit` 是权限集合审计的只读查询入口：页面只通过 `IAdminPermissionAuditService` 查询 `SysPermissionAuditLog`，支持主体类型、动作和主体 ID 筛选，并以折叠快照展示变更前后数据；它不直接访问角色、用户或菜单表，也不提供修改、删除或重放审计记录的操作。菜单权限仍在页面初始化时校验，后续组织级数据范围接入时沿用同一 Application 查询边界。

后续平台拆分遵循“Admin 纯前端、平台能力回到分层项目”的原则：核心平台实体、枚举和领域规则已先迁入 `VelrixWorkHub.Domain/Platform`，当前保留兼容命名空间和 FreeSql 映射行为；资源锁、登录失败限制、工作日历、变更通知、文件存储契约、安全路径策略、平台目录契约、权限查询契约、角色权限管理契约、用户角色分配契约和权限审计契约进入 `VelrixWorkHub.Application/Platform`；FreeSql 文件存储、权限查询、角色菜单/按钮授权替换、用户角色关联替换、权限集合审计、节假日加载器、Cron 调度器、管理会话和参数/字典目录实现进入 `VelrixWorkHub.Infrastructure/Platform`；Razor 页面、布局、输入控件、静态资源和 Blazor 会话状态保留在 `VelrixWorkHub.Admin`。Web 端的 HTTP 路由已按职责拆为 `AdminOperationalEndpoints`、`AdminIdentityEndpoints`、`AdminCatalogEndpoints` 和 `AdminProfileEndpoints`，鉴权、菜单树、响应模型集中在 `AdminApiSupport`/`AdminApiModels`，`AdminExtensions` 只负责宿主 DI、配置和模块装配。登录 Cookie 解密由 Infrastructure 的 `AdminAuthCookieService` 负责，用户、角色、租户和授权菜单由 `IAdminSessionService`/`AdminSessionService` 统一加载，菜单访问、按钮路径、管理员判断由 `IAdminPermissionService`/`AdminAuthorizationService` 统一处理，角色页的菜单/按钮授权由 `IAdminRolePermissionService`/`FreeSqlAdminRolePermissionService` 在事务中替换，用户页的角色分配由 `IAdminUserRoleService`/`FreeSqlAdminUserRoleService` 校验角色存在性、去重替换关联并保护最后一名管理员，角色权限和用户角色集合的前后差异由 `IAdminPermissionAuditService`/`FreeSqlAdminPermissionAuditService` 在同一事务内写入 `SysPermissionAuditLog`，参数保存校验、审计字段和字典树组装由 `IPlatformCatalogService`/`FreeSqlPlatformCatalogService` 统一处理，Admin 只保留面向 Blazor 的 `AdminContext` 会话门面；登录日志因暂时依赖 Bootstrap 的 `WebClientDeviceType` 仍留在 Admin。后续再评估正式命名空间重构和组织级数据范围。

平台服务依赖方向固定为：`Admin` 只引用 `Application` 的契约，`Web` 组合 `Application` 与 `Infrastructure`，`Infrastructure` 不引用 Admin。文件上传的 UI 和 HTTP 端点只依赖 `IFileService`，路径校验由 `FileStoragePathPolicy` 统一执行，FreeSql 和物理文件写入集中在 `FreeSqlFileService`；调度器对页面只暴露 `ICronScheduler`，避免 Razor 页面直接依赖 `BackgroundService` 实现；会话加载、权限判断、角色授权、用户角色分配和权限审计分别只通过 `IAdminSessionService`、`IAdminPermissionService`、`IAdminRolePermissionService`、`IAdminUserRoleService`、`IAdminPermissionAuditService` 暴露给 Admin，Cookie、角色关联、审计写入和 FreeSql 查询不进入页面。布局、页面和通用控件只依赖 `IAdminContext`，具体 `AdminContext` 作为兼容实现注册。兼容命名空间暂时保留，待页面回归稳定后再统一命名。

## 分层规则

| 层 | 职责 | 允许依赖 |
|---|---|---|
| Domain | 实体、值对象、领域规则与模块常量 | 无业务基础设施依赖 |
| Application | 用例编排、命令、查询、权限意图 | Domain |
| Infrastructure | FreeSql 映射、仓储实现、文件/消息等适配 | Application、Domain |
| Web | Razor 页面、HTTP 端点、菜单种子与依赖注入 | Application、Infrastructure、VelrixWorkHub.Admin |

跨模块调用必须经由 Application 用例，不直接跨模块读写对方表。

简单自定义表单使用 `SimpleFormDefinition`、`SimpleFormDefinitionVersion`、`SimpleFormSubmission`、`SimpleFormWorkflowSnapshot` 和 `SimpleFormCompletionEvent`，由 `SimpleFormService` 统一管理。Definition 保存稳定编码、绑定 Workflow 代码和完成事件代码；发布 Version 后 Schema JSON 不可修改，Submission 在创建时冻结 Version、Schema 和数据 JSON。每个 Workflow 实例以唯一 `WorkflowInstanceId` 保存独立快照，重提编辑不会覆盖旧审批的 Definition、申请人、版本、Schema 或数据。`/Workflow/SimpleForm` 以 HTML 两栏编辑器维护字段，半宽字段由 `SimpleFormSchema.GetLayoutRows` 自动配对，未配对时转为全宽；运行页按快照动态渲染文本、选择、多选、复选和引用控件，并在 Application 层拒绝未知字段、错误类型、无效选项和缺失必填。申请卡片按冻结 Schema 将选项 ID 显示为冻结标签、复选显示为“是/否”、多选显示为标签集合，而不是向用户暴露原始数据 JSON；损坏历史快照只显示安全降级提示。`ReferencePicker` 使用固定来源和 Schema 选项作为受控下拉，提交的引用 ID 与标签必须同时命中冻结选项；人员和部门引用还会校验当前启用人员/组织和规范标签，避免客户端伪造引用显示名。草稿或驳回申请可由原申请人回填同一冻结 Schema 后编辑，审批中和终态保持只读；定义新建/编辑/发布及申请新建/编辑/提交/撤回分别受既有 `Workflow/SimpleForm/*` 按钮权限控制，页面不把菜单可见性当作写入授权。Workflow 收件箱仅在当前登录用户已被分派的 `SimpleFormSubmission` 待办中读取实例快照，审批人不需要绕过待办隔离访问申请人页面。`SimpleFormSubmissionWorkflowActionHandler` 只通过 Application 回写批准/驳回；完成事件随主事务写入 Outbox，提交后尝试投递，失败保留 Pending、错误和重试次数，由 Worker 后续重试，唯一键防止同一终态重复投递。首版内置 `NONE` No-op 处理器；节点级字段权限仍未实现，业务处理器必须按 Submission ID 幂等，不能直接写其他模块表。

OA 借款余额以 `OaCashAdvance.SettledAmount` 作为唯一的已批准结清投影：报销冲销和借款还款分别保留自己的审计记录，但仅由 `CashAdvanceService` 在 Application 事务中更新借款结清金额。`CashAdvanceRepaymentService` 通过该服务校验申请人、借款状态和实时余额；`OA_CASH_ADVANCE_REPAYMENT_APPROVAL` 批准时才同时写入结清余额与还款批准状态，拒绝或撤回不改变余额。`/Oa/CashAdvance` 对驳回还款回填同一编辑表单并重新提交，草稿/审批中还款可以单条撤回；页面的 `Oa/CashAdvance/Repayment`、`/Edit`、`/Submit`、`/Cancel` 只控制入口，申请人和状态门禁仍由 Application 服务执行。该边界只记录 OA 还款意图和凭据，不写 ERP、银行或库存记录。

OA 加班申请由 `OaOvertimeRequest`、`OvertimeRequestService` 和 `IOaOvertimeRequestRepository` 管理，页面入口为 `/Oa/Overtime`。Application 仅允许申请人操作草稿、驳回或可撤回状态；提交时通过 `LeaveRequestService` 查询同一员工的提交中/已批准请假申请并拦截时间重叠，避免把人事事务表直接互相读写。`OA_OVERTIME_APPROVAL` 的 handler 只经 Application 回写批准或驳回状态。已批准加班可在结束后 30 天内由本人通过 `OaOvertimeConversion` 二选一兑换：调休累计同年度调休额度；财务处理冻结来源加班单和小时数，再由 `/Oa/OvertimeFinance` 按 `Oa/OvertimeFinance/Process` 权限写入一次性的已处理状态、处理人、时间和可选备注。唯一索引保证同一加班单不可重复或拆分兑换，Domain 拒绝把调休记录送入财务处理以及重复完成。金额、薪资、付款申请、ERP 核销和银行付款保持在独立财务体系；节假日规则、班次、出差联动与考勤回写在后续独立用例实现。

Workflow 定义以规范化大写 `Code + VersionNumber` 作为数据库唯一键。启动迁移在 PostgreSQL advisory lock 或 SQL Server `sp_getapplock` 内执行：只删除没有 `WorkflowInstance` 或 `WorkflowTask` 引用的同键冗余定义，优先保留被引用定义；同组存在多个被引用定义时拒绝创建唯一索引并要求人工处置。迁移不重写实例、待办、快照或审批历史。

持久化枚举统一使用真实的 Domain enum 属性，并在 FreeSql 记录属性上声明 `[Column(MapType = typeof(string), StringLength = 50)]`，将枚举映射为数据库字符串；仓储层不再把枚举回退为整数保存。

JSON 统一通过 `Domain.JsonSerializationDefaults` 和 Web HTTP JSON 配置注册 `JsonStringEnumConverter`，业务接口、流程定义文档和流程实例快照中的枚举均使用名称字符串，并通过 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` 保留中文原文。

站内通知统一使用 `WorkNotification`、`NotificationService` 和 `OaNotification` 持久化表；通知按接收人和去重键幂等，Workflow 审批待办创建时生成通知，审批处理或流程终止时通过事务提交回调标记相关通知已读，回滚时不提前改变通知状态。通知发布和已读同步属于后置副作用，失败由 `INotificationFailureRecorder` 记录到可替换的失败边界，不阻断审批、订单或核销主交易；持久化重试和审计可在该边界上继续接入。接收人按平台用户名大小写无关匹配，通知页面只读取当前登录用户的数据。站外渠道通过 `IExternalNotificationRecipientResolver`、`IExternalNotificationChannelProvider` 和 `IExternalNotificationDispatcher` 预留邮件、短信、企业微信、钉钉的统一适配入口：`EmployeeProfileExternalNotificationRecipientResolver` 仅从目录启用且员工状态为在职的 `OaEmployeeProfile` 读取邮箱、手机号、企业微信和钉钉用户标识，绝不从业务自由文本猜测地址；消息携带渠道级稳定去重键，单渠道异常只进入分发结果而不影响其他渠道。`NotificationService` 成功写入站内通知后才通过事务提交回调调用 `ExternalNotificationOutboxService.Enqueue`，将已解析的渠道地址和冻结消息原子写入 `OaExternalNotificationOutbox`；`ExternalNotificationOutboxWorker` 每五分钟按租约消费 Pending 记录，成功才标记 Delivered，Provider 缺失时保留 Pending，渠道异常保留错误和重试次数。SMTP 邮件 Provider 位于 Infrastructure，只有 `ExternalNotifications:Email:Enabled=true` 才注册；启用时必须提供有效主机、端口和发件地址，用户名与密码必须成对出现，密码仅从部署环境变量或密钥存储（例如 `ExternalNotifications__Email__Password`）读取，不写入仓库配置、业务表、Outbox、日志或运维页面。SMTP 消息使用稳定去重键派生的 `Message-Id`，仍依赖 Outbox 租约和渠道端去重应对“发送成功但写入 Delivered 前进程中断”的至少一次投递语义。短信、企业微信和钉钉在没有显式 Provider 时保持 Pending；真实 Provider 必须在 Infrastructure 中管理第三方配置保护和渠道幂等，不能直接在业务交易或 Razor 页面中发网络请求。

站外 Outbox 的运行态采用“缺少 Provider 保持立即可尝试、Provider 调用失败后退避”的不同语义：后者保存 `LastError`、`RetryCount` 与 `NextAttemptAt`，按 5、15、30、60 分钟再逐步加倍到 12 小时；Worker 只查询到期记录，运维页只展示渠道配置状态、待投递/延迟/失败/最高重试聚合数、下次尝试和既有安全元数据。达到 3 次重试的渠道由 Worker 输出不含地址或正文的渠道级告警。`IExternalNotificationChannelConfigurationProvider` 不得返回主机、账号、密钥、收件地址或消息负载。受控员工档案解析中无效邮箱或手机号只跳过对应渠道，手机号会在进入 Outbox 前规范化空格、连字符和括号，避免同一号码因显示格式不同失去去重。

## 模块边界

| 模块 | 首期范围 | 共享依赖 |
|---|---|---|
| OA | 公告、日程、任务、审批、知识文档 | 组织、用户、角色、文件、字典 |
| CRM | 客户、联系人、跟进、商机、合同 | 组织、用户、角色、文件、字典 |
| ERP | 商品、计量单位、仓库、库位、供应商；后续采购、销售、库存流水 | 组织、用户、角色、文件、字典、CRM 往来单位 |
| PMS | 项目、阶段、里程碑、WBS 任务、项目成员；后续基线、风险/问题、工时和 EVM | 组织、用户、角色、文件、审计、CRM 客户 |
| LMS | 许可证申请、Workflow 审批与外部 License 授权登记；后续客户/机台/特性主数据和生命周期 | Workflow、组织、用户、角色、文件、审计 |
| Workflow | 流程定义、版本、节点、连线、处理策略、实例、待办和审计；被 OA/CRM/ERP/PMS 业务引用 | 组织、用户、角色、文件、表单定义、审计 |
| Form | 表单定义、字段、校验、布局、版本和业务表单快照 | 组织、用户、角色、文件、字典 |
| Platform | 认证、菜单、权限、组织、文件、审计、定时任务 | VelrixWorkHub.Admin |

客户不属于 OA，审批模板不属于 CRM。ERP 供应商与 CRM 客户先保持独立主数据边界，后续通过往来单位引用统一；PMS 项目可引用 CRM 客户和合同，但不把 CRM/ERP 业务字段复制进项目表。各模块通过任务、日程、附件、负责人和单据引用等稳定关系协作，而不是互相嵌入业务表。

Workflow 只保存通用流程和运行状态，业务模块保存自己的单据和领域状态；表单数据以已发布表单版本快照进入流程实例。设计器保存设计态，运行时只消费不可变发布版本。

PMS 工时矩阵按项目、成员和日期汇总全部 WBS 单元格，同一成员同一项目同日累计不得超过 24 小时；更新既有单元格时先排除当前记录再汇总，避免重复计数。周工时审批批准或驳回后，只向以稳定用户 ID 绑定且仍启用的项目成员投递结果通知；无绑定、停用人员和通知失败都不阻断状态回写，通知沿用平台幂等、失败记录和站外 Outbox 边界。

PMS 周工时审批使用 `PmsWeeklyWorkLogSubmission`，按项目、成员和周一建立 JSON 工时快照及总小时数，不让审批读取后续可编辑的 `PmsWorkLog`；每条快照同时冻结 WBS ID、标题、日期、小时数、出勤状态和说明，任务改名或删除不改变历史回放。非管理员页面按当前登录用户 ID 调用 `SubmitForProjectMember`、`ListForProjectMember` 解析受控项目成员，项目下拉也只枚举其稳定成员关系项目；工时明细读取同样调用 `PmsWorkLogService.ListForProjectMember`，在 Application 层按稳定用户 ID 找到成员快照后再筛选，避免页面先读整项目明细再过滤。成员身份读取要求同一项目中该用户 ID 恰好对应一条成员关系；历史异常数据若有多条同用户绑定，则读取安全返回空、写入与提交安全拒绝，不随机选择一条关系或让页面抛出异常。矩阵成员行只匹配该稳定 ID，构造无成员关系的项目 ID 时安全清空选择且不构建矩阵，普通成员不展示全项目累计工时，不能以成员名称替他人提交或读取。项目成员服务禁止同一项目下重复目录人员或重复成员名称，维持既有工时姓名快照的唯一归属。同成员同项目同周只能有一条审批中或已批准的记录；Application 查询是首道门禁，持久化层再以 `ActiveWeekKey`（项目 + 大写成员名 + 周一）唯一索引处理跨进程并发，键只在 `Submitted`/`Approved` 保留，驳回或撤回即释放。创建和审批状态回写都将该唯一键冲突转换为统一业务错误，不暴露数据库索引名。重提不会改写旧驳回/撤回快照，而是从当前工时生成新的周报和流程实例，保留旧审批意见以审计。迁移会先回填历史键并拒绝带冲突数据的索引创建。提交必须已配置并成功启动 `PMS_WEEKLY_WORKLOG_APPROVAL`，否则服务端拒绝，避免孤儿审批状态。Web 宿主用共享 `IWorkflowTransactionBoundary` 将周报写入和流程启动合并提交；未提供事务边界的轻量宿主遇到流程启动失败时补偿删除刚写入的周报。批准、驳回和撤回均通过 Workflow 动作回写。`PmsWeeklyWorkLogSubmissionWorkflowHistoryService` 只读聚合已有 `WorkflowOperation` 的最近有效审批结果，在周报卡片回显审批人、意见和时间，不复制 PMS 私有流程日志。撤回仅限原提交人和运行中实例，驳回必须保留原因；快照通过 FreeSql 同步到 PostgreSQL/SQL Server。

工时矩阵的整周保存先由 `PmsWorkLogService.SaveCells` 完成所有单元格的项目、成员、WBS、日期、小时数、重复单元格及日累计预校验，确认通过后才逐格持久化；一个单元格被拒绝不会留下本次操作前面已修改的半周工时。Web 宿主将整批写入置于共享 `IWorkflowTransactionBoundary`，因此 PostgreSQL/SQL Server 的持久化异常也会整体回滚。单格 API 仍保留相同服务端门禁，页面不直接写仓储。

PMS 表单采用 Workflow Form 编辑器承载，字段布局统一限制为两栏中的半宽或全宽；短字段使用半宽，说明、影响、纪要、批注、交付物、风险处置和附件使用全宽。字段的显示顺序、宽度、可见/可编辑/只读权限和校验随已发布表单版本固定，Workflow 实例保存表单版本与数据快照。项目立项首版把预立项/正式立项、项目别称、中英文名、产品线/分类/版本、预计/实际立项时间、开发方式、部门、领域经理、业务发起方、概况、目标和 `OtherInfo` 作为项目主数据字段；状态变更通过 PMS Application 校验操作者、说明和状态变化，并以 `PmsProjectStatusHistory` 保存不可变历史。工时矩阵以 `PmsWorkLog` 的项目、WBS、成员、日期为稳定单元格键，更新和清空仍经过 `PmsWorkLogService` 的项目周期、成员归属、任务归属和小时数规则；出勤状态使用字符串枚举保存，旧记录缺失时按正常状态兼容读取。需求使用独立的 `PmsRequirement` 聚合，不复用风险问题表；需求编号在项目内唯一，项目/产品/基线只保存稳定引用，优先级、状态和类型按字符串枚举持久化，`OtherInfo` 由领域统一校验。资源分配是只读 Application 聚合，使用项目成员的部门/角色、WBS 负责人和计划日期、工时记录计算人员×日期的任务数与工时；项目状态、任务状态、关键词和日期范围在 Application 查询中筛选，负荷阈值作为查询参数传入，颜色只对应派生等级而不是业务状态。PMS 稳定字段基线记录在 `docs/localpath/pms/pmp-field-baseline.md`；表单编辑器只能提供布局和输入能力，不能绕过 PMS Application 的项目归属、成员范围、状态、时间、基线和 EVM 规则。

LMS 首个切片的 Domain 只保存申请和外部授权资产：`LmsLicenseRequest` 保存申请编号、申请人、产品、客户机台引用、型号/运行环境快照、宽限天数、特性 JSON、可选到期时间与 `OtherInfo`；`LmsLicenseAuthorization` 保存外部输入的 License 原文、授权编号、可选申请引用、产品、客户机台引用、型号/运行环境快照、宽限天数、特性 JSON、到期时间与 `OtherInfo`。机台申请创建时从 LMS 客户机台复制元数据快照，批准登记及后续替代授权沿用快照；宽限天数只允许非负数。两类 JSON 分别限制为数组和对象，枚举经 FreeSql 映射为名称字符串；申请编号和授权编号在 FreeSql 记录上各有数据库唯一索引。授权的有效状态由 `GetEffectiveStatus(now)` 按 `ExpiresAt + GracePeriodDays` 派生：原始状态仍为 Active 且当前时间未超过宽限结束时间时保持 Active，超过后显示为 Expired；`EffectiveExpiresAt` 和 `IsWithinGracePeriod` 只读计算，查询不写回数据库，`ListAuthorizations(includeInactive: false)` 仍会包含宽限期中的授权。申请提交通过 `LmsLicenseService.SubmitAndStartWorkflow` 在 `IWorkflowTransactionBoundary` 中同时更新申请和启动 `LMS_LICENSE_APPROVAL`，并在回滚后恢复申请对象原状态，防止数据库与页面内存状态不一致。通用 Workflow 撤回不直接执行业务动作，因此 `ResubmitAfterWithdrawal` 只在申请仍为 Submitted、最近实例为 Cancelled 且操作者满足 Workflow 原发起人校验时，先将申请恢复为 Withdrawn，再创建带 `PreviousInstanceId` 的新实例并重新置为 Submitted。`LmsLicenseWorkflowActionHandler` 仅允许 Submitted 申请变为 Approved、Rejected 或 Withdrawn，且驳回必须携带非空审批意见，空意见在状态回写前拒绝；登记外部授权时 Application 层要求关联申请已批准且产品一致。系统不生成、解析、签名或伪造密钥，外部 License 只按不透明原文保存。授权的人工生命周期不复用 Workflow 状态动作：Application 用例要求停用、开启和作废均提供操作者与原因，并把动作、前后状态、操作者、原因和时间写为独立的 `LmsLicenseLifecycleEntry`。状态更新和该审计记录必须在同一个 `IWorkflowTransactionBoundary` 中写入；审计失败时由回滚回调恢复授权对象原状态。到期扫描在实际到期后先发布宽限期通知，宽限期结束后才发布已到期通知；两类通知使用独立去重键，扫描不写授权状态，通知失败沿用平台的非阻断失败记录边界。

`LmsLicenseOperationsSnapshotService` 用同一份当前仓储快照计算申请总数、待审批、已批准，以及有效、临期、到期、停用和作废授权数；临期仍是 Active 授权在未来 30 天内到期的派生集合。`/Lms/Overview` 的每张卡片仅生成 `/Lms/License` 状态筛选深链，不保存统计副本；明细页按照 `requestStatus` 和 `authorizationStatus` 复用相同枚举/时间判断，防止概览与列表口径漂移。

LMS 审批结果继续由通用 `WorkflowActionExecutor` 驱动：`LmsLicenseWorkflowActionHandler` 在状态动作成功后，针对 Approved 或 Rejected 向申请人发布 `WorkNotificationKind.Approval`，通知深链回 LMS 原单并携带审批意见。去重键包含申请、Workflow 实例与目标状态，避免同一实例重放动作产生重复结果通知；通知服务自身失败不抛回业务动作，按既有失败记录边界处理。

LMS 产品主数据独立于 ERP 商品：`LmsLicenseProduct` 只表示可授权的软件产品，保存唯一编码、名称、描述、启停状态和 `OtherInfo`。申请当前仍以产品名称保存历史快照；`LmsLicenseService` 仅在配置了产品主数据时校验新申请的名称必须匹配启用产品，产品停用不回写或破坏历史申请、授权。

LMS 特性主数据由 `LmsFeature` 与 `LmsFeatureVersion` 两层组成：特性保存唯一编码、名称、描述、启停状态和 `OtherInfo`；版本通过 `FeatureId + Version` 唯一索引保存等级（基础/中级/高级）、适用范围（客户/机台）、启停状态和 `OtherInfo`。`LmsFeatureVersionService` 只允许为存在且启用的特性创建版本，停用特性或版本不会删除历史数据；版本记录使用真实枚举并按 FreeSql 字符串枚举规范映射，以同时保持 PostgreSQL 与 SQL Server 一致。当前申请和授权仍保存特性 JSON 历史快照，客户特性、机台特性及申请选择器改用特性版本引用将在 CRM 客户/机台主数据接入后完成。

`LmsCustomerMachine` 属于 LMS，但客户仍是 CRM 的唯一主数据：机台只保存 `CustomerId`，不保存客户名称、编码或客户状态快照；页面展示时通过 `CustomerService` 实时查询 CRM 客户。创建和编辑机台前，`LmsCustomerMachineService` 必须确认客户处于 Active，并复用 `LmsLicenseProductService` 确认可授权产品处于 Active；机器码由数据库唯一索引和 Application 层大小写无关检查共同保护。机台停用不删除历史记录，也不会回写 CRM。申请、客户特性和机台特性尚未引用该表，后续接入时仍须在服务端复核客户、机台、产品及适用范围。

`LmsCustomerFeature` 表示客户级授权基线，只保存 `CustomerId`、`FeatureVersionId`、可选到期时间、备注、状态与 `OtherInfo`，不复制客户或特性名称。服务创建/编辑时同时复核 CRM 客户为 Active，特性版本为 Active 且 `Scope=Customer`；“客户 + 特性版本”在数据库和应用层均唯一。机台范围版本不能绕过该门禁写入客户基线。申请选择器尚未消费客户特性，后续必须只读取未停用、未过期的客户基线，并与机台特性覆盖规则共同决策。

`LmsMachineFeature` 是客户特性基线在具体机台上的细化或限制：它引用 `LmsCustomerMachine` 与 `Scope=Machine` 的特性版本，并保存可选到期时间、备注、状态与 `OtherInfo`。创建和编辑时，`LmsMachineFeatureService` 复核机台和版本均启用，并要求机台所属客户存在同一 `FeatureId` 的 Active、未过期客户基线；机台版本的等级不得高于该基线版本等级。每台机台的同一特性最多一条记录，应用层以版本反查 `FeatureId` 防止借不同版本重复授权，数据库同时保障“机台 + 特性版本”不重复。该模型不开放机台专属超范围授权；若未来需要，必须新增明确的审批来源与审计字段，而不能绕过当前服务直接写表。

许可证申请以兼容扩展方式接入主数据：`LmsLicenseRequest` 新增可空 `CustomerId`、`CustomerMachineId` 与 `FeatureVersionIdsJson`，旧记录仍可保留 `CustomerName` 与 `FeaturesJson` 历史展示快照。新的 `CreateMachineRequest` 是唯一允许页面新建的路径：它复核 CRM 客户 Active、机台 Active 且属于该客户、申请产品与机台产品一致，并要求所选版本全部出现在该机台的 Active `LmsMachineFeature` 集合中。通过后，特性版本引用和兼容特性快照一起持久化；流程审批和外部授权登记继续按申请 ID 工作，无需直接读取 CRM 或 LMS 主数据表。该边界使历史申请可读，同时避免新申请退化回任意客户名称或特性 JSON 输入。

`LmsLicenseAuthorization` 同样保存可空的 `CustomerId`、`CustomerMachineId` 与 `FeatureVersionIdsJson`，使授权记录可追溯到机台申请的主数据引用。对于无关联申请的历史/手工授权，原有登记接口仍保留；但页面选择已批准申请时必须调用 `RegisterExternalLicenseFromRequest`，由服务从申请原样继承产品、特性快照和所有引用，外部输入仅包含授权编号、外部 License 原文、到期时间与 `OtherInfo`。因此授权登记不能在审批后把产品、客户、机台或特性替换成与原申请不一致的组合。

申请和授权的可选 `ContactId` 复用 CRM `CustomerContact`，不缓存联系人姓名。`CreateMachineRequest` 仅在提供联系人时查询当前客户下的联系人集合，联系人不存在或跨客户即拒绝；`RegisterExternalLicenseFromRequest` 随申请继承该引用。当前 CRM 联系人模型没有启停状态，因此 LMS 不制造不存在的状态门禁；CRM 新增联系人状态后应在同一 Application 校验点补入。

机台许可证冲突按精确引用而不是名称快照判断：`LmsLicenseService` 在新机台申请和从批准申请登记有效外部 License 前，查询相同 `CustomerMachineId`、产品名称大小写无关匹配且 `GetEffectiveStatus(now)=Active` 的授权，再比较 `FeatureVersionIdsJson` 是否与待处理集合存在交集。任何重叠都会拒绝，避免相同特性版本被重复下发；停用、作废、到期授权和没有主数据引用的历史授权不参与。该规则在授权登记阶段再次执行，避免两个已批准申请在不同时间登记时绕开申请阶段门禁。

续期/重发/换机不会覆盖授权历史：替代后的 `LmsLicenseAuthorization` 保存 `SupersedesAuthorizationId` 和 `ReplacementKind`（Renewal、Reissue 或 MachineChange）。普通替代由 `ReplaceAuthorization` 停用原授权并保留同一机台，且显式拒绝 `MachineChange` 类型，防止调用方在未指定目标机台时伪造换机；`ChangeMachine` 额外校验目标机台不是原机台、目标机台启用、同一 CRM 客户、同产品、授权编号未重复和无有效特性冲突，再创建绑定目标机台的新授权。两条路径均写既有生命周期审计并复用事务边界；`/Lms/LicenseReplacement` 仅展示原授权所属客户、同产品且启用的候选目标机台，服务端仍作为最终门禁。`/Lms/License` 对替代生成的新授权展示替代类型和原授权深链，也会在旧授权上列出后续授权，`authorizationId` 查询参数只定位目标记录，不改变授权状态或授权范围。PostgreSQL 探针会在生命周期审计写入故障时断言旧授权状态、新授权和生命周期记录整体回滚。`LmsLicenseAuthorizationRecord` 的列位置为 1–16 连续唯一，替代类型和授权状态均按字符串枚举映射，避免 PostgreSQL/SQL Server 结构同步时产生重复列序。

`LmsLicenseReplacementRequest` 是独立于授权资产的替代审批原单：保存原授权、替代类型、候选目标机台、新授权编号、外部 License 原文、到期时间、扩展 JSON、申请人和原因。续期/重发不得设置目标机台，换机必须设置目标机台；提交仅改变申请状态，不能在审批前修改原授权。该原单已使用独立仓储契约与 `LmsLicenseReplacementRequest` FreeSql 表持久化，申请编号由数据库唯一索引保护，替代类型和状态均以字符串枚举映射。同一原授权只能有一个 Submitted 申请：创建、首次提交和撤回后的重提均重新检查该规则，草稿、驳回和撤回记录不阻止后续申请。`LmsLicenseReplacementRequestSchemaMigration` 进一步在 PostgreSQL/SQL Server 建立仅覆盖 `Status='Submitted'` 的 `OriginalAuthorizationId` 筛选唯一索引，确保跨进程并发不能绕过应用层校验；迁移前会检测历史重复数据并明确失败，绝不自动删除或改写原单。仓储捕获该索引冲突并统一转换为“该原授权已有审批中的替代申请”；检测同时兼容 PostgreSQL 对长索引名的 63 字符截断。发起人撤回后，原单仍保持 Submitted 以保留业务事实；`ResubmitAfterWithdrawal` 会先短暂置为 Withdrawn、以 `PreviousInstanceId` 重开同一原单的审批实例，再恢复 Submitted，并把全部状态变化置入事务边界。驳回申请则直接通过 `SubmitAndStartWorkflow` 从 Rejected 置回 Submitted；Workflow 绑定会验证原发起人并以 `PreviousInstanceId` 启动新实例。`LmsLicenseReplacementWorkflowActionHandler` 仅接受 Submitted 申请的 Approved/Rejected/Withdrawn 动作；批准时使用 `WorkflowActionContext.Actor` 作为生命周期审计操作者，在同一 Workflow 事务边界内调用 `ReplaceAuthorization` 或 `ChangeMachine`，随后才回写申请 Approved。新授权会持久化 `ReplacementRequestId`，使授权列表可深链回替代审批原单；历史或人工登记授权保持空值兼容。任何替代失败会回滚待办决策和申请状态。

为避免 `WorkflowActionExecutor`、替代动作处理器、`LmsLicenseService` 与 `WorkflowTaskService` 在 Web DI 容器构造期形成循环，替代动作处理器只在实际执行批准动作时从当前作用域取得 `LmsLicenseService`；执行器已在该阶段完成解析，事务边界不变。Web 宿主对 `WorkflowTaskService` 和 `WorkflowBindingService` 同样采用工厂加延迟解析运行时/动作执行器，避免 `WorkflowRuntimeService -> WorkflowActionExecutor -> 业务审批服务 -> WorkflowBindingService` 回到待办服务的循环；纯引擎测试仍可显式传入依赖。

`/Lms/License` 不再包含直接调用 `ReplaceAuthorization` 的遗留替代表单或状态；该页只展示授权状态、生命周期操作和审批追溯，保证 Web 层没有绕过替代审批原单的入口。

替代审批的业务深链使用 `replacementRequestId`：`LmsLicenseReplacementRequestService.ListVisible/CanRead` 在 Application 层统一执行范围门禁，管理员看全量，普通用户只能看到自己的原单或被分派的替代审批待办；原授权选择也复用 `LmsLicenseAccessService`，不依赖 Razor 层过滤。创建替代申请时同一服务再次校验当前申请人是否能读取原授权，防止直接调用 Application 绕过页面选择器。默认 `/Lms/LicenseReplacement` 仍只列出当前申请人的原单；从 Workflow 收件箱进入时，页面只在当前用户是申请人或曾被分派该业务待办时展示指定原单。这样审批人可以在处理前查看替代上下文，同时不会因 URL 参数把其他申请人的列表暴露给无关用户；更细粒度的数据范围仍由 LMS 权限切片统一收敛。

CRM 删除客户不会直接读取 LMS 表：`CustomerService` 调用 LMS Application 的 `LmsCustomerReferenceService`，由后者汇总该客户的机台、客户特性、归属机台特性、许可证申请和许可证授权数量。任一数量非零时，CRM 统一主数据影响决策拒绝物理删除并提示改为停用；客户页面同时显示 CRM 与 LMS 两组引用计数。该保护不改变历史记录，也不阻止停用客户，后续新增 LMS 客户关联资产必须纳入同一汇总服务。

当前 Workflow 首批业务绑定覆盖 CRM 合同、PMS 项目变更、ERP 核销、采购订单和销售订单；审批待办按启动时的已发布版本生成并与实例绑定。流程节点配置现在支持声明式 `SetField` 动作，审批动作继续使用 `onApproved`/`onRejected`/`onCancelled`，自动业务动作使用 `{\"action\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}`；动作随实例快照固定，由 `WorkflowActionExecutor` 分派到业务模块注册的强类型 `IWorkflowActionHandler`。通知节点使用 `recipients`、`title`、`content`、`href` 和 `kind` 配置，由 `NotificationService` 统一投递。引擎不使用反射或直接写业务表。ERP 核销、采购订单、销售订单、CRM 合同和 PMS 项目变更已接入第一批状态动作，审批完成/拒绝/撤回通过对应业务适配器修改领域状态。已有旧版种子流程不会被原地修改，启动时会补发带动作的新发布版本。

流程动作采用“引擎编排、业务适配器落地”的边界：Workflow 负责节点、触发时机和声明的字段/值，业务模块负责字段白名单、枚举转换、领域规则和仓储持久化。`WorkflowActionContext` 同时传递审批意见与实际处理人，审批型动作由 `WorkflowTaskService` 传入已通过待办权限校验的 actor；自动节点没有人工处理人时保持为空。这样可以让替代授权等业务动作写入正确审计身份，又不会把业务领域校验退化成任意 JSON 路径写入。后续自定义表单与 Canvas 分开建模：表单负责字段、校验、布局和版本快照，Canvas 只负责节点与连线；流程实例同时固定流程定义快照和表单版本/数据快照。

审批任务现在先校验待办仍处于 `Pending`，再以待办当前 `Revision` 做一次无状态变化的 CAS 预占，成功后才执行最终业务动作；动作处理器失败时待办仍保持 `Pending`，但保留已预占的版本，下一次重试可以继续处理，避免两个进程同时执行同一审批动作。对同一待办的过期内存副本，应用层会在执行动作前重新读取实例待办并拒绝状态或 Revision 已变化的副本。流程实例和审批待办都从 1 开始维护 `Revision`，FreeSql 仓储使用 `Id + Revision` 条件更新并在成功后递增；节点推进、完成、拒绝、取消和转交统一经过实例/待办 CAS 门禁，过期快照不会覆盖新状态。实例创建还通过 `IWorkflowInstanceRepository.TryAdd` 以数据库原子插入占用 Running 业务唯一键：PostgreSQL 使用 `ON CONFLICT DO NOTHING`，SQL Server 使用带 `HOLDLOCK` 的 `MERGE`；竞争失败时 `WorkflowInstanceService.Start` 回读胜出实例，旧 `Add` 调用仍保留冲突异常兼容边界。除 CAS 外，`WorkflowSchemaMigration` 在 PostgreSQL/SQL Server 为 `(BusinessType, BusinessId, DefinitionCode)` 的 Running 实例建立筛选唯一索引；迁移拒绝历史重复运行记录，使跨进程竞态保持幂等。审批决策主路径和自动节点运行时通过 `IWorkflowTransactionBoundary` 绑定 FreeSql 事务，把业务动作、待办结果、实例推进/终态和操作记录放入同一提交边界；动作异常时数据库状态和待办预占一起回滚，失败节点通过事务边界登记回滚后回调，确保 `NodeFailed` 不会被外层审批事务吞掉。审批待办通知不再在任务写入时立即投递，而是登记为事务提交回调；拒绝/取消终止实例时，其他待办的 `Cancelled` 操作历史也随主事务写入，相关通知已读统一登记为提交后回调；外层事务回滚时不消费通知，提交后才按待办去重键标记已读，因此不会留下孤立通知，通知发布或已读失败仍沿用不阻断主交易的后置副作用边界。边界实现检测并复用当前线程已有事务，若发现外部 FreeSql 事务未由 Workflow 边界管理，则拒绝登记提交/回滚回调，避免通知提前发布或失败审计丢失；由 Workflow 边界开启的嵌套调用仍按嵌套层级逆序恢复内存快照。独立数据库探针已验证销售订单动作、流程启动和发起人撤回三个 PostgreSQL 后段故障点：业务状态、实例、待办、CAS 版本与操作历史均不会形成部分提交；同一探针可通过显式连接串切换到 SQL Server。跨模块动作的故障注入与迁移验证按 PostgreSQL 优先、SQL Server 次之规划，不再新增 SQLite 专项。`WorkflowSchemaMigration` 在开发库结构同步后幂等回填旧记录的 Revision=0；实例快照读取同时兼容历史数字节点类型，但新写入仍使用枚举名称 JSON。流程启动、重新提交、发起人撤回和审批决策现在也通过同一事务边界提交实例、待办和操作历史；失败时回滚数据库写入并恢复内存实例、主待办及被动取消待办快照，通知在事务提交后再标记已读。

审批待办的 `Id` 不再依赖随机值，而由 `InstanceId`、`NodeId`、`Round` 和大小写无关的审批人计算得到。`FreeSqlWorkflowTaskRepository` 使用按主键的插入或忽略语义，因此同一待办的跨进程补偿可以安全重放；待办分派操作和通知分别使用同一稳定待办 ID 的去重键，不会重复投递。不同轮次仍生成不同待办 ID，保留退回后的完整审批历史。

涉及可能产生外部业务副作用的自动节点时，`WorkflowRuntimeService` 会在当前数据库事务内先对 `WorkflowInstance` 行加锁；`Retry` 额外在写入 `Retried` 前校验调用方快照的 `Revision` 仍为 Running，并在锁内重新核验当前候选节点和最近 `NodeFailed` 的稳定 ID，拒绝陈旧失败尝试。PostgreSQL 使用 `FOR UPDATE`，SQL Server 使用 `UPDLOCK/HOLDLOCK`。因此并发撤回、完成或另一重试先提交后，落败请求会在动作执行前停止。自动节点的回滚回调还区分动作执行和后续状态推进：只有通知/业务动作本身抛错才写入 `NodeFailed`，实例 CAS 或持久化冲突不会制造第二条伪失败审计；SQLite 仅保留无行锁兼容路径，不新增专项数据库测试。

`WorkflowOperation` 同样以 `DedupeKey` 派生稳定主键，`FreeSqlWorkflowOperationRepository` 按主键已存在时忽略写入。服务层首次记录直接尝试仓储原子 `TryAdd`，只有返回竞争失败或存储异常时才回读去重记录；这样并发重复记录“分派、节点进入、节点执行”等操作时，不再先触发 PostgreSQL 的唯一约束错误再在已失败事务中查询。既有 `DedupeKey` 唯一索引仍用于保护历史与外部写入。操作 ID 只由去重键决定，重放不会产生第二条时间线记录。

`WorkflowRuntimeService` 的进程内实例锁只服务于运行状态的同一实例串行推进。实例到达 Completed、Rejected 或 Cancelled 后会在终态事务成功提交后移除对应锁；再次查询终态实例也会清理遗留锁。该策略不替代数据库 CAS，而是避免长时间运行的宿主为已结束历史实例持续保留内存对象，同时不在回滚时错误放开运行中实例的串行门禁。

失败自动节点重试先以“实例 + 节点 + 最近一次 `NodeFailed`”派生的 `Retried` 去重键执行原子抢占；若另一请求已经抢占同一次失败，当前请求立即拒绝，不再进入图运行时。这样通知节点即使没有业务动作的 `NodeExecuted` 预占，也不会被两个并发重试请求重复执行；真正的下一次失败会产生新的 `NodeFailed`，从而开启新的重试尝试。运行时、实例服务和操作服务使用同一 `IFreeSql` 连接时，即使分别构造 `FreeSqlWorkflowTransactionBoundary` 实例，也共享异步回调注册表并加入同一数据库事务；不同连接或未由 Workflow 管理的外部事务仍会被拒绝。PostgreSQL 探针已覆盖这一约束。

审批决策的跨进程竞争同时由待办 `Revision` CAS 和实例活动节点门禁保护：两个独立数据库连接读取同一 Pending 待办并同时调用 `Approve` 时，只有一个请求能预占待办并进入最终动作，另一个请求在状态变化后拒绝，不会执行业务 handler、推进实例或追加审批历史。PostgreSQL 与 SQL Server 双连接探针覆盖该路径，确认待办只保留 Approved、实例只完成一次，业务动作和 `Approved` 操作历史均不重复。

审批与发起人撤回是跨资源的交叉竞争，不能只依赖待办状态检查：`Approve` 先以待办 CAS 占用决策，`Withdraw` 先以实例 CAS 进入终止事务，随后双方都必须在同一 Workflow 事务内完成另一资源的更新。两个独立连接同时执行时，任何一方发现 Revision 已变化都会回滚自身已写入，最终只能得到“实例 Completed + 待办 Approved + Approved 历史”或“实例 Cancelled + 待办 Cancelled + Withdrawn/Cancelled 历史”其中一种完整结果。PostgreSQL/SQL Server 双连接探针持续覆盖该交叉门禁。

重新提交沿用同一 Running 业务唯一键作为跨进程门禁：两个独立连接同时从同一 Rejected/Cancelled 历史实例发起 `Resubmit` 时，只有一个 `TryAdd` 写入新实例并记录 `Resubmitted`，竞争方在事务内回读并复用胜出实例，再按该实例快照幂等补齐审批待办。这样不会产生两个可审批的新实例，也不会让重提历史或待办随并发次数增长。

转交同样先以原待办 `Revision` CAS 占用，再在同一事务内将原待办标记为 `Transferred`、写入转交历史并创建目标待办；两个连接将同一待办转给不同目标时，竞争失败的一方在占用阶段拒绝，不能创建第二个目标待办。目标待办使用稳定主键和 `TryAdd` 作为补偿重放的第二道幂等门禁。

待办创建不能只检查调用方内存中的实例状态，否则会与发起人撤回交叉产生孤儿 Pending。PostgreSQL 待办原子插入通过 `WorkflowInstance ... FOR UPDATE` 锁定并确认 Running 实例，SQL Server 使用 `UPDLOCK/HOLDLOCK` 获得同等行级串行化；`Withdraw` 先提交实例 Revision CAS，再在同一事务内读取并取消当前 Pending 待办。两条路径因此只能按“先创建后撤回并取消”或“先撤回后拒绝创建”之一完成。

流程实例同时保存 `StartedBy`。`WorkflowTaskService.Withdraw` 只允许发起人撤回运行中的实例，撤回会将实例置为 `Cancelled`、取消该实例下全部待办并标记对应审批通知已读；撤回不执行审批通过/拒绝动作，业务对象保留在可重新提交前的状态。收件箱的发起人撤回区域只读取当前登录用户的运行中实例，避免把查询参数或页面展示身份当成操作权限。启动迁移在创建 Running 唯一索引前通过数据库端 `GROUP BY/HAVING` 检查 `(BusinessType, BusinessId, DefinitionCode)` 重复，只回传最多 5 个冲突键；迁移不会把完整 WorkflowInstance 历史加载到应用内存，PostgreSQL 与 SQL Server 均保留同一拒绝门禁。唯一索引的检查和 DDL 还由 PostgreSQL 事务级 advisory lock、SQL Server 事务级 `sp_getapplock` 串行化，避免多个 Web 实例首次启动时互相竞争系统目录或重复创建索引。旧实例的活动节点 JSON 回填在 PostgreSQL、SQL Server 和 SQLite 使用单条方言更新，同时完成 Revision、Join/Loop/审批快照默认值回填；SQL Server 对历史 `text` JSON 列统一先转换为 `nvarchar(max)` 再比较，并将 GUID 转为小写以保持跨数据库快照文本一致；整个回填在没有外层事务时由迁移自带事务，已有宿主事务时复用当前事务，避免历史数据量增长后产生逐条更新或半完成的启动长事务。

审批待办转交由 `WorkflowTaskService.Transfer` 统一编排：服务端先校验当前登录用户仍是原待办指定审批人，再将原待办标记为 `Transferred`，保留转交人、目标审批人和意见，并为目标审批人创建同节点的新待办。原通知标记已读，新通知按待办幂等创建；转交不终止流程实例，也不执行业务状态动作。审批退回使用同一服务的 `ReturnToNode`：当前审批节点必须在 JSON `returnTargets` 中声明目标审批节点，原待办记为 `Returned` 并保留意见，同节点其他待办取消，实例通过 CAS 回到目标节点；目标节点按 `WorkflowTask.Round` 创建新执行轮次，历史待办不会被复用或覆盖。这样合同、订单、核销和项目变更共享同一转交/退回语义，页面不能通过修改查询参数伪造审批人。
同一审批轮次的转交目标还必须避开该轮次已经出现过的审批人和历史转交目标；因此 A→B→A 等循环在 Application 层被拒绝，不会因稳定待办 ID已存在而出现只在内存中创建目标待办的分裂状态。退回进入新轮次后重新使用初始审批人快照，不受上一轮转交链影响。

流程操作历史由 `WorkflowOperation` 独立持久化，不把完整时间线继续堆叠到待办当前状态字段中。`WorkflowInstanceService` 和 `WorkflowTaskService` 在发起、分派、同意、拒绝、取消、转交、撤回以及节点进入/完成后写入带去重键的不可变记录；自动通知/业务动作成功后写入 `NodeExecuted`，失败自动节点由发起人重试时写入 `Retried`，同一实例可以按时间顺序查询所有操作，收件箱在处理历史中展示与任务相关的实例时间线。WorkflowOperation 的 `DedupeKey`、通知的“接收人 + DedupeKey”同时建立数据库唯一索引，并由仓储显式 `TryAdd` 原子占用：PostgreSQL 使用 `ON CONFLICT DO NOTHING`，SQL Server 使用带 `HOLDLOCK` 的 `MERGE`，应用层不再把正常并发竞争转成唯一键异常。审批决策、自动节点运行时、流程启动/重新提交和撤回的操作记录随事务提交，失败节点的 `NodeFailed` 在主事务回滚后单独保留；真实 Workflow 事务内的业务动作先以稳定 `NodeExecuted` 键原子占用，再调用 handler，竞争进程读取已提交占用后跳过，动作或后续推进失败时占用随事务回滚；无事务内存宿主保持兼容路径，不把预占记录留在失败状态。CRM 合同、PMS 项目变更、ERP 采购/销售订单和核销 handler 均通过 Application 审批入口参与当前 Workflow 事务，PostgreSQL 已验证业务状态写入后流程终态失败时不形成部分提交；更复杂的跨模块长事务和外部系统一致性仍属于后续可靠性边界。

自动节点重试由 `WorkflowTaskService.Retry` 作为收件箱应用用例编排：`WorkflowRuntimeService.Retry` 只推进图和记录重试审计，若结果进入审批节点，TaskService 在同一事务中调用 `EnsureCurrentApprovalTask` 补齐审批人快照对应的待办。这样 Web 层不会直接调用纯 Runtime 而留下空审批节点；待办写入失败时应用层恢复实例内存快照，数据库宿主则由同一 FreeSql 事务回滚实例、操作和待办写入。Runtime 仍保留独立 Retry API，供纯引擎测试和并行分支驱动使用。

失败自动节点的 `Retried` 审计键由实例、节点和最近一条 `NodeFailed` 操作记录的稳定 ID 派生，而不是每次调用随机生成；同一失败尝试在事务重放或跨进程竞争时复用唯一历史，动作再次失败产生新的 `NodeFailed` 后才开启下一次重试键。派生键保持在 `WorkflowOperation` 的 200 字符限制内，并继续由操作历史唯一索引和原子 `TryAdd` 保护。

`WorkflowBindingService` 对已有 Running 实例的 `StartOrGet/Resubmit` 补偿也经过同一事务边界：运行时继续推进、审批人快照固化和缺失待办补齐必须一起提交。补偿待办写入失败时，嵌套事务回调恢复实例快照，避免重复打开收件箱时把已经推进的节点或审批人集合留在当前作用域。首次启动或重新提交先写入实例、再准备运行时和待办时，绑定服务通过 `IWorkflowInstanceCompensationRepository` 只记录 `TryAdd` 实际成功的实例 ID；失败补偿不会因并发回读胜者而删除另一进程的 Running 实例，真实 PostgreSQL/SQL Server 仍优先由外层事务回滚。

运行时实例锁的生命周期与数据库终态提交保持一致。自动节点或审批后续推进到 Completed 时，`WorkflowRuntimeService` 只登记提交后释放回调；`WorkflowTaskService` 的 Reject、Cancel 和 Withdraw 终止路径也登记同样的外层提交回调，并在回滚回调中恢复实例、主待办及被动取消待办的内存快照。Approve、Transfer、ReturnTo 也登记待办/实例回滚快照，避免普通决策在外层提交失败后仍保留已处理的内存对象。待办创建入口会记录本次 `TryAdd` 实际成功的任务 ID，回滚补偿只删除这些 ID，不会把并发事务已提交的目标待办误删；实例启动入口使用同样的精确 ID 语义，失败时不会误删启动竞争的胜者。FreeSql 适配器提供数据库补偿删除，真实数据库仍以外层事务回滚为主。若外层事务回滚，实例恢复 Running，锁继续保护同一实例的串行推进，避免“数据库已回滚但进程锁已释放”的并发窗口。

`WorkflowRuntimeService.ExecuteStateTransaction` 无论是否配置基础设施事务都保存并恢复完整实例快照（当前节点、状态、完成时间、Revision、活动节点、Join/Loop 状态和审批人快照）。因此内存宿主、测试夹具或迁移工具即使未注入事务边界，也不会因仓储/操作记录异常继续持有半推进状态；配置事务边界时仍由数据库回滚和回滚回调负责持久化一致性。

`WorkflowInstanceService` 的公开状态变更使用相同约定：没有事务实现时由服务本身捕获异常并执行已注册的回滚快照，有事务时交给边界管理嵌套回滚。这样 Runtime、绑定服务和直接使用实例服务的调用者拥有一致的失败恢复语义。

拒绝或撤回后的重新提交由 `WorkflowBindingService` 统一处理：`StartOrGet` 发现最近实例为 `Rejected`/`Cancelled` 时转入重新提交路径，校验操作者必须是原发起人，并创建带 `PreviousInstanceId` 的新运行实例。首次启动和重新提交都可能遇到跨进程的 Running 唯一索引冲突；两条路径均会回读并返回胜出的实例。新实例沿用当前已发布定义并重新幂等生成待办，旧实例和旧任务保持只读历史；已完成实例不能借此路径重复发起新的审批尝试。

Workflow 运行时对已发布/已归档的不可变流程定义在应用服务作用域内做缓存，版本发布或归档后失效缓存。审批目标由 `IWorkflowApproverResolver` 在实例上下文中统一解析：`approver`/`approvers` 兼容固定用户名，`$initiator` 指向实例 `StartedBy`，`approverRoles` 查询启用角色成员，`approverOrgs` 查询启用组织下的启用用户，`approverBusinessFields` 则委托 `IWorkflowBusinessApproverLookup` 的业务模块适配器；首个适配器在 Application 层读取 `PmsProjectChange.RequesterName`，Workflow 不直接依赖 PMS 或平台表。所有来源均按用户名大小写无关去重，角色和组织适配器在 SQL 中显式标准化查询名以保持 PostgreSQL 与 SQL Server 一致。每个审批节点第一次进入时，将解析结果写入实例 `ApprovalAssigneesJson`，并通过实例 CAS 与事务边界持久化；该快照必须包含至少一个已规范化的审批人，损坏的空集合或非审批节点映射会在实例重建时被拒绝。进程重启或局部 Pending 待办丢失后的补偿只读取这个不可变快照，绝不因组织、角色或业务字段后续变化扩大当前审批集合。旧实例若尚无该字段但已有 Pending 待办，会将既有待办人员作为兼容快照；但若 Pending 中存在转交生成、且不属于初始快照的目标审批人，则该轮次的转交覆盖集优先，补偿不得复活原审批人。当前活动审批节点在没有任何快照或既有 Pending 待办时若解析为空，会拒绝进入无人待办状态，调用方处于事务边界时由该错误回滚实例、待办和操作历史。实例补待办按实例一次读取已有待办，再按“节点 + 审批人”去重后批量写入。草稿定义仍走仓储实时读取，避免设计态修改被运行时缓存持有。`WorkflowBindingService` 未注入 `WorkflowRuntimeService` 时仅保留 Start/Approval/End 线性兼容路径；包含条件、通知、业务动作、并行或 Loop 的图会在创建实例前拒绝，防止旧路径预建未来节点待办。

版本选择先尊重运行实例：`StartOrGet`/`Resubmit` 发现同业务对象已有运行实例时，使用该实例的 `DefinitionVersion` 与快照继续，不因随后发布的复杂新版改变既有运行态；只有确实要创建新实例时，才对最新已发布定义执行运行时服务注入检查。

运行实例从启动时的图快照编译节点配置和连线索引，快照重建会先校验实例外层的定义 ID、编码、版本与快照元数据一致，再重建临时流程定义并复用发布阶段的完整图校验，拒绝损坏的审批/条件/Loop/并行配置、不可达结构和重复连线，避免持久化定义绕过发布校验后让自动节点或条件分支失去唯一选择；读取时仍兼容历史数字节点类型，新写入继续使用枚举名称 JSON。`WorkflowInstance.GetOutgoingTransitions` 只返回当前节点的快照连线，`AdvanceTo` 必须命中允许的目标节点和条件键后才更新 `CurrentNodeId`，再由 `WorkflowInstanceService.Advance` 持久化。回退不是正向图连线：`ReturnTo` 只接受当前审批节点配置的 `returnTargets`，并仅能落到快照内的审批节点。这样节点推进不依赖当前数据库中的流程定义。`WorkflowRuntimeService` 统一驱动开始、条件、通知、业务动作、审批和结束节点：审批节点返回等待状态，通知按实例/节点/接收人去重，业务动作通过强类型处理器执行，动作成功后才写入 `NodeExecuted` 并继续推进；自动业务动作的 `NodeExecuted` 审计沿用实际审批人或 Retry 发起人，没有操作者的系统触发明确记录为 `system`，与传给 handler 的 `WorkflowActionContext.Actor` 保持一致；自动节点失败时实例停留在当前节点，写入 `NodeFailed` 后可由下一次绑定或运行时调用重试，显式 `Retry` 先要求实例仍为 Running，再校验操作者是流程发起人、失败节点仍在活动快照中，并支持传入 `NodeId` 精确选择目标分支；可重试节点候选由 `WorkflowRuntimeService.GetRetryableNodeIds` 统一按节点最新执行状态计算，只有最新状态为 `NodeFailed` 才开放重试，历史失败在节点成功执行后不会误开放重试；Web 不复制失败审计和节点类型规则；损坏的历史失败审计会被安全跳过。并行运行中某条自动分支失败时，事务回滚只撤销该次动作及其推进，实例的完整活动分支集合仍保留；重试成功后该分支按 Join 到达语义等待其他人工分支，不能跳过汇聚或提前结束。

线性串行审批已沿用该推进边界：启动时只激活开始节点后的当前审批节点；当前节点最后一个待办同意后，运行时推进到下一审批节点并幂等创建该节点待办，推进到结束节点后才完成实例。`ParallelSplit`/`ParallelJoin` 提供最小图级并行：实例以 `ActiveNodeIdsJson` 保存所有活动分支，以 `ParallelJoinArrivalsJson` 保存已到达 Join 的来源；Split 同时激活所有无条件分支，首条分支到达 Join 时仍等待，全部入边到达后才激活 Join 并继续。实例重建除校验 Join 和真实入边外，还要求到达来源已从活动集合移除、Join 本身尚未活动且到达集尚未齐全，防止损坏持久化状态伪造汇聚进度。当前受限模型要求 Split 的每条分支先进入实际节点，禁止 `ParallelSplit` 直接连接 `ParallelJoin`，避免 Join 被误作为普通活动节点提前推进。运行时优先清空活动自动节点，避免通知或业务动作分支被人工审批等待饿死；并行条件分支在提供字段时独立选择快照分支并可抵达 Join，无字段时不阻塞其他活动审批；`ParallelSplit` 不能直接连到 End，若 End 与其他活动分支同时存在则运行时也拒绝，强制设计者通过 Join 收口；实例重建同样拒绝活动快照同时包含 End 和其他节点，不能让损坏持久化状态绕过这一门禁。单个审批节点可通过 `approvalMode` 声明多审批人策略：缺省或 `All` 需全部同意，`Any` 则在首个同意后于同一事务取消同节点其他待办、记录取消历史，并在提交后标记对应通知已读；拒绝/取消终止实例时也会为被动取消待办记录 `Cancelled` 审计，并在主事务提交后统一标记所有相关通知已读；若该节点处在并行分支，取消范围仍严格限定为同一节点，随后只将本分支作为 Join 到达来源，其他活动分支仍须完成。审批节点还可通过 `returnTargets` 声明受控回退目标；回退后的目标待办以同节点最大轮次加一创建，待办去重仅对当前 `Pending` 轮次生效，保留历史审批与退回记录。若在并行分支回退，实例会以实际待办节点而非可指向其他分支的 `CurrentNodeId` 校验回退目标，取消其余 Pending 分支待办并清空并行到达状态，避免失活分支继续执行业务动作。旧实例的空活动节点快照兼容回退为单个 `CurrentNodeId`，但恢复前仍要求该节点存在于实例定义快照。

条件节点使用 `branches` 配置声明分支键和受限表达式，也兼容单表达式的 `trueKey`/`falseKey`；发布校验要求配置分支与出边分支键一一对应。运行时只读取调用方提供的字段上下文，支持基本比较、文本匹配以及 `&&`/`||`，不执行脚本、反射或任意业务代码。命中分支后必须匹配实例快照中的同名条件连线，没有命中且没有 `defaultKey` 时保持实例不推进。对并行活动条件，应用层通过 `ContinueAfterCondition` 指定 `conditionNodeId`、字段集合和可选 Actor，只推进仍处于活动状态的目标条件，不把同一字段上下文隐式复用到其他条件；条件命中后继续执行自动业务动作时沿用该 Actor，未指定时按系统触发处理；目标不是活动 Condition 时直接拒绝。显式 `Loop` 只接受 `maxIterations: 1..100`，并必须连接 `repeat` 与 `exit` 两条具名分支；发布校验移除所有 Loop `repeat` 边后仍检测图环，从而拒绝任意未受控循环。实例的 `LoopIterationsJson` 持久化每个 Loop 的已执行次数，运行时小于上限走 `repeat`，达到上限走 `exit`，且该状态与活动分支、Join 到达状态同样参与 CAS 和事务回滚。自动节点连续推进仍有 100 步保护；受控回退、受控循环和单层图级并行汇聚已实现，嵌套并行和条件分支内汇聚仍未支持。

嵌套并行沿用同一份 `ActiveNodeIdsJson` 与按 Join 节点分组的 `ParallelJoinArrivalsJson`：外层分支和内层分支可以同时活动，内层 Join 作为外层 Join 的一个来源节点；每次到达仍由实例 CAS、事务边界和操作历史共同提交，因此内层先汇聚不会提前结束外层实例。当前只覆盖结构化 Split/Join 嵌套，条件分支直接进入多层 Join 的复杂图仍留待后续。

Condition 的分支是互斥选择，不把每个 `ConditionKey` 当成并行来源：同一 Condition 的多个具名分支可连向同一 `ParallelJoin`，运行时只记录该 Condition 节点一次到达；Join 随后可作为更外层 Join 的来源。发布校验还要求 Join 的全部入边来源共同位于至少一个上游 `ParallelSplit` 的可达范围，并拒绝 Condition 在未经过新的 Split 时扇出到同一 Join 的多个来源，因而纯互斥 Condition 不能伪装成并行汇聚并永久等待。对于历史或损坏定义中的未知连线，校验稳定返回定义错误而不依赖节点查找异常。这一受限模型避免在未选中的条件分支上无限等待，但不开放任意非结构化、多入口循环图。

自动节点连续推进的运行时保护为 1000 步。该值覆盖 `Loop.maxIterations=100` 的常见“Loop→自动节点→Loop”受控环，同时仍用于阻断配置外的异常自动推进；发布校验继续要求所有图环经过显式 Loop 的 `repeat` 分支。

通知节点通过 `NotificationService` 投递：通知仓储失败先尝试读取并发幂等记录，仍失败时写入 `INotificationFailureRecorder` 边界且不向流程运行时抛出；流程节点照常写入 `NodeExecuted` 并推进。在 Workflow 事务边界内，失败记录不直接写入主事务，而是登记为提交后回调，因此主事务回滚不会留下孤儿失败记录；提交回调本身失败也不能反向阻断已提交流程。Web 默认注册 `FreeSqlNotificationFailureRecorder`，将失败写入 `OaNotificationFailure` 以保留跨进程审计与后续重试输入；发布失败记录包含可反序列化的通知负载（接收人、类型、标题、内容、链接、去重键和创建时间），使用 `JsonSerializationDefaults` 将枚举保存为名称且保留中文，不只保留异常文本；应用启动时与通知主表一并同步该失败表，避免新库首次失败被静默丢弃。`NotificationFailureRetryService` 可按记录 ID 或待重试批次补投，补投前先通过 `Pending + LastRetryAt` 的数据库条件更新抢占默认 5 分钟租约，只有抢占成功者执行通知写入；补投写入直接调用通知仓储按“接收人 + DedupeKey”原子 `TryAdd`，返回已存在时仍继续在同一事务内标记失败记录 `Resolved`，不把正常并发竞争变成新的重试失败。成功或失败结果只更新当前租约，不重复增加重试次数，租约未释放时其他执行者跳过，进程崩溃后可在租约过期后继续补投。`INotificationFailureRepository.TryClaim` 没有非原子默认实现，任何持久化适配器都必须显式提供该 CAS 更新。成功后写入 `Resolved`，失败则保留 Pending；缺少或无法反序列化重放负载的历史记录标记为 `InvalidPayload`，不再被后台批次反复扫描。当前探针覆盖通知仓储抛错后的提交隔离、主事务回滚、补投失败重试和并发租约 CAS；若调用方使用未由 Workflow 管理的外部 FreeSql 事务，则不能安全登记提交回调，失败记录会被安全丢弃，不能直接污染已中止连接；后台重试调度和处置界面也仍是后续边界。

Loop 作为并行分支时，`repeat` 或 `exit` 可以指向 `ParallelJoin`：引擎将 Loop 节点写入该 Join 的到达来源并继续等待其他来源，不能直接激活或穿透 Join。相对地，`ParallelSplit` 不允许直接连接 Join，因为 Split 本身只负责创建分支，不是可独立执行的分支来源。

任何通用节点推进也不能绕过 Join：`Advance`/`AdvanceActive` 在目标为 `ParallelJoin` 时由领域对象拒绝，只有 `ArriveAtParallelJoin` 可以写入到达来源、激活已收齐的 Join 并生成相应审计。

并行到达状态是实例快照的一部分：重建或 CAS 回滚恢复 `ParallelJoinArrivalsJson` 时，系统验证键为快照中的 Join、每个来源是该 Join 的真实入边，拒绝空集合、未知节点和不相连来源，防止损坏持久化数据伪造汇聚完成。

循环计数同样属于受保护运行态：重建或恢复 `LoopIterationsJson` 时，计数只能对应快照中的 Loop，且必须处于 `0..maxIterations`，不能把任意节点或越界次数写成循环进度。

并行分支不能把 End 当作普通目标：领域推进在其他分支仍活动时会在状态推进前拒绝直达 End，覆盖自动、条件、Loop 和通用应用入口；实例保留原活动集合而不形成 `End + 其他分支` 的中间态，设计者必须通过 Join 收口。


自定义表单、节点级字段/表格权限、审批控件以及 Blazor + JS Canvas 的详细设计见 [自定义表单与可视化流程设计](form-workflow-designer-design.md)。该文档当前为设计基线，不代表相关运行时或设计器已经实现。

ERP 采购订单和销售订单通过 `WorkflowApprovalService` 复用统一门禁：提交前必须存在同业务类型、同业务 ID 且已完成的对应审批实例；取消动作检查同编码的全部运行实例，拒绝或撤回后不再保留运行态门禁。订单页面只负责启动绑定和展示状态，不能绕过 Application 门禁。

实例与待办仓储接口不提供默认的 `Update` 伪 CAS：`TryUpdate` 必须由每个持久化适配器显式实现，并以当前 `Revision` 作为数据库条件返回成功/失败；测试夹具也显式模拟版本递增。这样新增 PostgreSQL、SQL Server 或其他适配器时，缺少真实 CAS 会在编译期暴露，而不会悄悄退化为无条件更新。

待办创建同样不使用“先查询再插入”的幂等回退。`IWorkflowTaskRepository.TryAdd` 以稳定待办 ID 执行原子插入，PostgreSQL 使用 `ON CONFLICT (Id) DO NOTHING`，SQL Server 使用带 `HOLDLOCK` 的 `MERGE`；竞争失败后 `WorkflowTaskService` 回读数据库中的胜出记录，再继续操作历史和通知幂等处理。SQLite 仅保留既有 FreeSql 自动同步兼容实现，不新增专项测试。

`WorkflowTaskService.CreateApprovalTask` 即使被业务模块单独调用，也会通过 `IWorkflowTransactionBoundary` 将待办原子写入、操作历史和提交后通知登记纳入同一事务；若操作历史或后续主交易失败，数据库回滚不会留下孤儿待办。已存在的外层 Workflow 事务由 FreeSql 边界复用，不嵌套提交。

FreeSql Workflow 事务提交后按顺序执行已登记的副作用回调；每个回调独立捕获异常并写入 Trace，单个通知、已读或终态锁释放失败不会阻断后续回调，也不会把已经提交的主交易重新报告为失败。

Workflow Running 唯一索引迁移的重复数据检查也必须位于迁移锁临界区：PostgreSQL 在事务级 `pg_advisory_xact_lock` 内重新执行数据库聚合检查，SQL Server 在事务级 `sp_getapplock` 内重新执行检查。这样历史重复检测与唯一索引 DDL 共用同一串行化迁移单元，不会因启动并发在检查后、建索引前出现竞态窗口。

审批终止动作同样沿用实际操作者：当多审批人节点先完成待办决策、再由 `FinishInstance` 执行一次终止型业务动作时，`WorkflowTaskService` 将当前审批人的 `actor` 继续传入 `WorkflowActionContext.Actor`，不能因为动作在终止阶段补执行而退化为空身份。

流程定义编码在应用层按大小写无关处理，`FreeSqlWorkflowDefinitionRepository` 对 PostgreSQL、SQL Server 和 SQLite 的编码筛选使用明确的 `UPPER(Code)` 方言 SQL，避免 ORM 字符串函数翻译差异；这样同一逻辑编码的草稿版本计算不会因数据库默认排序/比较规则不同而分裂。流程编码作为稳定标识统一保存为大写，运行实例使用发布定义的稳定编码快照。

流程编码的持久化规范进一步固定为大写标识：`WorkflowDefinition` 和新建/恢复的 `WorkflowInstance` 均写入 `ToUpperInvariant()` 结果；`WorkflowSchemaMigration.BackfillInitialRevisions` 在建立 Running 唯一索引前回填旧实例的混合大小写编码，并按规范化编码检测历史重复。定义快照校验仍使用大小写无关比较，保证旧版本实例可恢复。

  流程定义版本由 `WorkflowDefinition` 的 `Code + VersionNumber` 组合唯一标识。启动迁移在 PostgreSQL advisory lock 或 SQL Server `sp_getapplock` 临界区内先回填旧定义编码、检测最多 5 组重复版本，再创建唯一索引；历史重复不会自动删除或覆盖，而是阻止启动并要求人工处理。只读重复报告和迁移异常会列出每组全部 `DefinitionId`，并可进一步按 `DefinitionId` 查询全部 `WorkflowInstance` 引用（实例 ID、业务类型、业务 ID、状态），不自动选择保留记录或迁移实例，便于人工审计处置。`IWorkflowDefinitionRepository.TryAdd` 使用 PostgreSQL `ON CONFLICT DO NOTHING`、SQL Server `HOLDLOCK MERGE`，`WorkflowDefinitionService.CreateDraft` 在竞争失败后重新读取最大版本再重试，避免“查询最大值 + 直接插入”在跨进程并发时生成相同版本。SQLite 保留既有自动同步兼容路径，不新增专项测试。

  自动通知/业务动作的执行幂等键按实例当前持久化 `Revision` 形成执行范围，而不是只按 Loop JSON 判断。审批退回、循环重入或并行分支再次进入同一节点时，Revision 已变化，因此合法的新一次节点访问不会误用旧执行键；动作事务回滚后 Revision 不变，Retry 仍复用同一次尝试范围。对历史数据保留旧版无范围执行键读取兼容，避免升级后重复执行已有成功动作。

  审批人快照首次写入也按实例 Revision 做 CAS；若并发补偿请求因 CAS 失败，但重新读取到同一实例已经固化的审批人快照，则同步胜出实例的运行态并复用快照继续幂等补待办，不重新解析动态成员。若重新读取不到胜出快照，说明可能是其他状态推进竞争，仍保持失败并要求调用方刷新。

  待办补偿在真实 Workflow 事务内先锁定实例行并核对 Revision；陈旧进程若发现退回或推进已先提交，会刷新胜出运行态后重新读取活动节点集合，再创建待办。这样补偿不能沿用旧实例快照为历史审批节点创建 Pending 孤儿；无事务内存宿主仍保留原有 CAS 兼容路径。

  审批决策、拒绝、取消、转交、退回和发起人撤回也必须在同一 Workflow 事务内先锁定实例行，再执行待办 Revision CAS。锁校验失败时，陈旧待办在 Claim 前即被拒绝，不能继续调用业务状态动作、推进图运行时或追加审批历史；无事务宿主仍依赖待办和实例 CAS。

  独立待办创建入口遵循同一边界：`CreateApprovalTask` 在写入前校验实例行版本，`EnsureApprovalTasks` 在批量补偿前刷新陈旧运行态。前者拒绝陈旧调用，后者只按刷新后的实例继续幂等补偿，不能把历史节点写成新的 Pending。

  事务化 `CreateApprovalTask` 在实例进入运行态后还必须校验目标节点仍是当前活动审批节点；历史审批节点和非审批节点不能绕过图运行时直接写入 Pending。实例仍停留在 Start 阶段时保留旧宿主手工构造兼容路径，正式运行态的待办创建则由活动节点门禁和实例行锁共同保护。

  Runtime 的所有图状态推进也共享同一行锁边界：Start、Condition、ParallelSplit/Join、Loop、Approval 后推进和 End 在事务内先锁定 Running 实例，再计算和持久化活动节点、Join 到达或循环计数快照。通知/业务动作的外部副作用仍由更严格的自动节点事务包裹；无事务内存宿主保持原有快照恢复和 CAS 兼容路径。

  `WorkflowInstanceService` 的公开状态用例也不能成为旁路：`Advance`、`AdvanceActive`、`AdvanceCondition`、`AdvanceLoop`、`SplitParallel`、`ArriveAtParallelJoin`、`ReturnTo`、终态变更和审批人快照固化在真实事务中先锁定实例行，再执行领域变更与 Revision CAS。这样业务模块或迁移补偿直接调用 Application 服务时，仍与 Runtime 入口使用同一并发边界。

  审批完成后由 Runtime 继续推进的自动业务动作继承当前待办的实际 `actor`，并通过 `WorkflowActionContext.Actor` 传入跨模块 handler；手工/系统 Continue 未指定时仍按系统动作处理，`Retry` 则传递流程发起人作为操作者。这样业务状态变更和后续审计不会因为动作发生在审批后的自动节点而丢失真实审批身份。

  节点进入/完成操作历史的去重键还必须绑定实例持久化 `Revision`。同一条图连线在受控 Loop、审批退回重入或并行分支再次进入时，Revision 已随状态推进变化，因此每一轮迁移都写入独立审计；事务回滚时 Revision 与审计一起回滚，重放同一状态仍保持幂等。历史数据中的无 Revision 键继续保留为不可变历史，不被迁移改写；新写入统一使用 Revision 作用域。

## Web 约定

- 页面路由按模块划分：`/Oa/...`、`/Crm/...`、`/Erp/...`、`/Pms/...`、`/Lms/...`。
- 菜单与按钮权限使用相同路径命名空间，例如 `Oa/Task`、`Crm/Customer`。
- 左侧主导航由 `MenuTree` 递归消费已按角色过滤的 `Admin.RoleMenus`，并叠加用户隐藏菜单偏好；根模块下仅在展示层按稳定工作区聚类已授权页面，原始菜单、角色授权和业务表不因导航分层迁移。含子项的菜单行将左侧页面或模块看板链接与右侧展开按钮分开，单页工作区不渲染冗余三级叶子，当前路由所属分支自动展开。根模块只指向已有概览或高频工作入口，后续模块/子模块看板通过 Application 聚合查询补齐，不从布局直接读取业务表。
- 业务实体优先继承 Admin 的审计实体基类；是否启用审批由用例决定，不把工作流字段塞入所有实体。
- 每个完成的业务闭环需同时提供实体、应用用例、页面、菜单种子和对应测试。

OA 工作台 `/Oa/Overview` 只读组合 `WorkTaskService`、`WorkScheduleService`、`AnnouncementService` 和 `NotificationService` 的既有查询，按当前会话用户只读取其未读通知；它展示待处理/逾期任务、未来七日日程、已发布公告和未读通知，并深链回原页面，不建立第二套任务、日程、公告或通知数据。`Oa/Overview` 是协同办公根菜单和“我的协作看板”的优先入口；若角色未获该菜单，布局根据该角色现有可见子菜单回退，避免导航将用户带到无权页面。

`/Oa/UnifiedSearch` 是首版只读跨模块查询入口。`CrossModuleSearchService` 只组合 `CustomerService`、`SalesContractService`、`SalesOrderService`、`PurchaseOrderService`、`InventoryService`、`SettlementService` 和 `PmsProjectService` 的已有查询，不直接跨模块读取仓储或写入任何业务数据。Web 以当前登录账号的七个原页面菜单权限构造 `CrossModuleSearchScope`，服务仅返回被允许对象类型的编号、标题、状态、金额/数量或计划摘要、可用负责人显示和既有深链，不返回 `OtherInfo`、联系方式、附件或原始业务 JSON。客户命中可安全扩展同客户合同、销售订单、项目和收款核销；合同或项目命中可扩展已关联销售订单；采购来源单号可定位采购订单和库存流水，核销流水号可定位核销记录。检索结果的对象类型统计和筛选只对已返回的受控摘要集合执行，不能成为查询其他模块对象的旁路。目标页面仍各自执行原有路由和菜单权限检查。组织数据范围、库存/回款的跨对象汇总和可版本化经营报表属于后续 3B-08 工作，不在该查询中伪造汇总口径。

CRM 经营看板 `/Crm/Overview` 只读组合 `CustomerService`、`CustomerFollowUpService`、`SalesOpportunityService` 和 `SalesContractService`，展示启用客户、逾期跟进、进行中商机预计金额和生效合同金额，并按跟进、商机、合同工作区深链到原页面。它不复制客户、机会或合同数据，也不修改销售状态；`Crm/Overview` 是客户经营根菜单和“客户经营看板”的优先入口，菜单权限缺失时沿用布局的可见子菜单回退规则。

OA 员工通讯录首版是只读的 Application 查询：`EmployeeDirectoryService` 通过 `IEmployeeDirectoryRepository` 获取平台 `SysUser` 与 `SysOrg` 投影，Web 页面只依赖应用服务，不直接查询平台表。通讯录默认只显示启用且 OA 档案不是离职状态的用户，可按组织、账号/姓名、组织名称和备注筛选；停用或离职用户只在显式选择“全部/停用”时展示。员工档案由独立的 `OaEmployeeProfile`、`IOaEmployeeProfileRepository` 和 `EmployeeProfileService` 管理，以平台 `SysUser.Id` 为唯一键，保存员工编号、电话、邮箱、企业微信/钉钉用户标识、职位、入职日期、生命周期状态和 `OtherInfo`；通讯录页面的 `Oa/Directory/Edit` 按钮权限与 Application 的 `canEdit` 门禁同时生效。企业微信/钉钉标识只作为通知渠道地址输入，不在通讯录卡片公开展示。后续招聘、入职和离职应继续扩展 OA 专属资料，不复制平台账号、角色和权限数据。

招聘与面试首版由 `OaRecruitmentCandidate`、`OaRecruitmentInterview` 和 `RecruitmentService` 管理。候选人与面试记录分表持久化，面试轮次按候选人唯一约束；候选人基础资料在已录用后不可编辑，录用状态必须经过 Application 查询确认至少一轮面试结论为 `Pass`。页面按钮使用 `Oa/Recruitment/Create`、`Oa/Recruitment/Edit`、`Oa/Recruitment/Interview` 和 `Oa/Recruitment/Status`，招聘审批、简历附件和账号开通继续留在后续切片，不把平台用户表提前改造成候选人表。

入职办理首版由 `OaOnboardingRecord`、`OnboardingService` 和 `IOaOnboardingRepository` 管理，每个已录用候选人最多一条入职记录；Application 通过 `RecruitmentService.GetCandidate` 查询候选人状态，不直接读取招聘表。记录维护工号、部门、职位、入职/试用期日期、培训计划和 `OtherInfo`，资料提交、合同签署、账号申请、培训完成四项清单与状态由入职领域维护，只有四项全部完成才能进入 `Completed`。页面使用 `Oa/Onboarding/Create`、`Oa/Onboarding/Edit` 和 `Oa/Onboarding/Complete` 按钮权限；账号申请只作为入职清单，不直接创建 `SysUser`、角色或员工档案，后续通过独立用例接入审批、附件和档案联动。

离职办理首版由 `OaOffboardingRecord`、`OffboardingService` 和 `IOaOffboardingRepository` 管理，每个员工用户最多一条离职记录；Application 通过 `EmployeeProfileService` 查询员工档案，只允许在职或停职员工进入办理。记录维护最后工作日、原因、交接摘要和 `OtherInfo`，交接完成、资产归还、车辆归还、文件归还、权限回收申请五项清单由离职领域维护，全部完成后才进入 `Completed`。`OffboardingRiskService` 通过 `VehicleService`、`CashAdvanceService`、`ExpenseReimbursementService` 和 `AssetService` 聚合待审批/待归还车辆、未结清借款、未完成付款报销和在用资产，页面保留各原单深链；`OffboardingService.Complete` 在账号停用前检查风险并阻断未处理事项，不直接跨模块读表。Web 生产实现注入 `IEmployeeAccountLifecycleService`，在同一 `IWorkflowTransactionBoundary` 中先将对应 `SysUser.IsEnabled` 置为 `false` 并递增 `AuthVersion`，再通过员工档案 Application 用例回写 `Resigned`；离职记录同时保存账号停用操作者、时间和原因，停用异常会阻止员工状态和离职完成态提交。页面使用 `Oa/Offboarding/Create`、`Oa/Offboarding/Edit` 和 `Oa/Offboarding/Complete` 按钮权限；资产待办关联、角色清理和离职审批仍属于后续切片。

OA 资产与办公用品首版由 `OaAsset`、`OaAssetAssignment`、`OaAssetOperation`、`OaAssetTransfer`、`OaAssetStocktake`、`OaAssetRequest`、`AssetService`、`AssetRequestService` 和对应 FreeSql 仓储管理，页面入口为 `/Oa/Asset`。资产台账保存分类、编号、名称、序列号、责任人、位置、状态和 `OtherInfo`；登记/编辑/领用/归还/状态变更/位置转移/盘点追加不可变操作流水，领用、归还、转移和盘点记录写入处于 `IWorkflowTransactionBoundary`。`OaAssetTransfer` 独立保存转移前后位置、责任人快照、原因、操作者和时间；`OaAssetStocktake` 保存盘点时的账面与实盘状态、责任人、位置快照、结果、差异原因和 `OtherInfo`，盘点不直接改写资产台账，差异/未找到必须有原因。差异/未找到盘点可在一次独立的结案动作中记录结论、操作者和时间，原盘点快照保持不变且追加 `StocktakeResolved` 流水；一致盘点、重复结案和直接以处置改写台账均被拒绝。数量型行政办公用品由独立的 `OaConsumableSupply`、`OaConsumableTransaction` 与 `ConsumableSupplyService` 管理：目录、入库和发放只使用 OA 表，按正负流水聚合余额；发放必须指定接收员工，库存不足、停用目录、重复来源单号和越权写入均被拦截。它不引用 ERP 商品、仓库、采购收货或单件资产归还，也不进入离职资产风险。普通领用先创建 `OaAssetRequest`，提交 `OA_ASSET_REQUEST_APPROVAL`；Workflow 批准动作才调用 `AssetService.Assign` 创建领用记录并将资产置为 `InUse`，审批失败不会锁定资产，申请驳回/撤回可重提；在用资产不能直接编辑或改状态，重复编号/重复领用、重复申请和按钮权限在 Application/Domain 校验。`OffboardingRiskService` 通过 `AssetService.ListByUser` 将在用资产加入离职阻断风险；维修明细、附件与低值易耗品的采购/费用规则仍待后续，不把首版台账当作 ERP 固定资产核算。

请假申请由 `OaLeaveRequest`、`LeaveRequestService` 和 `IOaLeaveRequestRepository` 管理，页面只查询当前登录用户自己的申请。领域记录请假类型、起止时间、计算时长、事由、`OtherInfo`、驳回原因和状态；Application 在提交时按同一用户查询 `Submitted`/`Approved` 申请并执行时间重叠门禁，草稿/驳回申请可编辑，已提交可撤回。提交通过 `OA_LEAVE_APPROVAL` 在事务边界内幂等启动 Workflow，`LeaveRequestWorkflowActionHandler` 仅经 `IOaLeaveRequestWorkflowApprover` 回写批准/驳回，撤回同步撤回运行中流程，页面显示审批状态、驳回原因并提供通用附件。`OaLeaveBalance`、`OaLeaveBalanceReservation`、`LeaveBalanceService` 维护年假/调休的员工年度额度和申请唯一占用：提交占用，驳回/撤回释放，批准转已使用；额度不足、未配置、跨年度和已失效占用均在 Application/Domain 门禁，FreeSql 以员工+年度+类型及申请 ID 唯一索引保证基本幂等。页面新增 `/Oa/LeaveBalance`，维护按钮为 `Oa/LeaveBalance/Manage`。`Oa/Leave/Create`、`Oa/Leave/Edit`、`Oa/Leave/Submit` 和 `Oa/Leave/Cancel` 仅控制 Web 操作入口；普通病假、事假和其他暂不强制额度，考勤/日历、代理人、部门审核和 PostgreSQL/SQL Server 数据库级并发回归仍待后续。

车辆管理由 `OaVehicle`、`OaVehicleUseRequest`、`OaVehicleMaintenance`、`VehicleService`、`VehicleMaintenanceService` 及各自仓储管理，页面入口为 `/Oa/Vehicle`。车辆台账保存车牌、类型、品牌型号、座位数、负责人、年检/保险日期、状态和 `OtherInfo`；用车申请保存申请人、驾驶员、起止时间、起止里程、目的地、事由、状态、驳回原因和 `OtherInfo`。Application 提交时只允许选择 `Available` 车辆，并按车辆查询 `Submitted`/`Approved` 申请执行时间重叠门禁；`OA_VEHICLE_USE_APPROVAL` 的 handler 通过 `IOaVehicleUseWorkflowApprover` 在同一业务事务内批准申请并将车辆置为 `InUse`，驳回不占车，申请人撤回会同步撤回运行实例，归还要求结束里程不小于起始里程并将车辆恢复 `Available`。维修记录保存登记人、开始时间、里程、内容、服务商、费用、完成说明和 `OtherInfo`；仅可用车辆可创建进行中维修，进行中维修阻止用车提交，登记人完成或取消维修后恢复可用。`VehicleComplianceReminderService` 每六小时按未来 30 天窗口扫描年检/保险日期，只向当前启用的车辆负责人发布站内提醒；去重键由车辆、提醒类型与到期日组成，已报废、负责人为空或负责人停用的车辆跳过，扫描不改写车辆与用车状态。页面只通过按钮权限调用应用服务，不直接写车辆表；图片附件、资产联动和数据库级并发唯一保护留待后续切片。

OA 费用报销首版由 `OaExpenseReimbursement` 主单、`OaExpenseLine` 明细、`ExpenseReimbursementService` 和两个 FreeSql 仓储管理。主单只保存申请、部门/主体公司/项目归属、金额汇总、`OtherInfo` 与申请生命周期；发票号和付款流水号在 Application 层按全局未取消报销单及当前主单分别执行重复校验，不能把同一票据重复用于多个报销。通用 `AttachmentService` 以 `BusinessType = OaExpenseReimbursement` 关联发票/小票附件，附件读写仍经过当前会话和审计边界。提交通过 `OA_EXPENSE_REIMBURSEMENT_APPROVAL` 启动 Workflow，`ExpenseReimbursementWorkflowActionHandler` 只调用 `IOaExpenseReimbursementWorkflowApprover` 回写批准/驳回；已批准报销由 `ExpenseReimbursementPaymentService` 通过 `PaymentRequestService` 创建唯一员工付款申请，创建后标记 `Reimbursed`，`PaymentExecutionService` 完成员工付款后回写 `Paid`，并校验申请人、金额和来源单号。借款冲销仍由 `CashAdvanceService` 通过报销 Application 服务完成，预算和 ERP 付款/核销不直接从 OA 页面读表。主单与明细的真实 PostgreSQL/SQL Server 迁移和浏览器写入需在后续回归记录中分别确认，SQLite 不增加专项测试。

OA 借款/备用金首版由 `OaCashAdvance`、`OaCashAdvanceOffset`、`CashAdvanceService` 和两个 FreeSql 仓储管理。借款主单保存申请、主体公司/部门/项目归属、预计冲销日期、借款金额、已冲销金额、余额、`OtherInfo` 和生命周期；冲销记录只保存借款 ID、报销 ID、冲销金额、日期和说明，并以报销 ID 唯一约束避免同一报销重复冲销。`CashAdvanceService` 通过 `ExpenseReimbursementService.Get` 查询报销，不直接访问报销表；只有同一申请人的 `Approved`/`Reimbursed` 报销才能登记冲销，余额、重复关联和状态门禁均在 Application/Domain 校验。提交通过 `OA_CASH_ADVANCE_APPROVAL` 启动 Workflow，handler 只回写借款审批状态；付款账户、实际付款、预算、财务复核和 ERP 付款/核销不由本切片模拟。真实 PostgreSQL/SQL Server 迁移和浏览器写入后续分别记录，SQLite 不增加专项测试。

Workflow 多审批人策略支持 `All`、`Any` 和 `Majority`。`Majority` 按当前审批轮次实例快照中的原始审批人数计算超过半数的同意门槛；达到后在同一事务取消该审批节点其余 Pending 待办。转交只改变当前轮次的实际处理人，不改变投票人数；并行分支中取消范围仍只限同一节点，随后继续既有 Join 汇聚语义。
`Quorum` 用于业务指定固定通过票数，必须设置正整数 `requiredApprovals`，且运行时不得超过当前轮次原始审批人快照人数；达到该票数后与 `Any`、`Majority` 一样只取消同一节点剩余 Pending 待办。这样转交不会缩放法定人数，也不会影响并行分支的 Join 收口边界。

CRM 合同、PMS 项目变更以及 ERP 采购订单和销售订单的审批完成动作已进一步收口：各自 handler 在 Web 作用域内通过 `ISalesContractWorkflowApprover`、`IPmsProjectChangeWorkflowApprover`、`IPurchaseOrderWorkflowApprover`、`ISalesOrderWorkflowApprover` 调用对应 Application Service 的 `ApplyApproval` 入口；普通业务入口仍保留审批完成门禁，Workflow 专用入口负责幂等、状态来源校验和仓储更新，避免 handler 与业务服务各自维护一套状态流转。ERP 核销此前已通过 `SettlementService.Approve`/`RejectApproval` 使用同一边界。

通知失败记录由 `NotificationFailureRetryService` 负责幂等补投和状态更新，Web 宿主的 `NotificationFailureRetryWorker` 每 5 分钟在独立 DI 作用域执行批量重试；重试次数达到 3 次的 Pending 记录会输出待处理数量、持续失败数量和最高重试次数告警。调度异常只记录日志，不能阻断主 Web 进程。管理 API `/api/admin/notification-failures` 受 `Admin/NotificationFailures` 菜单权限保护，手动重试 API 额外要求 `Admin/NotificationFailures/Retry` 按钮权限；页面和 API 只返回失败元数据，不返回可重放正文。手动重试支持单条或最多 50 条批量操作，自动去重并通过独立 `NotificationFailureAudit` 记录每条操作者、结果和时间，审计失败不阻断补投。

通知补投若已原子插入本次通知但后续 `MarkResolved` 失败，服务只按本次 `TryAdd` 成功的通知 ID 执行补偿删除；原子插入返回 false 的既有通知不会被删除，避免内存宿主和无外层事务调用留下孤儿通知或误删并发胜出通知。真实 PostgreSQL/SQL Server 事务仍优先依赖数据库回滚。
付款申请使用 `OaPaymentRequest`、`PaymentRequestService` 和单一 `IOaPaymentRequestRepository` 持久化付款意图，页面入口为 `/Oa/PaymentRequest`，Workflow 编码为 `OA_PAYMENT_REQUEST_APPROVAL`。`PayeeAccountReference` 只允许保存受控账户引用或末四位，不保存完整银行卡号；提交时必须具备前置单据号或业务依据，单号、金额、币种、日期、预算引用和 `OtherInfo` 由 Domain/Application 校验。Workflow handler 只通过 Application 入口回写 `Approved`/`Rejected`；批准后必须经过独立财务复核，通过后才满足实际付款登记门禁，不通过则回到 `Rejected` 并保留复核人、时间和原因，申请人可编辑重提。实际付款由 `OaPaymentExecution`/`PaymentExecutionService` 记录外部流水；供应商付款若前置依据是采购订单，服务端必须校验供应商、订单状态和可用余额，并在同一事务内生成 ERP 应付核销后将申请置为 `Paid`；员工/其他付款只保存外部参考号，不伪造 ERP 订单或银行回执。`OaPaymentBudget`/`OaPaymentBudgetReservation` 记录预算总额、占用、已执行和释放状态，一笔付款申请最多一条占用记录，驳回重提重新激活同一记录。`OaPaymentRequestStatusHistory` 以不可变记录追踪提交、审批、驳回、撤回和实际付款，Workflow、预算与付款执行使用 Application 事务边界写入主单和历史。`OaPaymentBatch`/`OaPaymentBatchItem` 只对尚未实际付款的已复核申请做同币种组批，保存批次金额和申请明细；提交后不可调整，撤回保留历史并释放重新组批资格。真实 PostgreSQL/SQL Server 迁移与浏览器写入回归另行记录，SQLite 不增加专项测试。

OA 采购申请使用 `OaProcurementRequest`、`OaProcurementRequestLine`、`ProcurementRequestService` 和两个 FreeSql 仓储，页面入口为 `/Oa/ProcurementRequest`，Workflow 编码为 `OA_PROCUREMENT_REQUEST_APPROVAL`。主单保存申请类型、部门/主体公司/项目、预算依据、需求日期、预计金额和 `OtherInfo`；明细保存产品可空引用、物料分类、名称快照、规格、数量、单位、预计单价和扩展 JSON。产品相关申请的每条明细必须有产品引用，非产品相关、办公用品和寻源申请禁止绑定产品；申请至少有一条明细且预计金额大于零才能提交。当前已增加 `OaProcurementBudget`/`OaProcurementBudgetReservation`：带预算依据的申请按主体公司、部门和预计金额在提交时占用，驳回/撤回释放，生成采购订单后转已执行；来源订单取消会恢复已执行预算，重试生单重新占用，预算服务不替代 ERP 财务总账。寻源需求批准后由 `ProcurementSourcingService` 创建 `OaProcurementSourcing`，录入不同已准入供应商的 `OaProcurementSourcingQuote`，至少两家报价才可提交比价，采购人员选择一个未过期报价后进入 `Awarded`；该阶段只冻结比价结果，不直接写 ERP 订单或库存。批准只回写 OA 采购申请状态，不写库存流水；拥有 `Oa/ProcurementRequest/GeneratePurchaseOrder` 权限的采购复核人员可通过 `ProcurementRequestPurchaseOrderService` 将“已批准、产品相关、恰一条产品明细”的申请生成一张 ERP `PurchaseOrder` 草稿；包含两条及以上产品明细时，页面和服务按明细生成多张订单，使用订单号前缀加序号，并通过 `SourceLineId` 与申请明细建立一对一追溯。拆单前统一预检有效来源订单，结合事务边界避免部分生单；带预算申请仅在整批订单创建成功后转为已执行。订单使用 `Requisition` 来源及 OA 单号/明细去重，存在有效来源订单时申请不再出现在待复核列表，取消全部来源订单后才允许重试；OA 页面不直接写 ERP 仓储。隔离 PostgreSQL Web 已验证预算申请提交、Workflow 批准、生单转已执行以及取消来源订单恢复可用额的完整链路。报价结果到采购订单的自动带出、采购订单审批和收货入库继续由后续用例负责。真实 PostgreSQL/SQL Server 迁移与浏览器写入分别记录，SQLite 不增加专项测试。

Web 宿主统一使用 Serilog：`Serilog.AspNetCore` 通过 `UseSerilog` 接管 ASP.NET Core 日志，保留 `FromLogContext`，并启用请求耗时/状态码日志。默认输出到控制台，同时按天写入 `src/VelrixWorkHub.Web/logs/velrix-*.log`，保留最近 14 个文件；`logs/` 已由仓库忽略规则排除。业务代码继续依赖 `Microsoft.Extensions.Logging.ILogger<T>`，不直接依赖 Serilog API；只有 Web 启动失败和宿主终止使用 bootstrap/fatal logger，避免模块层与日志实现耦合。密码、连接字符串和附件正文不得写入日志，生产环境通过 `Serilog:MinimumLevel:Override` 调整框架日志等级。

PMS 项目工作项由 `PmsProjectWorkItem`、`PmsProjectWorkItemService` 和独立仓储管理，入口为 `/Pms/WorkItem`。它保存项目、可选父工作项、外部来源类型/ID、标题、负责人可空用户 ID 与名称快照、参与人用户 ID JSON 与名称快照、优先级、计划/实际时间、可选提醒时间、反馈、验收驳回意见、状态和 `OtherInfo`，不复用 WBS 表也不写 OA 任务。Application 校验项目/父项归属、计划时间、来源完整性、完成反馈、终态不可编辑和有子项草稿不可删除；附件复用通用附件模型。批注/操作历史、提醒、审批和组织/角色范围按 PP2-01 分段落地。

工作项的可追溯活动由 `PmsProjectWorkItemActivity` 与独立仓储维护，记录创建、状态变更和批注的操作者、时间、内容及状态前后值。`PmsProjectWorkItemService` 在创建和成功状态变更后追加活动，批注入口先确认工作项仍存在再追加不可修改的评论；页面以独立 `Pms/WorkItem/Comment` 按钮权限控制批注输入，并回显最近活动。活动不改变工作项主状态，也不删除或改写历史，因此会议来源行动项可沿用同一审计链。受控参与人、提醒和验收审批已落地，组织范围仍在后续 PP2-01 切片。

新建/编辑页面通过 `EmployeeDirectoryService` 提供启用人员单选/多选，`CreateForPeople` 和 `EditForPeople` 只接受人员 ID，在 Application 层验证人员启用、参与人不重复且负责人不重复进入参与人后，保存负责人/参与人用户 ID 与显示名快照。文本入口只保留给历史或来源兼容调用，Web 不再提供自由文本录入。`PmsProjectMember` 同样保存可空 `UserId`、成员姓名和部门快照；`CreateForPerson`/`EditForPerson` 仅解析启用目录人员，按用户 ID 保证同项目成员唯一，历史无 ID 成员继续以姓名规则兼容。人员目录同时投影平台 `SysRoleUser` 的稳定角色 ID 与名称。工作项的额外部门和角色范围分别保存为组织/平台角色稳定 ID JSON，Application 只接受目录中存在的组织或角色；`ListVisible` 对管理员返回全量，对普通用户返回其作为负责人、参与人、具有稳定项目成员关系、当前目录组织或平台角色命中工作项范围的数据。空范围不扩权，历史姓名/部门快照和项目自由文本角色不参与范围匹配，保证组织和角色调整以当前目录关系立即收敛。`PmsProjectWorkItemReminderService` 每五分钟扫描提醒时间已到的非终态项目项，只向仍在启用目录中的负责人用户名调用统一 `NotificationService.Publish(..., Reminder, ...)`；去重键由工作项 ID 与冻结提醒时间构成，重复扫描不产生重复通知。没有负责人、负责人停用、未来提醒和终态项均跳过，通知失败沿用统一失败记录与重试边界，不反向修改工作项或中断扫描。进行中工作项由负责人提交 `PMS_WORK_ITEM_COMPLETION_APPROVAL`；运行实例期间状态为 `PendingApproval` 且禁止编辑，`PmsProjectWorkItemWorkflowActionHandler` 只经 Application 的审批入口把批准回写为 `Completed`、驳回或撤回回写为 `InProgress`，同时保存意见和活动。职位和用户组范围需要先扩展目录契约，不以显示名伪造授权来源。

PMS 会议由 `PmsProjectMeeting`、`PmsProjectMeetingService` 和独立仓储管理，入口为 `/Pms/Meeting`。会议保存项目、主题、类型、起止时间、地点或会议方式、主持人、参与人文本快照、纪要、决定项和 `OtherInfo`；它不创建第二套行动项表。创建行动项时，Application 按会议 ID 重新读取持久化会议，再调用 `PmsProjectWorkItemService.Create` 并固定会议的 `ProjectId`、`SourceType=PmsProjectMeeting` 与 `SourceId`，因此调用方不能把行动项伪造到其他项目或不存在的会议。会议查询只按这两个来源字段回显行动项；已有行动项的会议不能删除，以保持项目和会议上下文可追溯。

PMS 需求到交付首版由 `PmsDeliveryRecord` 和 `PmsDeliveryRecordStatusHistory` 实现，入口为 `/Pms/Delivery`。一个记录通过类型区分缺陷、评审和发布，保存项目、可选需求/WBS、负责人、编号、状态、描述、评审结论、发布版本/结果及 `OtherInfo`，并复用统一附件，不复制需求或 WBS 表。Application 在写入前校验项目、需求和 WBS 均属同一项目，要求缺陷和评审提供需求、发布提供版本号；评审通过/不通过必须已有结论，发布必须已有结果。状态转换按类型限制，创建及每次状态转换都追加带操作者、说明和时间的历史，终态记录不可编辑，保证来源和结果能回溯。

PMS 项目工作日历由 `PmsProjectCalendarOverride`、`PmsProjectCalendarService` 和独立仓储管理，入口为 `/Pms/Calendar`。它只保存项目计划周期内的单日覆盖（工作日/非工作日及可选说明）；未覆盖日期由平台 `WorkingDayCalendar` 派生，项目模块不复制或修改全局 `SysHoliday`。同一项目同一日期保存时更新既有覆盖，删除覆盖恢复平台规则；查询窗口受限为 62 天，项目不存在、越出项目周期和过长说明都在 Application/Domain 拒绝。班次、任务/工时审批、逾期提醒以及基线/变更联动仍在 PP2-04 后续切片。
