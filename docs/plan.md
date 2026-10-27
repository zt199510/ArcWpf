# ArcWpf 实施计划

2026-10-27：用户反馈“应该没问题了可以调整成功 input 也是”，确认当前Select动效回归及Input最新起点修复通过，关闭这两项待确认事项。Select相关过滤器本次22/22通过；未重跑Input或全量测试，专项未测边界保留。见[Select记录](verification/2026-10-26-select.md)、[Input记录](verification/2026-10-25-input.md)。下文此前失败与待确认描述为历史状态，以本条及验证记录最新章节为准。

2026-10-26：Pagination收到用户“没问题上传代码”，记录当前版本用户确认；专项未测与全量回归失败仍保留，见验证记录。

2026-10-26：用户反馈 Search“现在控件没问题了”，记录当前动效/占位修复版本用户确认；不扩展为IME、DPI或读屏专项通过。按用户指令开始 Pagination，实现范围见[规格](components/Pagination.md)。

2026-10-26 用户确认“Calendar 已经完成”，覆盖当前Calendar及标题左右12 DIP留白版本；保留未执行的专项记录，不推断所有DPI/读屏通过。开始Search，来源Arc Search field，见[规格](components/Search.md)。

Search已完成来源冻结、原生Input派生控件、清除入口及动效、Gallery实时过滤和5项专项测试；真实输入/鼠标清除/焦点回归已观察。IME完整交互、多DPI、读屏和动效观感待验收；完整回归中的Calendar时序失败见[Search验证记录](verification/2026-10-26-search.md)。

日期：2026-10-25。状态：M1 实现进行中。目标是先验证可持续的 WPF 实现路径，再逐组移植控件；不以生成文件数量代表完成度。

## 1. 默认决策

| 事项 | 当前基线 | 后续确认时机 |
|---|---|---|
| 项目名 | ArcWpf | 发布前检查 GitHub/NuGet 名称与品牌说明 |
| 框架 | 用户已确定 .NET 10，`net10.0-windows`，WPF；SDK 固定 10.0.402 | SDK 与桌面运行时已核对；实际构建、运行与部署待 M1 验证 |
| 平台 | Windows 桌面 | M1 记录实际验证的 Windows 版本；M4 前定义支持矩阵 |
| 兼容范围 | 不承诺 .NET Framework 4.8 | 出现明确使用方需求时单独评估 |
| API | WPF 原生习惯；视觉行为参考 Arc | 每个组件规格记录差异 |
| 首期依赖 | 优先 WPF 内置能力 | 引入第三方包时记录用途、版本、许可和替代成本 |
| 主题 | 明/暗、强调色、资源覆盖 | M1 验证普通控件与浮层一致切换 |
| 输入与缩放 | 键盘、鼠标、中文 IME，100%/150%/200% DPI | M1 形成验证记录 |
| 暂不纳入 | 移动端、Web、原版付费源码、复杂富文本/工作流画布 | 有明确任务再增补范围 |

原项目曾以 110 个免费组件为范围；这是历史参考，不能当作当前源站数量或本项目首期承诺。组件全集在来源调查后建立，记录版本与日期。

## 2. 里程碑

### M0：计划与规范（本次）

- [x] 建立项目入口、实施计划、架构/验证/来源规范。
- [x] 建立组件规格模板与六项项目 Skills。
- [x] 明确按问题类型划分的参考优先级、证据等级、来源冻结、冲突决策、回归基准及上游更新规则。首批采集进度见 M1。
- [x] 通过 Skills 格式校验及本地引用检查（2026-10-25：六项 Skill 均通过 quick_validate；13 个 Markdown 文件无失效本地链接）。

退出条件：文档互相引用有效，技能职责明确，无把规划描述为已实现的内容。

### M1：技术验证（解决方案与 Button 首版已建立）

2026-10-25：本机 `dotnet --version` 返回 10.0.402，`dotnet --list-runtimes` 确认 Microsoft.WindowsDesktop.App 10.0.13。global.json 精确固定 SDK 10.0.402，禁用自动滚动与预览版本。此配置固定构建 SDK，不锁定使用方运行时补丁。升级 SDK 时显式修改配置并重新验证；已完成 WPF 构建与 16 项 Button 程序化测试，两个示例窗口完成不显示窗口的 XAML 构造检查；实际窗口交互及支持平台矩阵仍待验证。

1. 检查 Windows、SDK、构建工具；固定验证过的 SDK。创建最小解决方案、Core、Controls、Gallery、Tests。
2. 固定来源版本，为 Button、Input、Switch、Popover 写规格。2026-10-25 已完成首批文本/源码采集，并将四份规格完善为可直接开发的v1契约，含公开API、状态/事件、模板、动效与带编号验收场景；入口为 [共同契约](components/contracts.md)。用户明确本阶段不做状态截图和实际交互观察，不阻塞规格完成或进入编码。来源文本已固定，运行证据延期；Button 首版已实现并完成程序化检查；其他组件未实现，实际界面验收延期。
3. 用最少的资源和共享代码实现这些控件，并用 Switch 的滑块验证弹簧动画；不为凑数量另造动画控件。
4. Gallery 展示明暗主题、强调色、禁用/焦点/错误/加载状态；对未适用状态明确标注。
5. 验证 Input 中文组合输入、绑定/验证；Popover 验证 Escape、外部点击、焦点归还、屏幕边缘及跨 DPI 定位。
6. 记录关键决策：Popup/覆盖层选择、依赖属性约定、资源覆盖顺序、动画清理方式。

退出条件：五类技术问题（模板、文本输入、状态、浮层、弹簧）均有可运行示例；声明的主题/DPI/键盘场景有结果；没有已知的阻断性绑定或焦点问题。未具备的测试环境须明确列出，不能标记完整验收。

### M2：基础设施与高频组件

先整理 M1 证实可复用的 Token、主题、动效和浮层基础设施，再实现 Checkbox、RadioButton、Select、Tabs、Dialog、Tooltip、Toast、基础表单布局。公开命名以各组件规格为准。

退出条件：每个交付组件都有规格、API说明、Gallery 状态示例与适当测试；共享设施不依赖 Gallery；组件卸载不遗留持续动画或事件订阅。

### M3：按依赖分组扩展

- 表单组：数值、日期、搜索和多选；先验证文化格式、验证与编辑体验。
- 导航与展示组：菜单、树、列表、分页、卡片和进度；先验证选择和虚拟化。
- 图表与手势组：纯算法、坐标/命中、拖拽、过渡分别验证。
- 高复杂度组：DataGrid、富文本、工作流画布另写技术方案与成本评估；不阻塞基础控件发布。

每组进入条件：依赖设施已稳定、来源可用、规格含可测的退出条件。一次完成一个纵向交付，再据实估算下一组，不在 M0 给出无依据的整体工期。

### M4：发布准备

确定 Windows 支持矩阵、包版本与 API 兼容策略；验证干净环境安装和最小消费应用；补齐许可/NOTICE、文档、Gallery、变更日志及包内容检查。打包、上传和发布分开记录，按用户授权执行对外发布。

## 3. 依赖与风险

| 风险 | 最早处理 | 处理方式 |
|---|---|---|
| WPF Popup 焦点、资源继承与独立窗口行为 | M1 | 同时验证主题、定位、Escape、焦点归还；据实际约束选择宿主 |
| 中文 IME、密码输入、安全绑定语义 | M1 / 对应组件 | 保留原生编辑行为；密码控件单独设计，不能盲套普通 Input |
| 动画占用属性值、布局抖动、闲置 CPU | M1 | 结束清理、集中调度、卸载释放、记录真实帧与空闲行为 |
| 视觉近似但交互不一致 | 每个组件 | 状态表、键盘表、行为测试与实际窗口验收 |
| 一次移植过多导致共享 API 反复修改 | M1/M2 | 先少量验证；按依赖批次扩展 |
| 上游版本或授权边界变化 | 抓取/复用时 | 固定来源、保留许可、记录与原版差异 |

## 4. 任务记录方式

后续在 `docs/components/` 中新增实际组件规格。每个组件记录“未开始 / 分析中 / 实现中 / 待验收 / 已验收”，并分别列出代码、行为、视觉和平台验证状态。M1 后再建立有真实条目的组件索引；不预填虚假的完成率。

## 2026-10-25 实现记录

### Button 完整性补齐（本次实施）

- [x] 四变体、三尺寸、图标文字组合、复制反馈及加载示例。
- [x] 内容变化：旧内容退场、新内容进入；Full 使用淡入淡出、位移/模糊，Reduced 仅淡化，Off 立即更新。
- [x] 内容宽度：内部槽位做弹簧近似过渡，不修改使用方 Width/绑定；首次布局、普通重排不补播。
- [x] 悬停/状态颜色：前景、背景、边框平滑变化；不修改共享画刷，切换主题可打断。
- [x] 生命周期回归（程序化过渡引擎测试）：连续内容变更、Off、卸载、模板替换取消旧动画与排队回调。
- [x] 更新规格与验证结果；实际截图、键鼠操作和性能验收依用户先前要求继续暂缓。

完成以上代码和程序化验证后仍需记录与 Web Motion 求解器的差异，不以近似动画声称像素级完整还原。

已建立 Core、Controls、Gallery、Tests、Consumer 五个项目及 ArcWpf.slnx；Button 包含四变体、三尺寸、加载阻断、命令、主题和动效等级。消费示例仅引用控件库与公共主题资源，不依赖 Gallery。依赖版本记录在各项目 packages.lock.json。

本次验证见 [Button 首版记录](verification/2026-10-25-button.md)。M1 尚未整体完成；建议下一组件为 Switch，随后 Input 与 Popover。截图、实际输入、DPI 以及显示中动画生命周期继续列为待验收。


## Switch 开发与 Button 用户确认

Button：用户测试通过（用户原话“我测试没问题”）；具体专项范围未逐项确认，详情见Button验证记录。

Switch 本次范围：两态原生ToggleButton、默认双向绑定/null强制、命令和事件、标签、禁用、Enter支持、UIA Toggle、可反向弹簧、按压伸长、减少动效、Gallery与程序化回归。实际截图和专项输入/DPI验收仍待用户后续测试。

Switch首版已建立：两态、绑定、命令/事件、UIA、默认模板与弹簧调度；Gallery/Consumer均已接入。程序化结果见Switch验证记录。下一组件为Input。Switch轨道颜色交叉淡化、滑块阴影及实际键鼠/DPI验收保留为后续视觉完善项，不将首版标为完整原版还原。

## Input 开发

Switch胶囊外形修正后已获用户测试通过。开始Input首版：原生TextBox、标签/占位/描述、显式及绑定验证错误、只读/禁用、Gallery与程序化测试。IME实际输入和专项DPI仍保留待验收；消息高度/逐词动画依原规格暂缓。

Input首版已完成：原生TextBox编辑模板、标签/占位/说明/显式错误与绑定验证、只读/禁用、自动化辅助文本、Gallery及独立Consumer接入。新增10项程序化测试，总计43项通过；实际窗口编辑/IME/DPI等待验收。消息高度与逐词/数字动画按首版规格暂缓。下一个M1控件为Popover。

## 三组件经验规范收口（2026-10-25）

- [x] 更新六个项目Skills，增加层级定位、参考卡、WPF映射、逐项动效合同和交付矩阵。
- [x] 建立统一复刻规则、参考模板、规格矩阵模板和可复用任务提示。
- [x] Button/Switch/Input参考卡及完整度矩阵回填，保留原有缺失项与用户确认边界。
- [x] 归档三张用户图片及SHA-256，不将未知截图环境猜成固定DPI。
- [x] 完善Popover参考卡与规格v2补充：文档样本、宿主风险、坐标/焦点/生命周期合同。
- [ ] Popover代码实现与相应验证；不属于本轮规范工作。
- [x] Input最新修复用户确认（2026-10-27用户明确确认，见Input验证记录）。

本轮没有修改控件代码或Gallery。新的“同源主示例+扩展区”规范优先用于下一组件；Button现有中文分组的差异已记录，未以规范优化为由重做已确认页面。

本轮检查结果：六个Skills全部通过quick_validate（UTF-8）；30个Markdown文件本地链接检查无失效项；三张用户归档图片SHA-256复核通过。上述仅为文档/格式完整性检查，不代表Skills行为效果已独立评测，也不新增控件功能或视觉验收结论。

## Popover 开发（2026-10-25）

- [x] 原生Popup宿主、公开API、定位/状态/动效、输入及生命周期处理。
- [x] Gallery文档样本与扩展编辑场景、独立Consumer。
- [x] 13项Popover程序化测试；完整解决方案共57项，详情见 [验证记录](verification/2026-10-25-popover.md)。
- [x] 静态官网HTML补充与来源哈希记录。
- [ ] 实际窗口焦点/外部输入/跨DPI/视觉验收，依用户既定要求延期。

M1四组件代码均已建立，M1整体验收尚未完成。此处原记录Popover及Input待用户确认；Input最新对齐修复已于2026-10-27获用户确认，见Input验证记录。上文“仅规范未实现”描述为此前阶段记录。

## Popover 用户确认与下一组件建议（2026-10-25）

Popover收到用户“我测试没问题”的通过反馈，已补充验证记录。专项未测范围保持原记录，未扩展为M1所有退出条件均通过。

建议下一组件为CheckBox（复选框，最终公开命名随规格确定），对应既定M2高频组件顺序。先采集Arc参考、列出未选/选中/不确定、禁用/焦点及内容布局，再编写规格并实现。可复用现有状态与动效经验，补齐表单选择能力。后续建议RadioButton，再Select。此处为下一步建议，尚未开始CheckBox实现。

## CheckBox 开发（2026-10-26）

- [x] 冻结Arc CheckBox源码、Markdown、主预览HTML/CSS及动效依赖，保存哈希和许可依据。
- [x] 完成 [参考卡](references/components/CheckBox.md) 与 [组件规格](components/CheckBox.md)。
- [x] 原生CheckBox、Description、可选三态、绑定/命令/UIA、方框及勾号/横线动效、主题。
- [x] Gallery同源主示例与扩展状态，独立Consumer接入。
- [x] CheckBox新增12项，全解决方案69项测试通过；见 [验证记录](verification/2026-10-26-checkbox.md)。
- [ ] 实际窗口与视觉用户验收。

M2已开始；M1保留专项未测记录，不因新增组件自动标记全量验收。CheckBox未包含CheckBoxGroup或树父子汇总；后续建议RadioButton。

### CheckBox 取消选中闪烁（2026-10-26）

用户报告选中后取消会闪烁。已程序化复现并修复动画清除后内部填充透明度回到1的根因；新增回归，全量70项通过。等待修复后的用户确认，不将程序化结果视为视觉验收。

## CheckBox确认与RadioButton开发（2026-10-26）

CheckBox闪烁修复后收到用户“CheckBox 没问题了”，已记录对应版本用户通过；专项边界保留。

- [x] Radio group来源、主样本、CSS与动效依赖冻结及许可/哈希记录。
- [x] [RadioButton参考卡](references/components/RadioButton.md) 与 [规格](components/RadioButton.md)。
- [x] RadioButton原生单选项及RadioGroup、共享选中框、圆点/按压/颜色动效。
- [x] Gallery官网Review mode样本与扩展，独立Consumer。
- [x] 新增14项，全量84项程序化测试通过，详见 [验证记录](verification/2026-10-26-radio.md)。
- [ ] 用户实际测试及视觉/键盘焦点/跨DPI专项。

后续建议Select；当前不直接开始下一组件。

## RadioButton确认与Select开发（2026-10-26）

RadioButton收到用户“RadioButton 没问题了”的通过反馈，已记录对应版本，专项未测边界保留。

- [x] Select源码/文档/主预览/样本选项与依赖冻结，许可和哈希留档。
- [x] [参考卡](references/components/Select.md)、[规格](components/Select.md)、原生Select/SelectItem及Popup生命周期和动效。
- [x] Gallery同源Team示例、扩展数据/禁用/长列表，独立Consumer。
- [x] 12项新增，全量96项测试通过；见 [验证记录](verification/2026-10-26-select.md)。
- [ ] 实际窗口交互、焦点、DPI及视觉验收。

滚动条、系统字体、单层阴影、自定义模板的文字过渡差异已说明。不直接开始下一组件。

## Select用户确认（2026-10-26）

Select收到用户“没问题”的通过反馈，已补充验证记录；专项覆盖未知，不据此将所有未测项标记通过。后续按M2剩余高频组件安排，具体开发由下一任务确定。

## Tabs开发（2026-10-26）

- [x] 官网免费Registry、CSS、主预览与动效参数冻结，许可与哈希记录。
- [x] [Tabs参考卡](references/components/Tabs.md)、[组件规格](components/Tabs.md)。
- [x] 原生Tabs/TabItem、共享选中框、内容/高度动画、溢出滚动、RTL和减少动效。
- [x] Gallery主样本与扩展、独立Consumer；新增14项测试，全量110项通过，见[验证记录](verification/2026-10-26-tabs.md)。
- [ ] 实际窗口视觉、焦点、跨DPI与性能验收；尚无用户通过反馈。

垂直/手动激活、forceMount、关闭与拖动重排不在此次首期范围。不自动开始下一组件。

## Tabs用户确认与导航替换（2026-10-26）

用户反馈Tabs测试通过。Gallery顶层八个组件页面的导航已替换为ArcWpf Tabs/TabItem，继续使用现有页面实例；窄窗口采用标签条内部溢出滚动。专项未测边界及本次接入记录见[Tabs验证记录](verification/2026-10-26-tabs.md)。

## Dialog开发（2026-10-26）

- [x] 官网免费Registry、预览文案、CSS与动效依赖冻结，许可/SHA-256记录。
- [x] [Dialog参考卡](references/components/Dialog.md)与[规格](components/Dialog.md)。
- [x] Dialog/DialogHost、模态背景、绑定/可取消关闭、焦点管理、进退场和标题说明动效。
- [x] Gallery同源主预览与表单/长文本/文案切换/关闭保护；Consumer独立接入。
- [x] 新增16项程序化测试，全量126项通过，见[验证记录](verification/2026-10-26-dialog.md)。
- [ ] 实际窗口交互、焦点、DPI、视觉与性能验收。

Dialog为窗口内容宿主范围内模态，不是OS级ShowDialog；堆叠/非模态/拖动调整大小不在本次范围。尚无用户通过反馈，不自动开始下一组件。

## Dialog确认与Tooltip开发（2026-10-26）

Dialog收到用户“Dialog 没问题”反馈，已记录对应版本通过，专项覆盖未知项继续保留。

- [x] Tooltip免费Registry、官网主样本、共享动效/令牌及许可/哈希冻结。
- [x] [参考卡](references/components/Tooltip.md)与[规格](components/Tooltip.md)。
- [x] Tooltip包装器、Popup定位、延迟与共享warm、焦点/鼠标触发、进退场/文案/尺寸动效、生命周期。
- [x] Gallery官网样本及四边/长文案/连续/动态提示，Consumer同步。
- [x] 新增15项，全量141项程序化测试通过，见[验证记录](verification/2026-10-26-tooltip.md)。
- [ ] 实际Popup/输入/焦点/DPI/视觉/性能验收；无用户通过反馈。

Release Gallery运行中，使用Debug构建验证，未关闭用户窗口。不自动开发下一组件。

## Tooltip确认与Toast开发（2026-10-26）

Tooltip收到用户“没问题”，记录当前版本用户测试通过；焦点、Popup、DPI和性能专项仍保留未测边界。

- [x] Toast Registry、官网主样本、动效与许可/哈希冻结。
- [x] [参考卡](references/components/Toast.md)与[规格](components/Toast.md)。
- [x] Toast/ToastHost、队列、自动/手动关闭、成功状态、主题和动效。
- [x] Gallery/Consumer同源示例；新增4项程序化测试，全量145项通过，见[验证记录](verification/2026-10-26-toast.md)。
- [ ] 实际窗口交互、焦点、读屏、DPI、视觉和性能验收。

Toast首期暂缓滑动、动作按钮和类型变体；不自动开始下一组件。

## Toast确认与基础表单布局（2026-10-26）

Toast收到用户“没问题”，记录当前版本通过。

- [x] 基础表单布局定义为ArcWpf自有FormLayout，不伪称Arc原版独立组件。
- [x] FormLayout纵向字段间距、最大宽度、折叠项处理及原生子控件保留。
- [x] 新增2项布局测试通过；完整Debug构建0警告/0错误，全量147项通过，见artifacts/tests/full-debug.trx。Gallery已加入表单示例。
- [ ] Gallery表单页面、实际窗口响应式视觉和DPI验收。

本轮不实现校验引擎、业务提交状态、多步骤向导或两列断点。


## Textarea 开发（2026-10-26）

- [x] Arc Textarea 来源、源码、动效依赖与许可快照冻结；见 [参考卡](references/components/Textarea.md)。
- [x] 原生 `Textarea : TextBox`、标签/说明/错误/占位、自动换行、垂直滚动与尺寸规则实现；见 [规格](components/Textarea.md)。
- [x] Gallery 页面与基础表单布局示例已接入。
- [ ] 专项 6 项与全量 153 项测试通过；实际窗口视觉、DPI、IME 和性能验收待记录；见 [验证记录](verification/2026-10-26-textarea.md)。

本期暂不实现字符计数滚动、消息词语动效、业务校验引擎和提交状态机。


## Alert 开发（2026-10-26）

- [x] Arc Alert 来源、源码、动效依赖与许可快照冻结；见 [参考卡](references/components/Alert.md)。
- [x] 原生 `Alert : ContentControl`、四种语气、说明/自定义内容、关闭和主题实现；见 [规格](components/Alert.md)。
- [x] Gallery、FormLayout 与 Consumer 示例已接入。
- [x] Alert 专项 7 项测试通过；全量 160 项回归通过。
- [ ] 实际窗口视觉、焦点、DPI、读屏和性能验收待记录；见 [验证记录](verification/2026-10-26-alert.md)。

本期暂不实现复杂内容高度弹簧、字词替换动效和自动关闭。


## ToastStack 开发（2026-10-26）

- [x] Arc ToastStack 来源、CSS、动效依赖与许可快照冻结；见 [参考卡](references/components/ToastStack.md)。
- [x] 原生 `ToastStack`、`ToastStackItem`、队列/更新/关闭/堆叠布局与主题实现；见 [规格](components/ToastStack.md)。
- [x] Gallery 与 Consumer 示例已接入。
- [x] ToastStack 专项 8 项测试通过；全量 168 项回归通过。
- [ ] 实际窗口视觉、焦点、DPI、右滑手势、读屏和性能验收待记录；见 [验证记录](verification/2026-10-26-toast-stack.md)。

本期暂不实现右滑拖拽投掷、复杂内容高度弹簧、全局通知服务和系统级窗口宿主。

## Drawer 开发（2026-10-26）

- [x] Arc Drawer 页面、Markdown、Registry、动效依赖与许可快照冻结；见 [参考卡](references/components/Drawer.md)。
- [x] [Drawer规格](components/Drawer.md)；原生 `Drawer` / `DrawerHost`、四边布局、模态遮罩、绑定/取消关闭、焦点保护、自动化和 Full/Reduced/Off 动效。
- [x] Gallery 同源主示例与左/上/长内容/关闭保护扩展，独立 Consumer 接入。
- [x] Drawer 专项 8 项通过，全量 176 项通过；见 [验证记录](verification/2026-10-26-drawer.md)。
- [ ] 实际窗口交互、焦点、视觉、跨 DPI、读屏、拖拽和性能验收。

本期首版暂缓标题拖拽手势、rubber-band、快速 flick 保速关闭、逐层文案替换、自适应触摸行为和独立系统窗口宿主；WPF 模态宿主范围及退出清理由程序化测试覆盖。

## Accordion 开发（2026-10-26）

- [x] Arc Accordion Registry、Markdown、页面、动效依赖与许可快照冻结；见 [参考卡](references/components/Accordion.md)。
- [x] [Accordion规格](components/Accordion.md)；原生 `Accordion` / `AccordionItem`、单开/全部收起、绑定、键盘导航、自动化和 Full/Reduced/Off 动效。
- [x] Gallery FAQ 主样本与绑定/禁用/长内容/嵌套扩展，独立 Consumer 接入。
- [x] Accordion 专项 9 项通过，全量 186 项通过；见 [验证记录](verification/2026-10-26-accordion.md)。
- [ ] 实际窗口交互、焦点、视觉、跨 DPI、读屏、触摸和性能验收。

本期暂缓 CSS 媒体查询的逐断点复制、可展开多开模式、拖拽排序和独立窗口宿主；WPF 版本保持单开契约，`ExpandedIndex=-1` 支持全部收起。


## ExpandableCard 开发（2026-10-26）

- [x] Arc ExpandableCard Registry、Markdown、页面、动效依赖与许可快照冻结；见 [参考卡](references/components/ExpandableCard.md)。
- [x] [ExpandableCard规格](components/ExpandableCard.md)；原生摘要卡、默认展开、双向绑定、宽度上限、自动化和 Full/Reduced/Off 动效。
- [x] Gallery 主样本、扩展示例和 Consumer 接入。
- [x] ExpandableCard 专项 7 项通过，全量 193 项通过；见 [验证记录](verification/2026-10-26-expandable-card.md)。

## Menu 开发（2026-10-26）

- [x] Arc Dropdown Menu Registry、Markdown、页面、动效依赖与许可快照冻结；见 [参考卡](references/components/Menu.md)。
- [x] [Menu规格](components/Menu.md)；原生 Popup 菜单、命令项、禁用/危险项、绑定和焦点返回。
- [x] Gallery 主样本、扩展示例和 Consumer 接入。
- [x] Menu 专项 5 项通过；见 [验证记录](verification/2026-10-26-menu.md)。
- [ ] 实际窗口 Popup、焦点、碰撞、DPI、读屏和性能验收。

- [x] Menu 规格按官方 Dropdown menu 页面纠偏并复跑验证；5 项专项、198 项全量通过。

## Pagination 来源与规格（2026-10-26）

- [x] 冻结 Arc Pagination 页面、Markdown、Registry、CSS、共享动效令牌和许可页；见 `tests/Baselines/References/2026-10-26-pagination`。
- [x] 完成 [Pagination 参考卡](references/components/Pagination.md) 与 [组件规格](components/Pagination.md)。
- [x] Pagination原生控件、主题、Gallery及7项专项测试已实现并通过；最终窗口鼠标/键盘与明暗、窄宽布局已观察。
- [ ] 多DPI、读屏、逐帧视觉和性能专项待验收；全量回归仍有既有失败，见[验证记录](verification/2026-10-26-pagination.md)。

来源结论：受控 1-based 页码、最多五页窗口、边界禁用、Tab/Enter/Space、共享当前页标记、窗口滑动和 Reduced motion 已固定；省略号、首尾快捷按钮、每页大小和异步加载不在首期范围。

## Breadcrumb 开发（2026-10-26）

- [x] 冻结 Arc Breadcrumb 页面、Markdown、Registry、CSS、ArcProvider、共享动效令牌和许可页；见 `tests/Baselines/References/2026-10-26-breadcrumb`。
- [x] 完成 [Breadcrumb 参考卡](references/components/Breadcrumb.md) 与 [组件规格](components/Breadcrumb.md)。
- [x] 原生 `Breadcrumb` / `BreadcrumbItem`、命令项、当前项、禁用项、Chevron 分隔符、Gallery 和程序化测试已接入。
- [ ] 新项进入动效、AutomationPeer、实际窗口视觉、焦点、DPI 和性能验收待补。

## TreeView 来源与规格（2026-10-26）

- [x] 冻结 Arc Tree view 页面、Markdown、Registry、CSS、共享动效令牌和许可页；见 `tests/Baselines/References/2026-10-26-tree-view`。
- [x] 完成 [TreeView 参考卡](references/components/TreeView.md) 与 [组件规格](components/TreeView.md)。
- [ ] TreeView 原生控件、Gallery、测试与实际窗口验收尚未开始。

来源结论：节点树、受控/非受控展开、可见行键盘导航、单选、18 DIP 层级缩进、36px 行高、标签省略号和 Reduced motion 已固定；多选、懒加载、虚拟化、拖拽和搜索不在首期范围。

## List 开发（2026-10-26）

- [x] 确认 Arc 公开目录没有独立 List 条目，按 WPF 原生 ListBox 组合建立 ArcWpf List。
- [x] 实现 List/ListItem、单选、滚动、标题/说明/图标、禁用项和键盘行为。
- [x] Gallery 与 3 项程序化测试已接入。
- [ ] 实际窗口视觉、焦点、滚动条和 DPI 验收。

List 首版属于 ArcWpf 的 WPF 原生补充控件，不将其描述为 Arc 源码移植。后续可根据使用反馈增加分组、多选、拖拽或虚拟化。

## Progress 开发（2026-10-26）

- [x] 冻结 Arc Progress Markdown、Registry 入口和许可说明。
- [x] 实现确定值、Max 夹取、标签、百分比、完成态、禁用态和 Full/Reduced/Off 视觉更新。
- [x] 接入 Gallery 与 3 项 Progress 专项测试。
- [ ] 实际窗口视觉、辅助技术和 DPI 验收。

Progress 首版只覆盖已知比例的线性任务进度；不确定进度、圆环、分段和取消操作暂缓。

## NumberInput 开发（2026-10-26）

- [x] 冻结 Arc Number field 规格、Registry 入口和许可说明。
- [x] 实现有界值、步进按钮、键盘调整、Enter/Escape、前后缀和说明文本。
- [x] 接入 Gallery 与 3 项 NumberInput 专项测试。
- [ ] 实际窗口输入、焦点、中文环境、主题和 DPI 验收。

NumberInput 首版暂缓拖拽标签、极限回弹、数字滚轮动画和 limitHint 文案。

## DatePicker 开发（2026-10-26）

2026-10-26 用户追加：创建独立 Calendar，让 DatePicker 复用 Calendar 样式。此前弹层内部仍是系统默认外观，不能视为 Calendar 视觉完成。来源、范围见 [Calendar规格](components/Calendar.md)；本次补齐日历模板与状态，spring范围沿用暂缓约定。

- [x] Calendar来源冻结、原生控件与共享样式、Gallery和专项测试。
- [x] 用户追加Calendar选中动画：共享圆盘移动/进入/清除；替代此前选中动画暂缓项，月份翻页仍单独暂缓。渲染帧回归通过，实际观感待验收。
- [x] DatePicker复用Calendar样式，Today头部、Clear底栏及286 DIP内部实际宽度修复。
- [ ] Calendar/DatePicker真实鼠标键盘、IME、跨屏DPI和辅助技术专项验收；见[验证记录](verification/2026-10-26-calendar.md)。

- [x] 冻结 Arc Date picker 页面、Markdown、Registry JSON 与许可/快照哈希；见 [参考卡](references/components/DatePicker.md)。
- [x] 完成 [DatePicker 规格](components/DatePicker.md)，明确 WPF 原生日期、文化格式、绑定、验证、键盘和 BlackoutDates 契约。
- [x] 实现 Arc 风格原生 `DatePicker`、标签/说明/占位符、Today/Clear、Popup/Calendar 主题模板，接入 Gallery 与专项测试。
- [ ] 实际窗口的 Popup、焦点、中文输入、视觉、DPI、读屏和性能验收。

首期暂缓日期分段滚动、spring 动效、JS 函数型 disabledDates 与响应式 CSS 精确复制；`ShowToday` 和 `Clear` 已映射为 WPF 模板按钮并更新 `SelectedDate`。

## MultiSelect 开发（2026-10-26）

按用户指令开始 MultiSelect 端到端实现；来源 Multi-select 已冻结。搜索和逐标签删除为 WPF 扩展；[规格](components/MultiSelect.md)。状态实现中，未提交/推送。


MultiSelect 本次实现已建立：多选绑定、标签与移除、+N、搜索过滤、原生列表/Popup键盘焦点、逐项动效与Gallery。用户反馈标签遮挡后已修正；10项专项通过，实际效果待用户复验。按用户最新要求停止 computer-use，后续窗口由用户人工测试。关联回归存在Select动效测试失败，不宣称全量通过。详见[验证记录](verification/2026-10-26-multiselect.md)。


### MultiSelect 用户确认（2026-10-26）

标签遮挡修正后收到用户“测试没问题”，记录当前版本人工测试通过；专项未测保留。此前Select回归失败已于2026-10-27复跑通过并获用户确认关闭，见Select验证记录。此时下一项建议为ComboBox（可搜索单选下拉），复用Search、Select和Popup经验；当时尚未开始实现，不因建议自动扩展本次范围。

## ComboBox 开发（2026-10-26）

用户授权开始可搜索单选下拉ComboBox；测试仅限ComboBox专项，不运行其他控件/全量回归，不使用computer-use。来源已冻结，[规格](components/ComboBox.md)。实现中，实际窗口由用户人工验收。

ComboBox本次实现完成：输入/关键词过滤、SelectedValue双向绑定、清除、禁用/只读、验证、原生编辑与UIA、Popup及逐项动效、Gallery主样本和扩展样本。最终ComboBox专项9/9通过；仅编译测试工程的依赖，未执行其他控件测试或全量回归。实际IME、鼠标滚动/条目命中、多DPI和动态观感待用户人工测试，未使用computer-use。见[验证记录](verification/2026-10-26-combobox.md)；不将程序化通过标为完整视觉验收，未提交/推送。

## Slider 开发（2026-10-26）

用户授权开始Slider，范围为水平单滑块，含步长、范围、绑定、键盘、轨道点击/拖动、标签/读数、禁用及Arc动效；只运行Slider专项，不使用computer-use。用户随后明确“我来人工测试”，实际窗口由用户验收；代理做程序化布局/时钟及离屏检查。来源已冻结，详见[规格](components/Slider.md)。双滑块范围选择与垂直变体不属于本次范围。实现中。

Slider本次实现完成：水平单滑块、原生范围/命令/UIA、步长与绑定、WPF捕获手势、回弹/动量/边界反馈、数字轮与数位进退、气泡、标记/装饰、明暗主题与Gallery。最终Slider专项11/11通过，随后仅复跑2项离屏输出检查及1项气泡导出检查通过；未执行其他控件测试或全量回归。用户明确“我来人工测试”，未使用computer-use；真实拖动/键盘观感、多DPI和读屏待人工，不标记已视觉验收。见[验证记录](verification/2026-10-26-slider.md)，未提交/推送。

## PasswordInput 开发（2026-10-27）

按用户授权开始PasswordInput，范围为密码遮蔽/显示、原生输入与显式取值、标签/说明/错误/禁用、眼睛图标及消息动效、Gallery。只运行PasswordInput专项，不做computer-use，实际界面由用户人工测试。WPF PasswordBox为sealed，采用Control组合PasswordBox与显式显示编辑器，不增加默认双向明文DP；[规格](components/PasswordInput.md)。实现中，密码强度不在范围。

PasswordInput本次实现完成：原生遮蔽/显示编辑、显式密码与SecureString副本、生命周期明文清理、验证与自动化、眼睛/resolve/消息动效、Gallery。最终专项11/11通过，只执行PasswordInput测试；明暗离屏图已检查，实际IME、Tab/Space、鼠标交互、多DPI与读屏由用户人工验收，未使用computer-use。见[验证记录](verification/2026-10-27-password-input.md)。未提交/推送。

## OTPInput 开发（2026-10-27）

用户确认PasswordInput完成后授权开始OTPInput。数字验证码，默认六位、可配置长度、粘贴、导航、完成事件、状态动效及Gallery；只运行OTPInput专项，不用computer-use，实际界面用户人工验收。见[规格](components/OTPInput.md)。实现中。

OTPInput本次实现完成：默认六位/1..12长度、数字过滤与粘贴、原生编辑器/导航、双向Value与用户完成事件、禁用/只读、填充与错误状态、ring/glyph/消息动效及Gallery。最终OTPInput专项8/8通过，明暗离屏已检查；未运行其他控件测试，不使用computer-use，真实界面待用户人工验收。见[验证记录](verification/2026-10-27-otp-input.md)。未提交/推送。

2026-10-27：用户确认OTPInput“我这边测试没问题”，记录本次交付版本人工测试通过；未明确覆盖的专项边界保留，见[验证记录](verification/2026-10-27-otp-input.md)。
