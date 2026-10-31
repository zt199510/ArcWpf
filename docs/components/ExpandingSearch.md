# ExpandingSearch 组件规格

状态：已按 2026-10-30 冻结源码重做，构建通过；专项测试与实际窗口 **未运行/待验证**（当前环境为 Linux，无法运行 WPF）。事实见 [参考卡](../references/components/ExpandingSearch.md)。

## API

`ExpandingSearch : Control`：`Label`（默认 `Search`）、`Placeholder`（null 时用 Label）、`Query`（双向）、`IsExpanded`（双向）、`ExpandedWidth`（360）、`MaxResults`（6）、`Anchor`（`Start`/`End`，默认 End）、`SuggestionsLabel`（`Recent`）、`EmptyHint`、`Items`/`Suggestions`（`ExpandingSearchItem`：Id、Title、Meta、Group、Icon、Keywords），事件 `ItemSelected`、`Expanded`、`Collapsed`。旧版 `WidthCollapsed`/`WidthExpanded`/`ExpandOnFocus`/`SubmitCommand`/`Submitted` 已移除（原版无提交语义，以选择结果为准）。

## 范围矩阵

| 项 | 状态 |
|---|---|
| 44 圆形按钮 → 展开输入，宽度 morph/smooth 弹簧，圆角随宽度 | 已实现 |
| 排名（标题开头 > 词开头 > 包含 > 关键词/元数据，稳定次序）、分组、匹配加粗 | 已实现 |
| suggestions/results/empty 三态面板，高度弹簧，模式交叉 blur | 已实现 |
| 键盘 Up/Down 循环、Enter 选择、Escape 收起并还焦点；清除按钮 | 已实现 |
| 失焦规则（空查询收起、有查询保留）、窗口失活保持 | 已实现 |
| 高亮 snappy 平移 | 已实现 |
| 行 layout position 弹簧 | 已知差异：新行仅淡入 |
| Placeholder 位置 | 适配：TextBox 内边距差 2 DIP 已补偿，待窗口核对 |
| Gallery 主示例（源站 demo 数据、lane 居中位移） | 已实现 |
| 实际窗口比例、IME、明暗主题、Reduced/Off、读屏 | 未运行/待验证 |
