# ButtonGroup

状态：2026-10-28 实现完成，解决方案构建通过；ButtonGroupTests（9 项）与实际窗口未运行（WPF 需 Windows），待验收。来源冻结见[参考卡](../references/components/ButtonGroup.md)。

## WPF 契约

`ButtonGroup : ItemsControl`，项为 `ButtonGroupItem : System.Windows.Controls.Button`（保留 Click/Command/键盘激活/ButtonAutomationPeer）。不继承 `ArcWpf.Button`：其整体按压缩放与内容过渡与来源“只缩放分段内容”冲突。

| 成员 | 说明 |
|---|---|
| `Label` | 组的无障碍名称；自定义 Peer，ControlType=Group。 |
| `Variant` | `Outline`（默认）/ `Solid`。 |
| `Size` | `ControlSize.Small`/`Medium`；Arc 无大号组，`Large` 按 Medium 度量。 |
| `Orientation` | Horizontal/Vertical；垂直时分段拉伸到最宽者，方向键改为上下。 |
| `CollapseLabels` | 默认 true；水平溢出时带图标分段只留图标；`IsCompact` 只读反映状态。 |
| `MenuItems` / `MenuLabel` | 非空时末尾增加 chevron 分段，复用 `Menu`（`MenuItem` 数据模型）；`MenuItemInvoked` 转发 `Menu.ItemInvokedEvent`。 |
| `ButtonGroupItem.Label` | 可见文本与无障碍名称；变化时交叉淡入。 |
| `ButtonGroupItem.Reserve` | `IList<string>`，XAML 写 `Reserve="Share,Copied"`；分段宽度取最宽。 |
| `ButtonGroupItem.Icon` | `Geometry` 按 24 viewBox、1.75 描边、分段前景色绘制；其他对象原样显示。 |
| `ButtonGroupItem.IsIconOnly` | 只显示图标，Label 作为 tooltip 与名称。 |
| `ButtonGroupItem.Content` | 非空时替代可见文本作为实时值（如 100%），变化不淡入；Label 仍是名称。 |

模板部件：组 `PART_Scroll`/`PART_Track`/`PART_Highlight`/`PART_HighlightFill`/`PART_HighlightPressed`/`PART_Menu`/`PART_Announcer`；分段 `PART_Divider`/`PART_Content`/`PART_Slot`/`PART_Sizers`/`PART_Phase`/`PART_Icon`/`PART_Label`/`PART_Value`/`PART_PreviousPhase`。chevron 通过 `ArcWpf.ButtonGroup.MenuTemplate` 重写 Menu 模板，`PART_Trigger` 即 chevron 分段；Menu 新增内部 `AlignToStart`/`AlignOffset` 使菜单对齐组外边（end，垂直为 start，偏移 1）。菜单项样式/模板从 MenuTheme 抽出为 `ArcWpf.Menu.ItemContainerStyle`/`ArcWpf.Menu.ItemTemplate` 共享。

## 完整度矩阵

| 交付项 | 范围 | 状态 | 证据/剩余差异 |
|---|---|---|---|
| API 与绑定 | 必做 | 已实现，未运行测试 | 见上表；href 链接分段不适用（用 Command） |
| 外形/分隔线/variant/size | 必做 | 已实现，未运行测试 | 源码几何；颜色混合加入 ArcThemeResources（ButtonGroup*） |
| 滑动高亮（悬停/按下/菜单/键盘焦点） | 必做 | 已实现，未运行测试 | ControlSpring snappy .26；无→有原地出现 |
| 分隔线 quiet、按压内容下沉、按下高亮加深 | 必做 | 已实现，未运行测试 | .16 / .97(.9) 0.12s + ease-spring .58 / 0.12s |
| 文案交叉淡入、reserve、未预留宽度 morph | 必做 | 已实现；专项断言动画启动 | 快照出场 + blur；morph .42 临界阻尼 |
| 标签折叠与横向滚动 | 必做 | 已实现，未运行测试 | MeasureOverride 滞回：恢复到折叠前全宽才展开 |
| chevron 菜单（旋转、颜色、对齐、动效） | 必做 | 已实现，未运行测试 | 复用 Menu 弹出/条目动效；菜单退出动画沿用 Menu（无 130ms 退场） |
| 键盘/自动化/live region | 必做 | 已实现，未运行测试 | 方向键循环、Home/End、RTL；LiveRegionChanged 播报 |
| Reduced/Off、卸载/换模板 | 必做 | 已实现，未运行测试 | Reduced 高亮跳变、无按压/旋转、仅淡入淡出 |
| Gallery 主示例与扩展 | 必做 | 已实现，页面加载测试未运行 | 主例三组英文原文案；扩展：未预留、窄容器、垂直、禁用 |

## 有意差异

- 单段禁用：来源 `aria-disabled` 仍可聚焦；WPF 使用原生 `IsEnabled=false`（不可聚焦），内容 opacity .4 一致。
- 菜单退出：来源 130ms 淡出缩小，Menu 现有实现关闭即隐藏；保持与 Menu 组件一致，未单独修改。
- 字距：来源 tracking −.01em，WPF TextBlock 无字距属性，未实现。
- 触摸：触摸仅在按下时显示高亮（来源一致）；未在触屏设备验证。
- pin/trash/check 图标按 lucide 公开几何重绘（页面未采集到这三个图标）。

最终证据见[验证记录](../verification/2026-10-28-button-group.md)。
