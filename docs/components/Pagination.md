# Pagination 组件规格

## 身份与状态

- 组件名：`Pagination`
- 用途：在长列表或分页结果中移动到一个 1-based 页码。
- 所属里程碑：M3 导航与展示组。
- 状态：**已实现，专项验收部分完成**。
- 来源：2026-10-26 项目采集批次，见 [参考卡](../references/components/Pagination.md) 和 [冻结清单](../../tests/Baselines/References/2026-10-26-pagination/manifest.json)。
- 许可：来源页面标记 Free；许可页说明 Free 组件为 MIT。仅作行为/视觉参考，不直接复制上游代码或依赖。

## 范围与依据

本次必做：受控页码状态、页数夹取、最多五个连续页码、上一页/下一页、边界禁用、页码变更事件、当前页共享标记、键盘访问、Automation 名称、明暗主题和 Full/Reduced/Off 动效。

明确暂缓：省略号跳页、首尾页快捷按钮、每页大小选择、总条数文案、异步加载状态、虚拟化、触摸拖动、RTL 专用布局和服务器请求管理。它们不是当前 Arc Pagination 来源合同的一部分。

依据分层：Arc 页面/Markdown 确认用途、API、键盘、无障碍、响应式和性能；固定 Registry TSX/CSS 确认窗口算法、几何、状态和动效；WPF 键盘路由、依赖属性、AutomationPeer 与模板生命周期按 WPF 原生机制实现。

## 状态与交互

| 状态/触发 | 外观 | 操作结果 | 焦点/键盘 | 数据与事件 |
|---|---|---|---|---|
| `PageCount <= 0` | 保留带 `Label` 的空导航区域，不渲染按钮 | 不可翻页 | 无可聚焦页按钮 | `Page=0`，保留WPF基值恢复语义 |
| 普通页 | 上一页、最多五个连续页码、下一页 | 点击页码请求目标页 | Tab 依次经过上一页、页码、下一页；Enter/Space 激活 | 触发 `PageChanged`，携带目标页 |
| 当前页 | 当前页文字为前景色；共享表面标记覆盖页码按钮 | 再次激活不重复变更或执行命令 | Automation ItemStatus标为Current page | `Page` 为 1-based 且夹在 `[1, PageCount]` |
| 第一页 | 上一页 disabled、降低不透明度 | 点击无效 | 不进入可激活状态 | 保留原生 `IsEnabled=false` 语义 |
| 最后一页 | 下一页 disabled、降低不透明度 | 点击无效 | 不进入可激活状态 | 保留原生 `IsEnabled=false` 语义 |
| 页码窗口移动 | 页码按窗口槽位差滑动 | `Page` 更新并重新计算窗口起点 | 保留仍可见的页按钮焦点；原焦点页退出时转到新当前页；箭头焦点保留 | 共享标记跟随新当前页 |
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
| Tab、Enter、Space | 键盘 | Markdown Keyboard | 文档确认 | WPF行为已测试 |
| 当前页共享 mark spring，页码窗口按槽位滑动 | 动效 | Markdown Motion；TSX `markX/markY`、`slide` | 文档确认 + 源码确认 | WPF近似已实现 |
| 窄容器允许换行，调整尺寸重定位 mark | 响应式/性能 | Markdown Responsive/Performance；CSS `.nav` | 文档确认 + 源码确认 | WPF布局已测量 |

### 冲突或有意差异

| 冲突或有意差异 | 备选方案 | 采用方案与理由 | 影响范围 | 验证方式与状态 |
|---|---|---|---|---|
| React `onPageChange` 无 WPF 等价函数属性 | `ICommand`、路由事件或两者 | DP `Page` + `PageChanged` 事件，并可选 `Command`；保留 WPF 绑定习惯 | 公共 API | 已实现，见下方契约 |
| `aria-current` 与 nav landmark 不直接存在于 WPF | 仅设置 AutomationProperties 或自定义 AutomationPeer | 自定义 Peer 暴露分页器名称、页码按钮名称和当前页 | 无障碍 | 已实现并测试Invoke/当前页 |
| 原版仅声明 Tab/Enter/Space | 增加左右方向键 | 首期遵循来源声明，不增加未要求的方向键语义 | 键盘 | 暂不扩展 |
| CSS `ResizeObserver` | 固定布局或 SizeChanged | 使用 `SizeChanged` 更新共享标记位置，适配 WPF 布局生命周期 | 动效/响应式 | 已实现，尺寸变化直接对齐 |

## WPF 公开契约

继承 `Control`，因为控件需要组合多个页码按钮并控制共享标记，不适合把单一 `Selector` 的选中容器语义直接暴露给使用方。

| API | 类型/默认值 | 规则 |
|---|---|---|
| `Page` | `int`，默认 `1` | 1-based；`PageCount<=0` 时内部为空态；有效范围夹到 `[1, PageCount]`；默认双向绑定 |
| `PageCount` | `int`，默认 `0` | 小于0按0处理；变化时重新夹取 `Page` 和窗口 |
| `Label` | `string`，默认 `"Pagination"` | Automation/导航名称 |
| `Command` | `ICommand?`，默认null | 可选；目标页作为 `CommandParameter`，不能绕过 `Page` 夹取 |
| `PageChanged` | `RoutedEvent` | PaginationPageChangedEventArgs携带OldValue/NewValue/Origin；程序化变化和夹取也触发 |
| `IsInteractive` | 设计阶段暂不公开 | 不为异步加载引入未有来源依据的状态属性 |

模板部件：`PART_PagesHost`为按钮布局Panel；`PART_Mark`为可选Border；`PART_ExitLayer`为可选Canvas；`PART_Surface`仅为布局命名。箭头和页码由原生Button派生的内部类型生成，主题样式键为ArcWpf.Pagination.Button。缺少动画部件仍可操作；缺少宿主仍支持公开DP与事件，但无按钮。PaginationPanel提供4 DIP间距与无尾间距换行。

Automation：根控件提供名称；每个页码按钮提供 `Page N` 名称；当前页通过ItemStatus暴露Current page，保留Button原生Invoke；边界按钮为 disabled。实现不得依赖浏览器 ARIA。

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

## 完整度矩阵

| 交付项 | 本次范围 | 实现状态 | 验证证据 | 剩余差异/下一步 |
|---|---|---|---|---|
| API与绑定 | 必做 | 已实现 | PaginationTests绑定/命令/夹取 | 无已知阻断 |
| 控件外形与内部对齐 | 必做 | 已实现 | 36 DIP按钮、276 DIP行、116 DIP换行测量；实际窗口 | 多DPI待验收 |
| 内容组合与状态 | 必做 | 已实现 | 五页窗口、边界、空态、整数极值测试 | 实际动态页数专项待验收 |
| 动效与打断清理 | 必做 | 已实现 | 真实时钟打断/退场/终态/关闭清理测试 | 与浏览器逐帧对照、性能待验收 |
| 输入/自动化/主题 | 必做 | 已实现 | Invoke/当前页/合成Enter；实际鼠标Enter空格Shift+Tab和暗色 | 读屏/完整Tab顺序待验收 |
| 原版主示例与扩展示例 | 必做 | 已实现 | Gallery默认1/12、窄宽/禁用/动态页数 | 已打开最终构建观察 |

## 实施与证据

实现位于src/ArcWpf.Controls/Pagination；Gallery为PaginationPage；专项为PaginationTests。最终7项专项通过，实际窗口与全量回归限制见[验证记录](../verification/2026-10-26-pagination.md)。


## 本次实现契约（2026-10-26）

PageCount夹至非负；零页时Page=0，有页时夹至[1,PageCount]，保留WPF CoerceValue基值恢复语义。重复选择当前页不重复发事件。PageChanged使用旧值/新值并提供Origin（Programmatic/PageCount/PageButton/Previous/Next）；程序化变化和夹取也触发，只有用户请求执行Command，CanExecute=false阻止该请求。Command参数为目标页。Page默认双向绑定，内部SetCurrentValue保留绑定。Tab/Enter/Space沿用原生Button，不增加方向键约定。

动效清单：当前页mark位置morph .42s；保留页码位置与新页进入smooth .4s；进入opacity .24s enter；退出opacity .12s standard及位移smooth .4s；按钮背景/前景和箭头禁用opacity .16s standard；箭头按压scale .96/.1s，释放采用冻结CSS .58s的线性spring采样。均仅动画内部属性，Reduced/Off直接终态。释放曲线补充来源为同日Dialog批次foundation CSS（参考卡附哈希）；按压使用项目standard补间近似CSS ease-out；其余见冻结TSX/CSS与motion.json。快速反向从有效位置继续，近似不保速度。换行采用实际二维位置，不强制水平槽位穿越另一行；调整尺寸直接重新对齐。


### 动效实现与证据索引

| 触发/效果 | 来源 | 实现 | 验证与差异 |
|---|---|---|---|
| 当前页mark移动 | TSX spring.morph .42 | Pagination.PlaceMark | 真实时钟、反向、对齐、清理通过；采样近似不保速 |
| 保留页移动/新页进入 | TSX spring.smooth .4，opacity .24 enter | PaginationButton.MoveFrom/Enter | 五页窗口与连续切换终态通过 |
| 旧页退出 | TSX x smooth .4，opacity .12 standard | Pagination.PageExit | 独立无命中视觉副本，退出层清空测试通过 |
| 悬停/当前页颜色 | CSS background/color .16 standard | PaginationButton.UpdateAppearance | 私有画刷复制，当前页终态颜色及主题测试通过；悬停观感待专项 |
| 禁用淡化 | CSS step opacity .16 | PaginationButton内部Surface | 原生IsEnabled与边界测试；数字也采用淡化属WPF统一处理 |
| 箭头按压/释放 | CSS .96/.1 ease-out，释放.58 spring | PaginationButton内部ScaleTransform | 实现；实际按住过程未单独验收，按压曲线采用standard近似 |

Reduced/Off不保留空间/颜色过渡；隐藏、卸载、模板替换立即停止并清理。截图不能代替每项动态过程验收。


