# 简单自定义表单设计

> 状态：已确认，2026-07-21。本文定义 Workflow 简单表单运行时的首个可用边界；Canvas、复杂表格、公式、跨字段计算和任意脚本不属于本版本。

## 目标与边界

简单自定义表单用于“填写数据 -> 审批流转 -> 终态触发简单业务事件”的轻量流程，避免为每一个低复杂度流程新建业务表。它不替代 CRM、ERP、OA 人事、采购、财务等需要强领域规则、明细行、金额计算或跨模块事务的固定实体。

首版提供：

- 表单定义的代码、名称、描述、绑定的 Workflow 定义代码和完成事件代码。
- 可编辑草稿与不可变发布版本；运行中的申请始终引用发布版本和表单数据快照。
- 单行文本、多行文本、下拉单选、单选组、多选、复选框、部门选择、人员选择和通用引用选择器。
- 固定两栏布局：字段声明 `Half` 或 `Full`；渲染器从上到下配对相邻 `Half`，未配对的半宽字段自动以整行全宽渲染。
- 申请草稿、提交、审批批准/驳回、申请人撤回和通用附件。
- 流程终态的幂等完成事件，由 Application 注册的处理器消费，不允许运行时执行脚本或直接按 JSON 写业务表。

首版不提供：自定义表格、公式/脚本、条件显隐、动态远端查询、任意 SQL/HTTP 动作、画布流程编辑、组织数据范围继承、可配置审批人解析或历史表单版本迁移。

## 分层模型

| 对象 | 层 | 作用 |
|---|---|---|
| `SimpleFormDefinition` | Domain | 稳定表单标识、名称、描述、Workflow 代码、完成事件代码与当前发布版本号。 |
| `SimpleFormDefinitionVersion` | Domain | 不可变 JSON Schema 快照和发布信息；草稿版本可编辑，发布后不可变。 |
| `SimpleFormSubmission` | Domain | 表单申请的申请人、定义代码、版本号、Schema 快照、数据 JSON、审批状态与驳回原因。 |
| `ISimpleFormCompletionHandler` | Application | 业务模块订阅受控事件代码；处理器只能通过 Application 用例修改业务，不直接操作其他模块表。 |

Definition 与 Version 分表。`Definition.Code` 规范化为大写且永久稳定；创建草稿、保存草稿、发布版本都经过 Application 服务。发布只接受有效 Schema，并将该版本标记为不可编辑。新草稿从发布版本复制；绝不修改历史发布 JSON。

Submission 创建时保存 `DefinitionCode`、`FormVersionNumber`、`SchemaJson` 和 `DataJson`。即使定义随后发布新版本，历史申请和审批页面仍按自己的 Schema 快照渲染。Submission 是通用业务对象，Workflow `BusinessType` 固定为 `SimpleFormSubmission`，Workflow 代码来自已发布版本快照。

## Schema JSON 契约

发布 Schema 是一个 JSON 对象，枚举和控件名称使用字符串。最低契约：

```json
{
  "title": "外出登记",
  "fields": [
    {
      "key": "purpose",
      "label": "事由",
      "description": "说明外出安排",
      "control": "MultiLineText",
      "width": "Full",
      "required": true,
      "options": []
    },
    {
      "key": "city",
      "label": "城市",
      "description": "",
      "control": "Select",
      "width": "Half",
      "required": true,
      "options": [{ "value": "SH", "label": "上海" }]
    }
  ]
}
```

字段 key 仅允许字母、数字和下划线，以字母开头，长度 1-64，并在同一版本内唯一。label、description 和 option 标签由 Domain 校验长度和必填。`Select`/`Radio`/`MultiSelect` 必须有唯一的静态 options；`Checkbox` 不接受 options；`DepartmentPicker`、`PersonPicker`、`ReferencePicker` 通过受控 `source` 标识声明引用来源，首版不接受自由 SQL、URL 或脚本。

`DataJson` 也必须是 JSON 对象，未知字段、缺失必填字段、错误 JSON 类型和不在 options 内的值均在 Application 服务端拒绝。值约定：文本为 string，复选框为 boolean，多选为 string 数组，单选/下拉为 option value，引用选择器为 `{ "id": "...", "label": "..." }`。标签只是展示快照，业务处理器只能信任经受控选择器和 Application 校验过的 ID。

## 两栏布局规则

字段按 Schema 数组顺序渲染，布局不另存 HTML：

1. `Full` 独占一行，清空未配对半宽字段。
2. 相邻两个 `Half` 合并为一行，各占一半。
3. `Half` 后面紧跟 `Full`、列表结束或任何不兼容字段时，前一个 `Half` 的有效宽度自动提升为 `Full`。
4. 编辑器保存的是请求宽度 `width`，运行时按上述算法得到有效宽度，避免“右侧留白”成为历史快照数据。

因此布局可由 Schema 纯函数重建，发布版本和提交快照不依赖前端 CSS 状态。

## 生命周期与 Workflow

1. 管理员创建 Definition 草稿，维护字段 JSON，选择已发布 Workflow 代码和受控完成事件代码。
2. 发布时校验 Schema，并创建不可变 Version；每个 Definition 同时最多一个当前发布版本。
3. 发起人按当前发布 Version 创建 Submission 草稿。保存、编辑只允许本人且状态为 Draft/Rejected。
4. 提交时重新校验 `DataJson`，写入 Submission `Submitted`，在同一 Workflow 事务内用 Version 快照的 Workflow 代码启动实例。
5. Workflow 的 `SetField Status=Approved/Rejected` 由 `SimpleFormSubmissionWorkflowActionHandler` 处理，只经过 `SimpleFormSubmissionService` 回写终态。
6. 发起人撤回运行实例与 Submission 状态；撤回事件也属于终态，写入完成事件。

审批动作只能由 Workflow 待办指定审批人处理；表单页面不接受查询参数伪造审批人。申请数据读取默认限制为发起人本人，审批操作继续使用 Workflow 收件箱的现有隔离和权限边界。

## 完成事件与幂等

`ISimpleFormCompletionHandler` 按事件代码注册。终态变化与 `SimpleFormCompletionEvent` Outbox 在同一事务写入，提交后立即尝试投递；处理器失败不会回滚已提交的审批状态，而是保留 Pending 事件、记录重试次数和错误，由后台 Worker 每五分钟重试。唯一键按 Submission、事件码和终态保证重复 Workflow 状态动作不会重复派发；处理器仍必须按稳定 Submission ID 幂等，并通过 Application 用例关联目标业务对象。首版提供受控注册点和 No-op 处理器，不从表单配置执行 C#、JavaScript、SQL 或 HTTP。

## 权限、附件与审计

菜单路径：`Workflow/SimpleForm`；按钮路径：`Workflow/SimpleForm/Create`、`Edit`、`Publish`、`Submission/Create`、`Submission/Edit`、`Submission/Submit`、`Submission/Cancel`。页面入口只是体验层，Definition/Submission Application 服务重复执行状态、申请人和版本门禁。

附件复用 `AttachmentService`，`BusinessType = SimpleFormSubmission`。`SimpleFormAttachmentService` 在 Application 层限制为申请人本人访问，草稿、提交中和驳回申请可写，终态申请只读；下载端点同样按当前会话用户 ID 复核，附件内容、审计和访问控制不在表单 JSON 中复制。

## 验收切片

首个开发切片按以下顺序交付：

1. Domain Schema parser/validator、两栏布局归一化、Definition/Version/Submission 状态机与自动化测试。
2. FreeSql 仓储、Web DI、菜单种子、Definition 维护页和 Submission 填写页。
3. Workflow 绑定、终态 handler、幂等完成事件记录与最小 No-op handler。
4. 领域测试、Web 构建和浏览器回归；浏览器或真实 PostgreSQL 被历史 Workflow 重复版本保护阻断时必须保留未通过状态。
