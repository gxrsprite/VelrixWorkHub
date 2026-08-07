# Velrix Work Hub 开发规范

## 工作范围

- 默认只修改 `VelrixWorkHub` 当前项目目录内的文件。
- 继续开发时先阅读 `docs/roadmap.md`、`docs/testing-points.md` 和 `docs/architecture.md`，选择一个未完成且可独立验收的高价值切片。
- 不把外部参考项目、外部目录或外部链接写入 roadmap；外部资料只放在 `docs/localpath/`。
- 代码标识符、API、文件路径保留英文；面向用户的页面、测试点和架构文档使用中文。

## 架构边界

- 项目是模块化单体：OA、CRM、ERP、PMS、Workflow、LMS 通过 Application 用例和稳定引用协作。
- Domain 只放实体、值对象、枚举和领域规则；Application 编排用例、权限意图和跨模块查询；Infrastructure 实现 FreeSql、仓储和外部适配；Web 负责页面、端点、菜单种子和依赖注入。
- 跨模块不得直接读写对方表，必须经过 Application 服务；业务状态门禁由对应模块负责，Workflow 只负责通用流程和运行态。
- 新增核心能力应同时补实体/服务、仓储、页面或 API、菜单、自动化测试和文档。

## FreeSql 持久化规范

- 所有持久化枚举必须使用真实 enum 属性，不得改成 `int` 或在仓储层强转整数。
- 每个 FreeSql 枚举属性必须声明：`[Column(MapType = typeof(string), StringLength = 50)]`；原有 `Position`、`IsNullable` 等参数继续保留。
- 新增枚举字段时同步检查 SQLite/实际 PostgreSQL 建表和历史数据迁移，不只验证编译。
- 数据库中的枚举值使用枚举名称字符串，枚举数字调整不能改变历史业务含义。

## JSON 规范

- 业务 JSON 统一使用 `Domain.JsonSerializationDefaults`，枚举必须通过 `JsonStringEnumConverter` 序列化为名称字符串。
- Web HTTP JSON 必须注册同一套配置；中文使用 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` 保留原文，不输出 `\\uXXXX`。
- 流程定义 JSON、流程实例快照、API DTO 和新增 JSON 不得单独创建绕过共享约定的 `JsonSerializerOptions`。

## Workflow 与通知规范

- 审批动作只能由待办指定审批人处理；查询参数中的 `assignee` 只用于筛选，不能作为审批操作者身份。
- Web 页面必须使用当前登录用户作为审批 actor；首页和收件箱的审批待办都按当前登录用户隔离。
- 待办创建必须幂等；重复创建不能产生重复待办或重复通知。
- 通知按接收人和去重键幂等，接收人大小写无关；审批待办创建通知，审批处理后标记对应通知已读。
- 通知失败不能阻断主交易；新增事件接入时应预留失败记录、重试和审计边界。

## 测试、回归和记录

- 所有构建、测试和浏览器自动化临时产物必须存放在 `/artifacts/`；不得在项目根目录保留 `output`、`.playwright-cli`、`artifacts-*` 或其他未忽略的临时输出目录。
- 使用 Playwright 时应将会话快照、控制台日志和截图归档到 `artifacts/`；需要纳入回归证据的截图保存到 `artifacts/output/playwright/`。
- 后续浏览器回归应边测试边在关键业务状态节点截图（创建、提交、审批/处理完成及关键负向门禁），使用可识别的业务名称归档到 `artifacts/output/playwright/`；最终状态截图应保持页面完整、文字清晰，可直接作为 PPT 汇报素材。
- 不得创建、初始化、替换或删除 `.git` 目录；只使用项目既有仓库元数据执行非破坏性 Git 查询和操作。
- 新增核心规则优先补自动化测试；提交前至少运行：

  ```powershell
  dotnet test .\tests\VelrixWorkHub.Domain.Tests\VelrixWorkHub.Domain.Tests.csproj --artifacts-path .\artifacts /p:UseSharedCompilation=false --logger "console;verbosity=minimal"
  dotnet build .\src\VelrixWorkHub.Web\VelrixWorkHub.Web.csproj --artifacts-path .\artifacts /p:UseSharedCompilation=false
  ```

- `docs/testing-points.md` 中只有有实际证据的条目才能勾选；自动化通过、Web 构建通过和内置浏览器通过分开记录。
- 页面、权限、菜单、深链或跨模块口径变化时，补充相关回归点；内置浏览器未执行或被环境阻断时保持未勾选并记录原因。
- 验证完成后更新 `docs/roadmap.md`、`docs/testing-points.md` 和必要的 `docs/architecture.md`，记录测试总数、警告/错误数量和未完成边界。
- 不使用破坏性 Git 操作，不回滚用户已有改动；编辑文件使用 `apply_patch`。

## 继续开发默认流程

1. 阅读 roadmap 和测试点，定位当前主线中最有价值的未完成项。
2. 先实现一个可验收的垂直切片，再补测试、页面/菜单和文档。
3. 运行定向测试和 Web 构建；积累一批页面测试点后再使用内置浏览器回归。
4. 最终汇报只说明当前项目的改动、验证结果和仍待回归的测试点。
