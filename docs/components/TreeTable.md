# TreeTable 组件规格

状态：Arc Registry 已冻结；开始实现层级表格、展开/折叠、列排序、选择、空状态、滚动和基础懒加载契约。真实窗口键盘、DPI、懒加载视觉和逐帧动效待验收。

公开 API：`Rows : ObservableCollection<TreeTableRow>`、`Columns : ObservableCollection<TreeTableColumn>`、`ExpandedIds`、`SelectedIds`、`Selectable`、`SortColumn`、`SortDirection`、`MaxHeight`、`EmptyMessage`。`TreeTableRow` 提供 `Id`、`Data`、`Children`、`HasChildren`；`TreeTableColumn` 提供 `Key`、`Header`、`Path`、`Sortable`、`Width`、`Numeric`。

树行在同一 Grid 中生成缩进、展开按钮、选择框和列值；排序只在同一父节点的兄弟集合内进行，不破坏层级。已加载的 `Children` 直接展开；`HasChildren=true` 且 Children 为空时显示 loading/error 状态，`LoadChildren` 可异步填充。有限高度由内部 ScrollViewer 承载，空数据显示 EmptyMessage。

验收场景：多级行、同级排序、展开/收起、父子选择、空状态、长内容滚动、窄宽度、加载失败重试、换模板和卸载清理。
