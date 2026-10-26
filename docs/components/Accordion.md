# Accordion 手风琴规格

状态：代码及程序化验证完成，待实际窗口验收。见[验证记录](../verification/2026-10-26-accordion.md)与[参考卡](../references/components/Accordion.md)。

`Accordion : ItemsControl` 提供一组单开、可全部收起的内容行；`AccordionItem : HeaderedContentControl` 保留 WPF 的 Header/Content、ItemsSource 容器生成、绑定和模板替换机制。

## WPF公开契约

- `Accordion.ExpandedIndex`：`int`，默认 `0`，双向绑定；`-1` 表示全部收起。设置一个索引时自动收起其它行。
- `Accordion.Size`：`AccordionSize.Medium` 或 `Large`，默认 `Medium`；Large 用于页面级 FAQ。
- `AccordionItem.Header` / `Content`：标题和答案内容，均支持模板；普通 `ItemsSource` 项会同时作为标题和内容，复杂内容使用显式 `AccordionItem`。
- `AccordionItem.IsExpanded`：`bool`，默认 `false`，双向绑定；直接变更会回写宿主的 `ExpandedIndex`。
- `AccordionItem.Expanded` / `Collapsed`：行状态边界的路由事件。
- `Accordion.ExpandedIndexChanged`：展开索引变化事件。
- 必要模板部件：`Accordion` 的 `PART_ItemsHost`；`AccordionItem` 的 `PART_HeaderButton`、`PART_Panel`、`PART_PanelInner`、`PART_Chevron`。缺少部件时不抛出模板异常。

## 状态与交互

| 状态/触发 | 外观 | 操作结果 | 焦点/键盘 | 数据与事件 |
|---|---|---|---|---|
| 默认 | 第一项展开；其余收起 | `ExpandedIndex=0` | 第一项可由 Tab 进入 | 对应 `IsExpanded` |
| Enter / Space | 当前行切换 | 展开当前项或收起当前项 | 焦点留在触发器 | 更新 `ExpandedIndex` |
| ArrowUp / ArrowDown | 只移动触发器焦点 | 循环到上/下一项，跳过禁用/不可见项 | 不改变展开项 | 不改 `ExpandedIndex` |
| Home / End | 只移动触发器焦点 | 跳到首项/末项，跳过禁用/不可见项 | 保留触发器焦点 | 不改 `ExpandedIndex` |
| 直接设置 `ExpandedIndex` / `IsExpanded` | 同一时间最多一个面板打开 | `-1`允许全部关闭；其它项收起 | 不抢占使用方焦点 | 保留绑定并触发事件 |
| Disabled | 触发器半透明且不可操作 | 键盘导航跳过 | 不进入导航候选 | 保留内容 |

答案面板关闭后保留模板内容但设为 `Visibility.Hidden`，不参与可见交互。标题鼠标点击不会显示持久焦点框；键盘导航通过 ToggleButton 的 `FocusVisualStyle` 显示焦点。父级 `Accordion` 不接管使用方 `DataContext`。

## 视觉与动效

外壳宽度填满可用区域，上边框1 DIP；每行底边框1 DIP、透明背景。Medium 触发器最小高度44 DIP、字号14/500、正文字号14、正文右侧内边距32 DIP；Large 触发器76 DIP、字号18，答案字号16、行高26.4，最大宽度620 DIP。Chevron 为17 DIP的 `M 4,6 L 8.5,10.5 L 13,6` 圆角折线。

Full：面板高度以约0.4秒无过冲弹簧展开、约0.16秒收起；答案从 `Y=-6`、Blur=2、Opacity=0 进入终态，Chevron 以约0.26秒弹簧转到180度。切换中重开从当前值重新目标化。Reduced/Off 一步到位，完成后高度恢复 `NaN`，卸载和换模板停止内部时钟。

原版 520px 以下的 CSS 媒体查询不直接复制；WPF 依赖可用宽度自然换行，并保留 Large 的固定标题高度和正文最大宽度。实际窗口视觉、DPI、读屏和触摸仍待验收。

## 验收场景

1. 创建含两个 `AccordionItem` 的 Accordion，布局后确认默认第一项展开、第二项收起，边框和触发器几何符合规格。
2. 点击或按 Enter/Space 切换当前行，确认单开规则、`ExpandedIndex` 双向绑定和 `-1` 全部收起。
3. 使用 ArrowUp/ArrowDown/Home/End 在触发器之间导航，确认循环和跳过禁用项。
4. 在 Full 下连续展开/收起/重开，确认高度从当前值重定向、结束恢复 `Height=NaN` 且无持续时钟；Reduced/Off 直接终态。
5. 通过 ItemsSource、显式 Header/Content、长文本、禁用项和嵌套 Accordion，确认内容实例、模板和 DataContext 保留。
6. Gallery 显示官网 FAQ 主样本与绑定/禁用/长内容/嵌套扩展，Consumer 独立引用 Controls。

## 实施与完整度

代码：`src/ArcWpf.Controls/Accordion/`；Gallery：`samples/ArcWpf.Gallery/Pages/AccordionPage.xaml`；测试：`tests/ArcWpf.Tests/AccordionTests.cs`；来源基准：`tests/Baselines/References/2026-10-26-accordion/`。

| 交付项 | 范围 | 状态 | 证据/剩余差异 |
|---|---|---|---|
| API、ItemsSource、单开绑定 | 必做 | 已实现，程序化验证 | STA 测试；实际窗口待验收 |
| 控件外形、标题、答案布局 | 必做 | 已实现，程序化验证 | 最终布局测量；视觉/DPI待验收 |
| Full/Reduced/Off 动效和清理 | 必做 | 已实现，程序化验证 | Dispatcher 时钟；实际窗口连续切换待验收 |
| 键盘、自动化、主题 | 必做 | 已实现，程序化验证 | Automation Provider；真实焦点/读屏待验收 |
| Arc 主示例、扩展示例、Consumer | 必做 | 已实现 | 不启动可见窗口 |
| 实际窗口、截图、触摸、性能 | 暂缓 | 未运行 | 依用户约定；不能由单测替代 |
