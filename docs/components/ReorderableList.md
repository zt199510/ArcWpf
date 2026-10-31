# ReorderableList 组件规格

状态：已按 2026-10-31 冻结源码重做，构建通过；专项测试与实际窗口 **未运行/待验证**（Linux 环境）。事实见 [参考卡](../references/components/ReorderableList.md)。

## API

`ReorderableList : Control`：`ItemsSource`（可变 `IList`，元素为 `ReorderableListItem`：Id、Title、Detail；其他对象以 ToString 显示）、`Label`（`Reorder items`）、`ItemMoved`（`ReorderableListItemMovedEventArgs`：Item、OldIndex、NewIndex，每次相邻交换触发一次）。旧版 `ListBox` 基类、选择、`CanReorder`、Home/End 移动已移除（原版仅把手拖动与上下键）。

## 范围矩阵

| 项 | 状态 |
|---|---|
| 行外形、把手、标题/说明、序号 | 已实现 |
| 把手拖动（3 DIP 阈值，越过邻居中线交换）、上下键交换并保焦点 | 已实现；行顺序由自定义面板排列，不移动可视树节点 |
| 抬起 scale 1.02 + 浮起阴影、其他行 FLIP 平移、序号方向滚动 | 已实现 |
| 松手回槽 | 已实现；临界阻尼弹簧近似 bounceStiffness 520（无轻微回弹） |
| 把手 grab 光标 | 适配：WPF 无 grab，使用 Hand |
| Gallery 主示例 Launch checklist | 已实现 |
| 实际窗口拖拽命中、触控、明暗主题、Reduced/Off、读屏 | 未运行/待验证 |
