# Velrix Work Hub 继续开发交接

更新时间：2026-07-26

## 当前已验证基线

- Domain 全量：`670/670` 通过。
- Web 构建：`0` 警告、`0` 错误。
- `OtherInfo` 只保留为领域/存储内部扩展载荷；普通页面不得显示或编辑原始 JSON。LMS、PMS、CRM/ERP 主数据和 OA 资产相关页面已直接收口；全局 UI 守卫仅兜底遗留模板，不能隐藏相邻业务信息。
- 本轮已启动 Development Web 宿主并使用隔离 PostgreSQL 库 `velrixworkhub_webtest_20260719c` 完成 OA 页面写入与 Workflow 回归；浏览器截图和 CLI 会话证据均在 `artifacts/output/playwright/`，相关页面控制台 0 错误/0 警告。最终 Domain `606/606`、Web 构建 `0` 警告/`0` 错误。
- PostgreSQL/SQL Server 的 Workflow 探针已有历史通过记录；现有业务 PostgreSQL 库若发现重复流程定义版本，必须保持迁移保护，不能自动删除或覆盖历史数据。

## 最近完成

- `OaCashAdvanceRepayment` 借款还款申请已具备创建、审批、驳回、驳回编辑重提、草稿/审批中单条撤回、附件和余额结清边界。
- `UnifiedTodoService` 已将借款还款审批映射为 OA，显示“OA 借款还款审批”，并保留当前审批人的 Workflow 收件箱深链。
- OA 付款申请新增财务复核状态和权限入口：Workflow 批准后才可复核，复核不通过回到 `Rejected` 并支持申请人编辑重提。
- OA 付款申请新增实际付款登记：`RegisterPayment` 只能处理财务复核通过的申请；供应商付款校验采购订单和供应商后，在同一事务内生成 ERP 应付核销并置为 `Paid`，员工/其他付款只保存外部流水引用。
- OA 付款申请新增不可变状态历史：提交、Workflow 审批/驳回、撤回和实际付款均记录前后状态、操作者、时间和原因，页面回显最近记录。
- OA 付款申请新增预算台账：带预算编号的申请按主体公司、部门、币种提交占用，驳回/撤回释放，实际付款消耗；驳回重提复用同一占用记录。
- OA 付款申请新增付款批次首版：已批准且财务复核通过、尚未实际付款的申请可以按币种组批；批次提交后不可编辑，撤回保留明细并允许重新组批。银行接口、外部批量支付和真实回执仍未实现。
- OA 采购申请新增预算占用首版：带预算依据的申请按主体公司、部门和预计金额提交占用，驳回/撤回释放，生成采购订单后转已执行；取消来源订单恢复已执行预算，重试生单重新占用。
- OA 采购申请新增多明细拆单首版：已批准且每条均绑定产品的申请按明细生成多张 ERP 草稿订单，使用序号化订单号和 `SourceLineId` 追溯；拆单前统一预检，部分取消不能重试，全部取消后才可重新整批生单。报价结果已可通过独立寻源复核入口带入采购订单；采购订单审批、收货和关闭已有 ERP 应用入口及历史浏览器证据，本轮补齐收货事务回滚。
- OA 报销到员工付款申请首版：`ExpenseReimbursementPaymentService` 只允许已批准报销按申请人、金额和报销单号幂等生成一份 `EmployeePayment`，创建后报销为 `Reimbursed`；`PaymentExecutionService` 完成员工付款前重新校验关联报销，付款成功后回写 `Paid`。报销页已增加账户、银行、付款日期和 OtherInfo 输入及按钮权限入口；本轮浏览器验证的是独立员工付款成功路径，报销级联入口仍待专项回归。
- ERP 采购订单收货已收口为事务切片：`PurchaseOrderService.Receive` 在同一事务中推进订单状态并写入 `{OrderNo}-IN` 入库流水，入库失败恢复为 `Submitted`，已有来源流水在状态变更前被幂等拦截；自动化覆盖成功、回滚和重复来源三条路径。浏览器/PostgreSQL 业务写入本轮仍未执行。
- OA 中选报价转 ERP 订单首版已完成：`ProcurementSourcingPurchaseOrderService` 只允许已定标寻源单生成 `Sourcing` 来源草稿订单，自动带入中选供应商、报价金额和寻源编号；产品、数量、付款到期日由采购复核输入，重复调用返回已有未取消订单。浏览器/PostgreSQL 业务写入本轮仍未执行。
- OA Workflow 审批结果通知首版已完成：`OaWorkflowOutcomeNotificationService` 接入请假、加班、报销、借款、借款还款和付款申请 handler，按启用申请人和 Workflow 实例幂等投递批准/驳回通知，停用或未知用户跳过，驳回意见保留在通知内容中。浏览器/PostgreSQL 业务写入本轮仍未执行。
- OA 离职平台账号回收首版已完成：`OffboardingService` 在五项清单完成后通过 `IEmployeeAccountLifecycleService` 停用平台账号并递增 `AuthVersion`，再回写员工档案为 `Resigned`；`OaOffboardingRecord` 保存停用操作者、时间和原因，账号停用失败不会进入完成态，生产实现由 `IWorkflowTransactionBoundary` 保护。浏览器/PostgreSQL 业务写入本轮仍未执行。
- OA 请假额度首版已完成：`OaLeaveBalance`/`OaLeaveBalanceReservation` 和 `LeaveBalanceService` 按员工、年度、年假/调休维护额度；提交建立申请唯一占用，驳回/撤回释放，Workflow 批准转为已使用，额度不足、未配置、跨年度和失效占用被服务端拦截。新增 `/Oa/LeaveBalance` 页面和 `Oa/LeaveBalance/Manage` 按钮权限；病假、事假和其他暂不强制额度，浏览器/PostgreSQL 业务写入本轮仍未执行。
- OA 离职风险清单首版已完成：`OffboardingRiskService` 通过车辆、借款、报销和资产 Application 服务读取待审批/待归还车辆、未结清借款、未完成付款报销和在用资产；离职页面显示原单深链，`OffboardingService.Complete` 在账号停用前阻断风险。资产待办关联、角色清理和离职审批仍未实现，浏览器/PostgreSQL 业务写入本轮仍未执行。
- OA 资产台账首版已完成：`OaAsset`/`OaAssetAssignment`/`OaAssetOperation`、`AssetService` 和 `/Oa/Asset` 支持资产/办公用品台账、领用、归还、状态门禁和登记/编辑/领用/归还/状态变更不可变流水；领用/归还双写失败会回滚内存状态，在用资产已纳入离职风险。本轮 PostgreSQL Web 已验证资产申请批准后领用锁定、位置转移和一致盘点；库存数量、维修、附件及差异处置仍未实现。
- OA 资产申请审批首版已完成：`OaAssetRequest`/`AssetRequestService` 接入 `OA_ASSET_REQUEST_APPROVAL`；申请人只能针对可用资产提交申请，Workflow 批准动作才生成领用记录并锁定资产，驳回要求原因且可编辑重提，撤回不锁定资产，统一待办和审批结果通知复用平台能力。本轮 PostgreSQL Web 已验证申请创建、提交、收件箱批准、资产变为在用和领用记录；驳回/重提/撤回专项回归仍待执行。
- OA 资产位置转移追溯首版已完成：`OaAssetTransfer`/`IOaAssetTransferRepository` 接入 `AssetService` 和 `/Oa/Asset`；转移保存原/新位置、责任人快照、原因、操作者和时间，并追加 `Transferred` 操作流水。维修中/已报废资产禁止转移，在用资产责任人不直接变更，只允许位置迁移；转移或后续流水写入失败时恢复资产位置并补偿本次转移记录。本轮 PostgreSQL Web 已验证在用资产位置从“交付部办公区”迁移到“研发部设备间”，责任人保持不变。
- OA 资产盘点证据首版已完成：`OaAssetStocktake`/`IOaAssetStocktakeRepository` 接入 `AssetService` 和 `/Oa/Asset`；盘点保存账面与实盘状态/责任人/位置快照，区分一致、差异和未找到，差异/未找到要求原因，盘点不直接改写台账，后续操作流水失败时补偿盘点记录。本轮 PostgreSQL Web 已验证一致盘点回显且台账状态保持不变；差异处置、维修明细和附件仍未执行。
- OA 采购寻源/比价首版：已批准的寻源需求可建立寻源单，至少录入两家不同已准入供应商报价后提交比价，选择未过期中选报价；撤回保留历史并支持新一轮寻源。定标后可由采购复核生成 `Sourcing` 来源 ERP 草稿订单；采购订单审批和收货已有 ERP 应用入口，收货事务回滚已在本轮补齐。
- Workflow 纯引擎的 CAS、事务、受限图运行时、失败回滚和待办/通知幂等已完成阶段收尾；自定义表单和 Canvas 仍不在当前开发范围。
- 本轮浏览器新增：通讯录档案编辑、招聘→面试→录用、入职四项清单完成、请假/报销/借款/付款/加班/采购提交并由 Workflow 批准、员工付款财务复核与实际付款成功、供应商付款缺采购订单负向门禁、借款报销冲销与还款结清、资产申请批准后领用锁定、资产位置转移、资产一致盘点、请假额度配置和付款预算创建。采购无效预算编号先被服务端正确阻断，清空预算后提交成功。截图统一在 `artifacts/output/playwright/`。
- 本轮修复 Web 权限上下文：所有使用 `Admin.AuthButton` 的 OA/PMS 页面均在自身初始化阶段等待 `Admin.InitAsync`，解决布局初始化完成但页面按钮仍误隐藏的问题；服务端权限校验未放宽。

## 当前未完成边界

- OA 报销、借款、付款申请、加班、资产盘点和采购申请已完成首轮真实 PostgreSQL 浏览器写入；本轮已补充主要批准链、借款冲销/还款、付款财务复核/实际付款、资产领用审批和资产位置转移。仍待专项回归的是驳回/撤回/越权/重复门禁、付款批次、采购复核生单和采购寻源比价。
- 借款/还款暂不生成 ERP 付款、银行指令或核销流水；付款批次暂不触发银行或外部批量支付，真实回执和银行接口仍待后续切片。
- 站外通知的短信、企业微信和钉钉 Provider 尚未接入；SMTP 默认关闭。
- 离职已接入平台账号停用，并已通过 Application 风险服务核验资产/车辆/借款/报销；角色清理、离职审批和待办任务关联仍未实现。
- SQLite 按约定不增加专项测试。
- 请假额度已接入 Application/Domain，但数据库级并发 CAS、考勤/日历、代理人和部门审核仍未实现。
- 离职风险首版已覆盖车辆、借款、报销和在用资产；任务关联、库存数量/盘点/最终处理审计仍未实现，资产位置转移追溯已完成首版。

## 推荐下一步

优先选择一个可独立验收的 P0 垂直切片：

1. 补齐 OA-07 资产库存数量、盘点和归还结果的最终处理审计；或
2. 完成 OA-05 批准后的考勤/日历影响边界，补充代理人、部门审核和并发重复申请测试；或
3. 在独立、无历史重复定义的 PostgreSQL 测试库执行 OA 报销、付款申请和借款还款浏览器写入回归。

继续开发前必须先阅读 `AGENTS.md`、`docs/roadmap.md`、`docs/testing-points.md` 和 `docs/architecture.md`，所有验证产物放入 `artifacts/`，不得创建或修改 `.git`。
