# ExpandableCard 参考基准

[原页](https://uiarc.dev/components/expandable-card)、[Markdown 合约](https://uiarc.dev/components/expandable-card/markdown)、[Registry](https://uiarc.dev/r/expandable-card.json)、[冻结清单](../../../tests/Baselines/References/2026-10-26-expandable-card/manifest.json)。`2026-10-26-expandable-card` 是项目采集批次，不是上游发行版本；仅有静态源码与文档证据，没有实际 Arc 页面交互观察。

| 对象 | Arc事实 | WPF采用 |
|---|---|---|
| 主样本 | 单个摘要卡，标题、可选描述和详细内容；默认收起 | `ExpandableCard : ContentControl`，`DefaultExpanded` 控制首次状态 |
| 触发器 | 整个标题区可操作，右侧 Chevron 表示状态 | 原生 `ToggleButton`，保留 WPF 键盘和 Toggle 语义 |
| 尺寸 | 收起/展开可使用不同宽度上限，卡片居中 | `CollapsedWidth` / `ExpandedWidth`，按可用宽度约束 |
| 内容 | 收起时保留挂载，展开时高度展开并淡入 | `PART_Panel` 保留内容，关闭时 `Visibility.Hidden` |
| 动效 | 宽度与高度弹簧，内容延迟进入，反向切换可打断 | `OwnedMotion` 管理 Width/Height/Opacity/Translate/Blur |
| 无障碍 | `aria-expanded` 与标题触发器关联内容 | ToggleButton 和 `IExpandCollapseProvider` |

来源只作视觉、行为和动效意图参考，不复制 React/Motion/Lucide 运行依赖；WPF 的 ContentControl、模板部件和自动化契约为项目实现。
