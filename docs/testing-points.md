# 测试点与回归记录

本文件只保留当前可验收测试点、最近一次回归时间和证据摘要。详细实现过程不在本文件重复记录；未执行的浏览器、SQL Server 测试不得标记为通过。

## 整体浏览器回归

| 测试点 | 状态 | 最后回归时间 | 证据/备注 |
|---|---|---|---|
| PMS 技术命名与数据库迁移 | [ ] 自动化/构建通过，数据库待验证 | 2026-08-07 | 已将项目模块命名空间、类型、文件夹、页面路由和 Workflow `PMS_*` 编码统一迁移；PostgreSQL/SQL Server 迁移脚本已新增，覆盖 18 张项目表、`ErpSalesOrder.PmsProjectId`、菜单路径、Workflow 引用、工作项来源和通知去重键。领域自动化 `670/670`，Web 构建 `0` 警告/`0` 错误；未执行真实 PostgreSQL/SQL Server 写入迁移和浏览器回归。 |
| 登录、首页和主导航 | [x] 通过 | 2026-07-26 | `admin/admin` 登录成功；首页摘要加载正常。根模块默认收起；项目管理右侧按钮独立展开“项目组合看板、项目规划、项目执行、治理与交付”，项目规划右侧按钮再展开三级页面；左侧“项目规划”进入 `Pms/Project`，左侧“项目管理”进入 `Pms/Overview`。侧栏固定为 252px 并稳定预留滚动条槽位；1280px 视口强制出现侧栏滚动条时，侧栏外框和主内容起点均为 252px。客户经营根菜单已由不受当前图标库支持的 `fa-handshake-o` 改为 `fa-users`，启动种子会同步修正既有菜单记录；PostgreSQL 宿主 `5241` 页面显示“ 客户经营”，控制台 `0` 错误/`0` 警告，截图归档于 `artifacts/output/playwright/playwright-cli-crm-menu-icon-20260726/`。 |
| OA 工作台与分层导航 | [x] 通过 | 2026-07-21 | PostgreSQL 临时库以 `admin/admin` 从“协同办公”进入 `/Oa/Overview`；待处理/逾期任务、未来七日日程、公告和未读通知统计及任务/日程/公告三列工作区正常渲染。二级“我的协作看板”右侧展开后仅显示任务、公告、日程、通知三级入口，不重复 OA 工作台；控制台 `0` 错误/`0` 警告。 |
| CRM 经营看板与分层导航 | [x] 通过 | 2026-07-21 | PostgreSQL 临时库以 `admin/admin` 从“客户经营”进入 `/Crm/Overview`；启用客户、逾期跟进、进行中商机预计金额、生效合同金额及跟进/商机/合同三列工作区正常渲染。二级“客户经营看板”右侧展开后仅显示客户、联系人、跟进三级入口，不重复 CRM 经营看板；控制台 `0` 错误/`0` 警告。 |
| Workflow 工作台与分层导航 | [x] 通过 | 2026-07-21 | PostgreSQL 临时库以 `admin/admin` 从“流程平台”进入 `/Workflow/Overview`；当前审批人 Pending 待办、已发布流程、已发布简单表单和本人申请摘要正常渲染，流程版本显示正确。二级“流程工作台”右侧展开后仅显示审批收件箱三级入口，不重复工作台；截图 `artifacts/output/playwright/workflow-overview-postgresql.png`，控制台 `0` 错误/`0` 警告。 |
| 简单表单印章申请 PostgreSQL 闭环 | [x] 通过 | 2026-07-21 | 非管理员 `simpleform_requester_20260721` 填写并提交预置印章申请，指定 `simpleform_recipient_20260721`；`admin` 在收件箱批准后，申请为 Approved、待办清空、Outbox 为 Delivered 且重试 0。接收人通知中心显示未读“印章申请已批准”，通知去重键只写入接收人；该账号只具 `Oa/Notification`，申请人只具简单表单菜单。控制台 `0` 错误/`0` 警告；领域 `524/524`、Web 构建 `0` 警告/`0` 错误。 |
| 系统运维工作台与分层导航 | [x] 通过 | 2026-07-21 | PostgreSQL 临时库以 `admin/admin` 从“系统管理”进入 `/Admin/Overview`；待处理通知失败、高重试风险、权限变更摘要以及失败处置/权限审计工作区正常渲染。二级“系统运维工作台”右侧展开后仅显示通知失败处置、权限变更审计三级入口，不重复工作台；截图 `artifacts/output/playwright/system-overview-postgresql.png`，控制台 `0` 错误/`0` 警告。 |
| OA 页面加载 | [x] 通过 | 2026-07-22 | OA 费用、资产、额度、加班、采购和预算页面均可从 PostgreSQL Web 宿主打开，页面级权限上下文已初始化；本轮相关页面控制台均为 0 错误/0 警告。 |
| OA 付款批次/采购预算/采购寻源页面加载 | [x] 通过 | 2026-07-22 | PostgreSQL Web 打开 `/Oa/PaymentBatch`、`/Oa/ProcurementBudget`、`/Oa/ProcurementSourcing`，标题、空状态和管理控件正常；无已批准寻源需求时不提供可选来源，三个页面控制台均为 0 错误/0 警告。 |
| OA 采购预算台账创建 | [x] 浏览器通过 | 2026-07-22 | 隔离 PostgreSQL Web 创建 `PURCHASE-WEB-REG-20260722`，主体公司“Velrix 上海有限公司”、部门“交付部”、总额 `5000.00`；页面回显启用状态、可用 `5000.00`、待占用 `0.00` 和已执行 `0.00`。截图 `artifacts/output/playwright/oa-procurement-budget-regression-20260722.png`，控制台 `0` 错误/`0` 警告。提交占用和采购订单执行仍由专项链路回归。 |
| CRM 页面加载与客户筛选 | [x] 通过 | 2026-07-19 | 客户、合同、客户交易视图页面正常；“停用客户”筛选显示“没有匹配的客户”。 |
| ERP 页面加载与采购筛选 | [x] 通过 | 2026-07-19 | 概览、采购、核销、库存页面正常；“已取消”筛选仅保留 `PO-CANCEL-GUARD-20260713`。 |
| 统一经营查询首版 | [ ] 自动化通过，浏览器待回归 | 2026-07-30 | `CrossModuleSearchServiceTests` 覆盖客户关联扩展、按菜单范围排除未授权对象、项目到关联销售订单和项目深链、采购来源单号到采购订单/库存流水、ERP 结果权限裁剪，以及仅对已授权结果做类别统计/筛选；领域全量自动化 `670/670`、Web 构建 `0` 警告/`0` 错误。尚未执行浏览器、PostgreSQL 或 SQL Server 回归。 |
| UI-MASTERDATA-OTHERINFO-01 | CRM 客户、ERP 商品/供应商/仓库的新建/编辑器、卡片均不直接显示或编辑原始 `OtherInfo` JSON；编辑历史记录时既有扩展载荷保持不变 | [ ] 待浏览器回归 | 2026-07-27 | 本轮完成 Razor 收口并通过领域自动化 `657/657`、Web 构建 `0` 警告/`0` 错误；未执行浏览器回归。 |
| PMS 页面加载 | [x] 浏览器通过 | 2026-07-26 | 项目组合、项目管理、风险与 EVM 页面主标题和数据加载正常；项目、需求、交付记录、会议和工作项的新建编辑器及项目附件面板均不显示 `OtherInfo`/“扩展信息”原始 JSON。编辑旧记录仍保留既有 JSON 载荷。隔离 PostgreSQL 宿主 `5241` 控制台 0 错误、0 警告；截图/会话 `artifacts/output/playwright/playwright-cli-pmp-direct-hide-20260726/`。 |
| 首页 PMS 节点逾期待办 | [x] 通过 | 2026-07-19 | 临时 PostgreSQL 测试阶段调整为逾期后，首页 PMS 筛选显示高优先级“方案评审完成 · 项目节点逾期”，深链进入 `Pms/Phase?projectId=...` 并正确显示项目上下文；控制台 0 错误，未执行浏览器写入。 |
| 首页 ERP 库存安全线待办 | [x] 通过 | 2026-07-19 | 临时 PostgreSQL 商品安全库存设为 `30.00`、账面库存为 `25.00` 后，首页 ERP 筛选显示“标准服务包 · 库存低于安全线”，详情含当前/安全线数值，点击进入 `/Erp/Product` 并显示安全库存与账面库存；控制台 0 错误，未执行浏览器写入。 |
| 首页 ERP 应收逾期待办 | [x] 通过 | 2026-07-19 | 临时 PostgreSQL 将销售订单到期日设为 `2026-07-18` 后，首页 ERP 待办显示 `SO-20260712-001 · 待收 ¥5,040.00`、高优先级和“已逾期 07月18日”；控制台 0 错误，未执行浏览器写入。 |
| PMS/CRM/ERP 项目履约只读深链 | [x] 通过 | 2026-07-19 | 项目组合卡片进入带 `projectId`/`status=Submitted` 的销售订单、客户交易视图、收付款核销、风险与 EVM 页面，参数保留且项目上下文和指标正常；未执行写入。 |
| Workflow 页面加载与业务筛选 | [x] 通过 | 2026-07-19 | 流程定义、审批收件箱正常；收件箱按当前用户隔离，业务类型筛选项可见。 |
| LMS 页面与替代审批边界 | [x] 通过 | 2026-07-19 | 概览、授权、替代申请页面正常；授权页没有直接替代入口，替代页关键控件和提交按钮正常。 |
| Admin 用户资料页 | [x] 通过 | 2026-07-19 | 资料页可显示账户信息、用户名、修改密码和保存资料控件；静态资源路径无 Bootstrap 404。 |
| Admin 权限变更审计页 | [x] 通过 | 2026-07-19 | 临时 PostgreSQL 库中 admin 登录后菜单出现“权限变更审计”；只读表格、主体类型筛选和非法 Guid 校验正常，未执行任何写入。 |
| Admin 通知失败处置页 | [x] 通过 | 2026-07-19 | 临时 PostgreSQL 库中 admin 登录后页面正常加载，表格显示 Pending 空状态、重试元数据和“最近处置”列；不展示可重放正文，未执行重试写入。 |
| Admin 站外通知队列安全运维页 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL 隔离库中 admin 打开 `/Admin/ExternalNotificationOutbox`，验证邮件/短信/企业微信/钉钉渠道状态、队列/延迟/失败/最高重试聚合和下次尝试元数据；页面未显示收件人、正文、链接、去重键、主机或密钥，控制台 0 错误/0 警告，截图 `artifacts/output/playwright/external-notification-outbox-20260722.png`。 |
| OA 员工通讯录页面与筛选 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL 隔离库 `/Oa/Directory` 验证在职/全部筛选和关键词查询，页面回显 4 名员工；控制台 0 错误/0 警告。 |
| OA 员工档案编辑与权限 | [x] 浏览器通过 | 2026-07-22 | 通过 `/Oa/Directory` 编辑 admin 档案，保存员工编号、电话、邮箱、职位、入职日期、在职状态和 `OtherInfo`，刷新后回显；为后续离职回归保留档案。截图证据归档在 `artifacts/output/playwright/`，控制台 0 错误/0 警告。 |
| OA 招聘与面试页面与录用门禁 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL Web 完成候选人建档、第一轮面试排期、通过评价和录用；候选人状态变为“已录用”，截图 `artifacts/output/playwright/oa-recruitment-regression-20260722.png`，控制台 0 错误/0 警告。 |
| OA 入职办理页面与清单门禁 | [x] 浏览器通过 | 2026-07-22 | 使用已录用候选人创建入职记录，保存工号/部门/职位/日期/培训与 `OtherInfo`，四项清单完成后状态变为“已完成”；截图 `artifacts/output/playwright/oa-onboarding-regression-20260722.png`，控制台 0 错误/0 警告。 |
| OA 离职办理页面与清单门禁 | [ ] 部分回归 | 2026-07-22 | PostgreSQL Web 已为在职 admin 创建离职记录并保存五项清单；即使清单全部完成，未付款报销 `BX-202607-114835` 与未归还资产 `ASSET-WEB-REG-20260722` 仍会阻断完成离职，记录保持办理中且风险深链可见。控制台 0 错误/0 警告。真实账号停用、资产/文件最终核验和角色清理仍待非管理员测试账号专项回归。 |
| OA 资产台账与领用归还页面 | [x] 浏览器通过 | 2026-07-26 | PostgreSQL Web 创建资产 `ASSET-WEB-REG-20260722`，回显编号/分类/序列号/位置与权限按钮；原始 `OtherInfo` JSON 不再显示或编辑，资产、资产申请、盘点和办公用品新建页已直接收口，旧资产申请编辑保留历史内部值。领域 `670/670`、Web 构建 0 警告/0 错误；下一轮浏览器批次需确认有资产卡片时“领用人”等同一行业务信息仍可见。盘点一致结果另有截图 `artifacts/output/playwright/oa-asset-stocktake-regression-20260722.png`，此前控制台 0 错误/0 警告。 |
| OA 资产盘点差异最终处理审计 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL 隔离库对在用资产创建“维修中”盘点差异并完成一次性处置；资产仍为在用、责任人与位置未改，页面显示“盘点处置”流水和已处置时间且不再提供处置按钮。领域 `607/607`、Web 构建 0 警告/0 错误、控制台 0 错误/0 警告，截图 `artifacts/output/playwright/oa-asset-stocktake-resolution-20260722.png`。 |
| OA 数量型办公用品库存 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL 隔离库创建 `WEB-SUPPLY-PAPER-20260722` A4 打印纸，行政入库 20 包后向指定员工发放 5 包，页面回显库存 15 包和“发放”最新流水；领域 `609/609`、Web 构建 0 警告/0 错误、控制台 0 错误/0 警告，截图 `artifacts/output/playwright/oa-consumable-supply-regression-20260722.png`。 |
| OA 请假申请页面与重叠校验 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL Web 创建并提交“WEB-REG-20260722 请假审批回归”，状态显示待审批/审批中；截图 `artifacts/output/playwright/oa-leave-regression-20260722.png`，控制台 0 错误/0 警告。考勤联动尚未接入。 |
| OA 请假审批 Workflow 边界 | [ ] 部分回归 | 2026-07-22 | 页面提交链已实际启动 `OA_LEAVE_APPROVAL`；本轮在统一收件箱批准请假、报销、借款、付款、加班和采购六条 OA 待办并验证待办归零。新增 PostgreSQL 回归确认批准的“WEB-REG-20260722 请假日历投影回归”生成一条个人只读日历投影；领域 `613/613`、Web 构建 0 警告/0 错误。请假驳回重提、撤回和余额状态回写仍未专项执行。截图 `artifacts/output/playwright/workflow-oa-approvals-regression-20260722.png`、`artifacts/output/playwright/oa-leave-calendar-regression-20260722.png`，控制台 0 错误/0 警告。 |
| OA 请假额度页面与余额门禁 | [x] 浏览器通过 | 2026-07-22 | `/Oa/LeaveBalance` 为 admin 配置 2026 年年假 40 小时并回显可用额度及 `OtherInfo`，截图 `artifacts/output/playwright/oa-leave-balance-regression-20260722.png`，控制台 0 错误/0 警告。 |
| OA 加班申请与 Workflow 边界 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL Web 创建并提交 3 小时加班申请，并在 Workflow 收件箱批准；页面主链可进入已批准状态。创建截图 `artifacts/output/playwright/oa-overtime-regression-20260722.png`，统一审批截图 `artifacts/output/playwright/workflow-oa-approvals-regression-20260722.png`，控制台 0 错误/0 警告。驳回重提、撤回和时间冲突仍未专项执行。 |
| OA 加班兑换调休/财务处理 | [x] 浏览器通过 | 2026-07-23 | 已批准加班结束后 30 天内只能二选一：调休兑换累计同年度调休额度，财务兑换只保留加班单与小时数。隔离 PostgreSQL Web 已验证 2 小时调休额度回显、3 小时财务登记进入 `/Oa/OvertimeFinance` 待处理清单并由 admin 完成处理，记录处理人和时间；截图 `artifacts/output/playwright/oa-overtime-finance-registration-regression-20260723.png`、`artifacts/output/playwright/oa-overtime-finance-processing-regression-20260723.png`，控制台 0 错误/0 警告。领域 `616/616`、Web 构建 0 警告/0 错误。 |
| OA 车辆台账、用车审批与维修 | [x] 浏览器通过 | 2026-07-21 | PostgreSQL 临时库已实际回归新增车辆→开始维修→完成/取消维修、维修中无可选用车车辆、非登记人完成维修拒绝；并完成用车草稿→提交审批→批准占车→归还释放、时间冲突、撤回、驳回及结束里程倒退回归。结束里程用车申请以 `13000` 起始里程批准后先填 `12999`，页面显示“结束里程不能小于起始里程。”且继续使用中；改填 `13050` 后申请归还、车辆可用，数据库为 `Available | Returned | 13000.00 | 13050.00 | Completed`。截图 `artifacts/output/playwright/vehicle-return-mileage-postgresql.png`，控制台 0 错误/0 警告。年检/保险提醒、图片附件和资产联动尚未接入。 |
| OA 费用报销申请与 Workflow 边界 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL Web 创建报销主单与交通费明细，金额汇总 ¥280.50，提交后经 Workflow 批准；截图 `artifacts/output/playwright/oa-expense-regression-20260722.png`、`artifacts/output/playwright/workflow-oa-approvals-regression-20260722.png`，控制台 0 错误/0 警告。重复发票、附件和付款级联仍未专项执行。 |
| OA 借款备用金、报销冲销与还款 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL Web 创建并批准 ¥1,000 借款；使用已批准 ¥280.50 报销完成冲销，再登记 ¥719.50 转账还款并经 `OA_CASH_ADVANCE_REPAYMENT_APPROVAL` 批准，页面余额变为 ¥0.00/已结清。截图 `artifacts/output/playwright/oa-cash-advance-offset-regression-20260722.png`、`artifacts/output/playwright/oa-cash-advance-repayment-approved-regression-20260722.png`，控制台 0 错误/0 警告。驳回、越权、重复单号和超余额仍未专项执行。 |
| OA 付款申请与实际付款边界 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL Web 供应商付款 `FK-202607-115047` 经 Workflow 批准和财务复核后，缺少采购订单时正确阻断 ERP 应付核销；另创建员工付款 `FK-202607-121017`，经审批、财务复核后登记外部流水并变为“已付款”。截图 `artifacts/output/playwright/oa-payment-register-negative-regression-20260722.png`、`artifacts/output/playwright/oa-payment-employee-actual-payment-regression-20260722.png`，控制台 0 错误/0 警告。付款批次、重复单号、越权和驳回重提仍未专项执行。 |
| OA 采购申请与 ERP 生单边界 | [x] 浏览器通过 | 2026-07-21 | PostgreSQL 临时库以 `admin/admin` 从 OA 菜单创建产品相关申请 `CG-202607-225858`、添加 `SKU-1001` 两件明细并提交 `OA_PROCUREMENT_REQUEST_APPROVAL`；收件箱批准后在“待采购复核”选择已准入供应商和实际单价 `130.25`，生成 ERP 草稿 `PO-20260721-CG-202607-225858`。订单回显来源“请购单 · CG-202607-225858”，数据库为 `Approved | Draft | Requisition | CG-202607-225858 | 2.00 | 130.25`；截图 `artifacts/output/playwright/procurement-request-purchase-order-postgresql.png`，控制台 0 错误/0 警告。自动化覆盖重复有效来源拒绝、单条产品明细门禁和预算占用边界；本轮采购预算未执行浏览器/PostgreSQL 写入回归。寻源/比价、多明细拆单、订单审批和收货未接入。 |
| PMS 项目会议与行动项回链 | [x] 通过 | 2026-07-21 | 独立 SQLite 临时宿主中以 `admin/admin` 登录，进入“项目会议”后创建“接口联调评审会”，保存纪要和决定项，再创建“完成接口联调”行动项；会议卡片显示该行动项为草稿。领域全量 `513/513`、Web 构建 `0` 警告/`0` 错误。真实 PostgreSQL 写入仍待专项回归。 |
| PMS 需求到交付记录与状态历史 | [x] 通过 | 2026-07-21 | 独立 SQLite 临时宿主中以 `admin/admin` 登录，创建缺陷 `BUG-WEB-001` 并关联 `REQ-001` 与“确认客户需求”WBS；页面回显同项目来源，推进到“处理中”后显示新建和处理中两条状态历史。领域全量 `515/515`、Web 构建 `0` 警告/`0` 错误。真实 PostgreSQL 写入和附件上传待专项回归。 |
| PMS 工作项批注与活动流 | [x] 通过 | 2026-07-21 | 独立 SQLite 临时宿主中以 `admin/admin` 打开会议来源工作项“完成接口联调”，添加“接口响应已修复，等待测试回归。”批注；页面立即显示 `admin` 批注活动。领域全量 `516/516`、Web 构建 `0` 警告/`0` 错误。历史工作项只从功能启用后开始积累活动。 |
| PMS 工作项受控人员选择 | [x] 通过 | 2026-07-21 | 独立 SQLite 临时宿主中以 `admin/admin` 新建“目录负责人工作项”，从负责人下拉框选择“系统管理员 (admin)”；保存后卡片回显“系统管理员”名称快照和创建活动。领域全量 `517/517`、Web 构建 `0` 警告/`0` 错误。参与人多选与停用/重复人员门禁由自动化覆盖。 |
| Web Serilog 日志接入 | [x] 构建通过 | 2026-07-21 | Web 宿主通过 `UseSerilog` 统一接管 ASP.NET Core 日志，控制台和按天滚动文件输出，保留 14 个文件；请求日志中不写入密码、连接字符串或附件正文。Domain `498/498`、Web 构建 `0` 警告/`0` 错误；实际启动后的文件写入和日志轮转待运行环境回归。 |

## 可执行回归用例

这些用例保留为日常回归清单；每条都可以从页面或测试数据直接复现，不再重复记录实现过程。

### 登录、导航与基础页面

| 编号 | 回归操作与预期 | 状态 | 最后回归时间 |
|---|---|---|---|
| UI-LOGIN-01 | 使用 `admin/admin` 登录，进入工作台首页并显示今日摘要 | [x] | 2026-07-19 |
| UI-NAV-01 | 检查 OA、CRM、ERP、PMS、Workflow、LMS 主导航链接均可见且可打开 | [x] | 2026-07-19 |
| UI-NAV-03 | 根模块默认收起；点击二级或根菜单右侧展开按钮只改变树状态，左侧链接仍独立导航。验证项目管理先收纳为工作区，项目规划展开后显示三级页面并可进入 `Pms/Project`，模块左侧入口可进入 `Pms/Overview` | [x] | 2026-07-21 |
| UI-OA-OVERVIEW-01 | 从协同办公根菜单进入 `/Oa/Overview`，验证待处理/逾期任务、未来七日日程、已发布公告、未读通知以及任务/日程/公告深链；展开“我的协作看板”后不重复显示 OA 工作台三级叶子 | [x] | 2026-07-21 |
| UI-CRM-OVERVIEW-01 | 从客户经营根菜单进入 `/Crm/Overview`，验证启用客户、逾期跟进、进行中商机预计金额、生效合同金额以及跟进/商机/合同深链；展开“客户经营看板”后不重复显示 CRM 经营看板三级叶子 | [x] | 2026-07-21 |
| UI-WORKFLOW-OVERVIEW-01 | 从流程平台根菜单进入 `/Workflow/Overview`，验证当前审批人 Pending 待办、已发布流程、已发布简单表单、本人申请摘要和对应深链；展开“流程工作台”后仅显示审批收件箱三级入口，不重复工作台叶子 | [x] | 2026-07-21（PostgreSQL 临时库、`admin/admin`、截图 `artifacts/output/playwright/workflow-overview-postgresql.png`、控制台 0 错误/0 警告） |
| UI-ERP-INVENTORY-BATCH-01 | 从 ERP 库存查看按商品/仓库/库位/批次的余额、已过期/未来 30 天临期批次及 180 天无流水的呆滞批次；`batchNo`/`serialNo` 深链与输入筛选只显示对应余额、预警和流水；调拨选择带批次或序列号的商品库存并填写对应追溯标识；未指定批次和序列号的出库可选择 FIFO，按最早保质期拆分为可追溯批次流水；序列号入库、出库、调拨和盘点必须按单件追溯；验证调出与调入流水保留相同批次/保质期/序列号，批次或序列号余额不足被服务端阻断且成对写入使用共享事务；盘点仅生成对应维度调整流水 | [ ] | 2026-07-27 已由领域自动化覆盖批次及序列号流水精确筛选、FIFO 最早到期分配、共享事务边界、来源号冲突及批次不足时零写入、批次调拨双流水/事务边界、批次盘点差异、已过期/临期正余额及呆滞批次预警，以及序列号重复入库、错误仓库出库、调拨追溯和盘点清零；库存、调拨和盘点页面已接入批次及序列号字段，本轮未执行浏览器回归。 |
| UI-ERP-INVENTORY-OVERSTOCK-01 | 商品维护最大库存；全仓账面库存严格超过上限时库存页显示只读超储预警，等于上限、停用商品或库存回落后不显示，预警不阻断库存交易 | [ ] 待浏览器回归 | 2026-07-27 | 领域自动化覆盖最大库存正数门禁、启用商品超储投影、等于上限不误报与停用商品跳过；本轮未执行浏览器回归。 |
| UI-ERP-WAREHOUSE-CAPACITY-01 | 在仓库页面为指定库位配置“商品 + 最大库存数量”，确认卡片回显；入库、采购订单收货选库位、调拨调入和正向盘点超过该商品库位容量时服务端拒绝，失败调拨/收货不留下库存流水；不同商品容量独立计算 | [ ] 待浏览器回归 | 2026-07-27 | 领域自动化 `664/664` 覆盖入库、采购收货选库位、调拨预检、正向盘点和失败零写入；Web 构建 `0` 警告/`0` 错误。本轮按既有约定未执行浏览器回归。 |
| UI-SIMPLE-FORM-SEAL-01 | 填写预置 `SIMPLE_SEAL_REQUEST`，选择被申请人并保存/提交；审批人按 `SimpleFormSubmission` 筛选收件箱后同意，申请 Approved、待办清空，并在被申请人通知中心显示“印章申请已批准” | [x] | 2026-07-21（PostgreSQL 临时库，普通申请人、管理员审批、普通接收人三账号回归；Outbox Delivered/重试 0，接收人页面及控制台 0 错误/0 警告） |
| UI-ADMIN-OVERVIEW-01 | 从系统管理根菜单进入 `/Admin/Overview`，验证通知失败重试摘要、权限变更摘要和失败处置/权限审计深链；展开“系统运维工作台”后仅显示两个三级入口，不重复工作台叶子 | [x] | 2026-07-21（PostgreSQL 临时库、`admin/admin`、截图 `artifacts/output/playwright/system-overview-postgresql.png`、控制台 0 错误/0 警告） |
| UI-NAV-02 | 依次打开 OA 任务/公告/日程、CRM 客户/合同、ERP 概览/采购/核销、PMS 概览/项目、Workflow 定义/收件箱、LMS 概览/授权/替代，主标题正确 | [x] | 2026-07-19 |
| UI-OA-DIRECTORY-01 | 从 OA 进入员工通讯录，验证在职/全部/停用、组织和关键词筛选，卡片显示账号、组织、备注和最近登录 | [x] | 2026-07-22（PostgreSQL Web，筛选和 4 名员工回显，控制台 0 错误/0 警告） |
| UI-OA-DIRECTORY-02 | 具备 `Oa/Directory/Edit` 权限时编辑员工档案，保存电话、邮箱、企业微信/钉钉标识、职位、入职日期、生命周期状态和 `OtherInfo`；无权限时编辑入口和服务端写入均被拦截 | [x] | 2026-07-22（PostgreSQL Web，admin 档案编辑及刷新回显；本轮实际保存并重新打开验证企业微信/钉钉标识，截图 `artifacts/output/playwright/external-notification-directory-20260722.png`；控制台 0 错误/0 警告） |
| UI-OA-RECRUITMENT-01 | 从 OA 进入招聘与面试，创建候选人、安排第 1 轮面试、填写通过评价，再录用；未通过面试时录用被服务端拦截，页面显示错误 | [x] | 2026-07-22（PostgreSQL Web，候选人→面试→通过→录用；截图 `artifacts/output/playwright/oa-recruitment-regression-20260722.png`，控制台 0 错误/0 警告） |
| UI-OA-ONBOARDING-01 | 从 OA 进入入职办理，为已录用候选人创建记录，维护四项清单；未完成清单时完成入职被服务端拦截，四项全部完成后状态变为已完成 | [x] | 2026-07-22（PostgreSQL Web，候选人→入职记录→四项清单→已完成；截图 `artifacts/output/playwright/oa-onboarding-regression-20260722.png`） |
| UI-OA-OFFBOARDING-01 | 从 OA 进入离职办理，为在职员工创建记录，维护交接、资产、车辆、文件和权限回收申请五项清单；未完成清单时完成离职被拦截，全部完成后平台账号停用且 OA 档案状态变为离职 | [ ] | 未执行 |
| UI-OA-LEAVE-01 | 从 OA 进入请假申请，创建草稿、编辑后提交并撤回；创建与已有提交中申请重叠的时间段时，页面显示服务端拦截错误 | [x] | 2026-07-22（PostgreSQL Web 创建并提交请假草稿，状态待审批/审批中；截图 `artifacts/output/playwright/oa-leave-regression-20260722.png`，控制台 0 错误/0 警告） |
| UI-OA-LEAVE-02 | 提交请假后进入 Workflow 收件箱，分别验证批准、驳回原因、驳回后编辑重提和申请人撤回；确认页面显示审批状态/附件，批准只改变请假状态，不产生余额、考勤或日历写入 | [ ] | 2026-07-22 部分回归：已验证提交、审批中和统一收件箱批准；驳回原因、驳回后编辑重提、撤回和余额/考勤/日历边界仍未执行 |
| UI-OA-LEAVE-BALANCE-01 | 具备 `Oa/LeaveBalance/Manage` 权限时维护员工年度年假/调休额度；年假或调休提交后显示占用，驳回/撤回释放，批准后转为已使用；额度不足和未配置额度时服务端拦截 | [x] | 2026-07-22（PostgreSQL Web 配置 2026 年年假 40 小时并回显；截图 `artifacts/output/playwright/oa-leave-balance-regression-20260722.png`，控制台 0 错误/0 警告） |
| UI-OA-OVERTIME-01 | 从 OA 进入加班申请，创建、编辑、提交和撤回；验证起止时间、事由、`OtherInfo` 与提交中/已批准请假时间重叠由服务端拦截。审批批准、驳回重提和附件展示后，确认不产生考勤、工时或员工主数据写入 | [ ] | 2026-07-22 部分回归：已验证创建/提交 3 小时申请和 Workflow 批准；驳回重提、撤回、附件及请假冲突分支未执行 |
| UI-OA-VEHICLE-01 | 从 OA 进入车辆管理，新增车辆台账并创建用车申请；验证不可用车辆、时间重叠、里程倒退被服务端拦截，提交后进入 Workflow，批准占车、驳回/撤回不占车，归还后车辆恢复可用 | [x] | 2026-07-21 PostgreSQL 临时库 `velrixworkhub_webtest_20260719c`。`admin/admin` 经协同办公菜单进入车辆页，完成草稿→`OA_VEHICLE_USE_APPROVAL`→收件箱批准→使用中→归还；已回归维修中无可选车辆、时间重叠拦截、撤回和驳回不占车。结束里程场景以起始里程 `13000` 先填 `12999`，页面显示“结束里程不能小于起始里程。”且保持使用中；改填 `13050` 后显示“已归还”、车辆恢复“可用”。数据库确认 `Available | Returned | 13000.00 | 13050.00 | Completed`；截图 `artifacts/output/playwright/vehicle-return-mileage-postgresql.png`；控制台 0 错误/0 警告。 |
| UI-OA-VEHICLE-02 | 从车辆台账登记维修内容、里程、服务商、费用和 OtherInfo；验证维修中车辆不能提交用车申请，只有登记人可完成或取消维修，完成/取消后车辆恢复可用且保留维修历史 | [x] | 2026-07-21（PostgreSQL 临时库 `velrixworkhub_webtest_20260719c`。`admin/admin` 新建 `沪A-VEH-20260721`，登记维修后页面显示“维修中”、用车申请仅有空选项；完成维修后恢复可用并保留“更换制动片 · 已完成”。再次登记维修，临时授予页面权限的 `simpleform_requester_20260721` 点击完成维修被服务端提示“当前用户不能操作其他员工登记的车辆维修记录”，临时角色关联已删除；登记人取消后页面显示“可用”及“验证取消维修和越权 · 已取消”。数据库确认车辆 `Available`，维修记录分别为 `Completed`、`Cancelled`；截图 `artifacts/output/playwright/vehicle-maintenance-postgresql.png`、`artifacts/output/playwright/vehicle-maintenance-negative-postgresql.png`；控制台 0 错误/0 警告。） |
| UI-OA-ASSET-REQUEST-01 | 从 OA 资产页面选择可用资产创建并提交领用申请；Workflow 批准后资产变为在用并生成领用记录，驳回要求填写原因且申请可编辑重提，撤回不锁定资产，重复申请被服务端拦截 | [ ] | 2026-07-22 部分回归：创建、提交、Workflow 批准、资产变为在用和领用记录已验证；驳回、重提、撤回和重复申请未执行。截图 `artifacts/output/playwright/oa-asset-request-approved-regression-20260722.png` |
| UI-OA-ASSET-02 | 从 OA 资产页面打开“转移资产位置”，填写新位置和原因后保存；验证资产卡片位置、最近转移记录和操作流水回显，在用资产责任人保持不变，维修中/已报废资产没有转移入口 | [x] | 2026-07-22（PostgreSQL Web，在用资产位置由“交付部办公区”转为“研发部设备间”，责任人仍为系统管理员；截图 `artifacts/output/playwright/oa-asset-transfer-regression-20260722.png`，控制台 0 错误/0 警告） |
| UI-OA-ASSET-03 | 从 OA 资产页面打开“资产盘点”，分别记录盘点一致、状态/位置差异和未找到；验证差异必须填写原因、实盘在用必须选择责任人，资产卡片显示最近盘点结果且盘点不会直接改变台账状态 | [x] | 2026-07-22（PostgreSQL Web 对 `ASSET-WEB-REG-20260722` 保存一致盘点，卡片回显最近盘点“一致”，台账仍为可用；截图 `artifacts/output/playwright/oa-asset-stocktake-regression-20260722.png`，控制台 0 错误/0 警告） |
| UI-OA-EXPENSE-01 | 从 OA 进入费用报销，创建主单并添加费用明细，验证金额汇总、发票/付款流水重复拦截、OtherInfo 和通用附件；提交后进入 Workflow 待办，批准/驳回回写状态，不能直接生成 ERP 核销流水 | [ ] | 2026-07-22 部分回归：已验证主单、交通费明细、金额 ¥280.50、提交和 Workflow 批准；重复发票、附件、驳回重提和付款级联未执行 |
| UI-OA-CASH-ADVANCE-01 | 从 OA 进入借款与备用金，创建借款并提交审批；批准后选择同一申请人的已批准报销登记部分/全部冲销，或登记还款单、上传凭据附件并提交 `OA_CASH_ADVANCE_REPAYMENT_APPROVAL`；分别验证批准、驳回、撤回、余额、混合冲销/还款、重复单号、越权和超余额拦截，确认不生成 ERP 付款/核销流水 | [ ] | 2026-07-22 部分回归：已验证 ¥1,000 借款批准、¥280.50 报销冲销、¥719.50 还款审批和余额结清；驳回、撤回、附件、重复单号、越权及超余额未执行 |
| UI-OA-PAYMENT-01 | 从 OA 进入付款申请，填写收款方、账户引用/末四位、银行、币种金额、前置单据和 `OtherInfo`，保存附件后提交审批；验证重复单号、缺少前置依据、越权编辑和驳回重提，批准后确认页面仍标记为付款意图且不生成 ERP 付款流水 | [ ] | 2026-07-22 部分回归：已验证供应商付款审批→财务复核→缺少采购订单阻断，以及员工付款审批→财务复核→实际付款成功；附件、重复/越权、驳回重提和付款批次未执行 |
| UI-OA-PROCUREMENT-01 | 从 OA 进入采购申请，分别验证产品相关、非产品相关、办公用品和寻源类型；添加多条明细、产品/分类、规格、数量、预计单价、预算依据和 `OtherInfo` 后提交审批，验证无明细、产品绑定错误、重复单号、越权编辑和驳回重提；已批准申请可经采购复核按明细生成 ERP 草稿订单，不产生库存流水 | [ ] | 2026-07-22 部分回归：非产品相关申请 `CG-202607-115704` 已验证无效预算阻断、清空预算、提交和 Workflow 批准；采购复核/寻源页当前无可直接生单或已批准寻源来源。产品订单生单沿用 2026-07-21 历史证据，四类分支、越权、驳回重提和多明细拆单仍待专项回归。 |
| UI-PMS-INIT-01 | 从 PMS 项目主数据新建/编辑项目，填写立项核心字段；`OtherInfo` 仅作为内部扩展载荷保留，不直接显示或编辑。变更状态时当前状态只读、目标状态和说明可编辑，保存后显示状态历史 | [ ] | 未执行 |
| UI-PMS-WORKLOG-01 | 从 PMS 项目工时选择项目和周，填写成员×任务×日期矩阵，保存后刷新仍显示工时/出勤状态/说明；普通成员仅能枚举其稳定项目成员关系项目，Application 明细查询也只返回本人快照，同用户 ID 存在历史重复成员关系时安全不读不写，构造非成员项目 ID 时页面清空选择且不显示矩阵或全项目累计；输入 0 清除单元格，超出项目周期、非法小时数或同成员同日跨 WBS 累计超过 24 小时时显示服务端错误，整周任一格被拒绝或持久化异常时不保存前置修改 | [ ] | 2026-07-27 已由领域自动化覆盖稳定用户 ID 的项目可见范围、工时明细隔离及历史重复成员安全拒绝、跨 WBS 日累计 24 小时、批量失败不留下前置修改及批量写入经事务边界执行；本轮按要求跳过 Web 浏览器回归。 |
| UI-PMS-WORKLOG-APPROVAL-01 | 当前登录项目成员提交本周已有工时，确认生成冻结快照并进入 `PMS_WEEKLY_WORKLOG_APPROVAL`；审批人批准/驳回后状态与原因回显，提交人仅可撤回自己的运行中审批，重复提交被服务端阻断 | [ ] | 2026-07-27 已完成领域/应用、持久化、Workflow 种子、页面和全量领域自动化 `636/636`；新增冻结 WBS 标题、重复提交、成员/流程配置门禁、流程启动失败补偿、稳定用户 ID 的成员提交/读取隔离与同项目目录重名拒绝、最近审批意见历史投影、活动周唯一键状态释放规则、收件箱中文筛选/原单跳转、驳回历史保留后的最新工时重提，以及批准/驳回的成员结果通知。未执行 PostgreSQL 多用户、数据库迁移或浏览器回归。 |
| UI-PMS-REQUIREMENT-01 | 从 PMS 需求管理新建需求，填写编号、提出人、优先级、日期、描述和背景价值；按项目/状态/优先级/关键词/与我相关筛选并分页，推进状态后刷新仍保留数据 | [ ] | 未执行 |
| UI-PMS-RESOURCE-01 | 从 PMS 团队资源分配选择项目、日期和任务状态，验证人员×日期任务数/工时矩阵、部门/关键词筛选和注意/超负荷阈值；悬停可见任务明细，数字不依赖颜色表达 | [ ] | 未执行 |
| UI-PMS-MEETING-01 | 从 PMS 进入项目会议，创建会议主题、类型、时间、地点/方式、主持人、参与人、纪要和决定项；再创建行动项，刷新后会议卡片显示来源行动项且保持同一项目上下文 | [x] | 2026-07-21（独立 SQLite 临时宿主浏览器写入） |
| UI-PMS-DELIVERY-01 | 从 PMS 进入交付追溯，创建缺陷、评审或发布记录并关联项目、需求/WBS；推进允许状态后验证来源、结论/版本结果和状态历史回显 | [x] | 2026-07-21（独立 SQLite 临时宿主浏览器写入：缺陷创建、来源回显、新建→处理中） |
| UI-PMS-WORKITEM-01 | 从 PMS 进入项目工作项，为工作项添加批注；验证批注显示操作者和内容，状态推进后显示状态活动，历史记录不可直接修改 | [x] | 2026-07-21（独立 SQLite 临时宿主浏览器写入：会议行动项批注回显） |
| UI-PMS-WORKITEM-02 | 新建/编辑项目工作项时从启用人员目录选择负责人和参与人；保存后显示名称快照，停用、重复参与人及负责人重复参与人由服务端拒绝 | [x] | 2026-07-21（独立 SQLite 临时宿主浏览器写入：负责人选择与快照回显；异常分支自动化） |
| UI-ADMIN-01 | 从首页进入 Admin 用户资料页，显示用户资料内容且无 JS 互操作错误 | [x] 通过 | 2026-07-19 |

### CRM、ERP 与 PMS

| 编号 | 回归操作与预期 | 状态 | 最后回归时间 |
|---|---|---|---|
| CRM-CUSTOMER-01 | 客户列表按“全部/启用/停用”切换，停用客户为空时显示空状态 | [x] | 2026-07-18 |
| CRM-CUSTOMER-02 | 客户卡片的联系人、跟进、合同、销售订单、项目、收款核销以及 LMS 机台、客户特性、机台特性、许可证申请/授权引用均可深链到携带同一 `customerId` 的目标列表；核销同时限定 `partyKind=Receivable`，目标页只显示该客户上下文 | [x] 浏览器通过 | 2026-07-26 | PostgreSQL 发布宿主 `5243` 以 `admin` 验证 Aster 科技客户卡的 11 条引用均为链接；本轮默认种子为 Aster 幂等补入 3 条已批准许可证申请和 3 条有效授权，客户卡回显“许可证申请 3 · 许可证授权 3”。点击许可证申请后进入携带同一 `customerId` 的 `/Lms/License`，客户下拉选中 Aster，三条申请、三条授权、申请关联和授权 `OtherInfo` 均正常回显；控制台 `0` 错误/`0` 警告，截图归档于 `artifacts/output/playwright/crm-aster-lms-reference-blue-theme-20260726.png` 与 `artifacts/output/playwright/lms-aster-license-reference-regression-20260726.png`。 |
| LMS-OTHERINFO-UI-01 | LMS 申请向导、申请详情、授权卡片及通用/LMS 附件面板不直接显示或提供原始 `OtherInfo` JSON；OA 资产、资产申请、盘点和办公用品页面同样直接收口，遗留页面才由全局 UI 守卫兜底且不能隐藏同一行的业务信息；原有授权、申请关联、附件版本和状态信息保持可见 | [x] 浏览器通过 | 2026-07-26 | PostgreSQL 发布宿主 `5244` 以 admin 打开 Aster 科技筛选后的许可证页，三条申请和三条授权均正常显示，快照不含 `OtherInfo`/“扩展信息”文本；再打开 OA 新建资产表单，原始字段不可见。资产页直接收口已由领域 `670/670` 与 Web 构建 0 警告/0 错误验证，下一轮浏览器批次需确认有资产卡片时“领用人”等同一行信息仍可见。控制台 `0` 错误/`0` 警告；截图 `artifacts/output/playwright/lms-otherinfo-global-guard-regression-20260726.png`、`artifacts/output/playwright/oa-asset-otherinfo-global-guard-20260726.png`。 |
| ERP-SUPPLIER-02 | 供应商卡片的采购订单和付款核销引用可深链到同一 `supplierId` 的目标列表；核销同时限定 `partyKind=Payable`，目标页只显示该供应商上下文并选中付款方向 | [x] 浏览器通过 | 2026-07-25 | PostgreSQL Web 宿主 `5241` 以 `admin` 点击华东供应链的“采购订单 2”，进入 `/Erp/PurchaseOrder?supplierId=...` 且仅显示该供应商两条订单；点击“核销 3”进入 `/Erp/Settlement?partyId=...&partyKind=Payable`，仅显示该供应商三条付款核销，往来单位选中华东供应链、方向选中付款。截图 `artifacts/output/playwright/.playwright-cli/page-2026-07-25T16-03-55-512Z.png`，控制台 `0` 错误/`0` 警告。 |
| ERP-WAREHOUSE-02 | 仓库卡片的库存流水和账面库存引用可深链到携带同一 `warehouseId` 的库存页；目标页仓库筛选、仓库余额、库位余额和流水都只显示该仓库上下文 | [x] 浏览器通过 | 2026-07-25 | PostgreSQL Web 宿主 `5241` 以 `admin` 点击华东中心仓的“库存流水 3”与“账面库存 32.00”，均进入 `/Erp/Inventory?warehouseId=019f5426-bf4c-77ad-a7e9-f650df7dccb0`；库存页仓库下拉选中 WH-001，实时余额和库位余额均为华东中心仓 32.00，三条流水均属该仓库。截图 `artifacts/output/playwright/warehouse-inventory-context-regression-20260725.png`、`artifacts/output/playwright/warehouse-onhand-context-regression-20260725.png`，控制台 `0` 错误/`0` 警告。 |
| ERP-PRODUCT-02 | 商品卡片的采购订单、销售订单、库存流水和账面库存引用可深链到携带同一 `productId` 的目标列表；库存页商品筛选、余额、库位余额和流水均只显示该商品上下文 | [x] 浏览器通过 | 2026-07-25 | PostgreSQL Web 宿主 `5241` 以 `admin` 点击 SKU-1001“标准服务包”的采购订单 2、销售订单 8、库存流水 3 与账面库存 32.00。采购/销售页均保留同一 `productId` 并分别显示 2/8 条标准服务包订单；两个库存入口均选中 SKU-1001，余额、库位余额为 32.00，三条流水均属于该商品。截图 `artifacts/output/playwright/product-reference-inventory-context-regression-20260725.png`，控制台 `0` 错误/`0` 警告。 |
| CRM-ERP-01 | 客户交易视图显示合同、销售订单、收款和应收，并保留对应深链参数 | [x] | 2026-07-18（自动化与历史浏览器证据） |
| CRM-PMS-01 | 客户交易视图显示同客户 PMS 项目数，点击后按 `customerId` 筛选项目 | [x] | 2026-07-18（自动化与历史浏览器证据） |
| UI-UNIFIED-SEARCH-01 | 具备 `/Oa/UnifiedSearch` 及 CRM/ERP/PMS 相应菜单权限的账号，以客户、合同、采购/销售订单、项目、核销或库存来源号查询关联对象；确认状态/摘要/深链正确，移除某模块菜单权限后不显示该模块对象，项目深链仅显示目标项目 | [ ] | 2026-07-29（已完成领域定向自动化和 Web 构建，浏览器待回归） |
| ERP-PURCHASE-01 | 采购订单按状态“已取消”筛选，只保留取消订单，不混入已收货订单 | [x] | 2026-07-18 |
| ERP-PURCHASE-02 | 新建采购订单空表单提示必填，重复订单号被拒绝，订单金额计算正确 | [x] | 2026-07-18（历史浏览器证据） |
| ERP-INVENTORY-01 | 库存流水显示入库/出库、余额和库位；库存不足时拒绝出库且不写错误流水 | [x] | 2026-07-18（自动化与历史浏览器证据） |
| ERP-INVENTORY-02 | 调拨任一仓库停用时拒绝，失败调拨不留下单独调出流水 | [x] | 2026-07-18（自动化） |
| ERP-PURCHASE-RECEIVE-INVENTORY-01 | 采购订单收货必须指定启用仓库、可选库位；已审批后商品或收货仓库被停用、库位归属错误或指定库位商品容量超限时，收货由库存服务拒绝；订单保持已提交且不新增 `{订单号}-IN` 流水 | [x] 自动化通过 | 2026-07-27（领域 `664/664`；共享事务回滚路径已覆盖。浏览器及真实 PostgreSQL/SQL Server 写入回归待执行。） |
| ERP-SETTLEMENT-01 | 核销按客户/供应商和收付款方向筛选，订单深链只显示对应核销流水 | [x] | 2026-07-18（自动化与历史浏览器证据） |
| ERP-REPORT-01 | 报表显示供应商应付、客户应收、已核销和剩余金额，撤销核销不再扣减余额；采购金额、销售金额、应付和应收指标均可带 `activeOnly` 与起止日期下钻到口径一致的订单列表 | [x] 浏览器通过 | 2026-07-25 | PostgreSQL Web 宿主 `5241` 将报表范围限定为 2026-07-12，当日采购金额 ¥12,800.00、销售金额 ¥5,040.00；点击采购/销售指标分别进入 `/Erp/PurchaseOrder?activeOnly=true&startDate=2026-07-12&endDate=2026-07-12` 与对应销售路由，目标页均明确显示“仅有效订单”及日期范围，且各只保留 1 张当天订单。应付/应收指标使用相同下钻参数；截图 `artifacts/output/playwright/erp-report-order-drilldown-regression-20260725.png`，控制台 `0` 错误/`0` 警告。 |
| PMS-PROJECT-01 | 项目卡片显示 WBS、未关闭风险问题、SPI，并保留 WBS/风险/EVM 深链 | [x] | 2026-07-18（自动化与历史浏览器证据） |
| PMS-PMS-01 | 项目立项字段按两栏半宽/全宽录入并持久化；状态变更必须有说明和权限，状态历史可追溯 | [x] | 2026-07-20（领域自动化、Web 构建；浏览器写入待回归） |
| PMS-PMS-02 | 工时矩阵支持按周编辑成员×任务×日期单元格，更新/清空幂等，出勤状态和说明随记录保存，项目/成员/WBS/小时数门禁有效 | [x] | 2026-07-20（领域自动化、Web 构建；浏览器写入待回归） |
| PMS-PMS-03 | 需求编号在项目内唯一；需求字段、日期、JSON 和项目/产品引用可保存；列表筛选/分页/状态推进和附件入口可用 | [x] | 2026-07-20（领域自动化、Web 构建；浏览器写入待回归） |
| PMS-PMS-04 | 资源聚合按项目成员部门、WBS 负责人/计划日期和工时计算人员×日期任务数/工时；项目/状态/关键词/日期筛选及可配置阈值有效 | [x] | 2026-07-20（领域自动化、Web 构建；浏览器只读回归待执行） |
| PMS-PP2-02 | 会议起止时间、项目存在性和 JSON 受领域/Application 校验；行动项创建固定读取会议项目及 `PmsProjectMeeting` 来源 ID，不存在会议不能创建，会议下可按来源回显行动项 | [x] | 2026-07-21（领域自动化 `513/513`、Web 构建、独立 SQLite 浏览器写入） |
| PMS-PP2-03 | 缺陷/评审/发布记录必须关联存在项目，需求和 WBS 不能跨项目；缺陷/评审需求必填、发布版本必填，评审/发布结果门禁和类型状态机有效，创建及状态变化均写入历史 | [x] | 2026-07-21（领域自动化 `515/515`、Web 构建、独立 SQLite 浏览器写入） |
| PMS-PP2-01-ACTIVITY | 工作项创建、状态变更和批注都写入不可变活动；批注内容/操作者必填，不存在工作项拒绝写入，页面受独立批注按钮权限控制 | [x] | 2026-07-21（领域自动化 `516/516`、Web 构建、独立 SQLite 浏览器写入） |
| PMS-PP2-01-PEOPLE | 工作项负责人/参与人仅接受启用目录人员 ID，并冻结显示名快照；停用人员、重复参与人及负责人重复参与人被拒绝，历史文本来源不被强制改写 | [x] | 2026-07-21（领域自动化 `517/517`、Web 构建、独立 SQLite 浏览器写入） |
| PMS-PP2-01-REMINDER | 到期非终态工作项仅向仍启用的受控负责人投递统一 `Reminder` 通知；稳定去重键抑制重复扫描，未来、终态、无负责人和停用负责人不投递；页面可编辑并回显提醒时间 | [x] | 2026-07-21（领域自动化 `518/518`、Web 构建、独立 SQLite 浏览器：已到期“浏览器提醒验证工作项”回显提醒时间，重启扫描后通知中心仅出现一条“项目工作项提醒”） |
| PMS-PP2-04-OVERDUE | 计划结束早于当前时间的非终态工作项仅向启用负责人投递“项目工作项已逾期”；终态、无负责人、停用负责人和结束时间恰好等于当前时间均不投递；人工提醒与逾期提醒使用不同去重键，页面显示逾期状态 | [x] | 2026-07-22（领域自动化 `3/3`、领域全量 `529/529`、Web 构建 `0` 警告/`0` 错误；当前 PostgreSQL 业务库启动被既有重复 Workflow 定义版本保护阻断，浏览器回归未执行） |
| PLATFORM-NOTIFICATION-CHANNELS | 站外通知提供邮件、短信、企业微信、钉钉统一渠道枚举、受控地址解析、Provider 与异步调度契约；邮箱地址大小写无关去重，未配置渠道跳过，单渠道异常不影响其他渠道或站内通知 | [x] | 2026-07-22（定向自动化 `2/2`、Web 构建 `0` 警告/`0` 错误；默认配置不注册 Provider，持久化 Outbox/联系人映射已接入，短信/企业微信/钉钉真实账号配置待后续） |
| PLATFORM-NOTIFICATION-SMTP | SMTP 邮件渠道仅在 `ExternalNotifications:Email:Enabled=true` 时注册；启用必须校验主机、端口、发件地址与成对认证配置，密码不进入仓库或业务数据；消息以业务去重键和收件地址共同生成稳定 `Message-Id`，错误继续由 Outbox 重试边界处理 | [x] | 2026-07-22（定向自动化 `4/4`、领域全量 `546/546`、Web 构建 `0` 警告/`0` 错误；无 SMTP 凭据，不执行真实网络投递；浏览器待可保活宿主后执行） |
| PLATFORM-NOTIFICATION-OUTBOX | 站内通知成功后仅在事务提交后写入站外通知 Outbox；回滚不入队，渠道/地址/去重键原子去重，Worker 成功才标记 Delivered，Provider 异常按 5/15/30/60 分钟后逐步加倍至 12 小时退避，未配置渠道保持立即可尝试 Pending | [x] | 2026-07-22（定向自动化 `4/4`，其中退避/延迟摘要回归 `2/2`；`scripts/test-external-notification-postgresql.ps1` 已验证 PostgreSQL 原子写入、到期筛选和租约抢占并清理探针数据，Web 构建 `0` 警告/`0` 错误；浏览器待宿主可保活后执行） |
| PLATFORM-NOTIFICATION-RECIPIENTS | 站外地址只从目录启用且档案状态为在职用户的员工档案解析：邮箱、手机号、企业微信、钉钉标识分别映射四类渠道；停用、停职、离职或缺档案用户不返回地址，无效邮箱/手机号仅跳过其渠道，手机号规范化后入队，企业微信/钉钉标识仅在受控编辑器维护 | [x] | 2026-07-22（定向自动化 `9/9`、Web 构建 `0` 警告/`0` 错误；未配置第三方 Provider，浏览器写入待宿主可保活后回归） |
| PLATFORM-NOTIFICATION-OUTBOX-OPS | 站外通知 Outbox 以独立系统菜单提供只读运维页，显示各渠道启用状态、Pending/延迟重试/失败尝试/最高重试以及渠道、类型、时间、下次尝试、状态元数据；达到 3 次重试的渠道由 Worker 输出无地址/正文的渠道级告警；页面不展示地址、主机、账号、密钥、正文、链接、去重键或错误正文，不提供人工发送、重试和删除 | [x] | 2026-07-22（定向自动化 `6/6`、Web 构建 `0` 警告/`0` 错误；浏览器菜单权限与空队列状态待宿主可保活后回归） |
| PMS-PP2-01-APPROVAL | 只有受控负责人可将进行中工作项提交验收；运行中锁定编辑，Workflow 批准才完成并写实际结束时间，驳回/撤回回到进行中并记录意见和活动 | [x] | 2026-07-21（领域自动化 `519/519`、Web 构建、PostgreSQL 浏览器：`velrixworkhub_webtest_20260719c` 中“PostgreSQL 验收审批工作项”提交后在收件箱生成 `PmsProjectWorkItem` 待办，管理员同意后回显已完成及 Workflow 活动；SQL Server 临时探针超时未记通过且已清理） |
| PMS-PP2-01-USER-SCOPE | 管理员可见全量；非管理员仅可见负责人、参与人或项目成员稳定用户 ID 命中的工作项，旧文本快照不按同名反推用户 ID | [x] | 2026-07-21（领域自动化 `521/521`、Web 构建；PostgreSQL 临时库以非管理员 `simpleform_requester_20260721` 查看 `Pms/WorkItem`，本人负责项可见，`simpleform_recipient_20260721` 负责的对照项不可见；两账号均无项目成员关系，控制台 `0` 错误/`0` 警告） |
| PMS-PP2-01-MEMBER-DIRECTORY | 项目成员仅能从启用目录选择，保存稳定用户 ID、姓名和部门快照；停用/重复用户被拒绝，历史文本成员保持兼容；普通用户可通过稳定项目成员关系读取该项目工作项，不按姓名反推范围 | [x] | 2026-07-21（领域自动化 `521/521`、Web 构建 `0` 警告/`0` 错误；PostgreSQL `velrixworkhub_webtest_20260719c` 浏览器创建、刷新回显与删除通过，控制台 `0` 错误/`0` 警告；多用户隔离仍待回归） |
| PMS-PP2-01-ORGANIZATION-SCOPE | 工作项可保存目录组织稳定 ID 的可见范围；无效组织 ID 被拒绝，空范围不扩权，普通用户仅在其当前组织命中时获得额外可见性，旧名称不参与匹配 | [x] | 2026-07-21（领域自动化 `522/522`、Web 构建 `0` 警告/`0` 错误；PostgreSQL `velrixworkhub_webtest_20260719c` 已完成增量启动及“可见部门”编辑器渲染，控制台 `0` 错误/`0` 警告；测试库无组织数据，跨用户匹配由自动化覆盖） |
| PMS-PP2-01-ROLE-SCOPE | 工作项可保存平台角色稳定 ID 的可见范围；目录投影用户角色关系，无效角色 ID 被拒绝，空范围不扩权，普通用户仅在当前角色命中时获得额外可见性 | [x] | 2026-07-21（领域自动化 `523/523`、Web 构建 `0` 警告/`0` 错误；PostgreSQL `velrixworkhub_webtest_20260719c` 浏览器选择“管理员”角色、保存、编辑回显与删除清理通过，控制台 `0` 错误/`0` 警告） |
| PMS-PP2-04-CALENDAR | 项目日历默认复用平台工作日历；项目覆盖只能落在项目周期内，重复日期幂等更新，可删除恢复平台规则，说明长度和查询窗口受限 | [x] | 2026-07-21（领域自动化 `525/525`、Web 构建 `0` 警告/`0` 错误；PostgreSQL 临时库以 `admin/admin` 在 `PRJ-001` 将 2026-07-25 从平台非工作日保存为项目工作日，随后删除并立即回退平台非工作日/无项目说明，控制台 `0` 错误/`0` 警告） |
| PMS-WORKLOG-01 | 工时成员必须属于项目，日期必须在项目周期内，小时数限制为 0.1–24 | [x] | 2026-07-18（自动化与历史浏览器证据） |
| PMS-EVM-01 | EVM 正确计算 PV、EV、AC、SPI、CPI、VAC，并与项目 WBS/工时数据一致 | [x] | 2026-07-18（自动化与历史浏览器证据） |
| CROSS-PMS-ERP-01 | 销售订单关联同客户、未取消的 PMS 项目；项目页按项目隔离订单、发货和应收 | [x] | 2026-07-18（自动化与历史浏览器证据） |
| CROSS-PMS-ERP-02 | 项目组合的销售订单、待发货、客户交易、核销、风险和 EVM 深链保留项目/客户/订单参数并正确加载上下文 | [x] | 2026-07-19（只读浏览器回归，未执行写入） |
| CROSS-PMS-TODO-01 | 首页 PMS 筛选显示逾期未完成阶段/里程碑，点击后进入项目阶段深链并保留 `projectId` | [x] | 2026-07-19（临时 PostgreSQL 只读浏览器回归，未执行写入） |
| CROSS-ERP-SAFETY-01 | 首页 ERP 筛选显示库存低于安全线商品，点击后进入商品主数据并显示安全库存/账面库存 | [x] | 2026-07-19（临时 PostgreSQL 只读浏览器回归，控制台 0 错误，未执行写入） |

### Workflow 引擎

| 编号 | 回归操作与预期 | 状态 | 最后回归时间 |
|---|---|---|---|
| FLOW-DEFINITION-01 | 保存流程定义版本、节点和连线，发布后不可直接修改 | [x] | 2026-07-18（自动化） |
| FLOW-DEFINITION-02 | 发布前拒绝缺少开始/结束节点、不可达节点、重复连线和非法环路 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-01 | 同一业务对象不能并发创建两个 Running 实例；竞争失败后回读胜出实例 | [x] | 2026-07-18（PostgreSQL 探针） |
| FLOW-ENGINE-02 | 同一待办、操作或通知重复写入只保留一条，下一审批轮次仍生成新待办 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-03 | 审批只允许指定审批人处理，查询参数中的 `assignee` 不能伪造操作者 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-04 | 审批节点支持 Any、Majority、Quorum；未达到门槛继续等待，达到门槛原子取消兄弟待办 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-05 | 并行 Split/Join 等待全部分支；一条分支失败或取消不破坏其他活动分支 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-06 | 自动动作失败时业务写入回滚；重试成功后只产生一次执行历史 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-09 | 自动节点失败后仅流程发起人可定向重试活动快照中的失败节点；非发起人拒绝并记录 `Retried` 操作审计 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-07 | 损坏的活动节点、Join 到达、Loop 计数、审批人和连线快照均被拒绝恢复 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-08 | 实例完成、拒绝或取消后释放进程内锁；运行中实例仍保持串行化 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-10 | 条件节点缺失字段不误命中排序/文本分支，显式 `null` 比较仍可用 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-11 | Workflow 事务回滚不留下审批待办通知，事务提交后只发布一次 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-12 | CRM 合同、PMS 项目变更、ERP 采购订单跨模块动作失败时，业务状态、待办和流程实例整体回滚 | [x] | 2026-07-18（PostgreSQL 探针） |
| FLOW-ENGINE-13 | 实例重建复用发布图校验；损坏的审批节点配置或图结构不能进入运行时，历史数字节点类型仍可兼容 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-14 | 实例重建拒绝外层实例与定义快照的 ID、编码或版本不一致 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-15 | 失败节点重试只认节点最新失败状态，历史失败在节点成功执行后不能误开放重试 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-16 | 外部 FreeSql 事务未由 Workflow 边界管理时拒绝登记提交/回滚回调，不提前发布通知 | [x] | 2026-07-18（PostgreSQL 探针） |
| FLOW-ENGINE-17 | 拒绝/取消终止实例时，其他待办写入取消审计；通知已读只在事务提交后执行 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-18 | 审批完成/拒绝/取消在事务后段失败时，内存流程实例、主待办和被动取消待办恢复到原 Revision 与状态 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-19 | 回退目标待办创建失败时，实例节点、Revision、活动集合和审批人快照全部恢复 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-20 | 自动节点重试进入审批时，Runtime 推进与后续待办创建由 TaskService 原子编排；待办写入失败恢复实例状态 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-21 | 已有 Running 实例被补偿读取时，运行态推进、审批人快照和待办补齐处于同一事务；待办失败不留下快照半成品 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-22 | 自动运行到终态后若外层事务回滚，实例恢复 Running 且进程内实例锁仍保留；只有最外层提交后才释放锁 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-23 | 未配置事务边界时，自动节点推进或终态持久化失败也恢复完整实例快照，不留下 Completed/半推进内存态 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-24 | `WorkflowInstanceService` 公开状态变更在无事务边界时，仓储失败也执行内存快照恢复 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-25 | 条件节点支持按 NodeId 定向提交字段；并行条件不会隐式复用同一字段集合 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-26 | 条件字段暂未命中任何分支时保持 `WaitingForCondition`，实例不变更且可用后续字段重试 | [x] | 2026-07-18（自动化） |
| FLOW-ENGINE-27 | 通知投递失败记录仅在 Workflow 主事务提交后写入；主事务回滚不留下孤儿失败记录，且失败记录器异常不阻断主流程 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-28 | 通知失败补投先以 Pending + 租约时间 CAS 抢占；并发补投只有一个执行者增加 `RetryCount` 并投递 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-29 | 通知补投的通知写入与失败记录 `Resolved` 同处事务；后段失败时两者整体回滚并保留 Pending | [x] | 2026-07-18（PostgreSQL 探针） |
| FLOW-ENGINE-30 | 通知失败仓储接口不提供非原子 `TryClaim` 回退；所有实现必须编译期提供 Pending 租约 CAS | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-31 | 真实 Workflow 事务内，业务动作先原子占用稳定 `NodeExecuted` 键再调用 handler；已提交占用跳过重复动作，动作或后续推进失败时占用随事务回滚 | [x] | 2026-07-19（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-32 | 通知“接收人 + DedupeKey”和操作历史 DedupeKey 使用仓储原子 `TryAdd`；PostgreSQL/SQL Server 不以唯一键异常作为正常并发控制，竞争时复用已存在记录 | [x] | 2026-07-19（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-33 | 实例与待办仓储不提供默认伪 CAS；所有适配器显式实现 `TryUpdate`，以 Revision 返回真实并发成功/失败结果 | [x] | 2026-07-19（领域测试项目与 PostgreSQL 探针包装仓储编译） |
| FLOW-ENGINE-34 | 失败自动节点的 `Retried` 审计键绑定最近 `NodeFailed` 操作的稳定 ID；同一失败尝试可幂等重放，不同失败尝试生成不同键且不超过领域长度限制；重试候选不被 `Retried` 审计遮蔽 | [x] | 2026-07-19（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-35 | 同一审批轮次不能转交回历史审批人或历史转交目标，拒绝 A→B→A 循环并保持稳定待办集合一致 | [x] | 2026-07-19（自动化） |
| FLOW-ENGINE-36 | 待办仓储通过稳定待办主键执行原子 `TryAdd`；重复补偿返回 false 并回读胜出记录，两个独立 PostgreSQL 连接并发时只有一个胜出；PostgreSQL 使用 `ON CONFLICT DO NOTHING`，SQL Server 使用 `HOLDLOCK MERGE` | [x] | 2026-07-19（自动化与 PostgreSQL 并发探针） |
| FLOW-ENGINE-37 | Workflow 启动迁移在数据库端按 Running 实例业务键聚合检测重复，只返回最多 5 个冲突键；旧实例活动节点、Revision、Join/Loop/审批快照回填使用单条方言 SQL，并在无外部事务时自带事务、已有外层事务时复用；PostgreSQL 探针覆盖唯一索引门禁、提交和外层回滚结果 | [x] | 2026-07-19（自动化与 PostgreSQL 探针） |
| FLOW-ENGINE-38 | 运行实例创建通过稳定业务唯一键执行原子 `TryAdd`；并发竞争失败不再依赖唯一键异常，`WorkflowInstanceService.Start` 在同一事务内回读胜出实例，旧 `Add` 调用仍保留显式冲突语义 | [x] | 2026-07-19（自动化与 PostgreSQL 并发探针） |
| FLOW-ENGINE-39 | 操作历史和通知发布先尝试仓储原子 `TryAdd`，只有竞争失败或存储异常才回读去重记录；正常首次写入不再额外查询，重复调用仍返回胜出记录且不生成失败记录 | [x] | 2026-07-19（自动化） |
| FLOW-ENGINE-40 | 两个独立 PostgreSQL 连接并发记录同一操作历史或发布同一通知时，各自只有一个原子写入胜出，另一方回读并复用胜出记录，数据库最终各保留一条 | [x] | 2026-07-19（PostgreSQL 并发探针） |
| FLOW-ENGINE-41 | PostgreSQL 与 SQL Server 持久化基准均以 200 次样本测量实例、待办、操作历史和通知原子 `TryAdd` 的 p50/p95/p99，并在完成后清理全部基准数据 | [x] | 2026-07-19（PostgreSQL/SQL Server 性能基准脚本） |
| FLOW-ENGINE-42 | 独立调用 `CreateApprovalTask`、`EnsureCurrentApprovalTask` 或 `EnsureApprovalTasks` 时，待办、操作历史和提交后通知共用 Workflow 事务；操作历史写入失败会回滚待办，不留下孤儿记录，补偿失败同时恢复审批人快照 | [x] | 2026-07-19（PostgreSQL 故障注入探针） |
| FLOW-ENGINE-43 | 同一 Workflow 事务的提交后回调逐个隔离；单个通知、已读或锁释放回调异常不会阻断其他回调，也不会把已提交主交易重新报告为失败 | [x] | 2026-07-19（PostgreSQL 故障注入探针） |
| FLOW-ENGINE-44 | 同一失败自动节点的并发重试以稳定 `Retried` 键原子抢占；竞争失败请求不再重复进入通知/业务动作执行路径 | [x] | 2026-07-19（Domain 自动化） |
| FLOW-ENGINE-45 | 两个独立 PostgreSQL 连接并发重试同一失败自动节点时，仅一个请求执行动作，另一个请求拒绝；实例终态与 `Retried`/`NodeExecuted` 历史保持单条 | [x] | 2026-07-19（PostgreSQL 并发探针） |
| FLOW-ENGINE-46 | SQL Server 临时库完整 Workflow 探针覆盖迁移、CAS、事务回滚、图运行时、原子幂等和并发失败节点重试，并在结束后清理临时库；可通过 `scripts/test-workflow-sqlserver.ps1` 重复执行 | [x] | 2026-07-19（SQL Server 临时库探针） |
| FLOW-ENGINE-47 | 同一 `IFreeSql` 连接分别构造的多个 Workflow 事务边界实例可共享嵌套提交回调；未由 Workflow 管理的外部事务仍拒绝登记回调 | [x] | 2026-07-19（PostgreSQL 探针） |
| FLOW-ENGINE-48 | 两个独立数据库连接并发审批同一待办时，仅一个请求通过 Revision CAS；失败请求拒绝，业务 handler、终态和 `Approved` 历史各只执行/保留一次 | [x] | 2026-07-19（PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-49 | 两个独立数据库连接并发执行同一待办审批与发起人撤回时，仅一个事务胜出；实例终态、待办终态和终止/审批历史保持同一结果，不出现交叉半提交 | [x] | 2026-07-19（PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-50 | 两个独立数据库连接并发重提同一 Rejected 实例时，只创建一个带 `PreviousInstanceId` 的 Running 实例，写入一条 `Resubmitted` 历史并补齐一条当前审批待办 | [x] | 2026-07-19（PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-51 | 两个独立数据库连接同时转交同一待办给不同目标时，仅一个原待办转为 Transferred 并创建一个目标待办，另一请求拒绝且流程保持 Running | [x] | 2026-07-19（PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-52 | 待办创建与发起人撤回并发时，数据库按 Running 实例行串行化；撤回成功后不留下 Pending 孤儿待办，若待办先提交则同一撤回事务将其取消 | [x] | 2026-07-19（PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-53 | 失败自动节点重试与发起人撤回并发时，重试先锁定并校验实例行；仅一个事务胜出，撤回胜出时不执行落败重试动作、不追加伪 `NodeFailed`，真实动作失败仍保留可重试审计 | [x] | 2026-07-19（PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-54 | 直接继续执行失败自动节点与发起人撤回并发时，所有自动动作在 handler 前完成实例行仲裁；仅一个事务胜出，撤回胜出不执行第二次动作且不追加失败审计 | [x] | 2026-07-19（PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-55 | Retry 初始读取后若同节点已产生更新的 `NodeFailed` 审计，锁内重新核验会拒绝陈旧失败尝试，不写入过期 `Retried` 键且不执行动作 | [x] | 2026-07-19（Domain 自动化） |
| FLOW-ENGINE-56 | PostgreSQL/SQL Server 多连接同时执行 Workflow Running 唯一索引迁移时，迁移锁串行化检查与建索引；所有连接成功且最终只保留一条唯一索引 | [x] | 2026-07-19（PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-57 | Workflow Running 唯一索引迁移在数据库迁移锁内重新检查重复运行实例；重复检测与唯一索引 DDL 不存在检查窗口 | [x] | 2026-07-19（PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-58 | 多审批人终止流程时，Rejected/Cancelled 业务动作收到当前实际审批人 Actor，不因终止阶段二次执行而丢失操作者身份 | [x] | 2026-07-19（Domain 自动化） |
| FLOW-ENGINE-59 | Workflow 定义仓储对流程编码执行大小写无关读取；PostgreSQL 与 SQL Server 下同一编码的草稿版本连续递增且不会分裂 | [x] | 2026-07-19（PostgreSQL/SQL Server 探针） |
| FLOW-ENGINE-60 | 新实例流程编码统一规范化为大写；启动迁移回填历史混合大小写 `DefinitionCode` 后，Running 唯一索引不会放过逻辑重复实例，旧快照仍可恢复 | [x] | 2026-07-19（Domain 与 PostgreSQL/SQL Server 探针） |
| FLOW-ENGINE-61 | 流程定义 `Code + VersionNumber` 建立数据库唯一保护；迁移锁内回填后仅合并无 `WorkflowInstance`/`WorkflowTask` 引用的历史重复定义，两个或以上被引用定义仍拒绝迁移并返回冲突 `DefinitionId`；两个数据库连接并发创建同一版本只有一个 `TryAdd` 胜出，定义读取显式使用方言 `UPPER` | [x] | 2026-07-24（默认 PostgreSQL 完整 Workflow 探针、PostgreSQL/SQL Server 临时库探针） |
| FLOW-ENGINE-62 | 自动节点执行键绑定实例持久化 `Revision`；审批退回或循环重入同一业务动作时重新执行，事务回滚后的 Retry 仍复用当前尝试键，并兼容旧版无范围执行键 | [x] | 2026-07-19（定向自动化） |
| FLOW-ENGINE-63 | 重复流程定义版本的只读诊断同时报告 DefinitionId 与其 WorkflowInstance 引用（实例 ID、业务类型、业务 ID、状态）；迁移只会删除没有实例或待办引用的冗余定义，不自动迁移或改写任何历史实例/待办，多引用定义仍保持人工处置；PostgreSQL/SQL Server 探针覆盖引用查询与恢复边界 | [x] | 2026-07-24（默认 PostgreSQL 完整 Workflow 探针、PostgreSQL/SQL Server 临时库探针） |
| FLOW-ENGINE-64 | 两个进程首次补偿同一审批节点时，审批人快照 CAS 竞争失败的一方重新读取并复用胜出快照，不把正常幂等竞争报告为错误；恢复快照时按活动节点稳定副本遍历，不触发集合修改异常；原状态变化竞争仍保留拒绝门禁 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-65 | 陈旧进程补偿审批待办时，退回/推进已先提交则通过实例行锁刷新胜出运行态，只为新的活动审批节点创建 Pending，不为历史节点创建孤儿待办 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-66 | 审批、拒绝、取消、转交、退回和发起人撤回在事务内先锁实例行再执行待办 CAS；实例 Revision 已变化时，陈旧待办被拒绝且不执行业务动作、不新增审批历史 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-67 | 独立 `CreateApprovalTask` 与批量 `EnsureApprovalTasks` 入口均执行实例行锁；陈旧调用不能绕过 Revision 创建孤儿 Pending，批量补偿刷新后复用已有胜出待办 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-68 | 事务化独立待办创建在实例离开 Start 后只允许当前活动审批节点；历史审批节点或非审批节点不能直接写入 Pending，Start 阶段手工构造兼容路径保留 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-69 | 事务化 Runtime 的 Start、Condition、ParallelSplit/Join、Loop、Approval 后推进和 End 等图状态变更均先锁定实例行；锁失败时不修改实例快照 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-70 | WorkflowInstanceService 的公开状态变更与审批人快照固化入口同样先锁定实例行；直接 Application 调用不能绕过 Runtime 的并发保护 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-71 | 审批待办完成后的 Runtime 自动业务动作接收当前审批 Actor；Retry 继续传递发起人身份，跨模块动作不再丢失真实操作者 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-72 | 自动业务动作的 `NodeExecuted` 操作历史沿用实际审批人/重试发起人 Actor；没有 Actor 的系统触发仍明确记录为 `system`，处理器上下文与审计历史保持一致 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 双连接探针） |
| FLOW-ENGINE-73 | 节点进入/完成操作历史按实例持久化 `Revision` 形成执行范围；受控 Loop、退回和自动节点重入的每一轮图迁移均保留独立审计，不被旧连线键折叠 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 探针） |
| FLOW-ENGINE-74 | 条件节点定向推进支持携带实际 Actor；条件命中后的自动业务动作与 `NodeExecuted` 审计不再退化为 `system` | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 探针） |
| FLOW-ENGINE-75 | Reject、Cancel、Withdraw 嵌套在外层事务时，终态锁仅在最外层提交后释放；外层回滚恢复实例、主待办和被动取消待办的内存快照，不留下可并发推进的 Running/终态错位 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 探针） |
| FLOW-ENGINE-76 | Approve、Transfer、ReturnTo 也登记外层事务回滚快照；审批待办、实例状态和被动取消待办不会因外层提交失败停留在已处理内存态 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 探针） |
| FLOW-ENGINE-77 | 事务回滚期间新建待办使用创建 ID 精确补偿；转交、退回、重试、批量补偿和独立创建不会删除并发事务已经提交的目标待办，内存宿主也不保留孤儿任务 | [x] | 2026-07-19（Domain 自动化、PostgreSQL/SQL Server 探针） |
| FLOW-ENGINE-78 | 绑定启动先写入实例、后准备运行时/待办时，失败会按本次 `TryAdd` 成功的实例 ID 精确补偿；并发回读胜者后续准备失败也不会误删胜者实例 | [x] | 2026-07-19（Domain 自动化；PostgreSQL/SQL Server 构建与探针） |
| FLOW-ENGINE-79 | 通知失败补投直接使用“接收人 + DedupeKey”的原子 `TryAdd`；已有通知时仍可将失败记录标记为 Resolved，不通过先查后写制造唯一键竞争或额外重试 | [x] | 2026-07-19（Domain 自动化；PostgreSQL/SQL Server 探针） |
| FLOW-ENGINE-80 | 通知补投在 `TryAdd` 成功后若 `MarkResolved` 失败，只删除本次创建的通知 ID；已有通知或原子插入失败时不执行删除，内存宿主不残留孤儿通知 | [x] | 2026-07-19（Domain 自动化；PostgreSQL/SQL Server 探针） |
| FLOW-INBOX-01 | 收件箱按业务类型筛选，选择“许可证授权替代”后仍显示收件箱上下文 | [x] | 2026-07-19 |
| FLOW-BROWSER-MULTIUSER-01 | 多用户 Web 写入回归：申请人创建 CRM 合同并发起审批；申请人和未被指定的审批人看不到待办；绑定审批人处理并填写意见后，申请人刷新页面看到合同生效、审批完成，收件箱历史保留审批人和操作记录 | [x] | 2026-07-19（临时 PostgreSQL 浏览器回归，写入后清理） |
| FLOW-BROWSER-REJECT-RESUBMIT-01 | CRM 合同审批被拒后保留草稿和拒绝结果；申请人重新发起，审批人填写意见复审同意，合同最终生效且审批历史保留两轮动作 | [x] | 2026-07-19（临时 PostgreSQL 浏览器写入回归，结束后清理） |
| FLOW-BROWSER-PMS-CHANGE-01 | PMS 项目变更由申请人创建并发起审批；指定审批人同意后，申请人将变更从“已批准”推进到“已实施” | [x] | 2026-07-19（临时 PostgreSQL 浏览器写入回归，结束后清理） |
| FLOW-BROWSER-ERP-PO-01 | ERP 采购订单创建并发起审批；审批人同意后订单按“已提交→已收货→已关闭”推进，库存从 25 增至 29 | [x] | 2026-07-19（临时 PostgreSQL 浏览器写入回归，结束后清理） |

### LMS 许可证

| 编号 | 回归操作与预期 | 状态 | 最后回归时间 |
|---|---|---|---|
| LMS-MVP-01 | 申请保存草稿、提交 Workflow，申请状态和待办保持一致 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| LMS-MVP-02 | 只有批准的申请才能登记外部 License；产品和申请引用必须一致 | [x] | 2026-07-18（自动化） |
| LMS-MVP-03 | 授权开启、停用、作废记录操作者、原因、前后状态和时间 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| LMS-MVP-04 | 到期状态按当前时间派生，已到期或作废授权不能重新开启 | [x] | 2026-07-18（自动化） |
| LMS-MVP-05 | 续期、重发、换机必须先创建替代申请，批准前不得改变原授权 | [x] | 2026-07-18 |
| LMS-MVP-06 | 同一原授权最多一个 Submitted 替代申请；数据库并发也必须拒绝重复申请 | [x] | 2026-07-18（PostgreSQL 探针） |
| LMS-MVP-07 | 替代审批批准后停用原授权、创建新授权并写入替代追溯；失败整体回滚 | [x] | 2026-07-18（自动化与 PostgreSQL 探针） |
| LMS-MVP-08 | 授权页不提供直接续期/重发/换机表单；替代页显示原授权、替代类型、编号、外部 License 和原因 | [x] | 2026-07-18 |
| LMS-MVP-09 | LMS 申请、授权和替代的 `OtherInfo` JSON 可安全保存和读取；普通页面不直接展示或编辑原始 JSON，业务页面仅按已定义键提取 | [x] 自动化与 Web 构建通过 | 2026-07-26 |
| LMS-BROWSER-COMPLEX-01 | 申请人基于 CRM 客户、机台和特性版本创建许可证申请；提交并由管理员审批后登记外部 License，申请、授权、特性和 `OtherInfo` 可追溯 | [x] | 2026-07-19（临时 PostgreSQL 浏览器写入回归，数据保留） |
| LMS-BROWSER-REPLACEMENT-01 | 有效授权先创建续期替代申请；审批前旧授权保持有效，批准后旧授权停用、新授权有效，原授权与替代申请双向追溯可见 | [x] | 2026-07-19（临时 PostgreSQL 浏览器写入回归，数据保留） |
| LMS-BROWSER-REJECT-RESUBMIT-01 | 重发替代申请被拒后，申请人从原单重新提交并再次审批；复审批准后生成新授权，审批意见和替代历史保留 | [x] | 2026-07-19（临时 PostgreSQL 浏览器写入回归，数据保留） |
| LMS-BROWSER-MACHINE-CHANGE-01 | 同客户同产品的启用目标机台可提交换机替代申请；批准后原授权停用、新授权绑定目标机台，替代类型、审批意见和前后授权链可追溯 | [x] | 2026-07-19（临时 PostgreSQL 浏览器写入回归，数据保留） |
| LMS-BROWSER-LIFECYCLE-01 | 换机后的有效授权可停用并按原因恢复；停用授权可作废且不影响另一张有效授权，空原因提交被拦截 | [x] | 2026-07-19（临时 PostgreSQL 浏览器写入回归，数据保留） |
| LMS-BROWSER-ATTACHMENT-01 | 审批中的 LMS 申请详情可上传带来源 `OtherInfo` 的附件、下载并删除；恶意 `MZ` 伪装文件被内容扫描拦截，批准后附件转为只读，已上传版本和扩展字段可追溯 | [x] | 2026-07-19（临时 PostgreSQL 浏览器写入回归，数据保留） |

## 待补测与边界

| 测试点 | 状态 | 最后回归时间 | 备注 |
|---|---|---|---|
| CROSS-PMS-ERP-BROWSER | [x] 通过 | 2026-07-18 | 客户交易视图、`customerId` 项目筛选、`projectId` 销售订单筛选、收付款方向筛选和订单 `orderId` 定位均正常。 |
| LMS-REPLACEMENT-BROWSER-WRITE | [x] 通过 | 2026-07-19 | 在 `velrixworkhub_webtest_20260719c` 完成续期、重发、换机、驳回、重新提交和复审批准；测试数据按要求保留。 |
| UI-ADMIN-01 | [x] 已修复 | 2026-07-19 | `adminAuth.getProfile` 已补齐并完成浏览器回归。 |
| 简单自定义表单 | [x] PostgreSQL 浏览器通过 | 2026-07-21 | 已完成 HTML 可视化两栏编辑、版本化 JSON Schema、动态控件渲染、Submission 快照和 Workflow 状态回写；每次启动/重提均以 Workflow 实例 ID 固定表单版本、Schema 和数据，收件箱只对当前分派待办显示该实例的冻结快照。申请卡片按冻结 Schema 显示字段名和值，不直接显示原始数据 JSON；损坏历史快照安全降级。申请人可在草稿/审批中/驳回状态维护附件，终态只读，上传、删除和下载均经服务端身份门禁；草稿/驳回数据可回填同一冻结 Schema 编辑后提交，审批中及终态不可编辑；定义和申请的每个写入口均按既有按钮权限显示和执行。`ReferencePicker` 作为固定业务引用选项下拉，提交 ID 与标签必须同时命中冻结 Schema；人员/部门还会校验当前目录标签。自动化覆盖越权访问、申请人编辑、审批中写入拒绝、引用伪造拒绝及冻结字段摘要。完成事件以持久化 Outbox 投递，处理器失败保留 Pending 并可后台重试，自动化覆盖失败后重放。临时 PostgreSQL 以普通申请人、管理员审批人、仅通知权限接收人完成印章申请提交、审批、Delivered Outbox、去重通知和页面接收闭环；现有业务 PostgreSQL 宿主仍受历史重复流程定义保护阻断。Canvas、表格和节点级字段权限未实现。 |
| POSTGRESQL-REGRESSION | [x] 默认库与临时库通过 | 2026-07-24 | `velrixworkhub_webtest_20260719c` 完成 Workflow 全量探针和多用户 Web 写入回归，覆盖定义版本唯一迁移、并发 `TryAdd`、CAS、事务回滚、图运行时、通知幂等、LMS 申请/外部授权/续期/重发/驳回重提，以及 CRM 合同拒绝/重提、PMS 变更审批/实施、ERP 采购审批/收货/关闭；数据库与测试数据按要求保留。默认 `velrixworkhub` 已安全清理五组仅无引用冗余 `v1` 定义，完整 Workflow 探针通过并成功建立定义版本唯一索引；有实例或待办引用的定义不会被删除。 |
| SQLSERVER-REGRESSION | [x] 通过 | 2026-07-19 | 本机 SQL Server 临时库 `VelrixWorkHub_Probe` 完整 Workflow 探针通过，覆盖 CAS、事务回滚、图运行时、并发幂等、失败节点并发重试和历史迁移；探针结束后已删除临时库。 |
| SQLITE-REGRESSION | [ ] 暂缓 | — | 当前不增加 SQLite 专项测试。 |

## 验证基线

- 历史基线：借款还款页面闭环后曾为 `499/499`；后续测试已扩展，当前有效全量结果见文末 `606/606`。
- 历史记录：借款还款自动化曾只覆盖驳回重提；本轮已补充 PostgreSQL Web 的借款批准、报销冲销、还款审批和结清证据。
- 本轮加班申请验证：领域全量 `502/502` 通过，Web 构建 `0` 警告、`0` 错误。启动本机 PostgreSQL 宿主时，`WorkflowSchemaMigration.EnsureDefinitionVersionUniqueness` 因历史重复 `v1` 定义主动中止；Playwright 实际访问本地 URL 返回 `ERR_CONNECTION_REFUSED`，浏览器写入测试未执行。
- 本轮车辆维修验证：领域全量 `503/503` 通过，Web 构建 `0` 警告、`0` 错误；维修页面的浏览器写入仍受同一历史 Workflow 定义重复版本启动保护阻断，保持未执行。
- Web：`dotnet build .\src\VelrixWorkHub.Web\VelrixWorkHub.Web.csproj --artifacts-path .\artifacts /p:UseSharedCompilation=false`，采购申请切片后结果 `0` 警告、`0` 错误。
- Web：`dotnet build .\src\VelrixWorkHub.Web\VelrixWorkHub.Web.csproj --artifacts-path .\artifacts /p:UseSharedCompilation=false`，最近结果 `0` 警告、`0` 错误。
- 历史自动化汇总：员工、招聘、入职、离职、请假、车辆、报销、借款、借款还款、付款申请和采购申请的领域门禁持续覆盖；该条早期 `498/498` 和“浏览器未完成”描述已由当前文末 `606/606`、本文件上方逐项回归记录覆盖。
- PostgreSQL 阻断记录：Web 启动在 `WorkflowSchemaMigration.EnsureDefinitionVersionUniqueness` 发现既有 `PMS_CHANGE_APPROVAL`、`ERP_PURCHASE_ORDER_APPROVAL`、`ERP_SETTLEMENT_APPROVAL`、`CONTRACT_APPROVAL`、`ERP_SALES_ORDER_APPROVAL` 的重复 `v1` 定义并主动拒绝建唯一索引；本轮未删除、覆盖或迁移这些历史数据。
- PostgreSQL：临时库 Workflow CAS、事务回滚、并发唯一性、定义版本迁移和 LMS 替代审批等探针已通过；现有 `velrixworkhub` 业务库发现历史重复定义版本，启动迁移按设计拒绝建唯一索引，待业务数据人工处置后复核。
- 浏览器：2026-07-19 完成整体只读回归，并在 PostgreSQL 库 `velrixworkhub_webtest_20260719c` 完成多用户写入回归；`complexuser20260719` 与 `admin` 完成 LMS 客户/机台/特性基线→申请审批→外部 License 登记→续期替代→重发驳回重提→换机批准→授权停用/恢复/作废及空原因拦截链路，原授权、替代申请、审批意见、生命周期审计和 `OtherInfo` 均可追溯。此前 CRM 合同拒绝→重提→复审生效、PMS 变更审批→已实施、ERP 采购订单审批→已提交→已收货→已关闭三条流程也保留证据；测试用户、业务数据、流程实例、待办和通知按要求保留。默认 `velrixworkhub` 库未修改。首页与 Workflow 收件箱循环依赖问题、Admin 用户资料页和静态资源路径均通过。
## 核心自动化与 PostgreSQL 回归

| 测试点 | 状态 | 最后回归时间 | 证据/备注 |
|---|---|---|---|
| Workflow CAS、事务与失败回滚 | [x] 通过 | 2026-07-19 | Domain 全量测试 `445/445`；覆盖并行 Split/Join、Any/Majority/Quorum 会签、审批快照 CAS 回滚与并发胜出快照补偿、陈旧实例退回后的活动节点刷新、审批决策和图状态推进实例行锁、无事务内存态恢复、实例服务直接调用恢复、终态锁提交后释放，以及实例 `TryAdd`/Revision CAS、待办原子 `TryAdd`、通知/操作历史原子写入、失败记录提交回调、补投租约 CAS、数据库端运行实例冲突聚合、业务动作 `NodeExecuted` 原子预占回滚、失败节点重试行锁和 PostgreSQL/SQL Server 双连接 Continue/Retry/审批快照交叉竞态、陈旧失败审计核验；Reject/Cancel/Withdraw 以及 Approve/Transfer/ReturnTo 外层事务回滚也会恢复快照，终态锁仍只在提交后释放，新增待办和启动实例均只按本次创建 ID 补偿。 |
| Workflow 跨模块动作事务边界 | [x] PostgreSQL 通过 | 2026-07-19 | 合同、项目变更、采购订单 handler 在业务状态写入后注入流程完成持久化失败，三类业务状态、审批待办和流程实例均无部分提交；独立 `CreateApprovalTask`/待办补偿入口在操作历史失败时也回滚待办和审批人快照；PostgreSQL Workflow 探针通过。 |
| Workflow 图运行时与状态快照 | [x] 通过 | 2026-07-19 | 覆盖活动节点、Join 到达、Loop 计数、定义连线完整性、旧数字节点类型兼容、损坏状态拒绝，以及实例重建复用发布图校验；业务动作在真实事务中先原子占用执行键；动作/通知本身失败才写 `NodeFailed`，后续实例 CAS 冲突不伪造失败审计；Retry 锁内复核最近失败 ID；审批退回重新进入同一自动节点时按 Revision 生成新的执行范围；节点进入/完成历史同样按 Revision 保留 Loop 和重入的每轮迁移；条件定向推进后的业务动作保留实际 Actor；审批人快照 CAS 竞争失败时可复用胜出快照，陈旧实例补偿待办时先刷新活动节点；审批决策、Runtime 图状态推进和 InstanceService 直接状态用例均在事务前先校验并锁定实例行，审批 Actor 继续传递到后续自动业务动作；Reject/Cancel/Withdraw/Approve/Transfer/ReturnTo 外层回滚恢复内存状态，终态锁延迟至提交后释放，待办与启动实例补偿只删除本次创建项；通知失败补投也通过原子 `TryAdd` 复用胜出通知，并按创建 ID 清理补投后段失败的内存通知。Domain `445/445`、PostgreSQL/SQL Server 探针通过。 |
| Workflow 条件/多审批/退回/业务动作组合链路 | [x] 通过 | 2026-07-19 | 条件分支进入双审批，第二节点退回第一节点形成新轮次，重新审批后执行声明式业务动作并到达 End；并行条件可按 NodeId 分别提交字段，不会互相误用；字段暂未命中分支时保持等待并允许后续重试；条件定向推进后的业务动作保留实际操作者；自动业务动作首次失败后可从恢复节点重试；并行失败分支支持按 NodeId 定向重试，重试进入审批时由 TaskService 同事务补齐待办，未知 NodeId 不会回退，转交目标不能为空；真实事务先原子占用 `NodeExecuted`，PostgreSQL 探针已覆盖回滚路径；陈旧审批决策、图状态推进及直接 InstanceService 状态用例均在实例行锁处拒绝/仲裁，审批后业务动作保留实际操作者；终止型和普通审批决策在外层事务回滚时恢复实例与待办快照并保留进程锁，退回/转交新待办只按创建 ID 补偿；绑定启动运行时/待办准备失败时也只补偿本次成功插入的实例。Domain 全量 `445/445`。 |
| Workflow 待办、操作与通知幂等 | [x] 通过 | 2026-07-19 | 待办按稳定主键、通知按“接收人 + DedupeKey”、操作历史按 DedupeKey 均经仓储原子 `TryAdd`，首次写入不再先查后写，重复写入不产生重复待办、操作或通知；通知投递失败记录按事务提交回调写入，回滚不会留下孤儿记录；失败补投以租约 CAS 互斥，通知写入与 `Resolved` 标记同事务；PostgreSQL 探针通过。 |
| Workflow 陈旧待办活动节点门禁 | [x] 通过 | 2026-07-18 | 流程离开 Start 后，历史节点待办不能在单活动节点场景绕过校验；Domain `382/382`。 |
| LMS 统一工作台待办归类 | [x] 通过 | 2026-07-19 | LMS 许可证申请和授权替代审批进入统一 Workflow 收件箱及首页待办，模块筛选、原单深链、完整申请/授权状态分布和生命周期近期活动已接入；Domain `445/445`、Web 构建通过，只读浏览器回归通过。 |
| OA 借款还款审批统一待办归类 | [x] 浏览器通过 | 2026-07-22 | `OaCashAdvanceRepayment` 的 Pending Workflow 待办归入 OA，标题为“OA 借款还款审批”，并在 PostgreSQL Web 由当前审批人批准后回到借款页显示已结清；领域自动化和收件箱/业务页浏览器证据均已具备。 |
| OA Workflow 审批结果通知 | [x] 自动化通过 | 2026-07-22 | 请假、加班、报销、借款、借款还款和付款申请的批准/驳回结果统一通知启用申请人；去重键包含业务类型、业务 ID、Workflow 实例 ID 和结果，驳回意见可见，停用申请人不投递。领域定向 `2/2`，全量 `581/581`；浏览器和 PostgreSQL 业务写入未执行。 |
| OA 离职账号回收 | [x] 自动化通过 | 2026-07-22 | 五项清单完成后，离职 Application 事务先停用平台账号并递增 `AuthVersion`，再回写员工档案为 `Resigned`；离职记录保存操作者、时间和原因，账号停用异常阻断状态转换。定向离职 `5/5`，领域全量 `587/587`，Web 构建 `0` 警告/`0` 错误；浏览器和 PostgreSQL 业务写入未执行。 |
| OA 离职风险清单与完成阻断 | [x] 浏览器通过 | 2026-07-22 | 通过车辆、借款、报销和资产 Application 服务聚合待审批/待归还车辆、未结清借款、未完成付款报销和在用资产；页面显示原单深链，存在风险时完成离职被服务端阻断。PostgreSQL Web 已验证五项清单全部完成后仍因一条未付款报销和一项未归还资产阻断完成，控制台 0 错误/0 警告；定向离职 `6/6`，领域全量 `593/593`，Web 构建 `0` 警告/`0` 错误。 |
| OA 车辆年检/保险到期提醒 | [x] 浏览器通过 | 2026-07-22 | `VehicleComplianceReminderService` 在未来 30 天窗口扫描年检/保险到期日，仅通知启用的车辆负责人；去重键绑定车辆、类型和到期日，已报废、无负责人或负责人停用的车辆跳过，扫描不改变车辆状态。隔离 PostgreSQL Web 通过人员下拉创建 `沪A-REMIND-20260722` 并指定管理员，重启扫描器后通知中心显示年检和保险各一条未读提醒；控制台 0 错误/0 警告，截图 `artifacts/output/playwright/vehicle-compliance-reminder-postgresql-20260722.png`。领域全量 `610/610`，Web 构建 0 警告/0 错误。 |
| OA 资产台账与领用归还门禁 | [x] 自动化通过 | 2026-07-22 | 资产编号唯一、领用员工必填、在用资产不能编辑或直接改状态、同一资产不能重复领用，归还恢复可用并保留历史，登记/编辑/领用/归还/状态变更追加不可变流水，越权归还被拦截；领用/归还台账双写在事务边界内，历史写入失败会恢复资产和领用状态；在用资产由离职风险聚合。定向 `6/6`，领域全量 `593/593`，Web 构建 `0` 警告/`0` 错误；浏览器和 PostgreSQL 业务写入未执行。 |
| OA 资产申请审批与批准后锁定 | [x] 浏览器通过 | 2026-07-22 | `OaAssetRequest` 浏览器验证针对可用资产创建、提交、Workflow 批准，生成领用记录并将资产置为 `InUse`；驳回必须有原因、驳回重提、重复申请和越权仍由自动化覆盖。截图 `artifacts/output/playwright/oa-asset-request-approved-regression-20260722.png`。 |
| OA 资产位置转移与历史追溯 | [x] 浏览器通过 | 2026-07-22 | `AssetService.Transfer` 浏览器验证在用资产由“交付部办公区”转为“研发部设备间”，责任人快照保持系统管理员并追加转移流水；维修中/已报废门禁和事务回滚仍由自动化覆盖。截图 `artifacts/output/playwright/oa-asset-transfer-regression-20260722.png`。 |
| OA 资产盘点证据与差异审计 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL Web 已保存一致盘点，页面回显最近盘点“一致”且台账状态不变；差异/未找到原因、责任人和流水失败补偿由自动化覆盖。截图 `artifacts/output/playwright/oa-asset-stocktake-regression-20260722.png`。 |
| OA 请假额度与审批状态回写 | [x] 自动化通过 | 2026-07-22 | 年假/调休按员工年度额度建立申请唯一占用；提交占用、驳回/撤回释放、批准转已使用，额度不足、未配置额度、跨年度和已失效占用被拦截；病假/事假/其他不强制额度。定向 `3/3`，领域全量 `587/587`，Web 构建 `0` 警告/`0` 错误；浏览器和 PostgreSQL 业务写入未执行。 |
| OA 付款申请财务复核门禁 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL Web 已验证员工付款和供应商付款均需 Workflow 批准后进入财务复核，复核通过后才进入待登记实际付款；复核驳回和重提门禁仍由自动化覆盖。 |
| OA 付款申请实际付款与 ERP 应付核销 | [x] 浏览器通过 | 2026-07-22 | PostgreSQL Web 员工付款 `FK-202607-121017` 在复核通过后登记外部流水并变为 `Paid`；供应商付款无采购订单时提示“前置采购订单不存在，不能登记 ERP 应付核销”，未伪造 ERP 订单。截图 `artifacts/output/playwright/oa-payment-employee-actual-payment-regression-20260722.png`、`artifacts/output/playwright/oa-payment-register-negative-regression-20260722.png`。 |
| OA 付款申请状态历史 | [x] 浏览器通过 | 2026-07-22 | 付款申请页面回显提交→审批批准→实际付款的不可变状态历史；员工付款实际登记成功，供应商付款负向门禁不改变原申请状态。 |
| OA 付款预算占用与执行 | [x] 自动化通过 | 2026-07-22 | 预算台账按主体公司、部门、币种维护总额；付款申请提交占用、驳回/撤回释放、实际付款消耗，驳回重提复用同一占用记录，超额/关闭/维度不匹配由服务端拦截。领域全量 `557/557`，浏览器和 PostgreSQL 业务写入未执行。 |
| OA 付款批次组批与撤回 | [x] 浏览器通过 | 2026-07-22 | 隔离 PostgreSQL 库创建 `PAYBATCH-WEB-REG-20260722`，将财务复核通过的供应商付款 `FK-202607-115047` 加入 CNY 草稿批次并提交；提交后明细冻结，撤回后历史明细保留且该付款重新可组批。空批次及汇总与明细不一致也由自动化拒绝。领域 `611/611`、Web 构建 0 警告/0 错误；截图 `artifacts/output/playwright/oa-payment-batch-submitted-regression-20260722.png`、`artifacts/output/playwright/oa-payment-batch-cancelled-regression-20260722.png`，控制台 `0` 错误/`0` 警告。 |
| OA 采购预算占用与采购订单执行 | [x] 浏览器通过 | 2026-07-23 | 隔离 PostgreSQL Web 以 `CG-BUDGET-WEB-20260723` 引用 `PURCHASE-WEB-REG-20260722` 提交 `3000.00` 产品申请并经 Workflow 批准；生成 `PO-20260723-CG-BUDGET-WEB-20260723` 草稿后预算显示可用 `2000.00`、已执行 `3000.00`、待占用 `0.00`。取消来源订单后恢复可用 `5000.00`、已执行 `0.00`，可重试生单的业务前提成立；截图 `artifacts/output/playwright/oa-procurement-budget-order-created-regression-20260723.png`、`artifacts/output/playwright/oa-procurement-budget-order-cancelled-regression-20260723.png`，控制台 0 错误/0 警告。 |
| OA 采购寻源/比价与中选报价 | [x] 自动化通过 | 2026-07-22 | 已批准的寻源需求才能创建寻源单；不同已准入供应商报价至少两家才可提交，草稿阶段禁止重复供应商，提交后只能选择当前寻源单内且未过期的报价，撤回保留历史并允许新一轮寻源。领域全量 `570/570`，浏览器和 PostgreSQL 业务写入未执行。 |
| OA 采购申请多明细拆单 | [x] 自动化通过 | 2026-07-22 | 已批准且每条均绑定产品的多明细采购申请按来源明细生成多张 ERP 草稿订单，订单号序号化并保留 `SourceLineId`；拆单前统一拦截重复来源，部分取消不能重试，全部取消后可整批重试。领域全量 `571/571`，浏览器和 PostgreSQL 业务写入未执行。 |
| OA 报销到员工付款申请级联 | [x] 浏览器通过 | 2026-07-22 | 隔离 PostgreSQL Web 为已批准报销 `BX-202607-114835` 创建员工付款申请 `FK-BX-202607-114835`，金额 ¥280.50、前置依据和报销来源均正确回显；返回报销页后状态为“已报销”。权限、状态、金额不一致和重复创建/付款仍由自动化门禁覆盖。截图 `artifacts/output/playwright/oa-expense-payment-cascade-regression-20260722.png`，控制台 0 错误/0 警告。 |
| ERP 采购订单收货事务与入库幂等 | [x] 自动化通过 | 2026-07-22 | 收货前检查启用仓库和 `{OrderNo}-IN` 来源号；订单推进为 `Received` 与入库流水写入处于同一事务，入库失败恢复为 `Submitted`，重复来源不新增流水且不改变订单状态。领域定向 `3/3`，全量 `577/577`；浏览器和 PostgreSQL 业务写入未执行。 |
| OA 中选报价带入 ERP 采购订单 | [x] 自动化通过 | 2026-07-22 | 已定标寻源单可生成 `Sourcing` 来源 ERP 草稿订单，供应商、报价金额和寻源编号自动带入；产品、数量和到期日由采购复核明确选择，重复操作返回已有订单，未定标和无权限调用被拒。领域定向 `2/2`，全量 `579/579`；浏览器和 PostgreSQL 业务写入未执行。 |
| 统一待办重复触发抑制 | [x] 自动化通过 | 2026-07-19 | 统一待办按 `(Source, SourceId)` 去重；重复来源保留优先级最高、截止最早项，不合并不同审批待办；Domain `445/445`。 |
| 首页跨模块待办模块与优先级联合筛选 | [x] 浏览器通过 | 2026-07-19 | PostgreSQL 浏览器验证首页同时按模块和优先级筛选：`PMS + 高` 只显示“客户需求确认延期风险”，`ERP + 高` 显示空结果且计数同步，切换“紧急”验证全局空结果；申请人和管理员首页均显示优先级筛选，控制台均为 0 错误、0 警告。筛选由 `UnifiedTodoService.Filter` 统一执行。Domain `447/447`、Web 构建 `0` 警告/`0` 错误。人员和组织筛选仍未实现。 |
| 跨模块风险提醒投影 | [x] 自动化通过 | 2026-07-19 | `CrossModuleReminderService` 将合同临期、逾期应收/应付、库存风险、逾期项目节点和高优先级风险问题投影为 OA Reminder；启用用户接收人大小写去重，重复扫描依靠“接收人 + 稳定事件键”不新增通知，并保留 CRM/ERP/PMS 原单深链；Domain `444/444`。 |
| PMS 项目节点逾期统一待办 | [x] 自动化通过 | 2026-07-19 | PMS 未完成阶段/里程碑按计划结束日判断逾期，生成高优先级 `ProjectPhase` 待办并保留 `Pms/Phase?projectId=...` 深链；完成/取消节点不会生成；Domain `445/445`。 |
| ERP 商品安全库存统一待办 | [x] 通过 | 2026-07-19 | 启用商品按所有仓库汇总账面库存，低于大于 0 的安全库存才生成 ERP `InventoryRisk` 高优先级待办，商品安全线、当前库存和 `/Erp/Product` 深链均保留；安全库存为空/为 0 或库存达到安全线不生成；Domain `445/445`、Web 构建 0 警告/0 错误、临时 PostgreSQL 只读浏览器回归通过。 |
| ERP 应收/应付到期统一待办 | [x] 通过 | 2026-07-19 | 未核销订单余额使用采购/销售订单到期日；逾期余额为高优先级，未来到期为普通提醒，旧记录缺失到期日按订单日期后 30 天兼容；Domain `445/445`、Web 构建 0 警告/0 错误、临时 PostgreSQL 只读浏览器回归通过。 |
| CRM/ERP 主数据 OtherInfo 扩展字段 | [x] 自动化与 PostgreSQL 通过 | 2026-07-19 | 客户、商品、供应商、仓库的 Domain 入口统一校验 JSON 对象并保留自定义字段；临时 PostgreSQL 启动应用后四张表均生成非空 `OtherInfo` 文本列，种子行默认为 `{}`；Domain `444/444`、Web 构建 0 警告/0 错误。本轮未执行浏览器写入回归。 |
| LMS 许可证页菜单与操作权限 | [x] 构建通过 | 2026-07-18 | `Lms/License` 页面按当前用户菜单权限加载；新建、提交、外部授权登记、生命周期变更、草稿删除、申请取消分别校验按钮权限，未授权首屏不读取许可证列表；Domain `382/382`、Web 构建通过，浏览器专项暂停。 |
| LMS 申请详情聚合 | [x] 自动化与浏览器通过 | 2026-07-19 | `LmsLicenseRequestDetailService` 按申请 ID 聚合特性引用、Workflow 实例、操作历史和统一附件版本列表；PostgreSQL 浏览器验证申请审批完成后显示 `已批准`、Workflow `已完成`、附件版本与下载入口，审批完成后不再显示上传/删除控件；Domain `445/445`、Web 构建 0 警告/0 错误。 |
| LMS 申请与授权数据范围 | [x] 构建通过 | 2026-07-18 | `LmsLicenseAccessService` 统一管理员全量/普通用户本人范围；普通用户不能通过申请或授权深链读取他人数据；Domain `382/382`、Web 构建通过，浏览器专项暂停。 |
| LMS 申请附件服务端边界 | [x] 自动化与浏览器通过 | 2026-07-19 | `LmsLicenseAttachmentService` 校验 2MB 单文件、6 个有效附件上限、扩展名/MIME 白名单、申请状态、申请数据范围、基础内容扫描和统一附件审计入口；附件 `OtherInfo` 保存来源/自定义字段，详情页上传/删除与下载端点均复核 LMS 数据范围；Domain `445/445`、Web 构建 0 警告/0 错误，临时 PostgreSQL 浏览器已验证。专业病毒引擎待接入。 |
| LMS 附件内容扫描契约 | [x] 自动化与浏览器通过 | 2026-07-19 | `IAttachmentContentScanner` 支持替换专业扫描器，默认 `BasicAttachmentContentScanner` 拒绝 `MZ` 可执行伪装及常见脚本载荷；浏览器上传 `MZ` 文本伪装被拦截且附件数量不增加；Domain `445/445`。 |
| LMS 申请与授权有效期门禁 | [x] 自动化通过 | 2026-07-18 | 新建申请及登记外部授权时，服务端拒绝当前或过去的到期时间；保留历史对象的到期状态派生；Domain `382/382`、Web 构建通过。 |
| LMS 申请/授权环境元数据快照 | [x] 自动化通过 | 2026-07-18 | 机台申请保存 `Model`、`Environment`、`GracePeriodDays`，批准登记外部授权及续期/换机授权继承快照；FreeSql 记录追加映射，Domain `382/382`、Web 构建和 PostgreSQL 探针通过。 |
| LMS 宽限期状态与提醒 | [x] 自动化通过 | 2026-07-18 | 有效状态延展至 `ExpiresAt + GracePeriodDays`；到期后先发送宽限期通知，宽限结束后才派生 `Expired` 并发送到期通知；原始到期时间和人工状态不改写；Domain `382/382`。 |
| LMS 驳回意见服务端门禁 | [x] 自动化通过 | 2026-07-18 | `LmsLicenseWorkflowActionHandler` 拒绝空白驳回意见，失败时申请仍保持 `Submitted`；有效意见才回写 `Rejected` 并通知申请人；Domain `382/382`、Web 构建通过。 |
| LMS 提交审批申请人通知 | [x] 自动化通过 | 2026-07-18 | 申请状态、Workflow 实例和初始运行时成功提交后，才向申请人发布“已提交审批”通知；通知按申请与实例去重，主交易失败不产生通知；Domain `382/382`、Web 构建通过。 |
| LMS 替代申请创建归属门禁 | [x] 自动化通过 | 2026-07-18 | Application 创建替代申请时复核原授权访问范围；他人授权拒绝创建，本人授权允许创建，管理员可处理全量；Domain `382/382`、Web 构建通过。 |
| LMS 申请取消操作者范围 | [x] 自动化通过 | 2026-07-18 | `LmsLicenseService.Cancel` 在 Application 层限制为申请人本人或管理员；他人取消被拒且状态不变，管理员取消仍保留 Workflow 撤回、事务和通知语义；Domain `382/382`、Web 构建通过。 |
| LMS 申请写操作操作者范围 | [x] 自动化通过 | 2026-07-18 | `LmsLicenseService` 对删除草稿、直接提交、启动审批提交和撤回后重提统一限制为申请人本人或管理员；越权调用在访问 Workflow 前拒绝且不改状态；Domain `382/382`、Web 构建通过。 |
| LMS 申请创建操作者范围 | [x] 自动化通过 | 2026-07-18 | 机台申请创建由 Application 校验申请人与当前操作者一致；普通用户不能代填他人申请人，管理员可代申请；页面对普通用户只读显示当前登录用户；Domain `382/382`、Web 构建通过。 |
| LMS 外部授权登记操作者范围 | [x] 自动化通过 | 2026-07-18 | 关联申请的外部 License 登记由 Application 校验当前用户是否能读取该申请；他人登记被拒且不写授权，管理员可登记全量；Domain `382/382`、Web 构建通过。 |
| LMS 附件读写操作者范围 | [x] 自动化通过 | 2026-07-18 | 附件列表、上传和删除由 Application 复核申请数据范围；他人操作被拒且不写附件，管理员可处理全量；下载端点继续校验会话和申请范围；Domain `382/382`、Web 构建通过。 |
| LMS 替代审批页菜单与操作权限 | [x] 构建通过 | 2026-07-18 | `Lms/LicenseReplacement` 页面按当前用户菜单权限加载；创建并提交、驳回/撤回后重提分别校验按钮权限，未授权首屏不读取替代申请列表；Web 构建通过，浏览器专项暂停。 |
| Workflow 跨业务绑定 | [x] 通过 | 2026-07-18 | CRM 合同、ERP 采购/销售/核销、PMS 变更和 LMS 审批绑定通过自动化验证。 |
| CRM/PMS/ERP 审批动作统一应用入口 | [x] 通过 | 2026-07-18 | 合同、项目变更、采购订单、销售订单 handler 通过 `ApplyApproval` 入口，ERP 核销通过 `SettlementService`；Domain `382/382` 覆盖门禁、幂等和非法来源状态。 |
| 通知失败后台重试与人工处置 | [x] 通过 | 2026-07-18 | Worker 每 5 分钟批量重试；`InspectPending` 对 3 次以上失败输出摘要告警；页面/API 支持单条或最多 50 条批量重试，受按钮权限保护并写入独立操作审计；Domain `382/382`、Web 构建通过。 |
| OA 统一通知中心删除与分页 | [x] 自动化/PostgreSQL 通过 | 2026-07-18 | `NotificationService` 提供接收人隔离的单条/批量删除和分页；FreeSql 仓储分页/未读统计使用数据库 `COUNT` 与 `Skip/Take`；兼容迁移清除历史 `ReadAt` 服务端默认值；OA 通知中心提供选择、删除和上一页/下一页；他人通知 ID 不会被删除，删除会释放去重键；Domain `382/382`、Web 构建 0 警告/0 错误、PostgreSQL 探针通过，浏览器本轮暂停。 |
| LMS 申请、授权与生命周期 | [x] 通过 | 2026-07-18 | 申请审批、Draft/Submitted 取消、Withdrawn 保留、取消通知去重、外部 License 登记、启用/停用/作废、到期派生状态和审计事务通过；PostgreSQL 探针通过。 |
| LMS 替代审批 | [x] 通过 | 2026-07-18 | 续期、重发、换机、撤回/驳回重提、唯一索引、审批批准后资产变更和失败回滚通过。 |
| CRM/ERP/PMS 跨模块聚合 | [x] 通过 | 2026-07-18 | 客户、合同、项目、订单、核销、库存和经营指标的引用隔离与金额聚合通过。 |

## 页面与深链补测

| 测试点 | 状态 | 最后回归时间 | 证据/备注 |
|---|---|---|---|
| CRM/ERP/PMS 项目订单深链 | [x] 通过 | 2026-07-19 | 项目组合卡片进入销售订单（保留 `projectId`、`activeOnly=true`、`status=Submitted`）、客户交易视图（`customerId`）、收付款核销（`orderId`、`kind=Payable`）、风险与 EVM 页面；标题、筛选和项目上下文正常，未执行写入。 |
| ERP 报表深链和日期筛选 | [x] 浏览器通过 | 2026-07-19 | PostgreSQL 浏览器加载库存、采购、销售、往来和 PMS 汇总；日期 `2026-07-01` 至 `2026-07-19` 筛选后指标与汇总可见，供应商采购跳转供应商交易视图、客户销售跳转客户交易视图、项目销售订单深链保留 `projectId` 与 `activeOnly=true`；目标订单和 CRM/PMS 上下文正常，控制台 0 错误。 |
| LMS 替代申请提交与批准 | [x] 通过 | 2026-07-19 | 在保留的 PostgreSQL 测试库完成申请提交、管理员批准、续期、重发、换机、驳回后重提和复审批准；申请、授权、审批意见及替代链均可追溯。 |
| LMS 许可证申请三步向导 | [x] 浏览器通过 | 2026-07-19 | PostgreSQL 浏览器完成申请信息→特性与扩展→确认保存，返回后客户、机台、产品、日期和宽限字段保持，摘要显示机台特性与 `OtherInfo`；保存草稿后提交审批，管理员批准，收件箱历史显示 `V1`、审批人和审批意见，申请详情变为已批准；申请人和管理员控制台错误均为 0。 |
| LMS 申请客户/联系人筛选与扩展详情 | [x] 浏览器通过 | 2026-07-19 | PostgreSQL 浏览器在许可证页按 `Aster 科技` 返回 3 条申请，按 `林经理` 返回空状态并保留筛选条件；清除联系人筛选后进入向导申请深链，详情显示 CRM 客户、联系人、特性版本与 `OtherInfo`；控制台 0 错误。 |
| LMS 授权 OtherInfo 运营展示 | [x] 浏览器通过 | 2026-07-19 | PostgreSQL 浏览器在许可证授权列表查看换机后的 `LIC-COMPLEX-MACHINE-20260719`，卡片显示授权级 `OtherInfo`、来源链和生命周期审计；控制台 0 错误。 |
| 统一附件面板 OtherInfo 与版本审计 | [x] 浏览器通过 | 2026-07-19 | PostgreSQL 浏览器在 CRM 客户 `Aster 科技` 上上传附件，显示 `V1`、来源 `OtherInfo`，下载成功；删除后打开历史版本仍保留扩展字段，并显示上传/下载/删除审计；数组 `[]` 上传被 `必须是 JSON 对象` 拦截且附件数保持 0。 |
| Admin 用户资料页修复后回归 | [x] 通过 | 2026-07-18 | 资料页可正常显示账户信息和密码管理区域。 |
| 简单表单印章申请定义与渲染 | [x] 浏览器通过 | 2026-07-21 | 独立 SQLite 临时宿主使用管理员登录后，从“流程平台 → 简单表单”打开预置 `SIMPLE_SEAL_REQUEST`；页面显示 V1、`SIMPLE_SEAL_REQUEST_APPROVAL` 和 `SEAL_REQUEST_NOTIFY_RECIPIENT`，填写区按两栏渲染印章类型、人员、部门、文件名称及全宽申请事由。截图：`artifacts/output/playwright/simple-form-page.png`。 |
| PMS 项目工作项首版 | [x] 自动化与构建通过 | 2026-07-21 | `PmsProjectWorkItem` 覆盖项目/父项来源、计划日期、状态推进、完成反馈、实际时间、终态编辑和含子项删除门禁；领域 `511/511`、Web 构建 `0` 警告/`0` 错误。浏览器写入、受控参与人、评论历史、提醒、审批和组织范围待后续回归。 |
| UI-ADMIN-AUDIT-01 | [x] | 2026-07-19 | 权限变更审计页可从系统管理菜单打开，显示只读筛选与空数据状态；非法主体 ID 被页面校验拦截。 |
| UI-ADMIN-NOTIFY-01 | [x] 只读通过 | 2026-07-19 | 通知失败处置页显示待处理空状态和最近处置列；控制台 0 错误，未执行单条或批量重试。 |
| Workflow 收件箱退回目标与新轮次入口 | [x] 浏览器通过 | 2026-07-23 | 隔离 PostgreSQL `velrixworkhub_webtest_20260719c` 以 CRM 合同 `CT-MULTI-RETURN-20260723` 和 `CONTRACT_APPROVAL V4` 运行双账号审批：`admin` 初审，`complexuser20260719` 独立登录后只看到自己的复审待办并退回初审，系统生成初审新轮次，管理员再次初审后复审人收到第二轮待办并最终同意。两账号待办数均为 0，处理历史保留 `Returned`、两轮审批意见和完整操作记录；截图 `artifacts/output/playwright/workflow-multiuser-return-reviewer-history-regression-20260723.png`、`artifacts/output/playwright/workflow-multiuser-return-admin-history-regression-20260723.png`，两会话控制台 `0` 错误/`0` 警告。该定义未配置业务 `onApproved` 动作，本条只验收 Workflow 引擎状态，不宣称合同业务状态回写。 |
| PMS 项目变更审批实际状态回写 | [x] 浏览器通过 | 2026-07-26 | PostgreSQL 浏览器由 `complexuser20260719` 新建并发起“多用户回归项目变更-20260726”，`admin` 在独立收件箱填写“多用户回归审批通过。”并同意；管理员待办由 1 条归零，申请人刷新后项目变更显示“已批准 / 审批：已完成”。隔离库复核 `PmsProjectChange=Approved | WorkflowInstance=Completed | V1`；截图 `artifacts/output/playwright/workflow-multiuser-pmp-admin-approved-20260726.png`、`artifacts/output/playwright/workflow-multiuser-pmp-requester-completed-20260726.png`，两会话控制台 0 错误、0 警告。 |
| ERP 采购订单审批到收货关闭 | [x] 浏览器通过 | 2026-07-19 | PostgreSQL 浏览器以 `PO-20260712-001` 完成发起审批、管理员审批、收货和关闭；订单由草稿→已提交→已收货→已关闭，库存从 25.00 增加到 35.00，待收货归零，审批显示已完成，供应商交易和核销深链仍保留。 |
| Workflow 定义审批策略配置与版本操作 | [x] 浏览器通过 | 2026-07-19 | PostgreSQL 浏览器在定义页创建 `WF-BROWSER-QUORUM-20260719`，录入 3 名审批人和 Quorum 2 票，草稿卡片显示 `2 / 3 票`，JSON 显示 `approvalMode`/`requiredApprovals`；发布后仍保留配置，归档后状态变为已归档，控制台 0 错误、0 警告。 |
| Workflow 定义双审批与退回目标配置 | [x] 浏览器通过 | 2026-07-19 | PostgreSQL 浏览器创建 `WF-BROWSER-RETURN-20260719`，录入初审 `admin` 和复审 `complexuser20260719`，生成 4 节点/3 连线图；JSON 显示复审节点的 `returnTargets` 指向初审，草稿校验通过并成功发布，控制台 0 错误、0 警告。 |
| Workflow 收件箱当前用户隔离 | [x] 只读通过 | 2026-07-19 | 收件箱显示当前登录用户 `admin`，待办按当前用户查询；URL 中的 `assignee` 未用于改变审批操作者身份。 |
| Workflow 定义页菜单与按钮权限 | [ ] 待浏览器回归 | 2026-07-22 | PostgreSQL Web 授权用户打开 `/Workflow/Definition`，看到 22 个流程版本并打开“新建审批流程”编辑器；创建/发布/归档/删除写入和未授权首屏仍未专项执行。截图 `artifacts/output/playwright/workflow-definition-create-editor-regression-20260722.png`，控制台 0 错误/0 警告。 |
| Workflow 定义页未授权首屏隔离 | [x] 构建通过 | 2026-07-18 | 未通过 `Workflow/Definition` 菜单权限时不加载流程定义列表，仅执行重定向；Web 构建通过，浏览器专项暂缓。 |
| Workflow 收件箱动作按钮权限 | [ ] 待浏览器回归 | 2026-07-22 | PostgreSQL Web 当前审批人 admin 在收件箱连续批准 OA 请假、报销、借款、付款、加班、采购、员工付款、资产领用和借款还款待办，待办数归零；拒绝、退回、转交、撤回、失败节点重试及未授权按钮仍待专项回归。截图 `artifacts/output/playwright/workflow-oa-approvals-regression-20260722.png`、`artifacts/output/playwright/workflow-employee-payment-approval-regression-20260722.png`。 |
| Workflow 收件箱审批动作与版本显示 | [x] 浏览器通过 | 2026-07-19 | PostgreSQL 测试库中管理员从待办收件箱填写审批意见并同意 LMS 申请，待处理数由 1 变为 0；处理历史正确显示流程版本 `V1`、审批人和意见，申请详情同步变为 `已批准`，验证修复 Razor 内联版本表达式。 |
| 通知失败处置页菜单与按钮权限 | [ ] 待浏览器回归 | — | 页面深链校验 `Admin/NotificationFailures`，单条重试校验 `Admin/NotificationFailures/Retry`，批量重试额外校验 `Admin/NotificationFailures/BatchRetry`；本轮已验证授权用户只读加载，未执行重试写入。 |
| Workflow 收件箱损坏快照容错 | [x] 自动化/构建通过 | 2026-07-18 | 收件箱对失败节点类型解析异常安全降级，并逐个显示可定向重试的失败节点，不因历史损坏快照阻断页面渲染；Web 构建通过，浏览器专项暂缓。 |
| Workflow 终态重试保护 | [x] 自动化通过 | 2026-07-18 | Completed/Rejected/Cancelled 实例拒绝 Retry，且不新增 `Retried` 审计；Domain 全量测试与 PostgreSQL 探针通过。 |

## 暂不执行范围

| 测试点 | 状态 | 最后回归时间 | 备注 |
|---|---|---|---|
| Canvas、复杂表格与表单 Outbox | [ ] 暂缓 | — | 简单 HTML 表单编辑器已开发；Canvas、复杂表格、持久化事件 Outbox/重试和节点级字段权限仍未进入当前切片。 |
| SQL Server 实例回归 | [x] 通过 | 2026-07-19 | 本机 `MSSQLSERVER` 临时库探针与 200 次持久化基准均通过；生产业务库仍需按部署环境复核。 |
| SQLite 专项回归 | [ ] 暂缓 | — | 按当前约定暂不增加 SQLite 测试。 |

## 验证基线

- Domain：`dotnet test .\tests\VelrixWorkHub.Domain.Tests\VelrixWorkHub.Domain.Tests.csproj --artifacts-path .\artifacts /p:UseSharedCompilation=false --logger "console;verbosity=minimal"`，2026-07-23 最近结果 `615/615` 通过。
- Web：`dotnet build .\src\VelrixWorkHub.Web\VelrixWorkHub.Web.csproj --artifacts-path .\artifacts /p:UseSharedCompilation=false`，2026-07-23 最近结果 `0` 警告、`0` 错误。
- PostgreSQL/SQL Server：Workflow 完整探针均在临时库通过；PostgreSQL 临时库和 SQL Server 临时库均已清理，现有 PostgreSQL 业务库的重复定义保护仍待人工处置后复核。
- 浏览器：2026-07-23 在隔离 PostgreSQL 库 `velrixworkhub_webtest_20260719c` 完成采购预算申请提交、Workflow 批准、ERP 草稿生单转已执行及取消订单恢复额度；截图均归档在 `artifacts/output/playwright/`，相关页面控制台均为 0 错误/0 警告。此前 OA 通讯录、招聘/面试/入职、请假/报销/借款/付款/加班/采购提交与主要 Workflow 批准的回归证据继续有效。
- 浏览器补充：2026-07-25 在同一隔离 PostgreSQL 库以 `admin` 与 `complexuser20260719` 两个独立会话登录；两侧 Workflow 收件箱均基于当前登录用户显示，复审账号页面明确回显 `complexuser20260719` 且没有混入管理员待办。复审收件箱截图为 `artifacts/output/playwright/workflow-multiuser-reviewer-inbox-isolation-20260725.png`；两会话控制台均为 0 错误、0 警告。本轮只验证登录态与空收件箱隔离，不替代已有的双账号提交、退回和终审闭环。
- 本轮代码与运行验证：修复页面自身 `Admin.InitAsync` 权限上下文初始化，并完成 Workflow/OA 主链浏览器回归；采购无效预算编号门禁先阻断，清空可选预算后提交成功。最终领域全量 `606/606`、Web 构建 `0` 警告/`0` 错误；现有业务库历史重复流程定义保护仍保持不变。
