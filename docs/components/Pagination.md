# Pagination 组件规格

## 身份与状态

- 组件名：`Pagination`
- 用途：在长列表或分页结果中移动到一个 1-based 页码。
- 所属里程碑：M3 导航与展示组。
- 状态：**分析完成，待实现**。
- 来源：2026-10-26 项目采集批次，见 [参考卡](../references/components/Pagination.md) 和 [冻结清单](../../tests/Baselines/References/2026-10-26-pagination/manifest.json)。
- 许可：来源页面标记 Free；许可页说明 Free 组件为 MIT。仅作行为/视觉参考，不直接复制上游代码或依赖。

## 范围与依据

本次必做：受控页码状态、页数夹取、最多五个连续页码、上一页/下一页、边界禁用、页码变更事件、当前页共享标记、键盘访问、Automation 名称、明暗主题和 Full/Reduced/Off 动效。

明确暂缓：省略号跳页、首尾页快捷按钮、每页大小选择、总条数文案、异步加载状态、虚拟化、触摸拖动、RTL 专用布局和服务器请求管理。它们不是当前 Arc Pagination 来源合同的一部分。

依据分层：Arc 页面/Markdown 确认用途、API、键盘、无障碍、响应式和性能；固定 Registry TSX/CSS 确认窗口算法、几何、状态和动效；WPF 键盘路由、依赖属性、AutomationPeer 与模板生命周期按 WPF 原生机制实现。

## 状态与交互

| 状态/触发 | 外观 | 操作结果 | 焦点/键盘 | 数据与事件 |
|---|---|---|---|---|
| `PageCount <= 0` | 保留带 `Label` 的空导航区域，不渲染按钮 | 不可翻页 | 无可聚焦页按钮 | `Page` 夹到0或保持设计的空态，需在 API 中固定 |
| 普通页 | 上一页、最多五个连续页码、下一页 | 点击页码请求目标页 | Tab 依次经过上一页、页码、下一页；Enter/Space 激活 | 触发 `PageChanged`，携带目标页 |
| 当前页 | 当前页文字为前景色；共享表面标记覆盖页码按钮 | 再次激活仍请求当前页或由实现抑制重复事件，需在实现前固定 | Automation 标为当前页 | `Page` 为 1-based 且夹在 `[1, PageCount]` |
| 第一页 | 上一页 disabled、降低不透明度 | 点击无效 | 不进入可激活状态 | 保留原生 `IsEnabled=false` 语义 |
| 最后一页 | 下一页 disabled、降低不透明度 | 点击无效 | 不进入可激活状态 | 保留原生 `IsEnabled=false` 语义 |
| 页码窗口移动 | 页码按窗口槽位差滑动 | `Page` 更新并重新计算窗口起点 | 当前焦点保留在新当前页，需实现核对 | 共享标记跟随新当前页 |
| 容器过窄 | 行允许换行，按钮尺寸不变 | 仍可操作全部可见按钮 | Tab 顺序保持视觉顺序 | 监听尺寸变化重算标记位置 |
| Reduced/Off | 无空间滑动；标记直接到位 | 状态和事件不变 | 键盘行为不变 | 动效调度器停止旧时钟 |

窗口起点定义：`start = clamp(currentPage - 2, 1, max(1, PageCount - 4))`；可见页码为 `start .. min(PageCount, start + 4)`。`PageCount` 取非负整数，`Page` 取整数后夹取；具体 DP 变更回调不得破坏绑定。

## 参考依据与差异

来源冻结状态：**已固定**。来源文件、SHA-256 和获取日期见冻结清单。

| 结论或参数 | 问题类型/权威来源 | 精确引用 | 证据等级 | 验证状态 |
|---|---|---|---|---|
| 组件是受控分页器，页码 1-based 且夹取 | 用途/API | Markdown API、Registry `currentPage` | 文档确认 + 源码确认 | 已固定 |
| 最多显示五个页码，窗口随当前页移动 | 布局/行为 | Markdown Responsive；TSX `visible`/`start` | 文档确认 + 源码确认 | 已固定 |
| 上一页/下一页在边界 disabled | 交互/无障碍 | Markdown Accessibility；TSX `disabled` | 文档确认 + 源码确认 | 已固定 |
| Tab、Enter、Space | 键盘 | Markdown Keyboard | 文档确认 | WPF 行为待实现 |
| 当前页共享 mark spring，页码窗口按槽位滑动 | 动效 | Markdown Motion；TSX `markX/markY`、`slide` | 文档确认 + 源码确认 | WPF 等效待实现 |
| 窄容器允许换行，调整尺寸重定位 mark | 响应式/性能 | Markdown Responsive/Performance；CSS `.nav` | 文档确认 + 源码确认 | WPF 布局待实现 |

### 冲突或有意差异

| 冲突或有意差异 | 备选方案 | 采用方案与理由 | 影响范围 | 验证方式与状态 |
|---|---|---|---|---|
| React `onPageChange` 无 WPF 等价函数属性 | `ICommand`、路由事件或两者 | DP `Page` + `PageChanged` 事件，并可选 `Command`；保留 WPF 绑定习惯 | 公共 API | 设计选择，待实现 |
| `aria-current` 与 nav landmark 不直接存在于 WPF | 仅设置 AutomationProperties 或自定义 AutomationPeer | 自定义 Peer 暴露分页器名称、页码按钮名称和当前页 | 无障碍 | 设计选择，待 Automation 测试 |
| 原版仅声明 Tab/Enter/Space | 增加左右方向键 | 首期遵循来源声明，不增加未要求的方向键语义 | 键盘 | 暂不扩展 |
| CSS `ResizeObserver` | 固定布局或 SizeChanged | 使用 `SizeChanged` 更新共享标记位置，适配 WPF 布局生命周期 | 动效/响应式 | WPF 适配，待实现 |

## WPF 公开契约（设计稿）

建议继承 `Control`，因为控件需要组合多个页码按钮并控制共享标记，不适合把单一 `Selector` 的选中容器语义直接暴露给使用方。

| API | 类型/默认值 | 规则 |
|---|---|---|
| `Page` | `int`，默认 `1` | 1-based；`PageCount<=0` 时内部为空态；有效范围夹到 `[1, PageCount]`；建议双向默认绑定 |
| `PageCount` | `int`，默认 `0` | 小于0按0处理；变化时重新夹取 `Page` 和窗口 |
| `Label` | `string`，默认 `"Pagination"` | Automation/导航名称 |
| `Command` | `ICommand?`，默认null | 可选；目标页作为 `CommandParameter`，不能绕过 `Page` 夹取 |
| `PageChanged` | `RoutedEvent` | 携带旧页、新页和触发来源；程序化夹取是否触发需实现固定 |
| `IsInteractive` | 设计阶段暂不公开 | 不为异步加载引入未有来源依据的状态属性 |

必要模板部件：`PART_PreviousButton`、`PART_NextButton`、`PART_PagesHost`、`PART_Mark`、`PART_Surface`（可选）。缺少可选动画部件不得阻断页码和绑定行为。

Automation：根控件提供名称；每个页码按钮提供 `Page N` 名称；当前页暴露选中/当前状态；边界按钮为 disabled。实现不得依赖浏览器 ARIA。

## 视觉与动效

- 使用 `ArcWpf.Brush.Surface`、`Foreground`、`TextSecondary`、`Border`、`SurfaceMuted` 和 `Shadow.Resting`；不得写死黑色边框。
- 页码和箭头按钮高度使用 `ArcWpf.ControlHeight.Small`；内容居中；数字使用等宽数字特性或 WPF 等效字体设置。
- 当前 mark 为共享 Border，圆角使用 `ArcWpf.Radius.Control`，1 DIP 边框和 resting shadow；当前页按钮本体不重复绘制强边框。
- Full：mark 以 morph spring 跟随，页码窗口按槽位差平移并淡出/进入，箭头按下缩放约 `.96`。
- Reduced：页码和 mark 直接交换位置；保留状态变化，不保留空间滑动。
- Off：立即更新，停止所有内部时钟和尺寸监听带来的动画回调。
- 模板替换、卸载、`PageCount` 归零和快速反向必须清理旧动画，不能在新页状态上执行旧完成回调。

## 验收场景

1. 创建 `Page=1, PageCount=12`，确认显示上一页、页码1-5、下一页；页码1为当前页。
2. 点击页码3，确认 `PageChanged`、绑定值和当前 mark 都移动到3。
3. 点击下一页直到12，确认窗口从1-5移动到8-12，上一页/下一页边界禁用正确。
4. 设置 `PageCount=0`、负数和 `Page` 超界值，确认不抛异常、值按规则夹取、空态没有可操作按钮。
5. 使用 Tab、Enter、Space 完成一次上一页、页码和下一页操作，确认顺序和命令参数。
6. 在 100%、150%、200% DPI 和窄容器下测量按钮高度、间距、标记对齐和换行；记录实际窗口结果。
7. 切换明暗主题与 Reduced/Off，确认语义颜色、无黑色 Focus Adorner、动画终态和动态资源覆盖。
8. 快速连续点击页码、替换模板、卸载控件，确认没有旧动画回调、Dispatcher/CompositionTarget 订阅或焦点残留。

## 完整度矩阵（开发前）

| 交付项 | 本次范围 | 实现状态 | 验证证据 | 剩余差异/下一步 |
|---|---|---|---|---|
| API与绑定 | 必做 | 尚未实现 | 来源 API 已固定 | 实现 DP、事件、命令和夹取 |
| 控件外形与内部对齐 | 必做 | 尚未实现 | Registry CSS 已固定 | WPF 模板与共享 mark |
| 内容组合与状态 | 必做 | 尚未实现 | TSX 窗口算法已固定 | 边界、空态、窄容器 |
| 动效与打断清理 | 必做 | 尚未实现 | Motion tokens/TSX 已固定 | OwnedMotion 与生命周期 |
| 输入/自动化/主题 | 必做 | 尚未实现 | Markdown 键盘/无障碍 | WPF AutomationPeer 与主题 |
| 原版主示例与扩展示例 | 必做 | 尚未实现 | 页面 Usage 已固定 | Gallery 主示例 + 状态扩展 |

## 实施与证据

当前只完成来源采集、参考卡和规格；代码、Gallery、测试和实际窗口证据尚未建立。实现后更新本节，不以来源采集或程序化测试替代窗口视觉验收。
