# Accordion 参考基准

[原页](https://uiarc.dev/components/accordion)、[Markdown 合约](https://uiarc.dev/components/accordion/markdown)、[Registry](https://uiarc.dev/r/accordion.json)、[冻结清单](../../../tests/Baselines/References/2026-10-26-accordion/manifest.json)。`2026-10-26-accordion` 是项目采集批次，不是上游发行版本；来源为免费 Registry、公开 Markdown、页面 HTML、共享动效令牌和许可页。只有静态源码/文档证据，没有实际 Arc 页面交互观察。

| 对象 | Arc事实 | WPF采用 |
|---|---|---|
| 主样本 | FAQ，两行问答；默认第一项打开；单开且可全部收起 | `Accordion` + `AccordionItem`，`ExpandedIndex` 默认0，`-1`表示全部收起 |
| 行结构 | 顶部边框、行间底部边框、无卡片背景 | `ItemsControl` 外壳上边框，`AccordionItem` 底部边框 |
| 触发器 | 14/500，中号最小高度来自 `control-height-lg`；标题与右侧 chevron 分隔 | 原生 `ToggleButton` 触发器，WPF适配为44 DIP最小高度，保持键盘和Toggle语义 |
| 大号 | 76px行高、18px问题字号；520px以下68px并降为正文尺寸 | `AccordionSize.Large` 为76 DIP；WPF窄窗口自适应保留布局，不复制CSS媒体查询 |
| 内容 | 次级文字；右侧保留空间；答案最大62ch | `TextSecondary`，默认右侧32 DIP；Large最大620 DIP |
| 图标 | 17px ChevronDown，闭合0度、打开180度，使用 `TextMuted` | 自绘17 DIP Path，Full下以 `ControlSpring` 旋转 |
| 动效 | 高度无过冲弹簧；内容向上6px并轻微模糊；Reduced一步到位 | 内部 `Height`/Opacity/Translate/Blur 由 `OwnedMotion` 管理；完成恢复 `Height=NaN` |
| 无障碍 | 问题在 heading 内的按钮，aria-expanded/controls 关联答案；闭合答案保留挂载但离开可访问树 | `ToggleButton` 保留键盘；`AccordionItem` 提供 `IExpandCollapseProvider`，闭合面板 `Visibility.Hidden` |

来源只作视觉、行为和动效意图参考，不复制 React/Radix/Motion/Lucide 运行依赖。`items` 数据数组映射为 WPF 原生 `AccordionItem` 容器；保留 `ItemsSource` 的原生集合生成能力是 WPF 适配选择。

## 状态与动效索引

源码确认：单开、可折叠、`defaultOpen=0`、`size=md|lg`、四组键盘操作、闭合面板挂载和动效终态。WPF 的 `ExpandedIndex`、`AccordionItem.IsExpanded`、`AccordionSize`、ItemsControl 容器和 UI Automation Provider 是项目设计；当前尚无实际窗口、DPI、读屏或触摸观察。

## WPF差异和回归入口

- 不暴露 Radix 的 `value`、`type`、`forceMount` 或 ReactNode；WPF 使用 `Items`/`ItemsSource`、`Header`/`Content` 和 `ExpandedIndex`。
- 单开规则集中在 `Accordion`，直接切换 `AccordionItem.IsExpanded` 会同步其它项并保持绑定。
- Reduced/Off 直接进入终态；Full 使用 Dispatcher 动画时钟，重开会从当前高度重新目标化。
- `tests/ArcWpf.Tests/AccordionTests.cs` 覆盖默认状态、绑定、布局、单开、键盘候选、图标几何、自动化、动效清理、ItemsSource 和示例构造。
