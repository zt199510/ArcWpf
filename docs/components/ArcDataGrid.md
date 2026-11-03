# ArcDataGrid

> 无源码，动效为推测。Arc Pro 组件，依据公开预览的运行观察独立实现。

## 身份与状态

- 用途：可编辑的电子表格式数据网格。里程碑：M4-D 之后单独立项（用户 2026-11-03 指定）。
- 状态：待验收。真实窗口、DPI、IME 未运行。
- 来源与证据：[参考卡](../references/components/ArcDataGrid.md)。命名为 `ArcDataGrid`，避免遮蔽 `SortableDataTable` 使用的原生 `DataGrid`。

## 范围与依据

必做已实现：虚拟化、框选、地址与统计、就地编辑（文本/数字/选择/日期）、非法值、填充柄、TSV+HTML 剪贴板、撤销重做（100 步）、多列排序、筛选行、搜索、固定/隐藏/调宽列、密度、分组与小计、合计行、批量操作、CSV/XLSX 导出、空与加载状态、深色。

暂缓：服务端 `dataSource`；XLSX 分组大纲与 SUBTOTAL；单元格级自动化对等体（`GetItem` 返回 null）；表头键盘焦点（Alt+方向键作用于活动列）；触摸行为；480 宽度下状态栏换行。

## 状态与交互

| 状态/触发 | 外观 | 操作结果 | 焦点/键盘 | 数据与事件 |
|---|---|---|---|---|
| 单击/拖动单元格 | 活动框、选区 9% 强调色、填充柄 | 更新地址与统计 | 焦点留在绘制面；方向键、Shift 扩展、Ctrl+A/Space | 无 |
| Enter/F2/直接输入 | 编辑器覆盖单元格 | 提交后下移，Esc 取消 | 文本框焦点，Tab 右移 | `RowsEdited` |
| 非法值提交 | 红字、抖动 | 编辑器保持打开，值不变 | 焦点不动 | 无 |
| 排序/分组/折叠 | 行滑动 | 选区地址不变 | 不变 | `ViewStateChanged` |
| 筛选无结果 | 空状态与清除按钮，状态栏 `–` | 清除后恢复 | 清除按钮可 Tab | `ViewStateChanged` |
| 勾选行 | 工具栏切换为批量 | 复制/导出/删除，删除可撤销 | 工具栏按钮 | `RowsEdited` |

## 参考依据与差异

来源冻结状态：部分固定（只有截图与观察）。结论等级均为运行观察，暂定项在下表标明。

| 冲突或有意差异 | 备选方案 | 采用方案与理由 | 影响范围 | 验证方式与状态 |
|---|---|---|---|---|
| 字体 Inter | 打包 Inter | 用户此前未采纳打包，使用 Segoe UI 回退 | 全部文字 | 截图对比，已知差异 |
| 清除按钮被压成圆 | 照抄 | 上游缺陷，用完整胶囊 | 空状态 | 截图对比 |
| 筛选输入压住固定列 | 照抄 | 上游缺陷，裁到固定列下 | 横向滚动 | 代码与测试入口 |
| 单一焦点面 + `OnRender` | 原生 DataGrid | 1 万行与框选/填充柄需要的绘制控制 | 自动化粒度 | 暂缓项已列 |

## WPF 公开契约

`ArcDataGrid : Control`。属性：`Label`、`Rows`、`Columns`、`MaxGridHeight`、`Sort`、`Filters`、`SelectedRowIds`、`HiddenColumns`、`PinnedColumns`、`GroupBy`、`Density`、`RowSelection`、`RowNumbers`、`SelectionStats`、`ToolbarVisible`、`Totals`、`CanDeleteRows`、`ExportFileName`、`IsLoading`、`EmptyMessage`、`BulkActions`、`ExportFormats`。事件：`RowsEdited`、`ViewStateChanged`。方法：`ResetState`、`Undo`、`Redo`、`Export`、`CancelEdit`。模板部件见 `Themes/ArcDataGridTheme.xaml`。自动化：`DataGrid` 类型与 `IGridProvider` 行列数。

## 视觉与动效

令牌：`DataGridLine`、`DataGridRange`、`DataGridAccent`、`DataGridHeadActive`、`DataGridChecked`、`DataGridSearchFocus`、`DataGridDanger`（深色值未测量）。图标路径经程序对比与预览一致，`funnel-x` 取自预览 DOM。

以下参数均为**推测**，待核对：选区弹簧 visualDuration .26；行滑动 700ms 临界弹簧 visualDuration .35；闪烁 .9s、强调色 alpha .18；淡入淡出 .16s；合计数字滚动 .4s；状态消息 4s/8s；工具栏切换位移 6、模糊 4、.16s；抖动关键帧。`ArcMotion` Off 时全部跳过。

## 验收场景

见 [验证记录](../verification/2026-11-03-data-grid.md)。Gallery 页面 `ArcDataGrid`（Renewals，舞台 522）。

## 实施与证据

代码：`src/ArcWpf.Controls/ArcDataGrid/`；示例：`samples/ArcWpf.Gallery/Pages/ArcDataGridPage.xaml`；测试：`tests/ArcWpf.Tests/ArcDataGridTests.cs`。尚未建立回归基准；对照图只在本地 `artifacts/`。

## 完整度矩阵

| 交付项 | 本次范围 | 实现状态 | 验证证据 | 剩余差异/下一步 |
|---|---|---|---|---|
| 外形与布局 | 必做 | 已实现 | 28 状态截图 | 文字字形 |
| 内部状态 | 必做 | 已实现 | 截图 + 测试 | 无 |
| 动效 | 必做 | 已实现 | 无逐帧对照 | 参数推测，待真实窗口核对 |
| 键盘与编辑 | 必做 | 已实现 | 程序化测试 | 真实输入与 IME 未运行 |
| 服务端数据、触摸、单元格自动化 | 暂缓 | 未实现 | 无 | 单独立项 |
