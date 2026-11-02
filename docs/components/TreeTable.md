# TreeTable 组件规格

状态：已按 Arc 线上页面重做外观和交互，并实现懒加载与重试（见 [像素对照记录](../verification/2026-11-02-data-controls-pixel.md)）。离屏渲染与 Arc 截图几何对齐；真实窗口键盘、DPI 和逐帧动效未运行。

公开 API：`Rows : ObservableCollection<TreeTableRow>`、`Columns : ObservableCollection<TreeTableColumn>`、`ExpandedIds`、`SelectedIds`、`Selectable`、`SortColumn`、`SortDirection`、`MaxHeight`（默认不限）、`EmptyMessage`、`LoadChildren`，事件 `RowActivated`。`TreeTableRow` 提供 `Id`、`Data`、`Children`、`HasChildren`；`TreeTableColumn` 提供 `Key`、`Header`、`Path`、`Sortable`、`Width : GridLength`（默认 1*）、`Numeric`、`CellTemplate`（DataContext 为 `TreeTableCellContext`）。

树行在同一 Grid 轨道中生成缩进、导线、展开按钮、三态选择框和列值，行高 44、表头 40、圆角 26；排序按升序、降序、取消循环；排序只在同一父节点的兄弟集合内进行，不破坏层级。已加载的 `Children` 直接展开；`HasChildren=true` 且 Children 为空时显示 loading/error 状态，`LoadChildren` 可异步填充。表头固定，有限高度由内部滚动器承载（滚动条悬停才显示且不占布局），空数据显示 EmptyMessage。来源的 `hideBelow` 窄屏隐藏列、行退出和排序 FLIP 动效尚未实现。

验收场景：多级行、同级排序、展开/收起、父子选择、空状态、长内容滚动、窄宽度、加载失败重试、换模板和卸载清理。
