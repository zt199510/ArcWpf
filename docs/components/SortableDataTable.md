# SortableDataTable 组件规格

状态：Arc Registry 已冻结；开始实现排序、列宽、选择、空状态和内部滚动。真实窗口列头、键盘、DPI 和逐帧排序视觉待验收。

公开 API：`Rows : IEnumerable<object>`、`Columns : ObservableCollection<DataTableColumn>`、`RowKeyPath`、`Caption`、`EmptyMessage`、`DefaultSortKey`、`SortDirection`、`Selectable`、`SelectedKeys`、`ItemName`。`DataTableColumn` 提供 `Key`、`Header`、`Sortable`、`Numeric`、`Width`、`Path`。

模板部件：`PART_DataGrid`、`PART_EmptyState`、`PART_SelectionStatus`、`PART_ClearSelection`。数据为空时不创建可命中行；表格在有限高度/宽度内滚动。列按 Path 读取对象属性，排序使用自然字符串比较和数值比较；选择使用 RowKeyPath 稳定键，SelectedKeys 支持双向更新。

动效：排序更新保留现有 DataGrid 实例和列定义，避免列宽跳动；Full 模式允许行重排过渡，Reduced/Off 直接更新。选择数量和 Clear 状态即时更新，卸载/换模板解绑 DataGrid 事件。
