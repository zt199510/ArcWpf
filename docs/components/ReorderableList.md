# ReorderableList 组件规格

状态：WPF fallback 实现，Arc Registry/CSS 当前网络不可访问，未宣称网页完全对齐。

`ReorderableList : ListBox` 保留 ItemsSource、SelectedIndex、键盘选择和 UIA，增加鼠标拖拽排序、方向键/Home/End 移动、`CanReorder` 和 `ItemMoved` 事件。排序要求 ItemsSource 实现 `IList`。
