# List 组件规格

- 状态：已实现首版，程序化验证完成，实际窗口视觉验收待做。
- 组件名：`List` / `ListItem`。
- 范围：单选列表、标题、说明、图标、禁用项、滚动和键盘选择。
- 说明：Arc 当前公开目录没有独立的免费 List 条目；本组件按 Arc 的列表视觉语言，以 WPF 原生 `ListBox` 组合实现，不声称复制不存在的 Arc 源码。

## API

`List` 继承 `ListBox`，保留 `ItemsSource`、`SelectedItem`、`SelectionChanged`、键盘导航和原生自动化语义；新增 `Label` 用于列表辅助名称。

`ListItem` 提供 `Id`、`Title`、`Description`、`Icon`、`IsEnabled`。

## 视觉与交互

- Surface 外壳、1 DIP 边框、16 DIP 圆角和内部滚动区域。
- 每行最小高度 48 DIP，标题和说明单行省略，图标位于独立布局列并垂直居中。
- 鼠标点击或 Enter/Space 选中一行；上下方向键移动，Home/End 跳到首尾。
- 禁用项保持可见但不接受选择。

## 明确暂缓

多选、分组标题、拖拽排序、虚拟化、行内操作菜单和异步加载不在首版范围。
