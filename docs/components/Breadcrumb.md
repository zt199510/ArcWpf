# Breadcrumb 组件规格

## 身份与状态

- 组件名：`Breadcrumb`
- 用途：展示当前页面在层级路径中的位置。
- 所属里程碑：M3 导航与展示组。
- 状态：**实现中，待实际窗口验收**。
- 来源：2026-10-26 项目采集批次，见 [参考卡](../references/components/Breadcrumb.md) 和 [冻结清单](../../tests/Baselines/References/2026-10-26-breadcrumb/manifest.json)。
- 许可：来源页面标记 Free；许可页说明 Free 组件为 MIT。仅作行为/视觉参考，不直接复制上游代码或依赖。

## 范围与依据

本次必做：有序层级项、最后项当前页、可选命令项、禁用项、Chevron 分隔符、导航名称、Tab/Enter、窄容器换行、明暗主题和 Reduced/Off 适配。

明确暂缓：自动 Router/URI 导航、路径折叠、省略号、下拉历史、拖拽重排、触摸手势、RTL 专用分隔符和异步路由加载。

## 状态与交互

| 状态/触发 | 外观 | 操作结果 | 焦点/键盘 | 数据与事件 |
|---|---|---|---|---|
| 普通非当前项 | TextSecondary；有 Command 时为可点击文本，悬停转 AccentStrong 并显示下划线 | 执行 Command，携带 CommandParameter | Tab 可聚焦；Enter 激活 | 触发 `ItemInvoked` |
| 当前项 | AccentStrong、Medium；不显示按钮和链接装饰 | 不可激活 | 不进入可操作 Tab 顺序 | `IsCurrent=true`，Automation 标记当前页 |
| 禁用项 | 降低不透明度，仍保留路径位置 | 不执行 Command | 不可激活 | `IsEnabled=false` |
| 新增项 | 从左侧 8 DIP、轻 blur、透明进入 | 路径更新 | 新项按顺序进入 Tab | Items 集合变化 |
| 窄容器 | 行换行；每个 crumb 保持不换行 | 路径仍完整显示 | Tab 顺序保持集合顺序 | `WrapPanel` 重新布局 |
| Reduced/Off | 直接显示最终位置，不做空间/blur 过渡 | 状态和事件不变 | 键盘行为不变 | 停止旧动画时钟 |

第一个项没有分隔符，后续项在独立布局单元中显示向右 Chevron；图标垂直居中，不使用负 Margin 定位。

## 参考依据与差异

来源冻结状态：**已固定**。文件、SHA-256 和获取日期见冻结清单。

| 结论或参数 | 问题类型/权威来源 | 精确引用 | 证据等级 | 验证状态 |
|---|---|---|---|---|
| items 是从根到当前页的路径，最后项不是链接 | API/无障碍 | Markdown API、Accessibility；TSX `current` | 文档确认 + 源码确认 | 已固定 |
| 非当前项支持链接或按钮 | API/交互 | Markdown API；TSX `item.href`/`item.onClick` | 文档确认 + 源码确认 | WPF 用 Command |
| Chevron 分隔符 aria-hidden | 视觉/无障碍 | Markdown Accessibility；TSX `NavArrowRight` | 文档确认 + 源码确认 | WPF 独立 Path，不参与命中 |
| 路径换行，单项不换行，不截断 | 响应式 | Markdown Responsive；CSS `flex-wrap`/`white-space` | 文档确认 + 源码确认 | WPF 已映射 |
| 新项 8px + blur 进入，兄弟 smooth spring | 动效 | Markdown Motion；TSX `initial`/`layout` | 文档确认 + 源码确认 | WPF 进入动效待补齐 |

### 冲突或有意差异

| 冲突或有意差异 | 备选方案 | 采用方案与理由 | 影响范围 | 验证方式与状态 |
|---|---|---|---|---|
| React `href`/`onClick` | URI、Hyperlink、ICommand | 采用 `ICommand`，保留 WPF 绑定和命令 CanExecute 语义 | API/输入 | 已实现并有程序化测试 |
| React nav/ol/aria-current | 仅使用 AutomationProperties | 使用自定义控件语义和 AutomationPeer；不伪造 HTML 属性 | 无障碍 | Automation 测试待补充 |
| Arc 只声明 Enter 激活 | 增加 Space | 遵循 WPF Button 的 Enter/Space 原生激活习惯 | 键盘 | 当前由 Button 承载 |
| Arc Link 可注入 Router | WPF 不引入路由依赖 | Command 由消费方连接导航服务 | 依赖 | 设计选择 |

## WPF 公开契约

控件继承 `Control`，因为它组合一组可变路径项，不把层级路径误用为 Selector 选中模型。

`BreadcrumbItem`：

- `Label`：显示文本，默认空字符串。
- `Command`：可选 `ICommand`；无命令的非当前项仍保留文本位置但不可执行。
- `CommandParameter`：传给命令。
- `IsEnabled`：默认 `true`。
- `IsCurrent`：由 `Breadcrumb` 根据最后项维护，只读语义。

`Breadcrumb`：

- `Items`：`ObservableCollection<BreadcrumbItem>`，默认空集合。
- `Label`：默认 `"Breadcrumb"`，用于 Automation 名称。
- `ItemInvoked`：携带 `BreadcrumbItem` 的路由事件。

必要模板部件：`PART_ItemsHost`。缺少该部件时控件仍应保持集合和命令数据可用，不抛模板异常。

## 视觉与动效

- 使用 `TextMuted`、`TextSecondary`、`AccentStrong`、`BorderStrong` 和 `FontSize.Small`；不写死黑色边框。
- Chevron 使用独立布局单元，宽度约 8 DIP、行高约 14 DIP、笔画 1.5 DIP，垂直居中。
- 非当前项使用无边框透明按钮；当前项使用 TextBlock，不显示 Focus Adorner。
- Full：新增项透明/blur/左移 8 DIP 进入；兄弟位置平滑移动。
- Reduced：直接到最终状态；Off：立即更新并停止时钟。
- 换模板、卸载和集合快速变更必须解除事件、停止动画和旧完成回调。

## 验收场景

1. 创建 `Workspace / Settings / Billing`，确认前三项顺序、两个 Chevron、Billing 为当前文本。
2. 点击 Workspace，确认命令执行并触发 `ItemInvoked`；Billing 不生成可点击按钮。
3. 将中间项禁用，确认仍显示在路径中但不可执行、不可进入激活状态。
4. 使用 Tab、Enter、Space 操作可用 crumb，确认顺序、命令和焦点视觉无黑色默认 Focus Adorner。
5. 在窄容器中放入长路径，确认行换行、单项不拆行、不截断。
6. 切换明暗主题、Reduced/Off 和 100%/150%/200% DPI，确认 Chevron、文字基线和路径间距稳定。
7. 快速增删路径、替换模板、卸载控件，确认没有旧动画回调、事件订阅或 Dispatcher 残留。

## 实施与证据

- 控件：`src/ArcWpf.Controls/Breadcrumb/Breadcrumb.cs`
- 主题：`src/ArcWpf.Controls/Breadcrumb/Themes/BreadcrumbTheme.xaml`
- Gallery：`samples/ArcWpf.Gallery/Pages/BreadcrumbPage.xaml(.cs)`
- 测试：`tests/ArcWpf.Tests/BreadcrumbTests.cs`
- 当前构建和 Breadcrumb 专项测试已运行；实际窗口视觉、Automation、DPI 和动效观感待验收。

## 完整度矩阵

| 交付项 | 本次范围 | 实现状态 | 验证证据 | 剩余差异/下一步 |
|---|---|---|---|---|
| API与绑定 | 必做 | 已实现 | BreadcrumbTests | AutomationPeer、Command 参数扩展待补 |
| 控件外形与内部对齐 | 必做 | 已实现 | 模板与构建 | 实际窗口测量待验收 |
| 内容组合与状态 | 必做 | 已实现 | 4 项专项测试 | 长路径窗口验收待补 |
| 动效与打断清理 | 必做 | 部分实现 | 当前仅生命周期 Stop | 新项 blur/位移动效待补齐 |
| 输入/自动化/主题 | 必做 | 部分实现 | WPF Button 键盘继承 | 自定义 AutomationPeer 待补 |
| 原版主示例与扩展示例 | 必做 | 已实现 | Gallery 构建通过 | 实际窗口验收待进行 |
