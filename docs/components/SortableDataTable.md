# SortableDataTable 组件规格

状态：已按 Arc 线上页面重做外观和选择交互（见 [像素对照记录](../verification/2026-11-02-data-controls-pixel.md)）。离屏渲染与 Arc 截图几何对齐；真实窗口列头、键盘、DPI 和逐帧排序视觉未运行。

公开 API：`Rows : IEnumerable<object>`、`Columns : ObservableCollection<DataTableColumn>`、`RowKeyPath`、`Caption`、`EmptyMessage`、`DefaultSortKey`、`SortDirection`、`Selectable`、`SelectedKeys`、`ItemName`（复数）、`ItemNameOne`（单数）、`MinTableWidth`（默认 560）。`DataTableColumn` 提供 `Key`、`Header`、`Sortable`、`Numeric`、`Width`（默认等分）、`Path`、`CellTemplate`。

模板部件：`PART_DataGrid`、`PART_EmptyState`、`PART_SelectionStatus`、`PART_ClearSelection`、`PART_Footer`、`PART_Clip`。数据为空时保留表头并在其下显示 120 高的空状态；表格不窄于 `MinTableWidth`，超出时在 DataGrid 内部横向滚动，滚动条悬停才显示且不占布局。尺寸：圆角 18、表头 45（含底边）、行 52、页脚 44（含顶边，仅 `Selectable` 时显示）。点击行切换该行，Shift 连选，Esc 清空。列按 Path 读取对象属性，排序使用自然字符串比较和数值比较；选择使用 RowKeyPath 稳定键，SelectedKeys 支持双向更新。

动效：排序更新保留现有 DataGrid 实例，列宽不跳动，行直接落到新位置；来源的行重排 FLIP、箭头翻转和计数换字动效尚未实现。Arc 演示用浏览器自动表格布局算列宽，Gallery 用实测列宽复现；控件本身不复制该算法。选择数量和 Clear 状态即时更新，卸载/换模板解绑 DataGrid 事件。
