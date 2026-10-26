# ExpandableCard 规格

状态：代码及程序化验证完成，待实际窗口验收。见[验证记录](../verification/2026-10-26-expandable-card.md)与[参考卡](../references/components/ExpandableCard.md)。

`ExpandableCard : ContentControl` 是单个可展开的摘要卡，标题必填，描述可选，正文通过 `Content` 承载。

## WPF公开契约

- `Title` / `Description`：标题和摘要文本。
- `DefaultExpanded`：首次加载状态，默认 `false`。
- `IsExpanded`：双向绑定的展开状态。
- `CollapsedWidth` / `ExpandedWidth`：收起和展开宽度上限，未指定时使用可用宽度。
- `Expanded` / `Collapsed`：状态变化路由事件。
- 标准模板部件：`PART_Surface`、`PART_Trigger`、`PART_Panel`、`PART_PanelInner`、`PART_Arrow`。

## 状态与交互

标题区域使用原生 `ToggleButton`，支持 Tab、Enter、Space 和 Escape。Escape 收起并把焦点返回标题；正文关闭后保留挂载但隐藏并禁用命中测试。自动化提供 `IExpandCollapseProvider`。

## 视觉与动效

卡片使用 Surface、Border、圆角和 resting shadow；标题区最小高度64 DIP，右侧 Chevron 使用自绘折线路径。Full 使用宽度和面板高度的弹簧过渡，正文延迟淡入并从轻微上移/模糊进入；收起时先淡出再收缩。Reduced/Off 直接落到终态，重新切换从当前值重新目标化。

## 实施与完整度

代码：`src/ArcWpf.Controls/ExpandableCard/`；Gallery：`samples/ArcWpf.Gallery/Pages/ExpandableCardPage.xaml`；测试：`tests/ArcWpf.Tests/ExpandableCardTests.cs`；来源基准：`tests/Baselines/References/2026-10-26-expandable-card/`。

| 交付项 | 状态 |
|---|---|
| API、默认状态、双向绑定、自动化 | 已实现并程序化验证 |
| 标题/描述/正文布局、宽度上限 | 已实现并程序化验证 |
| Full/Reduced/Off 动效和清理 | 已实现并程序化验证 |
| Gallery 主样本、扩展示例、Consumer | 已实现 |
| 实际窗口、截图、DPI、读屏、触摸、性能 | 未运行，待实际窗口验收 |

## 图标布局修订

Chevron 位于标题行右侧，使用独立 Grid 单元垂直居中；标题按钮保留右侧内边距以避免文字与图标重叠。
